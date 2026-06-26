using System;
using System.Linq;
using System.Threading.Tasks;
using FleetCore.Fleet;
using FleetCore.Models.Fleet;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace FleetCore.Controllers;

/// <summary>
/// Read access to the permanent alarm event log (iot.tt19_alarm_log). Unlike
/// /realtime/alarms (which evaluates readings on-the-fly), entries here were
/// written during ingest only when the debounce counter actually fired — these
/// are the alarms that triggered real notifications. Every endpoint enforces
/// org scoping via DeviceBelongsToOrg().
/// </summary>
[ApiController]
[Route("api/fleet/alarm_log")]
[Authorize]
public class FleetAlarmLogController : ControllerBase
{
    private readonly FleetDbAlarmLogRepository  _alarmLog;
    private readonly FleetDbDevicesRepository   _dbDevices;
    private readonly FleetDbRealtimeRepository  _dbRealtime;
    private readonly FleetDbSettingsRepository  _dbSettings;

    public FleetAlarmLogController(
        FleetDbAlarmLogRepository  alarmLog,
        FleetDbDevicesRepository   dbDevices,
        FleetDbRealtimeRepository  dbRealtime,
        FleetDbSettingsRepository  dbSettings)
    {
        _alarmLog   = alarmLog;
        _dbDevices  = dbDevices;
        _dbRealtime = dbRealtime;
        _dbSettings = dbSettings;
    }

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    /// <summary>
    /// Alarm entries after "since" (ISO-8601 UTC), or the most recent "limit" if omitted.
    /// Frontend polls this every 30s with the last-seen timestamp as "since".
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent(
        [FromQuery(Name = "hardware_id")] string hardwareId = "",
        [FromQuery(Name = "since")]       string sinceStr   = "",
        [FromQuery(Name = "limit")]       int    limit      = 20)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        limit = Math.Clamp(limit, 1, 200);

