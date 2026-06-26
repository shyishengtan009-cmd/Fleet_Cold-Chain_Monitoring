using Microsoft.Extensions.Configuration;

namespace FleetCore.Fleet.Notifications;

/// <summary>
/// Dispatcher for Fleet alarm notifications. Supports comma-separated recipient lists.
/// Routes emails through the parent platform's notification queue (production) or falls back to
/// direct SMTP via FleetEmailService (local dev). Returns true if at least one
/// send/enqueue succeeded.
///
/// ── Delivery paths ────────────────────────────────────────────────────────────
///
///   Production / UAT (DatabaseSettingsDev reachable):
///     → FleetEmailQueue.EnqueueAsync inserts into notification.notification_email
///     → SendEmailQueueJob (Quartz) delivers via Brevo / SendGrid / SMPP
///     → Delivery outcome tracked in notification.notification_email
///
///   Local dev (parent platform DB not available):
///     → FleetEmailService.SendAlarmEmailAsync sends direct SMTP
///     → FleetEmailService.cs is a local-only file (never committed)
///
/// ── Enabling email ────────────────────────────────────────────────────────────
/// Email is disabled by default (FleetEmailSettings:Enabled = false).
/// To enable, add to your appsettings.json (or appsettings.Production.json):
///
///   "FleetEmailSettings": {
///     "Enabled":     true,
///     "FromAddress": "alerts@yourdomain.com",
///     "FromName":    "Fleet Alert"
///   }
///
/// For local dev with direct SMTP, also add:
///   "SmtpHost", "SmtpPort", "SmtpUser", "SmtpPass"
///
/// No code changes required — flipping Enabled activates the full pipeline.
/// </summary>
public static class FleetEmailDispatch
{
    private static bool _enabled;

    /// <summary>
    /// Called once at startup (from FleetIngestService and FleetSimService constructors)
    /// to read the FleetEmailSettings:Enabled flag from configuration.
    /// </summary>
    public static void Configure(IConfiguration config)
    {
        _enabled = config.GetValue<bool>("FleetEmailSettings:Enabled", false);
        if (!_enabled)
            FleetLog.Warn("[Fleet-Email] ⚠ Email notifications are DISABLED — alarm emails will NOT be sent. " +
                          "Set FleetEmailSettings:Enabled = true in appsettings.json to enable.");
    }

    public static async Task<bool> SendAlarmToAll(
        string  toEmails,
        string  hardwareId,
        string  sensor,
        double  value,
        string  unit,
        string  description,
        string? htmlBody = null,
        CancellationToken ct = default)
    {
        if (!_enabled) return false;

        var addresses = Split(toEmails);
        if (addresses.Length == 0) return false;

        var subject = FleetEmailQueue.BuildSubject(hardwareId, description);
        var body    = htmlBody ?? FleetEmailTemplate.HtmlPage(
                          "⚠ Fleet Alert", "#d32f2f",
                          FleetEmailTemplate.AlertParagraph(description, "#d32f2f") +
                          FleetEmailTemplate.InfoTable(new[]
                          {
                              ("Device", (string?)hardwareId),
                              ("Sensor", (string?)sensor),
                              ("Value",  (string?)$"{value:F1} {unit}"),
                          }));

        var any = false;

        if (FleetEmailQueue.IsConfigured)
        {
            // Production path: enqueue → SendEmailQueueJob delivers.
            foreach (var addr in addresses)
                if (await FleetEmailQueue.EnqueueAsync(addr, subject, body, ct).ConfigureAwait(false))
                    any = true;
            return any;
        }

        // Local dev fallback: direct SMTP via FleetEmailService (local-only file).
        foreach (var addr in addresses)
            if (await FleetEmailService.SendAlarmEmailAsync(
                    addr, hardwareId, sensor, value, unit, description, htmlBody, ct)
                .ConfigureAwait(false))
                any = true;
        return any;
    }

    private static string[] Split(string? emails) =>
        string.IsNullOrWhiteSpace(emails)
            ? []
            : emails.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
