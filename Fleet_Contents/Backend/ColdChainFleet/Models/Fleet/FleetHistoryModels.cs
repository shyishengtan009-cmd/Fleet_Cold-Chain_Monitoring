namespace FleetCore.Models.Fleet;

/// <summary>
/// Data models used by the Fleet History API endpoints.
///
/// History = sensor readings stored in iot.tt19_data, returned as a time-series
/// for charting and reporting on the frontend.
///
/// ── Used by ──────────────────────────────────────────────────────────────────
///   Controllers/FleetHistoryController.cs  — builds HTTP responses
///   Repositories/FleetHistoryRepository.cs — Dapper result mapping
/// </summary>

// ─── FleetHistoryMetaResponse ─────────────────────────────────────────────────

/// <summary>
/// Response from GET /api/fleet/history/meta — tells the frontend the date
/// range of available data for a device so it can pre-populate date pickers.
/// </summary>
public class FleetHistoryMetaResponse
{
    public bool    Found      { get; set; }
    public string  HardwareId { get; set; } = "";
    public string? MinTs      { get; set; }   // ISO-8601 UTC, oldest reading
    public string? MaxTs      { get; set; }   // ISO-8601 UTC, most recent reading
    public int     Count      { get; set; }
}

// ─── FleetHistoryRow ──────────────────────────────────────────────────────────

/// <summary>
/// A single sensor reading row returned in a history range query.
/// Used for building charts (temperature, humidity, light over time).
/// </summary>
public class FleetHistoryRow
{
    public string  Ts           { get; set; } = "";   // ISO-8601 UTC
    public float?  TemperatureC { get; set; }
    public float?  HumidityPct  { get; set; }
    public float?  LightLux     { get; set; }
    public int?    BatteryPct   { get; set; }
    public float?  VibrationG   { get; set; }         // from raw JSON field
}

// ─── FleetHistoryRangeResponse ────────────────────────────────────────────────

/// <summary>
/// Response from GET /api/fleet/history/range — all readings within the
/// requested UTC time window.
/// </summary>
public class FleetHistoryRangeResponse
{
    public string                 HardwareId { get; set; } = "";
    public int                    Count      { get; set; }
    public List<FleetHistoryRow>  Rows       { get; set; } = new();
}
