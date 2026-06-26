using System;
using System.IO;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Repositories;
using HIAS_NET_CORE.Fleet.Ingest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Manages per-device alarm thresholds, trip schedule, and notification settings.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET  /api/fleet/device_settings        — load settings for one device
///   POST /api/fleet/device_settings/save   — save (upsert) settings for one device
///
/// ── Settings structure (stored in iot.device_settings) ────────────────────────
///   alarm_json — all alarm + notification config:
///     temp_min_c / temp_max_c        — temperature thresholds in °C
///     humidity_min_pct / max_pct     — humidity thresholds in %
///     light_min_lux / max_lux        — light thresholds in lux
///     battery_min_pct                — low battery warning threshold
///     debounce_count                 — how many consecutive breaches before notifying
///     email_cooldown_minutes         — minimum gap between notification emails
///     notify_email                   — email address to send alerts to
///     reporting_interval_minutes     — how often the device should push to cloud
///     daily_start / daily_end        — HH:mm times for alarm window (and auto-trip schedule)
///     repeat_days                    — days the alarm is active ["Mon","Tue",...]
///     auto_trip                      — bool; auto-open/close trips on daily_start/end
///
///   trip_json — additional trip display info:
///     truck_name   — overrides the trucks table name for this device's active trip
///
/// ── Reporting interval cloud push ────────────────────────────────────────────
///   When alarm_json contains reporting_interval_minutes >= 1, this endpoint
///   automatically pushes the new interval to the physical device via the TZone
///   cloud API (FleetClient.UpdateDeviceInterval).
///
///   This is done fire-and-forget in Task.Run so a cloud API failure never blocks
///   the local save. Failure is logged via FleetLog.Warn — check application logs
///   if the device interval doesn't update on the cloud side.
///
///   GET-then-PUT: TZone requires a full GET before any PUT to avoid silently
///   overwriting unrelated device settings. This is handled inside FleetClient.
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   Both endpoints verify device ownership via FleetDbDevicesRepository.DeviceBelongsToOrg().
/// </summary>
[ApiController]
[Route("api/fleet")]
[Authorize]
public class FleetSettingsController : ControllerBase
{
    private readonly FleetDbDevicesRepository  _dbDevices;
    private readonly FleetDbSettingsRepository _dbSettings;

    public FleetSettingsController(FleetDbDevicesRepository dbDevices, FleetDbSettingsRepository dbSettings)
    {
        _dbDevices  = dbDevices;
        _dbSettings = dbSettings;
    }

    // ─── Response helper ──────────────────────────────────────────────────────

    // Responses go through Newtonsoft because alarm_json / trip_json values are
    // JObject — ASP.NET Core's default System.Text.Json would serialize them as
    // escaped strings. Using Content() with an explicit Newtonsoft serialization
    // keeps the JSON structure intact for the frontend.
    private ContentResult NewtonsoftJson(object payload)
        => Content(JsonConvert.SerializeObject(payload), "application/json");

    // ─── Helper: extract OrganizationId from JWT claim ────────────────────────

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    // ─── GET /api/fleet/device_settings ──────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/device_settings?hardware_id=HWID_AABBCCDD
    ///
    /// Returns the current alarm and trip settings for the requested device.
    /// Returns { found: false } if no settings row exists yet (settings are
    /// created on first save — the device can operate without a settings row,
    /// but no alarms will fire until thresholds are configured).
    ///
    /// Response (found):
    /// {
    ///   "code": 0, "message": "Success",
    ///   "details": {
    ///     "found": true,
    ///     "row": {
    ///       "hardware_id": "HWID_AABBCCDD",
    ///       "truck_name": "Cold Truck A",
    ///       "alarm_json": { "temp_max_c": 8.0, "notify_email": "...", ... },
    ///       "trip_json":  { "truck_name": "Override Name" },
    ///       "updated_at": "2026-03-20T10:00:00Z"
    ///     }
    ///   }
    /// }
    /// </summary>
    [HttpGet("device_settings")]
    public async Task<IActionResult> GetDeviceSettings(
        [FromQuery(Name = "hardware_id")] string hardwareId = "")
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        var row = await _dbSettings.GetDeviceSettings(hardwareId);

        if (row == null)
            return NewtonsoftJson(new { code = 0, message = "Success", details = new { found = false, row = (object?)null } });

