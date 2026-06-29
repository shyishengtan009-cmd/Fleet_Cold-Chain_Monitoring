using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FleetCore.Fleet;

/// <summary>
/// QuestPDF document that renders a cold-chain trip report as a PDF.
///
/// Sections:
///   1. Header  — device, truck name, trip ID, generated timestamp
///   2. Trip summary  — start/end (MYT), duration, distance, GPS points
///   3. Sensor stats  — min/max/avg temperature, humidity, battery over the trip
///   4. Alarm events  — every alarm log entry during the trip
///   5. Sensor readings table  — all readings (capped at 500 rows to keep PDF size sane)
/// </summary>
public class FleetTripReportDocument : IDocument
{
    // ── Input ──────────────────────────────────────────────────────────────────
    public string                                    HardwareId   { get; init; } = "";
    public string                                    TruckName    { get; init; } = "";
    public Dictionary<string, object?>               Trip         { get; init; } = new();
    public List<Dictionary<string, object?>>         Readings     { get; init; } = new();
    public List<Dictionary<string, object?>>         AlarmRows    { get; init; } = new();

    // ── MYT helpers ───────────────────────────────────────────────────────────
    private static readonly TimeSpan Myt = TimeSpan.FromHours(8);

    private static string ToMyt(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return "—";
        if (!DateTimeOffset.TryParse(iso, out var dto)) return iso;
        var local = dto.ToOffset(Myt);
        return local.ToString("dd MMM yyyy  hh:mm tt");
    }

    private static string Duration(string? startIso, string? endIso)
    {
        if (!DateTimeOffset.TryParse(startIso, out var s)) return "—";
        if (!DateTimeOffset.TryParse(endIso,   out var e)) return "In progress";
        var span = e - s;
        return span.TotalHours >= 1
            ? $"{(int)span.TotalHours}h {span.Minutes:D2}m"
            : $"{span.Minutes}m {span.Seconds}s";
    }

    // ── Colour constants ──────────────────────────────────────────────────────
    private static readonly string C_Header   = "#1565C0";
    private static readonly string C_SubHead  = "#E3F2FD";
    private static readonly string C_AlarmRed = "#FFEBEE";
    private static readonly string C_AlarmOrg = "#FFF3E0";
    private static readonly string C_TableAlt = "#F5F5F5";

    // ─────────────────────────────────────────────────────────────────────────
    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(28);
            page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    // ── Header ────────────────────────────────────────────────────────────────
    private void ComposeHeader(IContainer c)
    {
        c.Column(col =>
        {
            col.Item().Background(C_Header).Padding(10).Row(row =>
            {
                row.RelativeItem().Column(inner =>
                {
                    inner.Item().Text("Cold Chain Trip Report")
                        .FontSize(16).Bold().FontColor(Colors.White);
                    inner.Item().Text($"Device: {HardwareId}   |   Truck: {(string.IsNullOrWhiteSpace(TruckName) ? "—" : TruckName)}")
                        .FontSize(9).FontColor(Colors.White).Italic();
                });
                row.AutoItem().AlignRight().AlignMiddle()
                    .Text($"Trip #{(Trip.TryGetValue("trip_id", out var tid) ? tid : "?")}")
                    .FontSize(13).Bold().FontColor(Colors.White);
            });

            col.Item().BorderBottom(1).BorderColor(C_Header).PaddingBottom(4);
        });
    }

    // ── Content ───────────────────────────────────────────────────────────────
    private void ComposeContent(IContainer c)
    {
        c.Column(col =>
        {
            col.Spacing(10);

            col.Item().Element(ComposeTripSummary);
            col.Item().Element(ComposeSensorStats);
            if (AlarmRows.Count > 0) col.Item().Element(ComposeAlarmTable);
            col.Item().Element(ComposeReadingsTable);
        });
    }

