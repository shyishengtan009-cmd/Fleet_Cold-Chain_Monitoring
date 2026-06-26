using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using HIAS_NET_CORE.Common;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Manages the device registry — listing, registering, unregistering, and seeding devices.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET    /api/fleet/devices            — list all devices for the caller's org
///   POST   /api/fleet/devices/register   — claim an unregistered device by activation code
///   DELETE /api/fleet/devices/{hwId}     — unregister (release) a device from the org
///   POST   /api/fleet/devices/seed       — admin/dev: insert an unclaimed device row
///
/// ── Device lifecycle ──────────────────────────────────────────────────────────
///   1. SEED (admin/dev) — A device row is created with hardware_id + activation_code
///      but no organization_id. This row represents a physical device that exists
///      but hasn't been claimed by any org yet.
///
///   2. REGISTER (user) — An org admin enters the hardware_id + activation_code printed
///      on the device label. The server verifies the activation code and sets
///      organization_id on the row. The device is now owned by that org.
///
///   3. UNREGISTER (user) — Sets organization_id back to NULL. The device becomes
///      claimable again. Data in iot.tt19_data is NOT deleted.
///
/// ── Per-device API credentials ───────────────────────────────────────────────
///   Each device can optionally have its own TZone API credentials:
///     app_id, app_key, app_secret — stored in iot.tt19_devices
///   These are used by FleetIngestService to authenticate cloud API calls.
///   If not set, the global credentials from appsettings.json are used.
///
/// ── device_int_id requirement ────────────────────────────────────────────────
///   The device_int_id column must be set for cloud polling to work.
///   This is the TZone internal integer ID for the device — find it via
///   GET /Device?key={hardware_id} on the TZone API Swagger.
///   Without device_int_id, the ingest service silently skips the device.
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   All endpoints read the caller's org from the JWT "OrganizationId" claim.
///   Users can only list/register/unregister devices within their own org.
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

    // ─── GET /api/fleet/devices ───────────────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/devices
    ///
    /// Returns all Fleet devices registered to the caller's organisation.
    /// Used by the frontend to decide whether to show the device activation portal
    /// and to populate the device selector on the Device Settings page.
    ///
    /// Response:
    /// {
    ///   "code": 0,
    ///   "message": "Success",
    ///   "details": [
    ///     { "id": 12, "hardware_id": "HWID_AABBCCDD", "label": "Cold Truck A Sensor",
    ///       "organization_id": 1001, "registered_at": "2026-01-15T09:00:00Z", ... }
    ///   ]
    /// }
    ///
    /// If the org has no devices yet, returns an empty array.
    /// </summary>
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

    // ─── POST /api/fleet/devices/register ────────────────────────────────────

    /// <summary>
    /// POST /api/fleet/devices/register
    ///
    /// Registers a Fleet device to the caller's organisation by verifying
    /// the activation code printed on the device label.
    ///
    /// Body ([FromBody]):
    /// {
    ///   "HardwareId":     "HWID_AABBCCDD",     ← required; case-insensitive
    ///   "ActivationCode": "ACT-12345",           ← required; must match DB
    ///   "Label":          "Cold Truck A Sensor", ← optional display name
    ///   "AppId":          "my_app_id",           ← optional per-device TZone creds
    ///   "AppKey":         "my_app_key",
    ///   "AppSecret":      "my_app_secret"
    /// }
    ///
    /// Returns 400 if:
    ///   - hardware_id / activation_code not provided
    ///   - activation_code does not match the stored value
    ///   - device is already registered to another org
    ///
    /// On success:
    /// { "code": 0, "message": "Device registered successfully.",
    ///   "details": { "hardware_id": "HWID_AABBCCDD" } }
    /// </summary>
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

    // ─── DELETE /api/fleet/devices/{hardwareId} ───────────────────────────────

    /// <summary>
    /// DELETE /api/fleet/devices/HWID_AABBCCDD
    ///
    /// Unregisters a device from the caller's organisation by setting
    /// organization_id back to NULL. The device can then be claimed by another org.
    ///
    /// This does NOT delete any sensor data — iot.tt19_data rows are preserved.
    ///
    /// Returns 400 if the device is not found in this org.
    /// Returns 200 on success.
    /// </summary>
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

    // ─── POST /api/fleet/devices/seed ────────────────────────────────────────

    /// <summary>
    /// POST /api/fleet/devices/seed
    ///
    /// Admin / developer utility: inserts an unclaimed device row so it can be
    /// claimed via the activation portal by any org.
    ///
    /// This is how a new physical device is initially introduced into the system.
    /// In production you would run this once per device when it ships.
    /// In development, use Swagger or this endpoint to add test devices.
    ///
    /// Body ([FromBody]):
    /// {
    ///   "HardwareId":     "HWID_AABBCCDD",  ← required; the hardware_id on the device label
    ///   "ActivationCode": "ACT-12345"        ← required; printed on the device label
    /// }
    ///
    /// This endpoint is idempotent — calling it twice with the same hardware_id
    /// returns 200 both times (first call inserts, second call is a no-op).
    ///
    /// Requires authentication (normal JWT) to prevent abuse — but no org check,
    /// since the device is not owned by anyone yet.
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

    // ─── GET /api/fleet/devices/summary ──────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/devices/summary?limit=500
    ///
    /// Unified device list — every registered device for the caller's org,
    /// enriched with its latest telemetry reading via a LATERAL JOIN.
    /// Devices with no telemetry yet appear with null sensor fields and
    /// has_polling = false when device_int_id is missing.
    ///
    /// Response per item:
    /// {
    ///   "hardware_id":      "HWID_AABBCCDD",
    ///   "label":            "Cold Truck A",
    ///   "device_int_id":    12345,
    ///   "has_polling":      true,
    ///   "has_custom_creds": false,
    ///   "registered_at":    "2026-01-15T09:00:00Z",
    ///   "ts":               "2026-05-21T10:00:00Z",   // null if no data yet
    ///   "temperature_c":    4.2,
    ///   "humidity_pct":     65.1,
    ///   "battery_pct":      88.0,
    ///   "lat":              3.147,
    ///   "lng":              101.693,
    ///   "truck_name":       "Cold Truck A"
    /// }
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

    // ─── PUT /api/fleet/devices/{hardwareId} ──────────────────────────────────

    /// <summary>
    /// PUT /api/fleet/devices/HWID_AABBCCDD
    ///
    /// Updates mutable registration fields. Only supplied JSON keys are changed;
    /// absent keys leave the existing value untouched.
    ///
    /// Body (all fields optional — only include what you want to change):
    /// {
    ///   "label":             "New Truck Name",   // null = clear the label
    ///   "device_int_id":     12345,              // null = clear (stops ingest polling)
    ///   "app_id":            "new_id",           // omit = keep existing
    ///   "app_key":           "new_key",
    ///   "app_secret":        "new_secret",
    ///   "clear_credentials": false               // true = wipe all three cred fields
    /// }
    ///
    /// Returns 403 if the device is not owned by the caller's org.
    /// Returns 400 if hardware_id is invalid or no fields are supplied.
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

    // ─── Helper ───────────────────────────────────────────────────────────────

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }
}

// ─── Request DTOs ─────────────────────────────────────────────────────────────
// These are placed in the same file because they are small and only used here.
// If they grow, move them to Models/Fleet/.

/// <summary>
/// Body for POST /api/fleet/devices/seed.
/// Creates an unclaimed device row (no org owner).
/// </summary>
public class FleetSeedDeviceRequest
{
    [JsonPropertyName("hardware_id")]
    public string HardwareId     { get; set; } = "";
    [JsonPropertyName("activation_code")]
    public string ActivationCode { get; set; } = "";
    [JsonPropertyName("device_int_id")]
    public long?  DeviceIntId    { get; set; }       // TZone internal integer ID — required for ingest polling
}

/// <summary>
/// Body for POST /api/fleet/devices/register.
/// Claims a device for the caller's organisation using the activation code.
/// Per-device API credentials (AppId/Key/Secret) are optional — if omitted,
/// the global credentials from appsettings.json are used for cloud polling.
/// </summary>
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
