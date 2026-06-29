using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FleetCore.Context;
using FleetCore.Fleet;
using FleetCore.Models.Fleet;

namespace FleetCore.Repositories;

/// <summary>
/// Repository for the Fleet alarm log table (iot.tt19_alarm_log).
///
/// ── What this file does ───────────────────────────────────────────────────────
///   Insert         — writes one alarm event after FleetAlarmChecker fires
///   GetSince       — polls for new alarms since a timestamp (frontend polling)
///   GetByDateRange — gets all alarms for a specific date range (Alerts page)
///   GetRecent      — gets the most recent N alarms (alarm history view)
///
/// ── Table: iot.tt19_alarm_log ─────────────────────────────────────────────────
/// Append-only — rows are never updated or deleted.
/// One row per alarming sensor field per alarm event.
///
/// ── Why the frontend polls this table ────────────────────────────────────────
/// The frontend polls GET /api/fleet/alarm_log/recent?since=... every few seconds.
/// When new rows appear (new alarms), it shows a toast notification to the user.
/// </summary>
public class FleetDbAlarmLogRepository
{
    private readonly DatabaseContext _databaseContext;

    public FleetDbAlarmLogRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── Insert ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts one alarm event into the log and returns the new row's id.
    /// Returns 0 on error (caller treats 0 as "no id available").
    /// Called by FleetAlarmChecker before the email log insert so that
    /// the email log row can reference the alarm log row via alarm_log_id.
    /// </summary>
    public async Task<long> Insert(FleetAlarmLogEntry entry)
    {
        const string sql = @"
INSERT INTO iot.tt19_alarm_log
    (hardware_id, ts, alarm_type, field, value, threshold, message)
VALUES
    (@HardwareId, @Ts, @AlarmType, @Field, @Value, @Threshold, @Message)
RETURNING id;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            return await connection.ExecuteScalarAsync<long>(sql, entry);
        }
        catch (Exception ex)
        {
            FleetLog.Error("[Fleet-AlarmLog] Failed to insert alarm log.", ex);
            return 0;
        }
    }

    // ─── GetSince ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns alarm log entries created after <paramref name="since"/> (UTC).
    /// Used by the frontend to poll for new alarms since the last check.
    /// </summary>
    public async Task<List<FleetAlarmLogRow>> GetSince(
        string hardwareId, DateTime since, int limit = 20)
    {
        const string sql = @"
SELECT
    id            AS Id,
    hardware_id   AS HardwareId,
    ts            AS Ts,
    alarm_type    AS AlarmType,
    field         AS Field,
    value         AS Value,
    threshold     AS Threshold,
    message    AS Message,
    created_at AS CreatedAt
FROM iot.tt19_alarm_log
WHERE hardware_id = @hardwareId
  AND created_at  > @since
ORDER BY created_at DESC
LIMIT @limit;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<AlarmLogDbRow>(
            sql, new { hardwareId, since, limit });

        return rows.Select(ToDto).ToList();
    }

    // ─── GetByDateRange ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns alarm log entries for a device within a specific UTC date range.
    /// Used by the Cold Truck Alerts page to show all alarms for the selected date.
    /// </summary>
    public async Task<List<FleetAlarmLogRow>> GetByDateRange(
        string hardwareId, DateTime startDate, DateTime endDate, int limit = FleetLimits.AlarmLogMaxRows)
    {
        const string sql = @"
SELECT
    id            AS Id,
    hardware_id   AS HardwareId,
    ts            AS Ts,
    alarm_type    AS AlarmType,
    field         AS Field,
    value         AS Value,
    threshold     AS Threshold,
    message    AS Message,
    created_at AS CreatedAt
FROM iot.tt19_alarm_log
WHERE hardware_id = @hardwareId
  AND ts >= @startDate
  AND ts <= @endDate
ORDER BY ts DESC
LIMIT @limit;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<AlarmLogDbRow>(
            sql, new { hardwareId, startDate, endDate, limit });

        return rows.Select(ToDto).ToList();
    }

    // ─── GetRecent ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent alarm log entries for a device.
    /// Used by the frontend alarm history view when no "since" filter is applied.
    /// </summary>
    public async Task<List<FleetAlarmLogRow>> GetRecent(string hardwareId, int limit = FleetLimits.AlarmLogMaxRows)
    {
        const string sql = @"
SELECT
    id            AS Id,
    hardware_id   AS HardwareId,
    ts            AS Ts,
    alarm_type    AS AlarmType,
    field         AS Field,
    value         AS Value,
    threshold     AS Threshold,
    message    AS Message,
    created_at AS CreatedAt
FROM iot.tt19_alarm_log
WHERE hardware_id = @hardwareId
ORDER BY ts DESC
LIMIT @limit;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<AlarmLogDbRow>(
            sql, new { hardwareId, limit });

        return rows.Select(ToDto).ToList();
    }

    // ─── GetBreachSummary ─────────────────────────────────────────────────────

    /// <summary>
    /// Aggregates alarm log rows into per-sensor breach counts for a time window.
    /// Used by the Dashboard breach analytics panel.
    /// </summary>
    public async Task<List<BreachSummaryRow>> GetBreachSummary(
        string hardwareId, DateTime startUtc, DateTime endUtc)
    {
        const string sql = @"
SELECT
    field                   AS Field,
    alarm_type              AS AlarmType,
    COUNT(*)::int           AS BreachCount,
    MIN(ts)                 AS FirstBreachTs,
    MAX(ts)                 AS LastBreachTs,
    AVG(value)              AS AvgValue
FROM iot.tt19_alarm_log
WHERE hardware_id = @HardwareId
  AND ts >= @Start
  AND ts <= @End
GROUP BY field, alarm_type
ORDER BY BreachCount DESC
LIMIT 200;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<BreachSummaryRow>(
            sql, new { HardwareId = hardwareId, Start = startUtc, End = endUtc });

        return rows.ToList();
    }

    public class BreachSummaryRow
    {
        public string    Field         { get; set; } = "";
        public string    AlarmType     { get; set; } = "";
        public int       BreachCount   { get; set; }
        public DateTime? FirstBreachTs { get; set; }
        public DateTime? LastBreachTs  { get; set; }
        public double?   AvgValue      { get; set; }
    }

    // ─── Private Dapper mapping class ─────────────────────────────────────────
    // Maps raw DB column aliases to C# properties for QueryAsync<T>.
    // Private because callers use the public FleetAlarmLogRow DTO.

    private class AlarmLogDbRow
    {
        public long      Id           { get; set; }
        public string    HardwareId   { get; set; } = "";
        public DateTime? Ts           { get; set; }
        public string    AlarmType    { get; set; } = "";
        public string    Field        { get; set; } = "";
        public double?   Value     { get; set; }
        public double?   Threshold { get; set; }
        public string?   Message   { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    // ─── ToDto ────────────────────────────────────────────────────────────────

    private static FleetAlarmLogRow ToDto(AlarmLogDbRow r) => new()
    {
        Id           = r.Id,
        HardwareId   = r.HardwareId,
        Ts           = r.Ts?.ToUniversalTime().ToString("o"),
        AlarmType    = r.AlarmType,
        Field        = r.Field,
        Value     = r.Value,
        Threshold = r.Threshold,
        Message   = r.Message,
        CreatedAt = r.CreatedAt?.ToUniversalTime().ToString("o")
    };
}