    // ── Trip summary ──────────────────────────────────────────────────────────
    private void ComposeTripSummary(IContainer c)
    {
        var start      = Trip.TryGetValue("start_time",         out var sv)  ? sv?.ToString()  : null;
        var end        = Trip.TryGetValue("end_time",           out var ev)  ? ev?.ToString()  : null;
        var distKm     = Trip.TryGetValue("total_distance_km",  out var dv)  ? dv              : null;
        var pts        = Trip.TryGetValue("points_count",       out var pv)  ? pv              : null;
        var slaMin     = Trip.TryGetValue("sla_minutes",        out var slav) && slav != null   ? (int?)Convert.ToInt32(slav) : null;
        var minsLate   = Trip.TryGetValue("minutes_late",       out var mlv)  && mlv  != null   ? (int?)Convert.ToInt32(mlv)  : null;
        var breachHot  = Trip.TryGetValue("breach_minutes_hot", out var bhv)  && bhv  != null   ? (int?)Convert.ToInt32(bhv)  : null;
        var breachCold = Trip.TryGetValue("breach_minutes_cold",out var bcv)  && bcv  != null   ? (int?)Convert.ToInt32(bcv)  : null;

        string SlaStatus() => slaMin.HasValue
            ? (minsLate.HasValue && minsLate.Value > 0 ? $"LATE +{minsLate.Value} min" : "On Time")
            : "—";
        string BreachHotStr()  => breachHot.HasValue  && breachHot.Value  > 0 ? $"{breachHot.Value} min"  : "None";
        string BreachColdStr() => breachCold.HasValue && breachCold.Value > 0 ? $"{breachCold.Value} min" : "None";

        c.Column(col =>
        {
            SectionTitle(col, "Trip Summary");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(3);
                });

                SummaryCell(t, "Start Time (MYT)",    ToMyt(start));
                SummaryCell(t, "End Time (MYT)",      ToMyt(end));
                SummaryCell(t, "Duration",             Duration(start, end));
                SummaryCell(t, "Distance",             distKm != null ? $"{Convert.ToDouble(distKm):F2} km" : "—");
                SummaryCell(t, "GPS Points",           pts?.ToString() ?? "—");
                SummaryCell(t, "Readings Captured",    Readings.Count.ToString());
                SummaryCell(t, "Alarm Events",         AlarmRows.Count.ToString());
                SummaryCell(t, "Status",               end != null ? "Completed" : "In Progress");
                SummaryCell(t, "Temp Breach (High)",   BreachHotStr());
                SummaryCell(t, "Temp Breach (Low)",    BreachColdStr());
                SummaryCell(t, "SLA Duration",         slaMin.HasValue ? $"{slaMin.Value} min" : "—");
                SummaryCell(t, "SLA Status",           SlaStatus());
            });
        });
    }

    // ── Sensor stats ──────────────────────────────────────────────────────────
    private void ComposeSensorStats(IContainer c)
    {
        if (Readings.Count == 0)
        {
            c.Text("No sensor readings available for this trip.").Italic().FontColor(Colors.Grey.Medium);
            return;
        }

        double? GetVal(Dictionary<string, object?> r, string key)
        {
            if (!r.TryGetValue(key, out var v) || v == null) return null;
            return double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
        }

        var temps  = Readings.Select(r => GetVal(r, "temperature_c")).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var hums   = Readings.Select(r => GetVal(r, "humidity_pct")).Where(v => v.HasValue).Select(v => v!.Value).ToList();
        var bats   = Readings.Select(r => GetVal(r, "battery_pct")).Where(v => v.HasValue).Select(v => v!.Value).ToList();

        c.Column(col =>
        {
            SectionTitle(col, "Sensor Statistics");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                });

                // Header row
                TableHeader(t, "Sensor");
                TableHeader(t, "Min");
                TableHeader(t, "Max");
                TableHeader(t, "Average");

                if (temps.Count > 0)
                {
                    TableCell(t, "Temperature (°C)");
                    TableCell(t, $"{temps.Min():F1}");
                    TableCell(t, $"{temps.Max():F1}");
                    TableCell(t, $"{temps.Average():F1}");
                }

                if (hums.Count > 0)
                {
                    TableCell(t, "Humidity (%)");
                    TableCell(t, $"{hums.Min():F1}");
                    TableCell(t, $"{hums.Max():F1}");
                    TableCell(t, $"{hums.Average():F1}");
                }

                if (bats.Count > 0)
                {
                    TableCell(t, "Battery (%)");
                    TableCell(t, $"{bats.Min():F0}");
                    TableCell(t, $"{bats.Max():F0}");
                    TableCell(t, $"{bats.Average():F0}");
                }
            });
        });
    }

    // ── Alarm table ───────────────────────────────────────────────────────────
    private void ComposeAlarmTable(IContainer c)
    {
        c.Column(col =>
        {
            SectionTitle(col, $"Alarm Events ({AlarmRows.Count})");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(5);
                });

                TableHeader(t, "Time (MYT)");
                TableHeader(t, "Sensor");
                TableHeader(t, "Level");
                TableHeader(t, "Message");

                var alt = false;
                foreach (var row in AlarmRows)
                {
                    var ts      = row.TryGetValue("ts",         out var tv) ? tv?.ToString() : null;
                    var field   = row.TryGetValue("field",      out var fv) ? fv?.ToString() : "—";
                    var level   = row.TryGetValue("alarm_type", out var lv) ? lv?.ToString() : "—";
                    var msg     = row.TryGetValue("message",    out var mv) ? mv?.ToString() : "—";
                    string bg   = level == "ALARM" ? C_AlarmRed : (level == "WARN" ? C_AlarmOrg : (alt ? C_TableAlt : (string)Colors.White));

                    TableCellBg(t, ToMyt(ts), bg);
                    TableCellBg(t, field ?? "—", bg);
                    TableCellBg(t, level ?? "—", bg, bold: level == "ALARM");
                    TableCellBg(t, msg ?? "—", bg);

                    alt = !alt;
                }
            });
        });
    }

    // ── Readings table ────────────────────────────────────────────────────────
    private void ComposeReadingsTable(IContainer c)
    {
        const int MaxRows = 500;
        var rows = Readings.Count > MaxRows ? Readings.TakeLast(MaxRows).ToList() : Readings;
        var truncated = Readings.Count > MaxRows;

        c.Column(col =>
        {
            SectionTitle(col, $"Sensor Readings ({Readings.Count}{(truncated ? $", showing last {MaxRows}" : "")})");

            col.Item().Table(t =>
            {
                t.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(3);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                    cd.RelativeColumn(2);
                });

                TableHeader(t, "Time (MYT)");
                TableHeader(t, "Temp (°C)");
                TableHeader(t, "Humidity (%)");
                TableHeader(t, "Light (lux)");
                TableHeader(t, "Battery (%)");

                var alt = false;
                foreach (var row in rows)
                {
                    string bg = alt ? C_TableAlt : Colors.White;
                    TableCellBg(t, ToMyt(row.TryGetValue("ts", out var tv) ? tv?.ToString() : null), bg);
                    TableCellBg(t, FmtVal(row, "temperature_c", "F1"), bg);
                    TableCellBg(t, FmtVal(row, "humidity_pct",  "F1"), bg);
                    TableCellBg(t, FmtVal(row, "light_lux",     "F0"), bg);
                    TableCellBg(t, FmtVal(row, "battery_pct",   "F0"), bg);
                    alt = !alt;
                }
            });
        });
    }

    // ── Footer ────────────────────────────────────────────────────────────────
    private static void ComposeFooter(IContainer c)
    {
        var myt = DateTimeOffset.UtcNow.ToOffset(Myt);
        c.BorderTop(1).BorderColor(Colors.Grey.Lighten2).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text("Generated by Fleet Cold-Chain Monitoring").FontSize(8).FontColor(Colors.Grey.Medium);
            row.AutoItem().AlignRight().Text($"Generated: {myt:dd MMM yyyy HH:mm} MYT").FontSize(8).FontColor(Colors.Grey.Medium);
        });
    }

    // ── Shared layout helpers ─────────────────────────────────────────────────

    private static void SectionTitle(ColumnDescriptor col, string title)
    {
        col.Item().Background(C_SubHead).Padding(5).PaddingLeft(8)
            .Text(title).FontSize(10).Bold().FontColor(C_Header);
    }

    private static void SummaryCell(TableDescriptor t, string label, string value)
    {
        t.Cell().Padding(4).Column(c =>
        {
            c.Item().Text(label).FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            c.Item().Text(value).FontSize(9.5f).Bold();
        });
    }

    private static void TableHeader(TableDescriptor t, string label)
    {
        t.Cell().Background(C_Header).Padding(5)
            .Text(label).FontSize(8.5f).Bold().FontColor(Colors.White);
    }

    private static void TableCell(TableDescriptor t, string value)
    {
        t.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
            .Text(value).FontSize(8.5f);
    }

    private static void TableCellBg(TableDescriptor t, string? value, string bg, bool bold = false)
    {
        var txt = t.Cell().Background(bg).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(4)
            .Text(value ?? "—").FontSize(8.5f);
        if (bold) txt.Bold();
    }

    private static string FmtVal(Dictionary<string, object?> row, string key, string fmt)
    {
        if (!row.TryGetValue(key, out var v) || v == null) return "—";
        return double.TryParse(v.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d)
            ? d.ToString(fmt) : "—";
    }
}
