using System.Collections.Generic;
using System.Text;

namespace HIAS_NET_CORE.Fleet.Notifications;

/// <summary>
/// Shared HTML building blocks for all Fleet notification emails.
/// Centralises the common wrapper, info-table pattern, and row builder
/// so each email builder only specifies its unique content.
/// </summary>
public static class FleetEmailTemplate
{
    // ─── Page wrapper ─────────────────────────────────────────────────────────

    public static string HtmlPage(string iconTitle, string titleColor, string body)
        => $@"<!DOCTYPE html>
<html><body style=""font-family:sans-serif;font-size:14px;color:#333"">
<h3 style=""color:{titleColor};margin-bottom:4px"">{iconTitle}</h3>
{body}
</body></html>";

    // ─── Device / timestamp header line ──────────────────────────────────────

    public static string DeviceHeader(string hardwareId, DateTime nowLocal)
        => $"<p style=\"margin:0 0 4px\">Device: <strong>{hardwareId}</strong> &nbsp;|&nbsp; Time (MYT): {nowLocal:dd MMM yyyy HH:mm:ss}</p>";

    // ─── Coloured alert paragraph ─────────────────────────────────────────────

    public static string AlertParagraph(string text, string color)
        => $"<p style=\"margin:0 0 12px;color:{color}\"><strong>{text}</strong></p>";

    // ─── Info table ───────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a two-column label/value table. Rows with null or whitespace values
    /// are skipped so callers can pass optional fields without branching.
    /// Returns empty string when no rows have content.
    /// </summary>
    public static string InfoTable(IEnumerable<(string Label, string? Value)> rows)
    {
        var sb = new StringBuilder();
        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value)) continue;
            sb.Append(InfoRow(label, value));
        }
        return sb.Length == 0 ? ""
            : $"<table style=\"border-collapse:collapse;font-size:13px;margin-bottom:12px\">{sb}</table>";
    }

    public static string InfoRow(string label, string value)
        => $"<tr><td style=\"padding:3px 12px 3px 0;color:#777\">{label}</td><td><strong>{value}</strong></td></tr>";

    // ─── Sensor table header / footer ─────────────────────────────────────────

    public static string SensorTableOpen(string maxWidth = "560px")
        => $@"<table style=""border-collapse:collapse;width:100%;max-width:{maxWidth}"">
  <thead>
    <tr style=""background:#f0f0f0;font-weight:bold"">
      <th style=""padding:7px 12px;text-align:left;border:1px solid #ddd"">Parameter</th>
      <th style=""padding:7px 12px;text-align:right;border:1px solid #ddd"">Value</th>
      <th style=""padding:7px 12px;text-align:right;border:1px solid #ddd"">Min</th>
      <th style=""padding:7px 12px;text-align:right;border:1px solid #ddd"">Max</th>
      <th style=""padding:7px 12px;text-align:left;border:1px solid #ddd"">Status</th>
    </tr>
  </thead>
  <tbody>";

    public static string SensorTableClose() => "\n  </tbody>\n</table>";

    public static string SensorTableRow(string label, string valStr, string minStr, string maxStr, string status, string color)
    {
        var bold = color is "#1976d2" or "#888888" ? "" : "font-weight:bold;";
        return $@"
    <tr>
      <td style=""padding:6px 12px;border:1px solid #ddd;{bold}color:{color}"">{label}</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;text-align:right;{bold}color:{color}"">{valStr}</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;text-align:right;color:#666"">{minStr}</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;text-align:right;color:#666"">{maxStr}</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;{bold}color:{color}"">{status}</td>
    </tr>";
    }

    // ─── Section header + footer note ────────────────────────────────────────

    public static string SectionHeader(string title)
        => $"<h4 style=\"margin:16px 0 6px;color:#555\">{title}</h4>";

    public static string FooterNote(string text, string color = "#555")
        => $"<p style=\"margin-top:12px;color:{color}\">{text}</p>";

    // ─── Battery sensor table (2-column, different schema) ────────────────────

    public static string BatteryTableOpen(string maxWidth = "400px")
        => $@"<table style=""border-collapse:collapse;width:100%;max-width:{maxWidth}"">
  <thead>
    <tr style=""background:#f0f0f0;font-weight:bold"">
      <th style=""padding:7px 12px;text-align:left;border:1px solid #ddd"">Parameter</th>
      <th style=""padding:7px 12px;text-align:right;border:1px solid #ddd"">Value</th>
      <th style=""padding:7px 12px;text-align:right;border:1px solid #ddd"">Threshold</th>
      <th style=""padding:7px 12px;text-align:left;border:1px solid #ddd"">Status</th>
    </tr>
  </thead>
  <tbody>";

    public static string BatteryTableRow(double batt, int threshold)
        => $@"
    <tr>
      <td style=""padding:6px 12px;border:1px solid #ddd;font-weight:bold;color:#d32f2f"">Battery</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;text-align:right;font-weight:bold;color:#d32f2f"">{batt:F0} %</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;text-align:right;color:#666"">{threshold} %</td>
      <td style=""padding:6px 12px;border:1px solid #ddd;font-weight:bold;color:#d32f2f"">⚠ LOW</td>
    </tr>";

    public static string BatteryTableClose() => "\n  </tbody>\n</table>";
}
