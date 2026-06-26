using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FleetCore.Context;

namespace FleetCore.Repositories;

/// <summary>
/// Repository for the current fleet status — one row per device, latest reading only.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   GetLatestPerDevice — return the most recent sensor reading for every registered
///                        device in an organisation. Devices with no readings yet
///                        appear with null sensor fields and status OFFLINE.
///
/// ── How truck names are resolved ──────────────────────────────────────────────
/// truck_name comes from trip_json->>'truck_name' in iot.device_settings, with
/// dev.label as the fallback. The legacy iot.trucks / iot.truck_sensors tables
/// were removed in migration 0009 (they were never populated).
///
/// ── Org scoping ───────────────────────────────────────────────────────────────
/// When organizationId is supplied, only devices owned by that org are returned.
/// When null, ALL devices are returned (admin use).
/// </summary>
public class FleetDbStatusRepository
{
    private readonly DatabaseContext _databaseContext;

    // Cached per-process: once we know whether the archive table exists we
    // do not re-check on every call. Null = not yet checked.
    private static bool? _archiveExists;

    public FleetDbStatusRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── ArchiveExistsAsync ───────────────────────────────────────────────────

    private async Task<bool> ArchiveExistsAsync()
    {
        if (_archiveExists.HasValue) return _archiveExists.Value;
        const string sql = @"
SELECT 1 FROM information_schema.tables
WHERE table_schema = 'iot' AND table_name = 'tt19_data_archive'
LIMIT 1;";
        using var connection = _databaseContext.CreateConnection();
        var result = await connection.QueryFirstOrDefaultAsync<int?>(sql);
        _archiveExists = result.HasValue;
        return _archiveExists.Value;
    }

    // ─── GetLatestPerDevice ───────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent sensor reading for each device visible to the given org.
    /// Pass organizationId = null to get all devices (admin/super-user context).
    /// Limit is capped at 500.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetLatestPerDevice(
        int limit = 200, int? organizationId = null)
    {
        limit = Math.Clamp(limit, 1, 500);

        // Drive from iot.tt19_devices with a LEFT JOIN onto the latest reading so that
        // every registered device appears in the result — even if no data has been
        // polled yet. Devices with no readings show ts = NULL and are classified
        // OFFLINE by the controller. This means a newly added device is immediately
        // visible in the Fleet Dashboard without needing a successful poll cycle first.
        //
        // The latest reading is sourced from tt19_data UNION ALL tt19_data_archive —
        // a device whose only history has rolled into the archive (e.g. tt19_data is
        // a 30-day rolling window) would otherwise show null/OFFLINE forever even
        // though "last known" data genuinely exists. Falls back to tt19_data only if
        // the archive table doesn't exist yet on this database.
        //
        // DISTINCT ON (dev.hardware_id) ORDER BY dev.hardware_id, d.ts DESC NULLS LAST
        // picks the most recent reading per device; for devices with no data the single
        // NULL row is returned. Outer ORDER BY registered_at shows oldest devices first.
        var hasArchive = await ArchiveExistsAsync();
        var latestSource = hasArchive ? @"
  (
      SELECT hardware_id, ts, temperature_c, humidity_pct, light_lux, battery_pct
      FROM iot.tt19_data
      UNION ALL
      SELECT hardware_id, ts, temperature_c, humidity_pct, light_lux, battery_pct
      FROM iot.tt19_data_archive
  ) d" : "iot.tt19_data d";

        var sql = organizationId.HasValue ? $@"
SELECT * FROM (
  SELECT DISTINCT ON (dev.hardware_id)
    dev.hardware_id                                   AS HardwareId,
    d.ts                                              AS Ts,
    d.temperature_c                                   AS TemperatureC,
    d.humidity_pct                                    AS HumidityPct,
    d.light_lux                                       AS LightLux,
    d.battery_pct                                     AS BatteryPct,
    COALESCE(ds.trip_json->>'truck_name', dev.label)  AS TruckName,
    dev.registered_at                                 AS RegisteredAt
  FROM iot.tt19_devices dev
  LEFT JOIN {latestSource} ON d.hardware_id = dev.hardware_id
  LEFT JOIN iot.device_settings ds ON ds.hardware_id = dev.hardware_id
  WHERE dev.organization_id = @OrgId
    AND dev.is_active       = TRUE
  ORDER BY dev.hardware_id, d.ts DESC NULLS LAST
) sub
ORDER BY sub.RegisteredAt ASC NULLS LAST
LIMIT @Limit;
" : $@"
SELECT * FROM (
  SELECT DISTINCT ON (dev.hardware_id)
    dev.hardware_id                                   AS HardwareId,
    d.ts                                              AS Ts,
    d.temperature_c                                   AS TemperatureC,
    d.humidity_pct                                    AS HumidityPct,
    d.light_lux                                       AS LightLux,
    d.battery_pct                                     AS BatteryPct,
    COALESCE(ds.trip_json->>'truck_name', dev.label)  AS TruckName,
    dev.registered_at                                 AS RegisteredAt
  FROM iot.tt19_devices dev
  LEFT JOIN {latestSource} ON d.hardware_id = dev.hardware_id
  LEFT JOIN iot.device_settings ds ON ds.hardware_id = dev.hardware_id
  WHERE dev.organization_id IS NOT NULL
    AND dev.is_active       = TRUE
  ORDER BY dev.hardware_id, d.ts DESC NULLS LAST
) sub
ORDER BY sub.RegisteredAt ASC NULLS LAST
LIMIT @Limit;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<StatusDbRow>(
            sql, new { OrgId = organizationId, Limit = limit });

        return rows.Select(ToDict).ToList();
    }

    // ─── Private Dapper mapping class ─────────────────────────────────────────

    private class StatusDbRow
    {
        public string?   HardwareId   { get; set; }
        public DateTime? Ts           { get; set; }
        public double?   TemperatureC { get; set; }
        public double?   HumidityPct  { get; set; }
        public double?   LightLux     { get; set; }
        public double?   BatteryPct   { get; set; }
        public string?   TruckName    { get; set; }
        public DateTime? RegisteredAt { get; set; }  // used for ORDER BY only
    }

    // ─── ToDict ───────────────────────────────────────────────────────────────

    private static Dictionary<string, object?> ToDict(StatusDbRow r) => new()
    {
        ["hardware_id"]   = r.HardwareId,
        ["ts"]            = r.Ts?.ToUniversalTime().ToString("o"),
        ["temperature_c"] = r.TemperatureC,
        ["humidity_pct"]  = r.HumidityPct,
        ["light_lux"]     = r.LightLux,
        ["battery_pct"]   = r.BatteryPct,
        ["truck_name"]    = r.TruckName
    };

}
