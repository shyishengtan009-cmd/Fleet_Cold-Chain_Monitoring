using System.Text.Json.Serialization;

namespace FleetCore.Models.Fleet;

/// <summary>
/// Data models used by the Fleet Alarm Log API endpoints.
///
/// ── What "alarm log" means ────────────────────────────────────────────────────
/// The alarm log is a permanent, append-only record of every alarm event that
/// fired during live ingest (i.e. debounce counter exceeded, notification sent).
/// It is distinct from the real-time alarm evaluation endpoint, which re-evaluates
/// thresholds on-the-fly against raw sensor readings.
///
/// ── Used by ──────────────────────────────────────────────────────────────────
///   Controllers/FleetAlarmLogController.cs   — builds HTTP responses
///   Fleet/Database/FleetDbAlarmLog.cs        — Dapper query result mapping
/// </summary>

// ─── FleetAlarmLogRow ──────────────────────────────────────────────────────────

/// <summary>
/// One alarm event row as returned to the frontend.
///
/// Property names use snake_case via [JsonPropertyName] to match the existing
/// API contract — the frontend expects snake_case keys in the "rows" array
/// and the controller returns this model directly without an intermediate mapping.
///
/// Timestamps (Ts, CreatedAt) are ISO-8601 UTC strings formatted in the
/// repository layer, matching the "o" format used across all other Fleet endpoints.
/// </summary>
public class FleetAlarmLogRow
{
    [JsonPropertyName("id")]
    public long    Id           { get; set; }

    [JsonPropertyName("hardware_id")]
    public string  HardwareId   { get; set; } = "";

    /// <summary>UTC timestamp of the sensor reading that triggered the alarm.</summary>
    [JsonPropertyName("ts")]
    public string? Ts           { get; set; }

    /// <summary>"ALARM" or "WARN"</summary>
    [JsonPropertyName("alarm_type")]
    public string  AlarmType    { get; set; } = "";

    /// <summary>Sensor field name: "temperature", "humidity", "light", "battery".</summary>
    [JsonPropertyName("field")]
    public string  Field        { get; set; } = "";

    [JsonPropertyName("value")]
    public double? Value        { get; set; }

    [JsonPropertyName("threshold")]
    public double? Threshold    { get; set; }

    [JsonPropertyName("message")]
    public string? Message      { get; set; }

    /// <summary>UTC timestamp of when this log row was written (after ingest decision).</summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt    { get; set; }
}

// ─── FleetAlarmLogEntry ───────────────────────────────────────────────────────

/// <summary>
/// The data needed to write one alarm event into iot.tt19_alarm_log.
/// Passed directly to Dapper's ExecuteAsync — property names map to SQL parameters.
/// Used by FleetDbAlarmLog.Insert() instead of individual method parameters.
/// </summary>
public class FleetAlarmLogEntry
{
    public string   HardwareId   { get; set; } = "";
    public DateTime Ts           { get; set; }
    public string   AlarmType    { get; set; } = "";
    public string   Field        { get; set; } = "";
    public double?  Value        { get; set; }
    public double?  Threshold    { get; set; }
    public string?  Message      { get; set; }
}
