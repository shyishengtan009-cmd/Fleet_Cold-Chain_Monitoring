namespace FleetCore.Models.Fleet;

/// <summary>
/// Data models used by the Fleet Status API endpoints and their repositories.
///
/// ── Naming ───────────────────────────────────────────────────────────────────
/// "Status" in these model names refers to the fleet dashboard view that shows
/// the most recent reading from every sensor and classifies it as OK / WARN / OFFLINE.
/// It is NOT related to HTTP status codes.
///
/// ── Used by ──────────────────────────────────────────────────────────────────
///   Controllers/FleetStatusController.cs  — builds the HTTP response
///   Repositories/FleetStatusRepository.cs — Dapper query result mapping
/// </summary>

// ─── FleetStatusRow ───────────────────────────────────────────────────────────

/// <summary>
/// Raw database row returned by the fleet status query.
/// Mapped directly from SQL via Dapper — one row per hardware device.
/// </summary>
public class FleetStatusRow
{
    public string    HardwareId   { get; set; } = "";
    public DateTime? Ts           { get; set; }      // UTC timestamp of last reading
    public float?    TemperatureC { get; set; }
    public float?    HumidityPct  { get; set; }
    public float?    LightLux     { get; set; }
    public int?      BatteryPct   { get; set; }
    public string?   RawJson      { get; set; }      // raw JSONB column as text
    public long?     TruckId      { get; set; }
    public string?   SensorName   { get; set; }
    public string?   TruckName    { get; set; }
    public string?   Plate        { get; set; }
}

// ─── FleetStatusItem ──────────────────────────────────────────────────────────

/// <summary>
/// A single device's status as returned to the frontend.
/// Status is one of: "OK", "WARN", or "OFFLINE" — derived from how old the reading is.
///
///   OK      = reading age &lt; warn_seconds   (device is live)
///   WARN    = reading age &lt; offline_seconds (slightly delayed)
///   OFFLINE = reading age ≥ offline_seconds  (no data for a while)
/// </summary>
public class FleetStatusItem
{
    public string  Status       { get; set; } = "OFFLINE";
    public string  HardwareId   { get; set; } = "";
    public float?  TemperatureC { get; set; }
    public float?  HumidityPct  { get; set; }
    public float?  LightLux     { get; set; }
    public int?    BatteryPct   { get; set; }
    public string? Ts           { get; set; }        // ISO-8601 UTC string
    public int?    AgeSeconds   { get; set; }        // seconds since last reading
    public long?   TruckId      { get; set; }
    public string? TruckName    { get; set; }
    public string? Plate        { get; set; }
    public string? SensorName   { get; set; }
    public Dictionary<string, object?>? Raw { get; set; }   // parsed raw JSON fields
}

// ─── FleetStatusResponse ──────────────────────────────────────────────────────

/// <summary>
/// Top-level response envelope for GET /api/fleet/status.
/// </summary>
public class FleetStatusResponse
{
    public string             NowUtc         { get; set; } = "";   // ISO-8601 UTC
    public int                WarnSeconds    { get; set; }
    public int                OfflineSeconds { get; set; }
    public int                Count          { get; set; }
    public List<FleetStatusItem> Items       { get; set; } = new();
}
