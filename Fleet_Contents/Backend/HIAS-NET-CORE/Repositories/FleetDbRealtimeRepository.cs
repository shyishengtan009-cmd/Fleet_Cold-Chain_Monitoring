using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FleetCore.Context;
using FleetCore.Fleet;
using Npgsql;

namespace FleetCore.Repositories;

/// <summary>
/// Repository for sensor readings in the iot.tt19_data (live) and
/// iot.tt19_data_archive (rolling archive) tables.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   InsertRow         — store one new sensor reading (called every poll cycle)
///   GetLatestRow      — fetch the most recent reading for one device
///   GetRowsForRange   — fetch all readings in a time window (used for charts)
///   GetAggregatedRows — hourly min/max/avg buckets for long date ranges
///   GetMinMaxTs       — find the oldest and newest readings for a device
///
/// ── Archive transparency ─────────────────────────────────────────────────────
/// tt19_data is a 30-day rolling live table; older rows are moved to
/// tt19_data_archive (monthly partitions). All read queries UNION both tables
/// so the UI shows the full history regardless of age. If the archive table
/// does not yet exist on this database instance the queries fall back to
/// tt19_data only — no 500 errors during initial deployment.
/// </summary>
public class FleetDbRealtimeRepository
{
    private readonly DatabaseContext _databaseContext;

    // Cached per-process: once we know whether the archive table exists we
    // do not re-check on every query. Null = not yet checked.
    private static bool? _archiveExists;

    public FleetDbRealtimeRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── InsertRow ────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts one sensor reading row.
    /// Uses ON CONFLICT DO NOTHING so duplicate timestamps are silently skipped.
    /// Returns true if the row was actually inserted (new data),
    /// false if it was a duplicate (same hardware_id + ts already exists).
    /// </summary>
    public async Task<bool> InsertRow(
        string hardwareId,
        DateTime tsUtc,
        object? temperatureC,
        object? humidityPct,
        object? lightLux,
        object? batteryPct,
        object? vibrationG)
    {
        // ON CONFLICT DO UPDATE overwrites sensor values if TZone sends a corrected
        // reading for the same timestamp (previously silently dropped with DO NOTHING).
        // xmax = 0 is true only for genuine inserts — updates return false so alarm
        // dedup logic in FleetFetchRealtime continues to work correctly.
        const string sql = @"
INSERT INTO iot.tt19_data
    (hardware_id, ts, temperature_c, humidity_pct, light_lux, battery_pct, vibration_g)
VALUES
    (@HardwareId, @TsUtc, @TemperatureC, @HumidityPct, @LightLux, @BatteryPct, @VibrationG)
ON CONFLICT (hardware_id, ts) DO UPDATE
    SET temperature_c = EXCLUDED.temperature_c,
        humidity_pct  = EXCLUDED.humidity_pct,
        light_lux     = EXCLUDED.light_lux,
        battery_pct   = EXCLUDED.battery_pct,
        vibration_g   = EXCLUDED.vibration_g
RETURNING id, (xmax = 0) AS WasInserted;
";
        using var connection = _databaseContext.CreateConnection();
        var result = await connection.QueryFirstOrDefaultAsync<UpsertResult>(sql, new
        {
            HardwareId   = hardwareId,
            TsUtc        = tsUtc,
            TemperatureC = ToDouble(temperatureC),
            HumidityPct  = ToDouble(humidityPct),
            LightLux     = ToDouble(lightLux),
            BatteryPct   = ToDouble(batteryPct),
            VibrationG   = ToDouble(vibrationG)
        });
        return result?.WasInserted ?? false;
    }

    private class UpsertResult
    {
        public long Id           { get; set; }
        public bool WasInserted  { get; set; }
    }

    // ─── GetLatestRow ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent sensor reading for a device, or null if no data exists.
    /// </summary>
    public async Task<Dictionary<string, object?>?> GetLatestRow(string hardwareId)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) return null;

        var hasArchive = await ArchiveExistsAsync();
        var sql = hasArchive ? @"
SELECT ts            AS Ts,
       temperature_c AS TemperatureC,
       humidity_pct  AS HumidityPct,
       light_lux     AS LightLux,
       battery_pct   AS BatteryPct,
       vibration_g   AS VibrationG
