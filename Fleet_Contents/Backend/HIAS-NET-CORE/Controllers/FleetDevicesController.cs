using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using FleetCore.Common;
using FleetCore.Fleet;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace FleetCore.Controllers;

/// <summary>
/// Device registry: list, register (claim via activation code), unregister, and
/// admin-seed unclaimed devices. Lifecycle is seed (no org) → register (org claims
/// it) → unregister (releases it, sensor history is kept). All endpoints scope to
/// the caller's org via the JWT's OrganizationId claim.
/// </summary>
[ApiController]
[Route("api/fleet/devices")]
[Authorize]
public class FleetDevicesController : ControllerBase
{
    private readonly FleetDbDevicesRepository  _dbDevices;
    private readonly FleetDbSettingsRepository _dbSettings;

    public FleetDevicesController(FleetDbDevicesRepository dbDevices, FleetDbSettingsRepository dbSettings)
    {
        _dbDevices  = dbDevices;
        _dbSettings = dbSettings;
    }

    [HttpGet]
    public async Task<IActionResult> GetDevices()
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { message = "Organisation not found in token." });

        var devices = await _dbDevices.GetByOrgId(orgId);

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = devices
        });
    }

    /// <summary>Claims a device for the caller's org by verifying its activation code.</summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterDevice([FromBody] FleetRegisterDeviceRequest request)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { message = "Organisation not found in token." });

        if (string.IsNullOrWhiteSpace(request.HardwareId))
            return BadRequest(new { message = "hardware_id is required." });

        if (string.IsNullOrWhiteSpace(request.ActivationCode))
            return BadRequest(new { message = "activation_code is required." });

        var (success, error) = await _dbDevices.RegisterDevice(
            request.HardwareId,
            request.ActivationCode,
            orgId,
            request.Label,
            request.AppId,
            request.AppKey,
            request.AppSecret);

        if (!success)
            return BadRequest(new { message = error });

        return Ok(new
        {
            code    = 0,
            message = "Device registered successfully.",
            details = new { hardware_id = request.HardwareId.Trim().ToUpperInvariant() }
        });
    }

    /// <summary>Releases a device from the caller's org (sensor history is preserved).</summary>
    [HttpDelete("{hardwareId}")]
    public async Task<IActionResult> UnregisterDevice(string hardwareId)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { message = "Organisation not found in token." });

        var (success, error) = await _dbDevices.UnregisterDevice(hardwareId, orgId);

        if (!success)
            return BadRequest(new { message = error });

        _dbSettings.InvalidateCache(hardwareId);
        FleetPollCircuitBreaker.Remove(hardwareId);
        return Ok(new { code = 0, message = "Device unregistered successfully." });
    }

    /// <summary>
    /// Admin/dev utility: inserts an unclaimed device row so it can later be
    /// registered by any org. Idempotent — re-seeding the same hardware_id is a no-op.
    /// </summary>
    [HttpPost("seed")]
    public async Task<IActionResult> SeedDevice([FromBody] FleetSeedDeviceRequest request)
    {
        // Only SuperAdmin (1) and Admin (10) may seed devices to prevent flooding the registry
        var roleClaim = User.FindFirst("RoleCode")?.Value ?? "";
        var roles = roleClaim.Split(',', StringSplitOptions.RemoveEmptyEntries)
                             .Select(r => int.TryParse(r, out var n) ? n : -1);
        if (!roles.Any(r => r == ConfigHelpers.RoleCodeSuperAdmin || r == ConfigHelpers.RoleCodeAdmin))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.HardwareId))
            return BadRequest(new { message = "hardware_id is required." });

        if (string.IsNullOrWhiteSpace(request.ActivationCode))
            return BadRequest(new { message = "activation_code is required." });

        var (success, _) = await _dbDevices.SeedDevice(request.HardwareId, request.ActivationCode, request.DeviceIntId);

        // Always return 200 — idempotent for dev/admin use
        return Ok(new
        {
            code    = 0,
            message = success ? "Device inserted." : "Device already exists (no change).",
            details = new
            {
                hardware_id     = request.HardwareId.Trim().ToUpperInvariant(),
                activation_code = request.ActivationCode.Trim(),
                device_int_id   = request.DeviceIntId
            }
        });
    }

    /// <summary>
    /// Every device for the caller's org, enriched with its latest telemetry
    /// reading via a LATERAL JOIN. Devices with no telemetry yet get null sensor fields.
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetDevicesSummary(
        [FromQuery(Name = "limit")] int limit = 500)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { message = "Organisation not found in token." });

        var items = await _dbDevices.GetDevicesSummary(orgId, limit);
        return Ok(new { code = 0, message = "Success", details = items });
    }

    /// <summary>
    /// Patch-style update — only JSON keys present in the body are changed,
    /// absent keys leave the existing value untouched.
    /// </summary>
    [HttpPut("{hardwareId}")]
    public async Task<IActionResult> UpdateDevice(string hardwareId)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { message = "Organisation not found in token." });

        string bodyText;
        using (var reader = new StreamReader(Request.Body))
            bodyText = await reader.ReadToEndAsync();

        JObject req;
        try   { req = JObject.Parse(bodyText); }
        catch { return BadRequest(new { message = "Invalid JSON body." }); }

        // Extract each optional field — distinguish "not supplied" from explicit null
        string? label           = null;
        bool    clearLabel      = false;
        long?   deviceIntId     = null;
        bool    devIntSupplied  = false;
        string? appId           = null;
        string? appKey          = null;
        string? appSecret       = null;
        bool    clearCreds      = req["clear_credentials"]?.Value<bool>() == true;

        if (req.ContainsKey("label"))
        {
            if (req["label"]!.Type == JTokenType.Null) clearLabel = true;
            else label = req["label"]!.Value<string>();
        }

        if (req.ContainsKey("device_int_id"))
        {
            devIntSupplied = true;
            if (req["device_int_id"]!.Type != JTokenType.Null)
                deviceIntId = req["device_int_id"]!.Value<long?>();
        }

        if (!clearCreds)
        {
            if (req.ContainsKey("app_id"))    appId    = req["app_id"]!.Value<string>();
            if (req.ContainsKey("app_key"))   appKey   = req["app_key"]!.Value<string>();
            if (req.ContainsKey("app_secret"))appSecret= req["app_secret"]!.Value<string>();
        }

        var (success, error) = await _dbDevices.UpdateDevice(
            hardwareId, orgId,
            label, clearLabel,
            deviceIntId, devIntSupplied,
            appId, appKey, appSecret,
            clearCreds);

        if (!success)
            return BadRequest(new { message = error });

        // If device_int_id changed, invalidate ingest-side caches so the change is
        // picked up immediately on the next poll cycle without a restart.
        if (devIntSupplied)
            _dbSettings.InvalidateCache(hardwareId);

        return Ok(new { code = 0, message = "Device updated successfully." });
    }

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }
}

// Small request DTOs, kept here since they're only used by this controller.

public class FleetSeedDeviceRequest
{
    [JsonPropertyName("hardware_id")]
    public string HardwareId     { get; set; } = "";
    [JsonPropertyName("activation_code")]
    public string ActivationCode { get; set; } = "";
    [JsonPropertyName("device_int_id")]
    public long?  DeviceIntId    { get; set; }
}

public class FleetRegisterDeviceRequest
{
    [JsonPropertyName("hardware_id")]
    public string  HardwareId     { get; set; } = "";
    [JsonPropertyName("activation_code")]
    public string  ActivationCode { get; set; } = "";
    [JsonPropertyName("label")]
    public string? Label          { get; set; }
    [JsonPropertyName("app_id")]
    public string? AppId          { get; set; }
    [JsonPropertyName("app_key")]
    public string? AppKey         { get; set; }
    [JsonPropertyName("app_secret")]
    public string? AppSecret      { get; set; }
}
