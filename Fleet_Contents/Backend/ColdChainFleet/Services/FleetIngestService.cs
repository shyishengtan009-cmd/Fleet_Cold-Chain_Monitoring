using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FleetCore.Context;
using FleetCore.Fleet;
using FleetCore.Fleet.Hubs;
using FleetCore.Fleet.Migrations;
using FleetCore.Repositories;
using Microsoft.AspNetCore.SignalR;
using FleetCore.Fleet.Notifications;
using FleetCore.Fleet.Scheduling;
using FleetCore.Fleet.Ingest;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FleetCore.Services;

/// <summary>
/// Background service that continuously polls all registered cold-truck sensor devices
/// and stores new readings in PostgreSQL.
///
/// ── What this file does ───────────────────────────────────────────────────────
/// This is the main "heartbeat" of the entire Fleet system. It runs in the
/// background for the lifetime of the ASP.NET Core application and:
///   1. Polls the TZone cloud API for each registered device (default every 10 s)
///   2. Stores new sensor readings in iot.tt19_data
///   3. Evaluates alarm thresholds → sends email notifications
///   4. Appends GPS points to active trip records
///   5. Checks the auto-trip schedule → opens/closes trips automatically
///
/// ── Full Fleet system architecture ───────────────────────────────────────────
///
///   Entry points (registered in Program.cs):
///     FleetIngestService  ← this file (real device polling)
///     FleetSimService     ← Services/FleetSimService.cs (test/synthetic data)
///
///   Folder layout under Fleet/:
///     Alarm/         — FleetAlarmChecker.cs        threshold evaluation + alerting
///     Database/      — FleetDbCoreRepository, FleetDbRealtimeRepository, etc.   PostgreSQL access layer
///     Ingest/        — FleetClient, FleetFetchRealtime, FleetDevice  TZone API + ingest
///     Notifications/ — FleetEmailService                             outbound alerts
///     Scheduling/    — FleetTripScheduler           auto start/stop trips on schedule
///     Models/Fleet/  — FleetStatusModels, FleetHistoryModels          shared DTOs
///     FleetLog.cs    — static Serilog facade used by all Fleet static classes
///
///   Poll cycle per device (every PollSeconds):
///     1. FleetFetchRealtime.IngestOneDevice()
///          → FleetClient.GetRealtime()          GET latest reading from TZone cloud
///          → FleetDbRealtimeRepository.InsertRow()        insert into iot.tt19_data (dedup'd)
///          → FleetAlarmChecker.CheckAndNotify() evaluate thresholds → notifications
///          → FleetDbTripsRepository.AppendTripPoint()     add GPS point to open trip
///     2. FleetTripScheduler.CheckAndApplySchedule()
///          → auto-open / auto-close trips per alarm_json daily schedule
///
///   Database tables used (all in PostgreSQL iot.* schema):
///     iot.tt19_devices     — device registry, per-device API credentials
///     iot.tt19_data        — sensor readings (append-only, indexed by hw_id + ts)
///     iot.trips            — trip records with GPS route JSON blob
///     iot.tt19_alarm_state — per-sensor debounce counter + cooldown timestamp
///     iot.tt19_alarm_log   — permanent record of every alarm event
///     iot.tt19_email_log   — record of every email send attempt
///     iot.device_settings  — per-device alarm thresholds + schedule (alarm_json)
///
/// ── Configuration (appsettings.json) ─────────────────────────────────────────
///   "Fleet": {
///     "Enabled":     true,
///     "PollSeconds": 10
///   }
///
/// ── Device discovery ──────────────────────────────────────────────────────────
/// Devices are loaded from iot.tt19_devices (DB) every 5 minutes.
/// If the DB returns an empty list, environment variables (FLEET_DEVICES or
/// FLEET_DEVICE_INT_ID + FLEET_HARDWARE_ID) are used as a fallback.
/// See FleetDevice.cs for the env var format.
/// </summary>
public class FleetIngestService : BackgroundService
{
    private readonly ILogger<FleetIngestService> _logger;
    private readonly DatabaseContext        _databaseContext;
    private readonly FleetDbAlarmLogRepository             _alarmLog;
    private readonly FleetDbDevicesRepository              _dbDevices;
    private readonly FleetDbRealtimeRepository             _dbRealtime;
    private readonly FleetDbSettingsRepository             _dbSettings;
    private readonly FleetDbAlarmStateRepository           _dbAlarmState;
    private readonly FleetDbEmailLogRepository             _dbEmailLog;
    private readonly FleetDbTripsRepository                _dbTrips;
    private readonly FleetDbDwellRepository                _dbDwell;
    private readonly FleetDbLocationsRepository            _dbLocations;
    private readonly bool _enabled;
    private readonly int  _pollSeconds;
    private readonly int  _pollConcurrency;