        return NewtonsoftJson(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                found = true,
                row   = new
                {
                    hardware_id = row["hardware_id"],
                    truck_name  = row["truck_name"],
                    alarm_json  = row["alarm_json"],
                    trip_json   = row["trip_json"],
                    updated_at  = row["updated_at"],
                }
            }
        });
    }

    // ─── POST /api/fleet/device_settings/save ────────────────────────────────

    /// <summary>
    /// POST /api/fleet/device_settings/save
    ///
    /// Creates or updates alarm and trip settings for a device (upsert).
    /// Both alarm_json and trip_json are optional — omitted keys are set to {}.
    ///
    /// The save also accepts the legacy field names "alarm" and "trip" for
    /// compatibility with older frontend versions.
    ///
    /// Body:
    /// {
    ///   "hardware_id": "HWID_AABBCCDD",
    ///   "alarm_json": {
    ///     "temp_min_c": 2.0,
    ///     "temp_max_c": 8.0,
    ///     "humidity_min_pct": 20.0,
    ///     "humidity_max_pct": 90.0,
    ///     "debounce_count": 3,
    ///     "email_cooldown_minutes": 30,
    ///     "notify_email": "ops@example.com",
    ///     "reporting_interval_minutes": 5,
    ///     "daily_start": "08:00",
    ///     "daily_end": "17:00",
    ///     "repeat_days": ["Mon","Tue","Wed","Thu","Fri"],
    ///     "auto_trip": true
    ///   },
    ///   "trip_json": { "truck_name": "Cold Truck A" }
    /// }
    ///
    /// Side effect: if reporting_interval_minutes is >= 1, the value is pushed
    /// to the physical device via TZone cloud API in a background thread.
    /// </summary>
    [HttpPost("device_settings/save")]
    public async Task<IActionResult> SaveDeviceSettings()
    {
        // Read raw body and parse with Newtonsoft so the entire settings path
        // uses one JSON library — no JsonElement↔JObject conversion needed.
        string bodyText;
        using (var reader = new StreamReader(Request.Body))
            bodyText = await reader.ReadToEndAsync();

        JObject req;
        try   { req = JObject.Parse(bodyText); }
        catch { return BadRequest(new { code = 400, message = "Invalid JSON body." }); }

        var hardwareId = (req["hardware_id"]?.Value<string>() ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        // Accept both "alarm_json" and legacy "alarm" field names
        JObject AsObj(string primary, string legacy)
            => req[primary] as JObject ?? req[legacy] as JObject ?? new JObject();

        var alarmObj = AsObj("alarm_json", "alarm");
        var tripObj  = AsObj("trip_json",  "trip");

        try
        {
            var row = await _dbSettings.UpsertDeviceSettings(hardwareId, alarmObj, tripObj);

            // ── Push cloud device config (fire-and-forget) ───────────────────
            // Both calls are non-blocking — a TZone cloud failure must never block
            // the local DB save. Failures are logged via FleetLog.Warn.
            var creds = await _dbDevices.GetDeviceCredentials(hardwareId);
            if (creds.HasValue)
            {
                var (did, appId, appKey, appSecret) = creds.Value;

                // 1. Reporting interval — only if configured
                var intervalRaw = alarmObj["reporting_interval_minutes"];
                if (intervalRaw != null && intervalRaw.Type != JTokenType.Null)
                {
                    var intervalMinutes = (int?)intervalRaw.Value<double?>();
                    if (intervalMinutes.HasValue && intervalMinutes.Value >= 1)
                    {
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            try   { await FleetClient.UpdateDeviceIntervalAsync(did, intervalMinutes.Value, appId, appKey, appSecret).ConfigureAwait(false); }
                            catch (Exception ex) { FleetLog.Warn($"[Fleet-Settings] Could not push interval to cloud for {hardwareId}: {ex.Message}"); }
                        });
                    }
                }

                // 2. Alarm thresholds — always push so firmware detects breaches immediately
                _ = System.Threading.Tasks.Task.Run(async () =>
                {
                    try   { await FleetClient.UpdateDeviceAlarmThresholdsAsync(did, alarmObj, appId, appKey, appSecret).ConfigureAwait(false); }
                    catch (Exception ex) { FleetLog.Warn($"[Fleet-Settings] Could not push alarm thresholds to cloud for {hardwareId}: {ex.Message}"); }
                });
            }

            return NewtonsoftJson(new
            {
                code    = 0,
                message = "Success",
                details = new
                {
                    ok  = true,
                    row = new
                    {
                        hardware_id = row["hardware_id"],
                        truck_name  = row["truck_name"],
                        alarm_json  = row["alarm_json"],
                        trip_json   = row["trip_json"],
                        updated_at  = row["updated_at"],
                    }
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    // ─── GET /api/fleet/nav/menus ─────────────────────────────────────────────

    /// <summary>
    /// Returns Fleet sidebar navigation items injected under the "Monitoring" parent.
    /// Called by NavigationSideV2.vue on mount. A 401 or network failure is caught
    /// silently — no Fleet menus appear, which is correct for non-fleet tenants.
    /// </summary>
    [HttpGet("nav/menus")]
    public IActionResult GetNavMenus() => Ok(new[]
    {
        new { id = -3, name = "Fleet Dashboard",                 route = "/monitoring/tt19-fleet/dashboard",       sequence = 96 },
        new { id = -4, name = "Device Settings",                 route = "/monitoring/tt19-fleet/device-settings", sequence = 97 },
        new { id = -1, name = "Cold Truck Real-Time Monitoring", route = "/monitoring/tt19-fleet/real-time",       sequence = 98 },
        new { id = -2, name = "Alert",                           route = "/monitoring/tt19-fleet/alert",           sequence = 99 }
    });
}
