using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace HIAS_NET_CORE.Fleet.Ingest;

/// <summary>
/// Partial class — device configuration management on the TZone cloud API.
///
/// See FleetClientCore.cs for authentication, credentials, and class-level documentation.
///
/// ── CRITICAL: TZone PUT requires a full object ────────────────────────────────
/// The TZone /Device PUT endpoint does NOT support partial updates (no PATCH).
/// You MUST send the COMPLETE device object back — not just the one field you changed.
///
/// Every update method in this file follows this mandatory pattern:
///   1. GET  the current full device object from /Device/ID/{id}
///   2. Modify only the specific field(s) that need to change
///   3. PUT  the complete modified object back to /Device/{id}
///
/// If you skip the GET step and PUT only a partial object, TZone will silently
/// overwrite ALL other device settings (alarm thresholds, timezone, device name, etc.)
/// with empty or default values on the TZone cloud side. This breaks firmware behaviour.
/// </summary>
public static partial class FleetClient
{
    // ─── GetDevice ────────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the full device configuration object from the TZone cloud API.
    ///
    /// TZone endpoint:
    ///   GET /Device/ID/{deviceIntId}
    ///   Authorization: Bearer {token}
    ///
    /// Returns the raw JObject from the response body.
    /// This includes all TZone device fields: terminalDataInterval, alarmSettings,
    /// deviceName, timezone, etc.
    ///
    /// This method is called internally by update methods (e.g. UpdateDeviceInterval)
    /// to read the current device state before modifying one field and PUTting back.
    /// It can also be called directly to inspect the full TZone device configuration.
    ///
    /// Throws InvalidOperationException if the API returns status != 1.
    ///   This typically means the deviceIntId is wrong or the token has expired.
    /// </summary>
    public static async Task<JObject> GetDeviceAsync(
        long    deviceIntId,
        string? appId     = null,
        string? appKey    = null,
        string? appSecret = null,
        CancellationToken ct = default)
    {
        var (useId, useKey, useSecret) = ResolveCredentials(appId, appKey, appSecret);

        var url = $"{BaseUrl}/Device/ID/{deviceIntId}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        var token = await GetTokenAsync(useId, useKey, useSecret, ct).ConfigureAwait(false);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var json = JObject.Parse(text);

        if ((int?)json["status"] != 1 || json["body"] is not JObject bodyObj)
            throw new InvalidOperationException(
                $"[Fleet-Client] GetDevice failed for device {deviceIntId}: {json}");

        return bodyObj;
    }

    // ─── UpdateDeviceInterval ─────────────────────────────────────────────────

    /// <summary>
    /// Changes how often the sensor device sends readings to the TZone cloud.
    ///
    /// TZone endpoints used (GET-then-PUT pattern — see class doc):
    ///   GET /Device/ID/{deviceIntId}   — fetch current full config (step 1)
    ///   PUT /Device/{deviceIntId}      — submit complete updated config (step 3)
    ///
    /// What the field does:
    ///   terminalDataInterval controls the normal (non-breach) reporting frequency
    ///   in minutes. Setting this to 15 means the device uploads a reading every
    ///   15 minutes in normal conditions.
    ///
    ///   Breach-triggered readings are sent IMMEDIATELY by the device firmware
    ///   regardless of this interval — confirmed by TZone technical team.
    ///
    /// Where this is called:
    ///   FleetSettingsController.SaveDeviceSettings() calls this method fire-and-forget
    ///   via Task.Run() after saving alarm_json to the database. The fire-and-forget
    ///   pattern means the HTTP response to the frontend is not blocked by the TZone
    ///   API call. If the push fails, it is logged but does not fail the save operation.
    ///
    /// intervalMinutes must be >= 1.
    /// Throws ArgumentOutOfRangeException if intervalMinutes &lt; 1.
    /// Throws InvalidOperationException if the TZone PUT call returns status != 1.
    /// </summary>
    public static async Task UpdateDeviceIntervalAsync(
        long    deviceIntId,
        int     intervalMinutes,
        string? appId     = null,
        string? appKey    = null,
        string? appSecret = null,
        CancellationToken ct = default)
    {
        if (intervalMinutes < 1)
            throw new ArgumentOutOfRangeException(
                nameof(intervalMinutes), "Reporting interval must be at least 1 minute.");

        var (useId, useKey, useSecret) = ResolveCredentials(appId, appKey, appSecret);

        // Step 1: GET the current full device config to preserve all existing settings
        var current = await GetDeviceAsync(deviceIntId, useId, useKey, useSecret, ct).ConfigureAwait(false);

        // Step 2: Change only the reporting interval field — everything else stays as-is
        current["terminalDataInterval"] = intervalMinutes.ToString();

        // Step 3: PUT the complete modified object back to TZone
        var url  = $"{BaseUrl}/Device/{deviceIntId}";
        var body = new StringContent(
            current.ToString(Newtonsoft.Json.Formatting.None),
            System.Text.Encoding.UTF8,
            "application/json");
        var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = body };
        var token = await GetTokenAsync(useId, useKey, useSecret, ct).ConfigureAwait(false);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var json = JObject.Parse(text);

        if ((int?)json["status"] != 1)
            throw new InvalidOperationException(
                $"[Fleet-Client] UpdateDeviceInterval failed for device {deviceIntId}: {json}");
    }
}