FROM (
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct, vibration_g
    FROM iot.tt19_data         WHERE hardware_id = @HardwareId
    UNION ALL
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct, vibration_g
    FROM iot.tt19_data_archive WHERE hardware_id = @HardwareId
) combined
ORDER BY ts DESC
LIMIT 1;" : @"
SELECT ts AS Ts, temperature_c AS TemperatureC, humidity_pct AS HumidityPct,
       light_lux AS LightLux, battery_pct AS BatteryPct, vibration_g AS VibrationG
FROM iot.tt19_data
WHERE hardware_id = @HardwareId
ORDER BY ts DESC
LIMIT 1;";
        using var connection = _databaseContext.CreateConnection();
        try
        {
            var r = await connection.QueryFirstOrDefaultAsync<RealtimeDbRow>(
                sql, new { HardwareId = hardwareId });
            return r is null ? null : ToDict(hardwareId, r);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01" && hasArchive)
        {
            _archiveExists = false;
            var fallback = @"
SELECT ts AS Ts, temperature_c AS TemperatureC, humidity_pct AS HumidityPct,
       light_lux AS LightLux, battery_pct AS BatteryPct, vibration_g AS VibrationG
FROM iot.tt19_data WHERE hardware_id = @HardwareId ORDER BY ts DESC LIMIT 1;";
            var r = await connection.QueryFirstOrDefaultAsync<RealtimeDbRow>(
                fallback, new { HardwareId = hardwareId });
            return r is null ? null : ToDict(hardwareId, r);
        }
    }

    // ─── GetRowsForRange ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns all sensor readings for a device within a UTC time window.
    /// Limit is capped at 50 000 rows. Returns rows in ascending time order.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetRowsForRange(
        string hardwareId, DateTime? startUtc, DateTime? endUtc, int limit = FleetLimits.HistoryMaxRows)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) return new();

        var start = DateTime.SpecifyKind(startUtc ?? new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc), DateTimeKind.Utc);
        var end   = DateTime.SpecifyKind(endUtc   ?? DateTime.UtcNow, DateTimeKind.Utc);
        limit     = Math.Clamp(limit, 1, 50000);

        var hasArchive = await ArchiveExistsAsync();
        var sql = hasArchive ? @"
SELECT ts AS Ts, temperature_c AS TemperatureC, humidity_pct AS HumidityPct,
       light_lux AS LightLux, battery_pct AS BatteryPct, vibration_g AS VibrationG
FROM (
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct, vibration_g
    FROM iot.tt19_data
    WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End
    UNION ALL
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct, vibration_g
    FROM iot.tt19_data_archive
    WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End
) combined
ORDER BY ts ASC
LIMIT @Limit;" : @"
SELECT ts AS Ts, temperature_c AS TemperatureC, humidity_pct AS HumidityPct,
       light_lux AS LightLux, battery_pct AS BatteryPct, vibration_g AS VibrationG
FROM iot.tt19_data
WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End
ORDER BY ts ASC
LIMIT @Limit;";
        using var connection = _databaseContext.CreateConnection();
        try
        {
            var rows = await connection.QueryAsync<RealtimeDbRow>(
                sql, new { HardwareId = hardwareId, Start = start, End = end, Limit = limit });
            return rows.Select(r => ToDict(hardwareId, r)).ToList();
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01" && hasArchive)
        {
            _archiveExists = false;
            const string fallback = @"
SELECT ts AS Ts, temperature_c AS TemperatureC, humidity_pct AS HumidityPct,
       light_lux AS LightLux, battery_pct AS BatteryPct, vibration_g AS VibrationG
FROM iot.tt19_data
WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End
ORDER BY ts ASC LIMIT @Limit;";
            var rows = await connection.QueryAsync<RealtimeDbRow>(
                fallback, new { HardwareId = hardwareId, Start = start, End = end, Limit = limit });
            return rows.Select(r => ToDict(hardwareId, r)).ToList();
        }
    }

    // ─── GetAggregatedRows ────────────────────────────────────────────────────

    /// <summary>
    /// Returns hourly (or custom bucket) min/max/avg aggregates for a device.
    /// Used by the chart downsampling path when the requested range exceeds 24 hours.
    /// A 7-day range at 10 s resolution is ~60 000 raw points → ~168 hourly buckets.
    ///
    /// bucketMinutes is clamped to 5–1440 (5 min – 1 day).
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetAggregatedRows(
        string hardwareId, DateTime startUtc, DateTime endUtc, int bucketMinutes = 60)
    {
        hardwareId    = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) return new();
        bucketMinutes = Math.Clamp(bucketMinutes, 5, 1440);

        var start = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc);
        var end   = DateTime.SpecifyKind(endUtc,   DateTimeKind.Utc);

        var hasArchive = await ArchiveExistsAsync();
        var archiveUnion = hasArchive
            ? @"    UNION ALL
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct
    FROM iot.tt19_data_archive
    WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End"
            : "";

        var sqlHour = $@"
