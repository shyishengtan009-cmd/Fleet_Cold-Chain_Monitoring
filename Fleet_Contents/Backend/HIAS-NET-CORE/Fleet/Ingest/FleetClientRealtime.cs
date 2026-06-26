using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FleetCore.Fleet.Ingest;

/// <summary>
/// Partial class — live sensor data retrieval from the TZone cloud API.
///
/// See FleetClientCore.cs for:
///   - Authentication and Bearer token management
///   - Credential priority (per-device vs global)
///   - Class-level architecture notes
/// </summary>
public static partial class FleetClient
{
    // ─── GetRealtime ──────────────────────────────────────────────────────────

    /// <summary>
    /// Fetches the most recent sensor reading for a device from the TZone cloud API.
    /// Called by FleetFetchRealtime.IngestOneDevice on every poll cycle (default every 10 s).
    ///
    /// Retries up to 2 times on transient failures (network errors, 5xx responses).
    /// Does NOT retry on 4xx (bad credentials, device not found).
    /// Backoff: 1 s then 2 s between attempts.
    ///
    /// Throws InvalidOperationException on unrecoverable errors after all retries.
    /// </summary>
    public static async Task<Dictionary<string, object?>> GetRealtimeAsync(
        long    deviceIntId,
        string? appId     = null,
        string? appKey    = null,
        string? appSecret = null,
        CancellationToken ct = default)
    {
        var (useId, useKey, useSecret) = ResolveCredentials(appId, appKey, appSecret);
        Exception? lastEx = null;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(attempt * 1000, ct).ConfigureAwait(false);

            try
            {
                var url = $"{BaseUrl}/Data/Realtime/{deviceIntId}";
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                var token = await GetTokenAsync(useId, useKey, useSecret, ct).ConfigureAwait(false);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var resp = await Http.SendAsync(req, ct).ConfigureAwait(false);

                // Don't retry on 4xx — these are not transient (bad device ID, auth, etc.)
                if ((int)resp.StatusCode is >= 400 and < 500)
                    throw new InvalidOperationException(
                        $"[Fleet-Client] TZone returned {(int)resp.StatusCode} for device {deviceIntId} — not retrying.");

                // Guard against HTML error pages that would throw on JObject.Parse
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
                if (!resp.IsSuccessStatusCode || !contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
                {
                    var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"[Fleet-Client] Unexpected response {(int)resp.StatusCode} ({contentType}) for device {deviceIntId}: {body[..Math.Min(200, body.Length)]}");
                }

                var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                var json = JObject.Parse(text);

                if ((int?)json["status"] != 1 || json["body"] is not JObject bodyObj)
                    throw new InvalidOperationException(
                        $"[Fleet-Client] Unexpected realtime response for device {deviceIntId}: {json}");

                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in bodyObj.Properties())
                    dict[p.Name] = ExtractValue(p.Value);

                return dict;
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidOperationException) when (attempt == 2) { throw; }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not retrying")) { throw; }
            catch (Exception ex)
            {
                lastEx = ex;
                FleetLog.Warn($"[Fleet-Client] Attempt {attempt + 1}/3 failed for device {deviceIntId}: {ex.Message}");
            }
        }

        throw new InvalidOperationException(
            $"[Fleet-Client] All retries exhausted for device {deviceIntId}.", lastEx);
    }

    // ─── ExtractValue (private) ───────────────────────────────────────────────

    /// <summary>
    /// Unwraps a single field value from TZone's quirky nested-array JSON format.
    ///
    /// Why this exists:
    ///   The TZone API does not return plain scalar values. Instead, every sensor
    ///   field arrives wrapped in one or more levels of arrays:
    ///
    ///     "temperature": [["23.5"]]    →  "23.5"   (string, caller parses to double)
    ///     "humidity":    [["65.2"]]    →  "65.2"
    ///     "latLng":      [["3.12,101.56"]]  →  "3.12,101.56"
    ///     "battery":     [[null]]      →  null
    ///     "light":       []            →  null      (empty = no data)
    ///
    ///   This method recursively unwraps until it reaches the actual scalar value.
    ///   It also strips accidental double-quoting (e.g. "\"value\"" → "value")
    ///   which TZone occasionally sends for string-typed fields.
    /// </summary>
    private static object? ExtractValue(JToken t)
    {
        if (t == null || t.Type == JTokenType.Null) return null;

        if (t is JArray arr)
        {
            if (arr.Count == 0) return null;
            var f = arr[0];
            if (f is JArray) return ExtractValue(f);

            if (f.Type == JTokenType.String)
            {
                var s = f.ToString();
                return s.StartsWith("\"") && s.EndsWith("\"") && s.Length > 2 ? s[1..^1] : s;
            }

            return f.Type == JTokenType.Null ? null : f.ToObject<object?>();
        }

        if (t.Type == JTokenType.String)
        {
            var s = t.ToString();
            return s.StartsWith("\"") && s.EndsWith("\"") && s.Length > 2 ? s[1..^1] : s;
        }

        return t.ToObject<object?>();
    }
}
