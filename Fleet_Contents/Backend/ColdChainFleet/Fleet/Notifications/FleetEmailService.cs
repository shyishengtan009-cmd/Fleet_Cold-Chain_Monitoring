using System;
using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using FleetCore.Fleet;
using MimeKit;
using System.Collections.Concurrent;

namespace FleetCore.Fleet.Notifications;

/// <summary>
/// Sends alarm notification emails via SMTP using the MailKit library.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   Configure      — reads SMTP settings from appsettings.json at startup
///   SendAlarmEmail — sends one alarm email (HTML or plain text)
///
/// ── SMTP setup (appsettings.json) ────────────────────────────────────────────
/// Add this section to appsettings.json (or appsettings.Production.json):
///
///   "FleetEmailSettings": {
///     "SmtpHost":    "smtp-relay.brevo.com",
///     "SmtpPort":    587,
///     "SmtpUser":    "your@email.com",
///     "SmtpPass":    "your_smtp_password",
///     "FromAddress": "alerts@yourdomain.com",
///     "FromName":    "Fleet Alert"
///   }
///
/// Tested with Brevo (formerly Sendinblue) transactional SMTP on port 587 with
/// STARTTLS (SecureSocketOptions.Auto). Other SMTP providers (Gmail, SendGrid)
/// should also work — just update the SmtpHost and credentials.
///
/// ── Who calls this ────────────────────────────────────────────────────────────
/// Called by FleetAlarmChecker.CheckAndNotify() and FleetAlarmChecker.CheckBatteryLevel()
/// after deciding that a notification should be sent.
/// The return value (bool success) is used to decide whether to update the device-level
/// cooldown timestamp and whether to log a success or failure in iot.tt19_email_log.
///
/// ── Why MailKit instead of SmtpClient ────────────────────────────────────────
/// System.Net.Mail.SmtpClient (the built-in .NET class) is marked obsolete for
/// async use on some platforms. MailKit is the recommended modern alternative —
/// it handles TLS negotiation, MIME construction, and authentication more robustly.
/// </summary>
public static class FleetEmailService
{
    // SMTP configuration — populated once by Configure() at service startup
    private static string _host     = "";
    private static int    _port     = 587;
    private static string _user     = "";
    private static string _pass     = "";
    private static string _fromAddr = "";
    private static string _fromName = "Fleet Alert";

    // Persistent SMTP connection pool — avoids TLS handshake + auth on every alarm.
    // Pool size 2 is enough: most alarm storms are per-device, rarely truly concurrent.
    private static readonly ConcurrentBag<SmtpClient> _pool = new();
    private static readonly SemaphoreSlim _poolLock = new(2, 2);

    // ─── Configure ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads SMTP settings from IConfiguration (appsettings.json).
    /// Must be called once at startup (from Program.cs or FleetIngestService constructor)
    /// before any emails can be sent.
    ///
    /// Logs a warning if SmtpHost is not configured — emails will be silently skipped
    /// rather than throwing exceptions at send time.
    /// </summary>
    public static void Configure(IConfiguration config)
    {
        _host     = config["FleetEmailSettings:SmtpHost"]    ?? "";
        _port     = config.GetValue<int>("FleetEmailSettings:SmtpPort", 587);
        _user     = config["FleetEmailSettings:SmtpUser"]    ?? "";
        _pass     = config["FleetEmailSettings:SmtpPass"]    ?? "";
        _fromAddr = config["FleetEmailSettings:FromAddress"] ?? "";
        _fromName = config["FleetEmailSettings:FromName"]    ?? "Fleet Alert";

        if (string.IsNullOrWhiteSpace(_host))
            FleetLog.Warn("[Fleet-Email] SmtpHost is not configured — alarm emails will not be sent.");
        else
            FleetLog.Info($"[Fleet-Email] Configured: {_host}:{_port} user={_user} from={_fromAddr}");
    }

    // ─── SendAlarmEmail ───────────────────────────────────────────────────────

    /// <summary>
    /// Sends one alarm notification email.
    ///
    /// If htmlBody is provided, the email is sent as HTML (rich format with the
    /// sensor table). Otherwise, a plain text fallback is sent.
    ///
    /// The email subject uses the sensor/field name for quick triage in the inbox:
    ///   "[Fleet Alert] temperature alarm on HWID_AABBCCDD"
    ///
    /// Returns true if the SMTP connection and send succeeded.
    /// Returns false (without throwing) if SMTP is not configured, the recipient
    /// address is empty, or an SMTP error occurs. All failures are logged.
    ///
    /// The result is used by the caller to:
    ///   - Update the device-level cooldown (only update if success)
    ///   - Write the correct success/failure flag in iot.tt19_email_log
    /// </summary>
    public static async Task<bool> SendAlarmEmailAsync(
        string  toEmail,
        string  hardwareId,
        string  sensor,
        double  value,
        string  unit,
        string  description,
        string? htmlBody = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_host))
        {
            FleetLog.Warn("[Fleet-Email] Not configured — set FleetEmailSettings in appsettings.json.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(toEmail))
        {
            FleetLog.Warn("[Fleet-Email] No recipient address — skipping.");
            return false;
        }

        try
        {
            var nowMyt = DateTime.UtcNow.AddHours(8);

            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(_fromName, _fromAddr));
            msg.To.Add(MailboxAddress.Parse(toEmail));
            msg.Subject = $"[Fleet Alert] {sensor} alarm on {hardwareId}";

            if (!string.IsNullOrWhiteSpace(htmlBody))
            {
                msg.Body = new TextPart("html") { Text = htmlBody };
            }
            else
            {
                msg.Body = new TextPart("plain")
                {
                    Text = $"Device:     {hardwareId}\n"    +
                           $"Sensor:     {sensor}\n"        +
                           $"Value:      {value:F1} {unit}\n" +
                           $"Details:    {description}\n"   +
                           $"Time (MYT): {nowMyt:dd MMM yyyy HH:mm:ss}"
                };
            }

            await _poolLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (!_pool.TryTake(out var client))
                    client = new SmtpClient();

                try
                {
                    if (!client.IsConnected)
                    {
                        await client.ConnectAsync(_host, _port, SecureSocketOptions.Auto, ct).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(_user))
                            await client.AuthenticateAsync(_user, _pass, ct).ConfigureAwait(false);
                    }
                    await client.SendAsync(msg, ct).ConfigureAwait(false);
                    _pool.Add(client); // return to pool
                }
                catch
                {
                    client.Dispose(); // discard on failure — next call gets a fresh one
                    throw;
                }
            }
            finally
            {
                _poolLock.Release();
            }

            FleetLog.Info($"[Fleet-Email] Sent to {toEmail} for {hardwareId}/{sensor}");
            return true;
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-Email] Failed to send to {toEmail}.", ex);
            return false;
        }
    }
}