    // ── Device list cache (refreshed every 5 minutes from DB) ─────────────────
    private List<FleetDevice> _cachedDevices       = new();
    private DateTime          _lastDeviceRefresh   = DateTime.MinValue;
    private const int         DeviceCacheMinutes   = 5;
    private bool              _noDevicesWarnedOnce = false;
    private DateTime          _lastPurge           = DateTime.MinValue;
    // Ensures only one concurrent call can refresh the cache. GetDevices() is called
    // serially in the main poll loop today, but the lock makes it safe if a second
    // caller is ever added (e.g. a health-check endpoint).
    private readonly SemaphoreSlim _deviceRefreshLock = new(1, 1);

    // ─── Constructor ──────────────────────────────────────────────────────────

    public FleetIngestService(IConfiguration configuration, ILogger<FleetIngestService> logger, DatabaseContext databaseContext, IHubContext<FleetAlarmHub> alarmHub)
    {
        _logger          = logger;
        _databaseContext = databaseContext;
        _alarmLog     = new FleetDbAlarmLogRepository(databaseContext);
        _dbDevices    = new FleetDbDevicesRepository(databaseContext);
        _dbRealtime   = new FleetDbRealtimeRepository(databaseContext);
        _dbSettings   = new FleetDbSettingsRepository(databaseContext);
        _dbAlarmState = new FleetDbAlarmStateRepository(databaseContext);
        _dbEmailLog   = new FleetDbEmailLogRepository(databaseContext);
        _dbTrips      = new FleetDbTripsRepository(databaseContext);
        _dbDwell      = new FleetDbDwellRepository(databaseContext);
        _dbLocations  = new FleetDbLocationsRepository(databaseContext);

        // Initialize the shared static logger facade so all Fleet static classes
        // can log through Serilog without needing DI wiring
        FleetLog.Initialize(logger);
        FleetAlarmPusher.Initialize(alarmHub);

        _enabled         = configuration.GetValue<bool>("Fleet:Enabled", false);
        _pollSeconds     = Math.Clamp(configuration.GetValue<int>("Fleet:PollSeconds", 10), 5, 3600);
        _pollConcurrency = Math.Clamp(configuration.GetValue<int>("Fleet:PollConcurrency", 10), 1, 100);

        if (_enabled)
        {
            // Configure notification services
            FleetEmailService.Configure(configuration);
            FleetEmailDispatch.Configure(configuration);
            FleetEmailQueue.Configure(configuration, databaseContext);
        }
    }