SELECT date_trunc('hour', ts)   AS Ts,
       MIN(temperature_c)       AS TempMin,
       MAX(temperature_c)       AS TempMax,
       AVG(temperature_c)       AS TempAvg,
       MIN(humidity_pct)        AS HumMin,
       MAX(humidity_pct)        AS HumMax,
       AVG(humidity_pct)        AS HumAvg,
       MIN(light_lux)           AS LightMin,
       MAX(light_lux)           AS LightMax,
       AVG(light_lux)           AS LightAvg,
       MIN(battery_pct)         AS BattMin,
       MAX(battery_pct)         AS BattMax,
       AVG(battery_pct)         AS BattAvg
FROM (
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct
    FROM iot.tt19_data
    WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End
{archiveUnion}
) combined
GROUP BY date_trunc('hour', ts)
ORDER BY 1 ASC;";

        var sqlCustom = $@"
SELECT to_timestamp(floor(extract(epoch FROM ts) / @BucketSecs) * @BucketSecs) AS Ts,
       MIN(temperature_c)       AS TempMin,
       MAX(temperature_c)       AS TempMax,
       AVG(temperature_c)       AS TempAvg,
       MIN(humidity_pct)        AS HumMin,
       MAX(humidity_pct)        AS HumMax,
       AVG(humidity_pct)        AS HumAvg,
       MIN(light_lux)           AS LightMin,
       MAX(light_lux)           AS LightMax,
       AVG(light_lux)           AS LightAvg,
       MIN(battery_pct)         AS BattMin,
       MAX(battery_pct)         AS BattMax,
       AVG(battery_pct)         AS BattAvg
FROM (
    SELECT ts, temperature_c, humidity_pct, light_lux, battery_pct
    FROM iot.tt19_data
    WHERE hardware_id = @HardwareId AND ts >= @Start AND ts <= @End
{archiveUnion}
) combined
GROUP BY floor(extract(epoch FROM ts) / @BucketSecs)
ORDER BY 1 ASC;";

        var sql = bucketMinutes == 60 ? sqlHour : sqlCustom;
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<AggregatedDbRow>(sql, new
        {
            HardwareId  = hardwareId,
            Start       = start,
            End         = end,
            BucketSecs  = (long)(bucketMinutes * 60)
        });

        return rows.Select(r => new Dictionary<string, object?>
        {
            ["ts"]        = r.Ts?.ToUniversalTime().ToString("o"),
            ["temp_min"]  = r.TempMin,
            ["temp_max"]  = r.TempMax,
            ["temp_avg"]  = r.TempAvg.HasValue ? Math.Round(r.TempAvg.Value, 1) : (double?)null,
            ["hum_min"]   = r.HumMin,
            ["hum_max"]   = r.HumMax,
            ["hum_avg"]   = r.HumAvg.HasValue  ? Math.Round(r.HumAvg.Value,  1) : (double?)null,
            ["light_min"] = r.LightMin,
            ["light_max"] = r.LightMax,
            ["light_avg"] = r.LightAvg.HasValue ? Math.Round(r.LightAvg.Value, 1) : (double?)null,
            ["batt_min"]  = r.BattMin,
            ["batt_max"]  = r.BattMax,
            ["batt_avg"]  = r.BattAvg.HasValue  ? Math.Round(r.BattAvg.Value,  1) : (double?)null
        }).ToList();
    }

    // ─── GetMinMaxTs ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the oldest and newest reading timestamps for a device.
    /// Returns (null, null) if the device has no data yet.
    /// </summary>
    public async Task<(DateTime? MinTs, DateTime? MaxTs)> GetMinMaxTs(string hardwareId)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) return (null, null);

        var hasArchive = await ArchiveExistsAsync();
        var sql = hasArchive ? @"
