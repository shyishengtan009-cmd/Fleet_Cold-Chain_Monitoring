using System;
using System.Linq;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Models.Fleet;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Provides access to the permanent alarm event log stored in iot.tt19_alarm_log.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET /api/fleet/alarm_log/recent          — latest alarms (polling endpoint)
///   GET /api/fleet/alarm_log/by_date         — all alarms for a specific date
///   GET /api/fleet/alarm_log/sensor_readings — all sensor readings for a date
///                                              (used to build the alert report table)
///
/// ── Difference between alarm_log and realtime/alarms ─────────────────────────
///   /realtime/alarms  — evaluates readings on-the-fly; includes WARN + ALARM;
///                       no debounce; purely for retrospective analysis
///   /alarm_log/*      — reads iot.tt19_alarm_log; entries here were written
///                       during live ingest only when the debounce counter fired;
///                       these are the "real" alarms that triggered notifications
///
/// ── Frontend polling ─────────────────────────────────────────────────────────
///   The Cold Truck Alert page polls /recent every 30 seconds and passes a
///   "since" timestamp so only newly-written alarm entries are returned.
///   This drives the toast notifications and the alert count badge.
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   Every endpoint enforces FleetDbDevicesRepository.DeviceBelongsToOrg() so one org
///   cannot read another org's alarm log.
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

    // ─── Helper: extract OrganizationId from JWT claim ────────────────────────

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    // ─── GET /api/fleet/alarm_log/recent ─────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/alarm_log/recent?hardware_id=...&amp;since=2026-03-04T10:00:00Z&amp;limit=20
    ///
    /// Returns alarm log entries created AFTER the "since" timestamp.
    /// If "since" is omitted, returns the most recent "limit" entries.
    ///
    /// Typical usage (frontend polling):
    ///   Poll every 30s. On first load, omit "since" to get recent history.
    ///   On subsequent polls, pass the timestamp of the last received entry
    ///   as "since" so only new entries are returned.
    ///
    /// Query parameters:
    ///   hardware_id — required; the device to query
    ///   since       — optional; ISO-8601 UTC; return only entries after this time
    ///   limit       — max entries returned, clamped to 1–200 (default 20)
    ///
    /// Response:
    /// {
    ///   "code": 0, "message": "Success",
    ///   "details": {
    ///     "hardwareId": "HWID_AABBCCDD",
    ///     "count": 3,
    ///     "rows": [
    ///       { "id": 101, "hardware_id": "...", "sensor": "temperature", "value": 27.3,
    ///         "message": "Temperature 27.3°C exceeded max 8°C", "fired_at": "..." },
    ///       ...
    ///     ]
    ///   }
    /// }
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

    // ─── GET /api/fleet/alarm_log/by_date ────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/alarm_log/by_date?hardware_id=...&amp;date=2026-03-16
    ///
    /// Returns all alarm log entries for the given calendar date (local timezone).
    /// "Date" is interpreted in local time: midnight to 23:59:59.9999999 local,
    /// then converted to UTC for the DB query.
    ///
    /// Query parameters:
    ///   hardware_id — required; device to query
    ///   date        — required; date in YYYY-MM-DD format (e.g. "2026-03-16")
    ///   limit       — max entries returned, clamped to 1–5000 (default 2000)
    ///
    /// Used by the Cold Truck Alerts page's date selector to show all alarms for
    /// a specific day in the alert history table.
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

    // ─── GET /api/fleet/alarm_log/sensor_readings ────────────────────────────

    /// <summary>
    /// GET /api/fleet/alarm_log/sensor_readings?hardware_id=...&amp;date=2026-03-16&amp;limit=2000
    ///
    /// Returns all raw sensor readings for a specific date, shaped into the
    /// alarm-log row format used by the Alert Report table in the frontend.
    ///
    /// Unlike /by_date (which reads iot.tt19_alarm_log), this endpoint reads
    /// iot.tt19_data — the full sensor reading history — and applies a simple
    /// status classification: temperature > 25°C = "WARNING", otherwise "OK".
    ///
    /// This gives the alert page a merged view of both alarm events and raw
    /// readings so operators can see the full picture for the day.
    ///
    /// Query parameters:
    ///   hardware_id — required; device to query
    ///   date        — required; YYYY-MM-DD
    ///   limit       — max rows, clamped to 1–5000 (default 2000)
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

    // ─── GET /api/fleet/alarm_log/breach-summary ─────────────────────────────

    /// <summary>
    /// GET /api/fleet/alarm_log/breach-summary?hardware_id=...&amp;days=30
    ///
    /// Aggregates alarm log rows into per-sensor breach counts for the specified
    /// lookback window. Useful for dashboard analytics showing which sensors
    /// breach most frequently.
    ///
    /// Query parameters:
    ///   hardware_id — required; device to query
    ///   days        — lookback window in days, 1–365 (default 30)
    ///
    /// Response:
    /// {
    ///   "details": {
    ///     "hardwareId": "HWID_...",
    ///     "days": 30,
    ///     "totalBreaches": 12,
    ///     "breakdown": [
    ///       { "field": "temperature", "alarmType": "ALARM", "breachCount": 8,
    ///         "avgValue": 27.3, "firstBreachTs": "...", "lastBreachTs": "..." },
    ///       ...
    ///     ]
    ///   }
    /// }
    /// </summary>
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

    // ─── POST /api/fleet/alarm_log/test/{hardwareId} ──────────────────────────

    /// <summary>
    /// POST /api/fleet/alarm_log/test/{hardwareId}
    ///
    /// Inserts a synthetic "[TEST]" alarm log entry and pushes it via SignalR
    /// so the frontend toast/notification pipeline can be verified end-to-end
    /// without waiting for a real threshold breach.
    ///
    /// The test entry uses fixed values (temperature 35.2°C, threshold 30°C)
    /// and is indistinguishable from a real alarm in the log — the "[TEST]"
    /// prefix in the message is the only marker.
    ///
    /// Returns: { signalrPushed: true, alarmLogInserted: true }
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
