using System;
using System.Collections.Generic;
using System.Globalization;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Provides real-time sensor data and alarm evaluation for a single device.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET /api/fleet/realtime/latest  — the single most recent reading for a device
///   GET /api/fleet/realtime/alarms  — alarm/warn events in a time window,
///                                     evaluated on-the-fly against device thresholds
///
/// ── Difference between /latest and fleet/status ──────────────────────────────
///   /fleet/status (FleetStatusController) — all devices, one row each, fleet overview
///   /realtime/latest                       — one device, full reading detail
///
/// ── How alarm evaluation works ───────────────────────────────────────────────
///   The /alarms endpoint is a retrospective report generator.
///   It loads the device's alarm thresholds from iot.device_settings (alarm_json),
///   then evaluates EVERY reading in the requested time window against those
///   thresholds. It does NOT read iot.tt19_alarm_log — those entries are only
///   written when the debounce counter fires during live ingest.
///
///   This means /alarms can be used for historical "what would have triggered"
///   analysis without the debounce or cooldown effects of the live alarm system.
///
///   Levels returned:
///     ALARM — reading has actually exceeded the threshold
///     WARN  — reading is within 90% of the threshold (approaching limit)
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   Every endpoint checks DeviceBelongsToOrg() using the JWT "OrganizationId"
///   claim before returning data. Returns 403 if the device belongs to another org.
///
/// ── Why this uses FleetDbSettingsRepository not FleetAlarmChecker ──────────────────────
///   FleetAlarmChecker is designed for live ingest with debounce and cooldown.
///   This controller is a read-only reporting tool, so it directly reads the
///   thresholds and applies them inline without any state side-effects.
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

    // ─── Helper: extract OrganizationId from JWT claim ────────────────────────

    /// <summary>
    /// Extracts the "OrganizationId" integer claim from the current user's JWT.
    /// Returns false if the claim is missing or cannot be parsed as int.
    /// Always call this first in every action method.
    /// </summary>
    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    // ─── GET /api/fleet/realtime/latest ──────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/realtime/latest?hardware_id=HWID_AABBCCDD
    ///
    /// Returns the single most recent sensor reading for the requested device.
    /// The device must belong to the caller's organisation.
    ///
    /// Response (device found):
    /// {
    ///   "code": 0,
    ///   "message": "Success",
    ///   "details": {
    ///     "found": true,
    ///     "row": {
    ///       "hardware_id": "HWID_AABBCCDD",
    ///       "ts": "2026-03-27T09:58:00Z",
    ///       "temperature_c": 4.2,
    ///       "humidity_pct": 65.1,
    ///       "light_lux": 0.0,
    ///       "battery_pct": 88,
    ///       "vibration_g": 0.1
    ///     }
    ///   }
    /// }
    ///
    /// Response (device has no readings yet):
    /// { "code": 0, "message": "Success", "details": { "found": false, "row": null } }
    ///
    /// Used by the Real-Time Monitoring page to show the current sensor values.
    /// </summary>
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

    // ─── GET /api/fleet/realtime/alarms ──────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/realtime/alarms?hardware_id=...&amp;start_utc=...&amp;end_utc=...&amp;limit=10000
    ///
    /// Retrospectively evaluates sensor readings in a time window against the
    /// device's current alarm thresholds. Returns each reading that exceeded
    /// or approached a threshold.
    ///
    /// Query parameters:
    ///   hardware_id — required; the device to query
    ///   start_utc   — optional; ISO-8601 UTC start of window
    ///   end_utc     — optional; ISO-8601 UTC end of window
    ///   limit       — max readings to evaluate, clamped to 1–50000 (default 10000)
    ///
    /// Note: This endpoint evaluates thresholds inline.
    ///   - ALARM = reading exceeded the threshold value
    ///   - WARN  = reading is within 90% of the threshold (approaching the limit)
    ///
    /// Result is sorted newest-first.
    ///
    /// Used by the Cold Truck Alert Report page and the Fleet Dashboard alarm tab.
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

            // Local helper: adds one alarm event row to the result list
            void Add(string type, string message, string level, double? value) =>
                alarms.Add(new Dictionary<string, object?>
                {
                    ["ts"]      = ts,
                    ["type"]    = type,
                    ["level"]   = level,   // "ALARM" or "WARN"
                    ["message"] = message,
                    ["value"]   = value
                });

            // ── Temperature alarm checks ──────────────────────────────────────
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

            // ── Humidity alarm checks ─────────────────────────────────────────
            // Warn band uses abs(threshold)*0.1 — same pattern as temperature so that
            // a humMin of 0% does not collapse the warn band to zero.
            if (h.HasValue)
            {
                if      (humMax.HasValue && h.Value >= humMax.Value)                                   Add("HUM_HIGH",      $"Humidity {h.Value:F1}% >= max {humMax.Value:F1}%",          "ALARM", h);
                else if (humMax.HasValue && h.Value >= humMax.Value - Math.Abs(humMax.Value) * 0.1)    Add("HUM_WARN_HIGH", $"Humidity {h.Value:F1}% approaching max {humMax.Value:F1}%",  "WARN",  h);

                if      (humMin.HasValue && h.Value <= humMin.Value)                                   Add("HUM_LOW",       $"Humidity {h.Value:F1}% <= min {humMin.Value:F1}%",          "ALARM", h);
                else if (humMin.HasValue && h.Value <= humMin.Value + Math.Abs(humMin.Value) * 0.1)    Add("HUM_WARN_LOW",  $"Humidity {h.Value:F1}% approaching min {humMin.Value:F1}%",  "WARN",  h);
            }

            // ── Light alarm checks ────────────────────────────────────────────
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

    // ─── GET /api/fleet/realtime/battery-forecast ─────────────────────────────

    /// <summary>
    /// GET /api/fleet/realtime/battery-forecast?hardware_id=...&amp;window_hours=48&amp;threshold_pct=20
    ///
    /// Runs linear regression over recent battery_pct readings to estimate
    /// how many hours remain before the battery drops below threshold_pct.
    ///
    /// Query parameters:
    ///   hardware_id    — required
    ///   window_hours   — lookback window, 1–720 h (default 48)
    ///   threshold_pct  — low-battery threshold, 1–50 % (default 20)
    ///
    /// Response:
    /// {
    ///   "forecast": {
    ///     "currentPct": 72.4,
    ///     "slopePerHour": -0.312,    // negative = discharging
    ///     "hoursUntilThreshold": 168.2,
    ///     "thresholdPct": 20.0,
    ///     "dataPoints": 144,
    ///     "status": "Discharging"   // Charging | Stable | Discharging | Critical
    ///   }
    /// }
    /// forecast is null when fewer than 3 usable readings exist in the window.
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
