using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;
using Npgsql;

namespace HIAS_NET_CORE.Repositories;

/// <summary>
/// Tracks per-device, per-sensor alarm state for debounce and email cooldown logic.
///
/// ── What this file does ───────────────────────────────────────────────────────
/// FleetAlarmChecker needs to know, for each device+sensor combination:
///   - Is it currently alarming?
///   - How many consecutive breach readings have we seen?  (for debounce)
///   - When did we last send an email for this device?     (for cooldown)
///
/// All of this per-sensor state lives in iot.tt19_alarm_state.
///
/// ── Debounce explained ────────────────────────────────────────────────────────
/// A sensor must exceed the threshold for N consecutive readings before an
/// alarm is fired. This prevents one bad/noisy reading from triggering alerts.
/// N is set by "debounce_count" in the device's alarm_json settings.
///
/// ── Cooldown explained ────────────────────────────────────────────────────────
/// Even if many sensors are alarming, only ONE combined notification is sent
/// per device per cooldown window (default 30 minutes). This prevents spam.
/// The cooldown is tracked using the special sensor key "_device".
///
/// ── Table: iot.tt19_alarm_state ───────────────────────────────────────────────
/// Key: (hardware_id, sensor) — one row per device+sensor combination.
/// </summary>

// ─── FleetAlarmStateRow ───────────────────────────────────────────────────────

/// <summary>
/// Represents one row in iot.tt19_alarm_state.
/// Loaded by GetOrCreate(), modified in-memory, then persisted by Save().
/// </summary>
public class FleetAlarmStateRow
{
    public int       Id               { get; set; }
    public string    HardwareId       { get; set; } = "";
    public string    Sensor           { get; set; } = "";    // e.g. "temperature", "_device"
    public bool      IsAlarming       { get; set; }
    public DateTime? AlarmStartedAt   { get; set; }
    public DateTime? LastAlarmedAt    { get; set; }
    public DateTime? LastEmailSentAt  { get; set; }          // used for device-level cooldown
    public int       ConsecutiveCount { get; set; }          // debounce counter
}

// ─── FleetDbAlarmStateRepository ────────────────────────────────────────────────────────

public class FleetDbAlarmStateRepository
{
    private readonly DatabaseContext _databaseContext;

    public FleetDbAlarmStateRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── GetOrCreate ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the alarm state row for a device+sensor pair.
    /// If no row exists yet, inserts a default row and returns it.
    ///
    /// Callers modify the returned object in-memory, then call Save() to persist.
    ///
    /// The sensor key "_device" is a special virtual sensor used to track the
    /// device-level notification cooldown (last email sent timestamp).
    /// </summary>
    public async Task<FleetAlarmStateRow> GetOrCreate(string hardwareId, string sensor)
    {
        // CTE: INSERT DO NOTHING (no write when row exists), then SELECT from either
        // the freshly-inserted row or the pre-existing one — single round-trip, zero
        // no-op WAL write on the hot path.
        const string sql = @"
WITH ins AS (
    INSERT INTO iot.tt19_alarm_state (hardware_id, sensor)
    VALUES (@HardwareId, @Sensor)
    ON CONFLICT (hardware_id, sensor) DO NOTHING
    RETURNING id, hardware_id, sensor, is_alarming, alarm_started_at,
              last_alarmed_at, last_email_sent_at, consecutive_count
)
SELECT id AS Id, hardware_id AS HardwareId, sensor AS Sensor,
       is_alarming AS IsAlarming, alarm_started_at AS AlarmStartedAt,
       last_alarmed_at AS LastAlarmedAt, last_email_sent_at AS LastEmailSentAt,
       consecutive_count AS ConsecutiveCount
FROM ins
UNION ALL
SELECT id AS Id, hardware_id AS HardwareId, sensor AS Sensor,
       is_alarming AS IsAlarming, alarm_started_at AS AlarmStartedAt,
       last_alarmed_at AS LastAlarmedAt, last_email_sent_at AS LastEmailSentAt,
       consecutive_count AS ConsecutiveCount
FROM iot.tt19_alarm_state
WHERE hardware_id = @HardwareId AND sensor = @Sensor
  AND NOT EXISTS (SELECT 1 FROM ins);
";
        try
        {
            var param = new { HardwareId = hardwareId, Sensor = sensor };
            using var connection = _databaseContext.CreateConnection();
            var row = await connection.QueryFirstOrDefaultAsync<FleetAlarmStateRow>(sql, param);
            return row ?? new FleetAlarmStateRow { HardwareId = hardwareId, Sensor = sensor };
        }
        catch (Exception ex)
        {
            FleetLog.Error("[Fleet-AlarmState] GetOrCreate failed.", ex);
            return new FleetAlarmStateRow { HardwareId = hardwareId, Sensor = sensor };
        }
    }

