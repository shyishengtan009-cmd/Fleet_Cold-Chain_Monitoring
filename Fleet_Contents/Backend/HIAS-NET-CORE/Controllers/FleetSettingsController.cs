using System;
using System.IO;
using System.Threading.Tasks;
using FleetCore.Fleet;
using FleetCore.Repositories;
using FleetCore.Fleet.Ingest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FleetCore.Controllers;

/// <summary>
/// Per-device alarm thresholds, trip schedule, and notification settings, stored
/// as alarm_json/trip_json in iot.device_settings. Saving with a configured
/// reporting_interval_minutes also pushes that interval to the physical device
/// via the cloud API, fire-and-forget so a cloud failure never blocks the local save.
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

    // Newtonsoft, not System.Text.Json — alarm_json/trip_json are JObject and the
    // default serializer would escape them as strings instead of nested JSON.
    private ContentResult NewtonsoftJson(object payload)
        => Content(JsonConvert.SerializeObject(payload), "application/json");

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    /// <summary>
    /// Current alarm/trip settings for a device. found=false if no settings row
    /// exists yet — the device works without one, but no alarms fire until configured.
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

    /// <summary>
    /// Upserts alarm/trip settings for a device. Also accepts legacy "alarm"/"trip"
    /// field names. If reporting_interval_minutes is set, pushes it to the physical
    /// device via the cloud API in the background.
    /// </summary>
    [HttpPost("device_settings/save")]
    public async Task<IActionResult> SaveDeviceSettings()
    {
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

            // Push cloud device config — fire-and-forget, never blocks the local save.
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

    /// <summary>
    /// Fleet sidebar items injected under the "Monitoring" parent. A 401 or network
    /// failure is caught silently by the frontend — no Fleet menus appear, which is
    /// correct for non-fleet tenants.
    /// </summary>
    [HttpGet("nav/menus")]
    public IActionResult GetNavMenus() => Ok(new[]
    {
        new { id = -3, name = "Fleet Dashboard",                 route = "/monitoring/fleet/dashboard",       sequence = 96 },
        new { id = -4, name = "Device Settings",                 route = "/monitoring/fleet/device-settings", sequence = 97 },
        new { id = -1, name = "Cold Truck Real-Time Monitoring", route = "/monitoring/fleet/real-time",       sequence = 98 },
        new { id = -2, name = "Alert",                           route = "/monitoring/fleet/alert",           sequence = 99 }
    });
}