        // Use the "since" filter if a valid timestamp was provided, otherwise return most recent
        var rows = !string.IsNullOrWhiteSpace(sinceStr)
                   && DateTimeOffset.TryParse(sinceStr, out var sinceDto)
            ? await _alarmLog.GetSince(hardwareId, sinceDto.UtcDateTime, limit)
            : await _alarmLog.GetRecent(hardwareId, limit);

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new { hardwareId, count = rows.Count, rows }
        });
    }

    /// <summary>
    /// All alarm entries for one calendar date, in the device's local timezone.
    /// Backs the Alerts page's date-picker history view.
    /// </summary>
    [HttpGet("by_date")]
    public async Task<IActionResult> GetByDate(
        [FromQuery(Name = "hardware_id")] string hardwareId,
        [FromQuery(Name = "date")]        string dateStr,
        [FromQuery(Name = "limit")]       int    limit = 2000)
    {
        hardwareId = (hardwareId ?? "").Trim();
        dateStr    = (dateStr    ?? "").Trim();

        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (string.IsNullOrWhiteSpace(dateStr))
            return BadRequest(new { code = 400, message = "date is required (format: YYYY-MM-DD)" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsedDate))
            return BadRequest(new { code = 400, message = "Invalid date format. Use YYYY-MM-DD" });

        limit = Math.Clamp(limit, 1, 5000);

        // Compute full-day UTC window using the device's configured timezone.
        var tzInfo     = FleetTime.Resolve(await _dbDevices.GetTimezone(hardwareId));
        var tzOffset   = tzInfo.GetUtcOffset(parsedDate.Date);
        var startOfDay = new DateTimeOffset(parsedDate.Date,                         tzOffset).UtcDateTime;
        var endOfDay   = new DateTimeOffset(parsedDate.Date.AddDays(1).AddTicks(-1), tzOffset).UtcDateTime;

        var rows = await _alarmLog.GetByDateRange(hardwareId, startOfDay, endOfDay, limit);

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                hardwareId,
                date     = dateStr,
                startUtc = startOfDay.ToString("O"),
                endUtc   = endOfDay.ToString("O"),
                count    = rows.Count,
                rows
            }
        });
    }

    /// <summary>
    /// Raw sensor readings for a date, reshaped into alarm-log row format so the
    /// Alert page can show a merged view of events + readings for the day.
    /// </summary>
    [HttpGet("sensor_readings")]
    public async Task<IActionResult> GetSensorReadings(
        [FromQuery(Name = "hardware_id")]     string hardwareId,
        [FromQuery(Name = "date")]            string dateStr,
        [FromQuery(Name = "limit")]           int    limit          = 2000,
        [FromQuery(Name = "organization_id")] int?   organizationId = null)
    {
        hardwareId = (hardwareId ?? "").Trim();
        dateStr    = (dateStr    ?? "").Trim();

        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (string.IsNullOrWhiteSpace(dateStr))
            return BadRequest(new { code = 400, message = "date is required (format: YYYY-MM-DD)" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        if (!DateTime.TryParseExact(dateStr, "yyyy-MM-dd",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsedDate))
            return BadRequest(new { code = 400, message = "Invalid date format. Use YYYY-MM-DD" });

        limit = Math.Clamp(limit, 1, 5000);

        var tzInfo     = FleetTime.Resolve(await _dbDevices.GetTimezone(hardwareId));
        var tzOffset   = tzInfo.GetUtcOffset(parsedDate.Date);
        var startOfDay = new DateTimeOffset(parsedDate.Date,                         tzOffset).UtcDateTime;
        var endOfDay   = new DateTimeOffset(parsedDate.Date.AddDays(1).AddTicks(-1), tzOffset).UtcDateTime;

        var sensorData = await _dbRealtime.GetRowsForRange(hardwareId, startOfDay, endOfDay, limit);

        // Load device thresholds once for the whole batch — used by SensorStatus below
        var settings = await _dbSettings.GetDeviceSettings(hardwareId);
        var alarmObj = settings?["alarm_json"] as JObject;
        double? tempMax = alarmObj?["temp_max_c"]?.Value<double?>();
        double? tempMin = alarmObj?["temp_min_c"]?.Value<double?>();
        double? humMax  = alarmObj?["humidity_max_pct"]?.Value<double?>();
        double? humMin  = alarmObj?["humidity_min_pct"]?.Value<double?>();
        double? batMin  = alarmObj?["battery_min_pct"]?.Value<double?>();

        string SensorStatus(double t, double h, double b)
        {
            if (tempMax.HasValue && t >= tempMax.Value || tempMin.HasValue && t <= tempMin.Value) return "ALARM";
            if (batMin.HasValue  && b <= batMin.Value)                                            return "ALARM";
            if (humMax.HasValue  && h >= humMax.Value || humMin.HasValue && h <= humMin.Value)    return "WARN";
            return "OK";
        }

        // Shape raw sensor rows into the alarm-log format the frontend expects
        var rows = sensorData.Select((row, idx) =>
        {
            var temp     = row.TryGetValue("temperature_c", out var t) ? Convert.ToDouble(t ?? 0) : 0;
            var humidity = row.TryGetValue("humidity_pct",  out var h) ? Convert.ToDouble(h ?? 0) : 0;
            var light    = row.TryGetValue("light_lux",     out var l) ? Convert.ToDouble(l ?? 0) : 0;
            var battery  = row.TryGetValue("battery_pct",   out var b) ? Convert.ToDouble(b ?? 0) : 0;
            var ts       = row.TryGetValue("ts",            out var tsVal) ? tsVal?.ToString() : "";

            return new System.Collections.Generic.Dictionary<string, object?>
            {
                ["id"]            = idx + 1,
                ["hardware_id"]   = hardwareId,
                ["ts"]            = ts,
                ["alarm_type"]    = SensorStatus(temp, humidity, battery),
                ["field"]         = "temperature",
                ["value"]         = temp,
                ["threshold"]  = null,
                ["message"]    = $"Temp: {temp:F1}C, Humidity: {humidity:F1}%, Light: {light:F0} lux, Battery: {battery:F0}%",
                ["created_at"] = ts,
                ["temperature"]   = temp,
                ["humidity"]      = humidity,
                ["light"]         = light,
                ["battery"]       = battery
            };
        }).ToList();

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                hardwareId,
                date     = dateStr,
                startUtc = startOfDay.ToString("O"),
                endUtc   = endOfDay.ToString("O"),
                count    = rows.Count,
                rows
            }
        });
    }

    /// <summary>Per-sensor breach counts over a lookback window (1-365 days, default 30).</summary>
    [HttpGet("breach-summary")]
    public async Task<IActionResult> GetBreachSummary(
        [FromQuery(Name = "hardware_id")] string hardwareId,
        [FromQuery(Name = "days")]        int    days = 30)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        days = Math.Clamp(days, 1, 365);

        var end   = DateTime.UtcNow;
        var start = end.AddDays(-days);

        var breakdown = await _alarmLog.GetBreachSummary(hardwareId, start, end);

        var result = breakdown.Select(r => new
        {
            field         = r.Field,
            alarmType     = r.AlarmType,
            breachCount   = r.BreachCount,
            avgValue      = r.AvgValue.HasValue ? Math.Round(r.AvgValue.Value, 2) : (double?)null,
            firstBreachTs = r.FirstBreachTs?.ToUniversalTime().ToString("o"),
            lastBreachTs  = r.LastBreachTs?.ToUniversalTime().ToString("o")
        }).ToList();

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                hardwareId,
                days,
                totalBreaches = result.Sum(r => r.breachCount),
                breakdown     = result
            }
        });
    }

    /// <summary>
    /// Inserts a synthetic "[TEST]" alarm and pushes it over SignalR, so the
    /// notification pipeline can be verified without waiting for a real breach.
    /// </summary>
    [HttpPost("test/{hardwareId}")]
    public async Task<IActionResult> TestAlarm(string hardwareId)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        var now = DateTime.UtcNow;
        var entry = new FleetAlarmLogEntry
        {
            HardwareId = hardwareId,
            Ts         = now,
            AlarmType  = "ALARM",
            Field      = "temperature",
            Value      = 35.2,
            Threshold  = 30.0,
            Message    = $"[TEST] Temperature 35.2°C exceeds threshold 30°C on device {hardwareId}"
        };

        await _alarmLog.Insert(entry);

        _ = FleetAlarmPusher.Push(hardwareId, new
        {
            id          = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            hardware_id = hardwareId,
            ts          = now.ToString("o"),
            alarm_type  = "ALARM",
            field       = "temperature",
            value       = (double?)35.2,
            threshold   = (double?)30.0,
            message     = entry.Message,
            created_at  = now.ToString("o")
        });

        return Ok(new
        {
            code    = 0,
            message = "Test alarm inserted and pushed.",
            details = new { hardwareId, signalrPushed = true, alarmLogInserted = true }
        });
    }
}
