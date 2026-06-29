using System.Threading.Tasks;
using Dapper;
using FleetCore.Context;
using FleetCore.Fleet;
using FleetCore.Models.Fleet;

namespace FleetCore.Repositories;

/// <summary>
/// Repository for the Fleet email log table (iot.tt19_email_log).
/// Records every email send attempt made by the Fleet alarm system,
/// including whether it succeeded or failed.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   Insert — log one email attempt (called after FleetEmailService.SendAlarmEmail)
///
/// ── Table: iot.tt19_email_log ─────────────────────────────────────────────────
/// Append-only — rows are never updated or deleted.
/// Columns: id, hardware_id, sensor, to_email, description,
///          success, error_message, alarm_log_id, created_at
///
/// ── Why this table exists ─────────────────────────────────────────────────────
/// It gives operations staff a way to check whether alarm emails are actually
/// being delivered, and to diagnose SMTP failures without reading server logs.
/// </summary>
public class FleetDbEmailLogRepository
{
    private readonly DatabaseContext _databaseContext;

    public FleetDbEmailLogRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── Insert ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts one email attempt record into the log.
    /// Called by FleetAlarmChecker immediately after each SendAlarmEmail call,
    /// regardless of whether the email succeeded or failed.
    /// </summary>
    public async Task Insert(FleetEmailLogEntry entry)
    {
        const string sql = @"
INSERT INTO iot.tt19_email_log
    (hardware_id, sensor, to_email, description, success, error_message, alarm_log_id)
VALUES
    (@HardwareId, @Sensor, @ToEmail, @Description, @Success, @ErrorMessage, @AlarmLogId);
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            await connection.ExecuteAsync(sql, entry);
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-EmailLog] Failed to insert for {entry.HardwareId}/{entry.Sensor}.", ex);
        }
    }
}