    // ─── Save ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Persists an updated FleetAlarmStateRow back to the database.
    /// Called after modifying a state row in FleetAlarmChecker.
    /// </summary>
    public async Task Save(FleetAlarmStateRow state)
    {
        const string sql = @"
UPDATE iot.tt19_alarm_state
SET    is_alarming        = @IsAlarming,
       alarm_started_at   = @AlarmStartedAt,
       last_alarmed_at    = @LastAlarmedAt,
       last_email_sent_at = @LastEmailSentAt,
       consecutive_count  = @ConsecutiveCount,
       updated_at         = NOW()
WHERE  hardware_id = @HardwareId
  AND  sensor      = @Sensor;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            await connection.ExecuteAsync(sql, state);
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-AlarmState] Save failed for {state.HardwareId}/{state.Sensor}.", ex);
        }
    }

    // ─── GetAllForDevice ──────────────────────────────────────────────────────

    /// <summary>
    /// Loads all alarm state rows for a device in a single query.
    /// Returns a dictionary keyed by sensor name.
    /// Used by FleetAlarmChecker to batch-load all states at the start of each
    /// poll cycle, eliminating N individual GetOrCreate round-trips.
    /// </summary>
    public async Task<Dictionary<string, FleetAlarmStateRow>> GetAllForDevice(string hardwareId)
    {
        const string sql = @"
SELECT id                 AS Id,
       hardware_id        AS HardwareId,
       sensor             AS Sensor,
       is_alarming        AS IsAlarming,
       alarm_started_at   AS AlarmStartedAt,
       last_alarmed_at    AS LastAlarmedAt,
       last_email_sent_at AS LastEmailSentAt,
       consecutive_count  AS ConsecutiveCount
FROM iot.tt19_alarm_state
WHERE hardware_id = @HardwareId;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            var rows = await connection.QueryAsync<FleetAlarmStateRow>(sql, new { HardwareId = hardwareId });
            return rows.ToDictionary(r => r.Sensor, r => r);
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-AlarmState] GetAllForDevice failed for {hardwareId}.", ex);
            return new Dictionary<string, FleetAlarmStateRow>();
        }
    }

    // ─── SaveAll ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts all dirty alarm state rows in a single transaction.
    /// Called once at the end of a CheckAndNotify / CheckBatteryLevel cycle
    /// to replace the per-sensor Save round-trips with one batched write.
    /// </summary>
    public async Task SaveAll(IEnumerable<FleetAlarmStateRow> states)
    {
        const string sql = @"
INSERT INTO iot.tt19_alarm_state
    (hardware_id, sensor, is_alarming, alarm_started_at, last_alarmed_at, last_email_sent_at, consecutive_count, updated_at)
VALUES
    (@HardwareId, @Sensor, @IsAlarming, @AlarmStartedAt, @LastAlarmedAt, @LastEmailSentAt, @ConsecutiveCount, NOW())
ON CONFLICT (hardware_id, sensor) DO UPDATE SET
    is_alarming        = EXCLUDED.is_alarming,
    alarm_started_at   = EXCLUDED.alarm_started_at,
    last_alarmed_at    = EXCLUDED.last_alarmed_at,
    last_email_sent_at = EXCLUDED.last_email_sent_at,
    consecutive_count  = EXCLUDED.consecutive_count,
    updated_at         = NOW();
";
        var rows = states.ToList();
        if (rows.Count == 0) return;
        try
        {
            using var connection = (NpgsqlConnection)_databaseContext.CreateConnection();
            await connection.OpenAsync();
            await using var tx = await connection.BeginTransactionAsync();
            await connection.ExecuteAsync(sql, rows, transaction: tx);
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-AlarmState] SaveAll failed ({rows.Count} rows).", ex);
        }
    }
}
