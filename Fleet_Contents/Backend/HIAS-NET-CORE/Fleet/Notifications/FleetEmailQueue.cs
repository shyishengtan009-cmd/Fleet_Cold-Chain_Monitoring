using System;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace HIAS_NET_CORE.Fleet.Notifications;

/// <summary>
/// Routes Fleet alarm notifications through the HIAS notification email queue
/// (notification.notification_email) so that SendEmailQueueJob delivers them
/// via the platform-configured provider (Brevo / SendGrid / SMPP).
///
/// ── Why queue instead of direct SMTP ─────────────────────────────────────────
///   - No duplicate SMTP credentials needed in Fleet config
///   - Retry on delivery failure is handled automatically by SendEmailQueueJob
///   - Unified email audit trail in notification.notification_email
///   - Multi-provider fallback (Brevo → SendGrid) managed by the HIAS job
///   - SendEmailQueueJob is [DisallowConcurrentExecution] so no duplicate sends
///
/// ── Fallback path ─────────────────────────────────────────────────────────────
/// If the HIAS main database is not reachable (local dev without the full stack),
/// IsConfigured returns false and FleetEmailDispatch falls back to FleetEmailService
/// for direct SMTP. This means:
///   - Production / UAT  → queue path (this class)
///   - Local dev machine → direct SMTP via local FleetEmailService.cs
///
/// ── Configuration ─────────────────────────────────────────────────────────────
/// Reads DatabaseSettingsDev (same section used by DatabaseContext) to build the
/// HIAS main DB connection string. Reads FleetEmailSettings:FromAddress / FromName
/// for the sender identity. No additional config keys required.
/// </summary>
public static class FleetEmailQueue
{
    private static DatabaseContext? _databaseContext;
    private static string  _fromEmail = "";
    private static string  _fromName  = "Fleet Alert";

    // ─── Configure ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call once at startup (from FleetIngestService and FleetSimService constructors)
    /// after FleetEmailDispatch.Configure(). Stores the DatabaseContext so EnqueueAsync
    /// can reuse the application's existing connection pool rather than maintaining a
    /// separate Npgsql pool. If databaseContext is null, IsConfigured stays false and
    /// FleetEmailDispatch will use the direct-SMTP fallback.
    /// </summary>
    public static void Configure(IConfiguration config, DatabaseContext? databaseContext)
    {
        _databaseContext = databaseContext;

        _fromEmail = config["FleetEmailSettings:FromAddress"]
                  ?? config["BrevoMailSettings:UserName"]
                  ?? "";
        _fromName  = config["FleetEmailSettings:FromName"] ?? "Fleet Alert";

        if (IsConfigured)
            FleetLog.Info("[Fleet-EmailQueue] Queue path active — alarm emails will be delivered by SendEmailQueueJob.");
        else
            FleetLog.Info("[Fleet-EmailQueue] HIAS DB not configured — alarm emails will fall back to direct SMTP.");
    }

    // ─── IsConfigured ─────────────────────────────────────────────────────────

    /// <summary>
    /// True when the HIAS DB connection string and sender address are both set.
    /// FleetEmailDispatch checks this before deciding whether to queue or go direct.
    /// </summary>
    public static bool IsConfigured =>
        _databaseContext != null && !string.IsNullOrWhiteSpace(_fromEmail);

    // ─── EnqueueAsync ─────────────────────────────────────────────────────────

    /// <summary>
    /// Inserts one pending row into notification.notification_email (Status = 0).
    /// SendEmailQueueJob picks it up on its next execution and delivers it via the
    /// platform's configured email provider. Delivery outcome is tracked in that table.
    ///
    /// Returns true if the row was successfully inserted (enqueued), false on error.
    /// </summary>
    public static async Task<bool> EnqueueAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken ct = default)
    {
        if (!IsConfigured) return false;

        const string sql = @"
INSERT INTO notification.notification_email
    (EventId, UserId, StatusId, ToEmail, FromEmail, Cc, Bcc, Subject, Body, CreateBy, ToName, FromName, Status)
VALUES
    (0, 0, 0, @ToEmail, @FromEmail, '', '', @Subject, @Body, 0, @ToEmail, @FromName, 0);";

        var param = new
        {
            ToEmail   = toEmail,
            FromEmail = _fromEmail,
            Subject   = subject,
            Body      = htmlBody,
            FromName  = _fromName,
        };

        // Up to 3 attempts with 500 ms / 1000 ms back-off so a transient DB hiccup
        // doesn't permanently lose an alarm notification.
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var conn = (NpgsqlConnection)_databaseContext!.CreateConnection();
                await conn.OpenAsync(ct);
                await conn.ExecuteAsync(sql, param, commandTimeout: 10);
                FleetLog.Info($"[Fleet-EmailQueue] Queued alarm email → {toEmail} | {subject}");
                FleetMetrics.RecordEmailEnqueued();
                return true;
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                FleetLog.Warn($"[Fleet-EmailQueue] Enqueue attempt {attempt} failed — retrying in {attempt * 500} ms. {ex.Message}");
                await Task.Delay(attempt * 500, ct);
            }
            catch (Exception ex)
            {
                FleetLog.Error($"[Fleet-EmailQueue] Failed to enqueue email to {toEmail} after {maxAttempts} attempts.", ex);
                FleetMetrics.RecordEmailFailed();
                return false;
            }
        }

        return false;
    }

    // ─── BuildSubject ─────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a concise email subject from alarm parameters.
    /// Example: "[Fleet Alert] ABC-001 — Temperature is too high"
    /// </summary>
    public static string BuildSubject(string hardwareId, string description)
    {
        var prefix  = $"[Fleet Alert] {hardwareId} — ";
        var maxDesc = Math.Max(0, 150 - prefix.Length);
        var desc    = description.Length <= maxDesc ? description : description[..maxDesc] + "…";
        return prefix + desc;
    }
}