    // ─── ExecuteAsync ─────────────────────────────────────────────────────────

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("FleetIngestService is disabled (Fleet:Enabled = false). Skipping.");
            return;
        }

        // Run Fleet schema migrations (creates/updates tables and indexes).
        // Retry with exponential backoff so a transient DB hiccup at startup
        // doesn't permanently kill the service (requires a full app restart to recover).
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await FleetMigrationRunner.RunAsync(_databaseContext);
                _logger.LogInformation("FleetIngestService: Fleet schema migrations complete.");
                await LogDatabaseIdentity();
                break;
            }
            catch (Exception ex)
            {
                var delay = Math.Min(30, (int)Math.Pow(2, attempt)); // 2s, 4s, 8s, 16s, 30s cap
                _logger.LogError(ex,
                    "FleetIngestService: Migration attempt {Attempt} failed — retrying in {Delay}s.", attempt, delay);
                await Task.Delay(TimeSpan.FromSeconds(delay), stoppingToken);
                if (stoppingToken.IsCancellationRequested) return;
            }
        }

        // Load global TZone credentials from iot.tt19_api_config (DB-stored config).
        // This fills in FleetClient's global fallback so per-device polling works even
        // without FLEET_APP_ID/KEY/SECRET environment variables being set.
        // Env vars still take priority if they are non-empty (SetGlobalCredentials only
        // overwrites when the current value is blank).
        try
        {
            var dbCreds = await new FleetDbApiConfigRepository(_databaseContext).GetActiveAsync();
            if (dbCreds != null)
            {
                FleetClient.SetGlobalCredentials(dbCreds.AppId, dbCreds.AppKey, dbCreds.AppSecret, dbCreds.Url);
                _logger.LogInformation(
                    "FleetIngestService: TZone credentials loaded from iot.tt19_api_config (app_key={K}).",
                    dbCreds.AppKey);
            }
            else
            {
                _logger.LogWarning(
                    "FleetIngestService: No active row in iot.tt19_api_config — falling back to env vars.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FleetIngestService: Could not read iot.tt19_api_config — using env vars.");
        }

        // Validate that at least one credential source is configured
        var configError = FleetClient.ValidateConfig();
        if (configError != null)
            _logger.LogWarning("FleetIngestService: {Warning}", configError);

        // Circuit breaker state is in-memory only — it resets to zero on every restart.
        // If devices were in exponential backoff before the restart (e.g. during a TZone outage),
        // all devices will be polled immediately on startup. This is expected and safe.
        _logger.LogInformation(
            "FleetIngestService: circuit breaker state reset (in-memory — does not persist across restarts).");

        // Auto-seed devices from FLEET_DEVICES / FLEET_DEVICE_INT_ID env vars so they
        // appear in iot.tt19_devices and can be registered via the UI with code "FLEET-ENV".
        try
        {
            var envDevices = FleetDeviceLoader.LoadDevicesFromEnv();
            foreach (var d in envDevices)
            {
                await _dbDevices.SeedDevice(d.HardwareId, "FLEET-ENV", d.DeviceIntId);
                _logger.LogInformation(
                    "FleetIngestService: Auto-seeded device {HardwareId} (int_id={DeviceIntId}).",
                    d.HardwareId, d.DeviceIntId);
            }
        }
        catch { /* env vars not set — skip auto-seed */ }

        // Warm the device cache on startup
        await GetDevices();

        // ── Main poll loop ────────────────────────────────────────────────────
        var sw = new Stopwatch();
        while (!stoppingToken.IsCancellationRequested)
        {
            sw.Restart();
            var devices = await GetDevices();
            _logger.LogDebug("FleetIngestService: Polling {Count} device(s).", devices.Count);

            // Poll devices in parallel with a bounded concurrency so a fleet of 100s-1000s
            // completes well within the poll interval instead of serially at ~200ms/device.
            // Repos use _databaseContext.CreateConnection() per call (new NpgsqlConnection with
            // Pooling=true,MaxPoolSize=350) so parallel calls are thread-safe.
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _pollConcurrency,
                CancellationToken      = stoppingToken
            };

            try
            {
                await Parallel.ForEachAsync(devices, parallelOptions, async (device, ct) =>
                {
                    if (ct.IsCancellationRequested) return;

                    // Step 1: fetch reading from TZone cloud and store in DB
                    // Circuit breaker skips devices that are in exponential backoff
                    var circuit = FleetPollCircuitBreaker.GetOrCreate(device.HardwareId);
                    long? activeTripId = null;
                    if (!circuit.IsAllowed())
                    {
                        _logger.LogDebug(
                            "FleetIngestService: {HardwareId} circuit open — skipping until {Until:HH:mm:ss} UTC.",
                            device.HardwareId, circuit.NextPollAllowed);
                    }
                    else
                    {
                        try
                        {
                            // IngestOneDevice returns the active trip ID it fetched internally
                            // so CheckAndApplySchedule below can reuse it and skip a second DB call.
                            activeTripId = await FleetFetchRealtime.IngestOneDevice(
                                device.DeviceIntId, device.HardwareId,
                                _dbRealtime, _dbSettings, _dbAlarmState,
                                device.AppId, device.AppKey, device.AppSecret,
                                _alarmLog, _dbEmailLog, _dbTrips,
                                _dbDwell, _dbLocations, device.Timezone, device.Label);

                            circuit.RecordSuccess();
                            _logger.LogDebug("FleetIngestService: Polled {HardwareId}.", device.HardwareId);
                        }
                        catch (Exception ex)
                        {
                            circuit.RecordFailure(ex.Message);
                            _logger.LogWarning(ex,
                                "FleetIngestService: Error polling {HardwareId} (fail #{Fails}, next retry in ~{BackoffS}s).",
                                device.HardwareId, circuit.ConsecutiveFails,
                                Math.Round((circuit.NextPollAllowed - DateTime.UtcNow).TotalSeconds));
                        }
                    }

                    // Step 2: auto-start or auto-stop a trip based on the device's daily schedule.
                    // Pass the activeTripId already fetched by IngestOneDevice — avoids a second
                    // GetActiveTripForDevice DB round-trip on the same poll cycle.
                    // This only runs when auto_trip = true in Device Settings
                    try
                    {
                        await FleetTripScheduler.CheckAndApplySchedule(device.HardwareId, _dbSettings, _dbAlarmState, _dbTrips, device.Timezone, activeTripId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "FleetIngestService: Error in trip scheduler for {HardwareId}.", device.HardwareId);
                    }
                });
            }
            catch (OperationCanceledException) { /* shutdown requested */ }

            // Record poll cycle duration + device count for the metrics endpoint
            FleetMetrics.RecordPollCycle(sw.ElapsedMilliseconds, devices.Count);

            // Hourly retention purge — runs in background, never blocks the poll loop
            if ((DateTime.UtcNow - _lastPurge).TotalHours >= 1)
            {
                _lastPurge = DateTime.UtcNow;
                _ = Task.Run(async () =>
                {
                    if (stoppingToken.IsCancellationRequested) return;
                    try
                    {
                        await FleetDbCoreRepository.PurgeOldData(_databaseContext);
                        _logger.LogInformation("FleetIngestService: Retention purge completed.");
                    }
                    catch (OperationCanceledException) { /* expected on shutdown */ }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "FleetIngestService: Retention purge failed.");
                    }
                });
            }

            // Subtract elapsed poll time so the interval stays accurate regardless of fleet size
            var remaining = (_pollSeconds * 1000) - (int)sw.ElapsedMilliseconds;
            if (remaining > 0)
                await Task.Delay(remaining, stoppingToken);
        }

        _logger.LogInformation("FleetIngestService stopped.");
    }

    // ─── GetDevices ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the cached device list, refreshing from the database every 5 minutes.
    ///
    /// Source priority:
    ///   1. iot.tt19_devices (PostgreSQL) — all active devices with device_int_id set
    ///   2. FLEET_DEVICES / FLEET_DEVICE_INT_ID env vars — fallback for simple setups
    ///
    /// The 5-minute cache means newly registered devices appear within 5 minutes
    /// without restarting the service. This is intentional.
    ///
    /// Device loading errors are logged as warnings — the previously cached list
    /// is returned if the refresh fails, so a temporary DB hiccup doesn't stop polling.
    /// </summary>
    private async Task<List<FleetDevice>> GetDevices()
    {
        // Fast path: cache still fresh — no lock needed for a read
        if ((DateTime.UtcNow - _lastDeviceRefresh).TotalMinutes < DeviceCacheMinutes
            && _cachedDevices.Count > 0)
            return _cachedDevices;

        // Only one refresh can run at a time
        await _deviceRefreshLock.WaitAsync();
        try
        {
            // Re-check inside the lock — another waiter may have just refreshed
            if ((DateTime.UtcNow - _lastDeviceRefresh).TotalMinutes < DeviceCacheMinutes
                && _cachedDevices.Count > 0)
                return _cachedDevices;

            // Try loading from the database first
            try
            {
                var dbRows = await _dbDevices.GetAllActiveDevices();
                if (dbRows.Count > 0)
                {
                    _cachedDevices = dbRows
                        .Select(r => new FleetDevice(r.DeviceIntId, r.HardwareId, r.AppId, r.AppKey, r.AppSecret, r.Timezone, r.Label))
                        .ToList();
                    _lastDeviceRefresh   = DateTime.UtcNow;
                    _noDevicesWarnedOnce = false;
                    _logger.LogInformation(
                        "FleetIngestService: Loaded {Count} device(s) from DB.", _cachedDevices.Count);
                    return _cachedDevices;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "FleetIngestService: Could not load devices from DB — falling back to env vars.");
            }

            // Env var fallback (for development / simple deployments)
            try
            {
                var envDevices = FleetDeviceLoader.LoadDevicesFromEnv();
                if (envDevices.Count > 0)
                {
                    _cachedDevices     = envDevices;
                    _lastDeviceRefresh = DateTime.UtcNow;
                    _logger.LogInformation(
                        "FleetIngestService: Loaded {Count} device(s) from env vars.", _cachedDevices.Count);
                }
            }
            catch (Exception ex)
            {
                if (!_noDevicesWarnedOnce)
                {
                    _logger.LogWarning(ex,
                        "FleetIngestService: Could not load devices from env vars either. " +
                        "No devices will be polled until a device is registered or env vars are set.");
                    _noDevicesWarnedOnce = true;
                }
            }

            return _cachedDevices;
        }
        finally
        {
            _deviceRefreshLock.Release();
        }
    }

    // ─── LogDatabaseIdentity ──────────────────────────────────────────────────

    /// <summary>
    /// One-time startup diagnostic: logs which physical PostgreSQL server/database
    /// this connection actually resolves to, plus how much archive data exists for
    /// device 190026010000218. Compare inet_server_addr() against the same query run
    /// in pgAdmin to confirm whether the app and pgAdmin are pointed at the same
    /// database instance — useful when "data visible in pgAdmin" disagrees with
    /// "data visible in the app" (e.g. UAT vs production confusion).
    /// Failures are logged but never fatal — this is diagnostic only.
    /// </summary>
    private async Task LogDatabaseIdentity()
    {
        try
        {
            using var connection = _databaseContext.CreateConnection();
            const string sql = @"
SELECT inet_server_addr()::text AS ServerAddr,
       current_database()       AS DbName,
       (SELECT COUNT(*) FROM iot.tt19_data_archive
        WHERE hardware_id = '190026010000218')::bigint AS ArchiveRowsForDevice;";
            var row = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<DbIdentityRow>(connection, sql);
            _logger.LogInformation(
                "FleetIngestService: connected to server={ServerAddr} database={DbName} — " +
                "archive rows for device 190026010000218 = {ArchiveRows}",
                row?.ServerAddr ?? "(unix socket / unresolved)", row?.DbName, row?.ArchiveRowsForDevice);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FleetIngestService: LogDatabaseIdentity diagnostic failed (non-fatal).");
        }
    }

    private class DbIdentityRow
    {
        public string? ServerAddr           { get; set; }
        public string? DbName               { get; set; }
        public long    ArchiveRowsForDevice  { get; set; }
    }
}
