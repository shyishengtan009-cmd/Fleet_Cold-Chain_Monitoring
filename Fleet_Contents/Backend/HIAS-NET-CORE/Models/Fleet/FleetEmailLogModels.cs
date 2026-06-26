namespace HIAS_NET_CORE.Models.Fleet;

/// <summary>
/// Data needed to write one email send attempt into iot.tt19_email_log.
/// Passed directly to Dapper's ExecuteAsync — property names map to SQL parameters.
/// </summary>
public class FleetEmailLogEntry
{
    public string  HardwareId   { get; set; } = "";
    public string  Sensor       { get; set; } = "";
    public string  ToEmail      { get; set; } = "";
    public string? Description  { get; set; }
    public bool    Success      { get; set; }
    public string? ErrorMessage { get; set; }
    public long?   AlarmLogId   { get; set; }
}