SELECT MIN(ts) AS MinTs, MAX(ts) AS MaxTs
FROM (
    SELECT ts FROM iot.tt19_data         WHERE hardware_id = @HardwareId
    UNION ALL
    SELECT ts FROM iot.tt19_data_archive WHERE hardware_id = @HardwareId
) combined;" : @"
SELECT MIN(ts) AS MinTs, MAX(ts) AS MaxTs
FROM iot.tt19_data
WHERE hardware_id = @HardwareId;";
        using var connection = _databaseContext.CreateConnection();
        MinMaxDbRow? r;
        try
        {
            r = await connection.QueryFirstOrDefaultAsync<MinMaxDbRow>(
                sql, new { HardwareId = hardwareId });
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01" && hasArchive)
        {
            _archiveExists = false;
            const string fallback = @"
SELECT MIN(ts) AS MinTs, MAX(ts) AS MaxTs
FROM iot.tt19_data WHERE hardware_id = @HardwareId;";
            r = await connection.QueryFirstOrDefaultAsync<MinMaxDbRow>(
                fallback, new { HardwareId = hardwareId });
        }

        if (r is null || r.MinTs is null || r.MaxTs is null) return (null, null);

        return (
            DateTime.SpecifyKind(r.MinTs.Value, DateTimeKind.Utc),
            DateTime.SpecifyKind(r.MaxTs.Value, DateTimeKind.Utc)
        );
    }

    // ─── Private Dapper mapping classes ───────────────────────────────────────

    private class RealtimeDbRow
    {
        public DateTime? Ts           { get; set; }
        public double?   TemperatureC { get; set; }
        public double?   HumidityPct  { get; set; }
        public double?   LightLux     { get; set; }
        public double?   BatteryPct   { get; set; }
        public double?   VibrationG   { get; set; }
    }

    private class MinMaxDbRow
    {
        public DateTime? MinTs { get; set; }
        public DateTime? MaxTs { get; set; }
    }

    private class AggregatedDbRow
    {
        public DateTime? Ts       { get; set; }
        public double?   TempMin  { get; set; }
        public double?   TempMax  { get; set; }
        public double?   TempAvg  { get; set; }
        public double?   HumMin   { get; set; }
        public double?   HumMax   { get; set; }
        public double?   HumAvg   { get; set; }
        public double?   LightMin { get; set; }
        public double?   LightMax { get; set; }
        public double?   LightAvg { get; set; }
        public double?   BattMin  { get; set; }
        public double?   BattMax  { get; set; }
        public double?   BattAvg  { get; set; }
    }

    // ─── Archive existence check ──────────────────────────────────────────────

    private async Task<bool> ArchiveExistsAsync()
    {
        if (_archiveExists.HasValue) return _archiveExists.Value;
        // Use pg_class (system catalog) instead of information_schema.tables.
        // information_schema.tables only lists tables the current role has a
        // registered privilege on — in Azure PostgreSQL the archive table can
        // exist and be queryable but still be absent from information_schema if
        // the owning role differs.  pg_class has no such filter.
        const string sql = @"
SELECT 1
FROM   pg_class     c
JOIN   pg_namespace n ON n.oid = c.relnamespace
WHERE  n.nspname = 'iot'
  AND  c.relname = 'tt19_data_archive'
LIMIT  1;";
        using var connection = _databaseContext.CreateConnection();
        var result = await connection.QueryFirstOrDefaultAsync<int?>(sql);
        _archiveExists = result.HasValue;
        return _archiveExists.Value;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static double? ToDouble(object? v)
    {
        if (v is null) return null;
        if (v is double d) return d;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture);
        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result : null;
    }

    private static Dictionary<string, object?> ToDict(string hardwareId, RealtimeDbRow r) =>
        new()
        {
            ["hardware_id"]   = hardwareId,
            ["ts"]            = r.Ts?.ToUniversalTime().ToString("o"),
            ["temperature_c"] = r.TemperatureC,
            ["humidity_pct"]  = r.HumidityPct,
            ["light_lux"]     = r.LightLux,
            ["battery_pct"]   = r.BatteryPct,
            ["vibration_g"]   = r.VibrationG
        };
}
