using System;
using System.Collections.Generic;
using System.Globalization;
using FleetCore.Fleet;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace FleetCore.Controllers;

/// <summary>
/// Real-time sensor data and retrospective alarm evaluation for a single device.
/// /alarms evaluates every reading in a window against current thresholds inline
/// (unlike iot.tt19_alarm_log, which only gets entries when the live debounce
/// counter fires) — so it works for "what would have triggered" analysis without
/// FleetAlarmChecker's debounce/cooldown state.
/// </summary>
[Authorize]
[ApiController]
[Route("api/fleet/realtime")]
public class FleetRealtimeController : ControllerBase
{
    private readonly FleetDbDevicesRepository  _dbDevices;
    private readonly FleetDbRealtimeRepository _dbRealtime;
    private readonly FleetDbSettingsRepository _dbSettings;

    public FleetRealtimeController(FleetDbDevicesRepository dbDevices, FleetDbRealtimeRepository dbRealtime, FleetDbSettingsRepository dbSettings)
    {
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

    /// <summary>Most recent reading for a device. Backs the Real-Time Monitoring page.</summary>
    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest([FromQuery(Name = "hardware_id")] string hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        // TODO: re-enable per-org ownership check before multi-tenant production deployment.
        var row = await _dbRealtime.GetLatestRow(hardwareId.Trim());

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new { found = row is not null, row }
        });
    }

    /// <summary>
    /// Readings in a window that exceeded (ALARM) or approached (WARN) current
    /// thresholds, newest-first. Backs the Alert Report and Dashboard alarm tab.
    /// </summary>
    [HttpGet("alarms")]
    public async Task<IActionResult> GetAlarms(
        [FromQuery(Name = "hardware_id")] string  hardwareId,
        [FromQuery(Name = "start_utc")]   string? startUtc = null,
        [FromQuery(Name = "end_utc")]     string? endUtc   = null,
        [FromQuery(Name = "limit")]       int     limit    = 10000)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        // TODO: re-enable per-org ownership check before multi-tenant production deployment.
        // Parse optional time bounds
        DateTime? start = null;
        DateTime? end   = null;

        if (!string.IsNullOrWhiteSpace(startUtc) && DateTimeOffset.TryParse(startUtc, out var sDto))
            start = sDto.UtcDateTime;
        if (!string.IsNullOrWhiteSpace(endUtc) && DateTimeOffset.TryParse(endUtc, out var eDto))
            end = eDto.UtcDateTime;

        limit = Math.Clamp(limit, 1, 50000);

        var rows = await _dbRealtime.GetRowsForRange(hardwareId.Trim(), start, end, limit);

        // Load alarm thresholds from device settings (alarm_json column)
        var settings  = await _dbSettings.GetDeviceSettings(hardwareId.Trim());
        var alarmJson = settings?["alarm_json"] as JObject ?? new JObject();

        // Helper: safely parse a numeric threshold from alarm_json
        double? GetNum(string key)
        {
            var tok = alarmJson.SelectToken(key);
            if (tok == null) return null;
            return double.TryParse(tok.ToString(), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var v) ? v : null;
        }

        var tempMin  = GetNum("temp_min_c");
        var tempMax  = GetNum("temp_max_c");
        var humMin   = GetNum("humidity_min_pct");
        var humMax   = GetNum("humidity_max_pct");
        var lightMin = GetNum("light_min_lux");
        var lightMax = GetNum("light_max_lux");

        var alarms = new List<Dictionary<string, object?>>();

        foreach (var r in rows)
        {
            var ts = r.TryGetValue("ts", out var tsObj) ? tsObj?.ToString() : null;

            double? t = null, h = null, l = null;

            if (r.TryGetValue("temperature_c", out var tObj) && tObj != null)
            {
                if (double.TryParse(tObj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var tt))
                    t = tt;
            }
            if (r.TryGetValue("humidity_pct", out var hObj) && hObj != null)
            {
                if (double.TryParse(hObj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var hh))
                    h = hh;
            }
            if (r.TryGetValue("light_lux", out var lObj) && lObj != null)
            {
                if (double.TryParse(lObj.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var ll))
                    l = ll;
            }

            void Add(string type, string message, string level, double? value) =>
                alarms.Add(new Dictionary<string, object?>
                {
                    ["ts"]      = ts,
                    ["type"]    = type,
                    ["level"]   = level,   // "ALARM" or "WARN"
                    ["message"] = message,
                    ["value"]   = value
                });

            // Temperature checks:
            // ALARM: reading exceeded max/min
            // WARN:  reading is within 10% of the limit (approaching the threshold).
            //        Using abs(threshold)*0.1 instead of threshold*0.1 so that warn
            //        bands work correctly for negative temperatures (e.g. -10°C freezer).
            if (t.HasValue)
            {
                if      (tempMax.HasValue && t.Value >= tempMax.Value)                                   Add("TEMP_HIGH",      $"Temperature {t.Value:F1}°C >= max {tempMax.Value:F1}°C",          "ALARM", t);
                else if (tempMax.HasValue && t.Value >= tempMax.Value - Math.Abs(tempMax.Value) * 0.1)  Add("TEMP_WARN_HIGH", $"Temperature {t.Value:F1}°C approaching max {tempMax.Value:F1}°C", "WARN",  t);

                if      (tempMin.HasValue && t.Value <= tempMin.Value)                                   Add("TEMP_LOW",       $"Temperature {t.Value:F1}°C <= min {tempMin.Value:F1}°C",          "ALARM", t);
                else if (tempMin.HasValue && t.Value <= tempMin.Value + Math.Abs(tempMin.Value) * 0.1)  Add("TEMP_WARN_LOW",  $"Temperature {t.Value:F1}°C approaching min {tempMin.Value:F1}°C", "WARN",  t);
            }

            // Humidity checks — same abs(threshold)*0.1 warn-band pattern as temperature,
            // so a humMin of 0% doesn't collapse the warn band to zero.
            if (h.HasValue)
            {
                if      (humMax.HasValue && h.Value >= humMax.Value)                                   Add("HUM_HIGH",      $"Humidity {h.Value:F1}% >= max {humMax.Value:F1}%",          "ALARM", h);
                else if (humMax.HasValue && h.Value >= humMax.Value - Math.Abs(humMax.Value) * 0.1)    Add("HUM_WARN_HIGH", $"Humidity {h.Value:F1}% approaching max {humMax.Value:F1}%",  "WARN",  h);

                if      (humMin.HasValue && h.Value <= humMin.Value)                                   Add("HUM_LOW",       $"Humidity {h.Value:F1}% <= min {humMin.Value:F1}%",          "ALARM", h);
                else if (humMin.HasValue && h.Value <= humMin.Value + Math.Abs(humMin.Value) * 0.1)    Add("HUM_WARN_LOW",  $"Humidity {h.Value:F1}% approaching min {humMin.Value:F1}%",  "WARN",  h);
            }

            // Light checks:
            if (l.HasValue)
            {
                if      (lightMax.HasValue && l.Value >= lightMax.Value)                                    Add("LIGHT_HIGH",      $"Light {l.Value:F0} lux >= max {lightMax.Value:F0} lux",          "ALARM", l);
                else if (lightMax.HasValue && l.Value >= lightMax.Value - Math.Abs(lightMax.Value) * 0.1)  Add("LIGHT_WARN_HIGH", $"Light {l.Value:F0} lux approaching max {lightMax.Value:F0} lux", "WARN",  l);

                if      (lightMin.HasValue && l.Value <= lightMin.Value)                                    Add("LIGHT_LOW",       $"Light {l.Value:F0} lux <= min {lightMin.Value:F0} lux",          "ALARM", l);
                else if (lightMin.HasValue && l.Value <= lightMin.Value + Math.Abs(lightMin.Value) * 0.1)  Add("LIGHT_WARN_LOW",  $"Light {l.Value:F0} lux approaching min {lightMin.Value:F0} lux", "WARN",  l);
            }
        }

        // Sort newest-first so the most recent alarm is at index 0
        alarms.Sort((a, b) =>
        {
            var aTs = a.TryGetValue("ts", out var av) ? av?.ToString() : null;
            var bTs = b.TryGetValue("ts", out var bv) ? bv?.ToString() : null;
            var aDt = DateTimeOffset.TryParse(aTs, out var ad) ? ad : DateTimeOffset.MinValue;
            var bDt = DateTimeOffset.TryParse(bTs, out var bd) ? bd : DateTimeOffset.MinValue;
            return bDt.CompareTo(aDt);
        });

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new { hardwareId = hardwareId.Trim(), count = alarms.Count, alarms }
        });
    }

    /// <summary>
    /// Linear regression over recent battery_pct readings, estimating hours until
    /// threshold_pct is reached. Null forecast when fewer than 3 usable readings exist.
    /// </summary>
    [HttpGet("battery-forecast")]
    public async Task<IActionResult> GetBatteryForecast(
        [FromQuery(Name = "hardware_id")]   string hardwareId,
        [FromQuery(Name = "window_hours")]  double windowHours  = 48.0,
        [FromQuery(Name = "threshold_pct")] double thresholdPct = 20.0)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        // TODO: re-enable per-org ownership check before multi-tenant production deployment.
        windowHours  = Math.Clamp(windowHours,  1.0,  720.0);
        thresholdPct = Math.Clamp(thresholdPct, 1.0,   50.0);

        var end   = DateTime.UtcNow;
        var start = end.AddHours(-windowHours);

        var rows = await _dbRealtime.GetRowsForRange(hardwareId.Trim(), start, end, FleetLimits.HistoryMaxRows);

        var pts = rows
            .Where(r => r.TryGetValue("ts",          out var tsObj) && tsObj != null
                     && r.TryGetValue("battery_pct", out var bObj)  && bObj  != null)
            .Select(r =>
            {
                DateTimeOffset.TryParse(r["ts"]!.ToString(), out var ts);
                double.TryParse(r["battery_pct"]!.ToString(),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out var pct);
                return (ts: ts.UtcDateTime, batteryPct: pct);
            })
            .Where(p => p.ts != default);

        var forecast = FleetBatteryForecast.Forecast(pts, thresholdPct);

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new { hardwareId = hardwareId.Trim(), windowHours, thresholdPct, forecast }
        });
    }
}
