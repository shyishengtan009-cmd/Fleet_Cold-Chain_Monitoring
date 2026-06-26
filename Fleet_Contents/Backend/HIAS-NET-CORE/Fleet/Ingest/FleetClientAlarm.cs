using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FleetCore.Fleet.Ingest;

/// <summary>
/// Partial class — alarm threshold push to device firmware via TZone cloud API.
///
/// See FleetClientCore.cs for authentication, credentials, and class-level documentation.
/// See FleetClientDevice.cs for the GET-then-PUT pattern all update methods must follow.
///
/// ── Purpose ──────────────────────────────────────────────────────────────────
/// When a user saves alarm thresholds in Device Settings, this method pushes those
/// same thresholds to the physical device firmware via TZone so the firmware can
/// detect breaches IMMEDIATELY and push readings in sub-second latency, rather than
/// waiting for the 10-second app poll cycle to notice a breach.
///
/// ── TZone alarm field mapping (verify against actual TZone GET /Device response) ──
/// Our alarm_json field     → TZone device object field
/// temp_max_c              → tempHighAlarm
/// temp_min_c              → tempLowAlarm
/// humidity_max_pct        → humHighAlarm
/// humidity_min_pct        → humLowAlarm
///
/// NOTE: TZone field names were inferred from their API conventions and should be
/// confirmed against an actual GET /Device response. If a field name is wrong, the
/// PUT will silently ignore it (TZone does not return validation errors for unknown
/// fields). The fire-and-forget call from FleetSettingsController means any mismatch
/// does NOT affect the local DB save.
/// </summary>
public static partial class FleetClient
{
    // ─── UpdateDeviceAlarmThresholdsAsync ─────────────────────────────────────

    /// <summary>
    /// Pushes temperature and humidity alarm thresholds to the TZone device firmware.
    ///
    /// Uses the mandatory GET-then-PUT pattern (see FleetClientDevice.cs class doc).
    ///
    /// Only fields present in <paramref name="alarmJson"/> are updated — fields not
    /// present in the local settings are left at their current firmware values.
    ///
    /// Called fire-and-forget from FleetSettingsController after a successful DB save.
    /// </summary>
    public static async Task UpdateDeviceAlarmThresholdsAsync(
        long    deviceIntId,
        JObject alarmJson,
        string? appId     = null,
        string? appKey    = null,
        string? appSecret = null,
        CancellationToken ct = default)
    {
        var (useId, useKey, useSecret) = ResolveCredentials(appId, appKey, appSecret);

        // Step 1: GET current full device config to preserve all other settings
        var current = await GetDeviceAsync(deviceIntId, useId, useKey, useSecret, ct).ConfigureAwait(false);

        // Step 2: Map our alarm_json fields → TZone device fields (only if present)
        bool changed = false;

        void ApplyField(string ourKey, string tzoneKey, Func<double, object> transform)
        {
            var token = alarmJson[ourKey];
            if (token == null || token.Type == JTokenType.Null) return;
            if (!double.TryParse(token.ToString(), out var val)) return;
            current[tzoneKey] = transform(val).ToString();
            changed = true;
        }

        ApplyField("temp_max_c",       "tempHighAlarm",  v => Math.Round(v, 1));
        ApplyField("temp_min_c",       "tempLowAlarm",   v => Math.Round(v, 1));
        ApplyField("humidity_max_pct", "humHighAlarm",   v => Math.Round(v, 1));
        ApplyField("humidity_min_pct", "humLowAlarm",    v => Math.Round(v, 1));

        if (!changed) return;

        // Step 3: PUT the complete modified object back to TZone
        var url  = $"{BaseUrl}/Device/{deviceIntId}";
        var body = new StringContent(
            current.ToString(Newtonsoft.Json.Formatting.None),
            System.Text.Encoding.UTF8,
            "application/json");
        var req = new HttpRequestMessage(HttpMethod.Put, url) { Content = body };
        var token2 = await GetTokenAsync(useId, useKey, useSecret, ct).ConfigureAwait(false);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token2);

        var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);
        var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var json = JObject.Parse(text);

        if ((int?)json["status"] != 1)
            throw new InvalidOperationException(
                $"[Fleet-Client] UpdateDeviceAlarmThresholds failed for device {deviceIntId}: {json}");
    }
}
