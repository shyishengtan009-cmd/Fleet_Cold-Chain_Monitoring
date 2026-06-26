// Generates fake sensor readings for demo/dev devices so the Fleet UI has data without TZone.
// Disabled by default (FleetSim:Enabled = false in appsettings.json).
// Override locally in appsettings.Development.json:
//   FleetSim:Enabled         true
//   FleetSim:IntervalSeconds 300
//   FleetSim:OrgId           <your org id>

using Dapper;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Fleet.Alarm;
using HIAS_NET_CORE.Fleet.Notifications;
using HIAS_NET_CORE.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HIAS_NET_CORE.Services;

public class FleetSimService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<FleetSimService> _logger;
    private readonly int _intervalSeconds;
    private readonly int _orgId;

    // Runtime toggles — readable/writable via FleetSimController.
    // volatile backing fields prevent CPU reordering when the sim controller sets these
    // while the background loop reads them on a different thread.
    private static volatile bool   _isEnabled = true;
    private static volatile string _simMode   = "normal";
    public static bool   IsEnabled { get => _isEnabled;  set => _isEnabled = value; }
    public static string SimMode   { get => _simMode;    set => _simMode   = value; }
    public static object? LastReading { get; private set; }

    private record SimDevice(string HardwareId, string Label, double TempMin, double TempMax);

    // Hardware IDs start with SIM-U so they sort after SIM-TEST-001 alphabetically.
    private static readonly SimDevice[] Devices =
    [
        new("SIM-U-COLD-1", "Sim Cold Room 1",  10, 20),
        new("SIM-U-COLD-2", "Sim Cold Room 2",  10, 20),
        new("SIM-U-FRZE-1", "Sim Freezer 1",     0,  5),
        new("SIM-U-FRZE-2", "Sim Freezer 2",     0,  5),
        new("SIM-U-WARM-1", "Sim Warm Zone",    30, 40),
    ];

    // Old hardware IDs from a previous run — removed on startup.
    private static readonly string[] ObsoleteIds =
    [
        "SIM-COOL-01", "SIM-COOL-02", "SIM-FREZE-01", "SIM-FREZE-02", "SIM-WARM-01"
    ];

    public FleetSimService(IServiceProvider sp, ILogger<FleetSimService> logger, IConfiguration config)
    {
        _sp = sp;
        _logger = logger;
        _intervalSeconds = config.GetValue("FleetSim:IntervalSeconds", 300);
        _orgId = config.GetValue("FleetSim:OrgId", 1);

        FleetLog.Initialize(logger);
        FleetEmailService.Configure(config);
        FleetEmailDispatch.Configure(config);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        FleetEmailQueue.Configure(config, db);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await SeedDevicesAsync(ct);

        // First reading immediately on startup so the UI has data right away.
        await GenerateReadingsAsync(ct);

        _logger.LogInformation(
            "FleetSimService: running — {Count} devices, interval {Sec}s, orgId {Org}",
            Devices.Length, _intervalSeconds, _orgId);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(_intervalSeconds), ct);
                await GenerateReadingsAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FleetSimService: error generating readings.");
            }
        }
    }

    // ─── Seed ────────────────────────────────────────────────────────────────────

    private async Task SeedDevicesAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        using var conn = db.CreateConnection();

        // Auto-detect org from any existing device so sim devices appear under the same org.
        var detectedOrg = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT organization_id FROM iot.tt19_devices WHERE organization_id IS NOT NULL LIMIT 1;");
        var orgId = detectedOrg ?? _orgId;

        if (!detectedOrg.HasValue && orgId == 1)
            _logger.LogWarning(
                "FleetSimService: ⚠ No existing device found — defaulting to orgId=1. " +
                "Sim devices will appear under org 1. Set FleetSim:OrgId in appsettings to override.");
        else
            _logger.LogInformation("FleetSimService: using orgId={Org} (detected={Detected}).",
                orgId, detectedOrg.HasValue);

        foreach (var d in Devices)
        {
            await conn.ExecuteAsync(@"
INSERT INTO iot.tt19_devices (hardware_id, label, organization_id, is_active, activation_code, registered_at)
VALUES (@HardwareId, @Label, @OrgId, TRUE, '', NOW())
ON CONFLICT (hardware_id) DO UPDATE SET label = EXCLUDED.label, organization_id = EXCLUDED.organization_id;",
                new { d.HardwareId, d.Label, OrgId = orgId });

            await conn.ExecuteAsync(@"
INSERT INTO iot.device_settings (hardware_id, alarm_json, trip_json)
VALUES (@HardwareId, '{}', '{}')
ON CONFLICT (hardware_id) DO NOTHING;",
                new { d.HardwareId });
        }

        // Remove old-named devices so they don't linger in the UI.
        foreach (var old in ObsoleteIds)
        {
            await conn.ExecuteAsync(
                "DELETE FROM iot.device_settings WHERE hardware_id = @Id;", new { Id = old });
            await conn.ExecuteAsync(
                "DELETE FROM iot.tt19_devices WHERE hardware_id = @Id;", new { Id = old });
        }

        _logger.LogInformation("FleetSimService: seeded {Count} sim devices (orgId={Org}).",
            Devices.Length, _orgId);
    }

    // ─── Generate ─────────────────────────────────────────────────────────────

    private async Task GenerateReadingsAsync(CancellationToken ct)
    {
        if (!IsEnabled) return;

        using var scope      = _sp.CreateScope();
        var db               = scope.ServiceProvider.GetRequiredService<DatabaseContext>();
        var repo             = new FleetDbRealtimeRepository(db);
        var dbSettings       = new FleetDbSettingsRepository(db);
        var dbAlarmState     = new FleetDbAlarmStateRepository(db);
        var alarmLog         = new FleetDbAlarmLogRepository(db);
        var dbEmailLog       = new FleetDbEmailLogRepository(db);

        var now = DateTime.UtcNow;

        foreach (var d in Devices)
        {
            try
            {
                // In "breach" mode override temp to 25–35°C regardless of device config
                var (tMin, tMax) = SimMode == "breach" ? (25.0, 35.0) : (d.TempMin, d.TempMax);
                var temp     = Math.Round(tMin + Random.Shared.NextDouble() * (tMax - tMin), 2);
                var humidity = Math.Round(40 + Random.Shared.NextDouble() * 40, 2);   // 40–80 %
                var light    = Math.Round(Random.Shared.NextDouble() * 300, 2);        // 0–300 lux
                var battery  = Math.Round(60 + Random.Shared.NextDouble() * 40, 1);   // 60–100 %
                var vibe     = Math.Round(Random.Shared.NextDouble() * 1.5, 3);        // 0–1.5 g
                var ts       = now.AddMilliseconds(Array.IndexOf(Devices, d));

                var inserted = await repo.InsertRow(d.HardwareId, ts, temp, humidity, light, battery, vibe);

                LastReading = new { hardware_id = d.HardwareId, ts, temperature_c = temp, sim_mode = SimMode, inserted };

                _logger.LogInformation(
                    "FleetSimService: {Id}  {Temp:F1}°C  {Hum:F1}%RH  bat={Bat:F0}%",
                    d.HardwareId, temp, humidity, battery);

                if (inserted)
                {
                    await FleetAlarmChecker.CheckAndNotify(
                        d.HardwareId, ts,
                        temp, humidity, light, battery,
                        dbSettings, dbAlarmState,
                        vibe, null, null,
                        alarmLog, dbEmailLog,
                        timezone: "Asia/Kuala_Lumpur",
                        deviceLabel: d.Label);

                    await FleetAlarmChecker.CheckBatteryLevel(
                        d.HardwareId, ts, battery,
                        dbSettings, dbAlarmState,
                        alarmLog, dbEmailLog,
                        timezone: "Asia/Kuala_Lumpur");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "FleetSimService: failed to process reading for {Id}.", d.HardwareId);
            }
        }
    }
}
