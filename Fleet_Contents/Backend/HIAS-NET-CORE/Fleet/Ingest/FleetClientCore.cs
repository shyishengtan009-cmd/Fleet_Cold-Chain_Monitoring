using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace FleetCore.Fleet.Ingest;

/// <summary>
/// HTTP client for the TZone cloud IoT API (https://i-cloud.tzonedigital.com).
///
/// This class is split across three partial-class files. Each file covers one concern:
///
///   FleetClientCore.cs     — shared HTTP client, credentials, token authentication
///   FleetClientRealtime.cs — fetching live sensor readings  (GET /Data/Realtime)
///   FleetClientDevice.cs   — reading and updating device config (GET+PUT /Device)
///
/// IMPORTANT: Do not mix responsibilities between the three files. If you are adding
/// a new TZone API call, add it to the file that matches its concern, or create a new
/// partial file with a clear name (e.g. FleetClientAlarm.cs for alarm push endpoints).
///
/// ── Credential priority ──────────────────────────────────────────────────────
/// Every API call requires a Bearer token tied to an "app" credential set
/// (appId + appKey + appSecret). Two sources are supported:
///
///   1. Per-device credentials — stored in iot.tt19_devices (app_id, app_key, app_secret).
///      These TAKE PRIORITY when provided. Allows each physical device to have its
///      own isolated TZone account. Recommended for production.
///
///   2. Global credentials — read from environment variables at startup:
///        FLEET_APP_ID, FLEET_APP_KEY, FLEET_APP_SECRET
///      Used as a fallback when per-device credentials are NULL in the database.
///      Good for development or when all devices share one TZone account.
///
///   FLEET_BASE_URL env var overrides the default cloud base URL (useful for staging/testing).
///
/// ── Token caching explained ────────────────────────────────────────────────────
/// The TZone /Identity endpoint issues a time-limited Bearer token.
/// We cache one token per appId so we do not call /Identity on every poll cycle
/// (default: every 10 seconds × hundreds of devices = many wasted requests).
///
/// Cache invalidation: we check expiry - 30 seconds before every use.
/// If the token is about to expire, we fetch a new one silently.
///
/// Thread safety: cache reads and writes are wrapped in lock(_tokenLock).
/// The HTTP call itself is outside the lock so we do not block other threads
/// while waiting for the network response.
/// </summary>
public static partial class FleetClient
{
    // ── Shared HTTP client ────────────────────────────────────────────────────
    // SocketsHttpHandler with PooledConnectionLifetime ensures TCP connections
    // are recycled every 15 minutes so DNS changes (e.g. TZone failover) are
    // picked up without restarting the service.
    private static readonly HttpClient Http = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(15),
        ConnectTimeout           = TimeSpan.FromSeconds(10)
    }) { Timeout = TimeSpan.FromSeconds(30) };

    // ── Base URL and global credentials ───────────────────────────────────────
    // Initialised from env vars at startup; can be overridden by SetGlobalCredentials()
    // once the ingest service has read iot.tt19_api_config from the database.
    // Not readonly so SetGlobalCredentials() can update them after startup.
    private static string BaseUrl      = (Environment.GetEnvironmentVariable("FLEET_BASE_URL")    ?? "https://i-cloud.tzonedigital.com").Trim().TrimEnd('/');
    private static string GlobalAppId  = (Environment.GetEnvironmentVariable("FLEET_APP_ID")     ?? "").Trim();
    private static string GlobalAppKey = (Environment.GetEnvironmentVariable("FLEET_APP_KEY")    ?? "").Trim();
    private static string GlobalSecret = (Environment.GetEnvironmentVariable("FLEET_APP_SECRET") ?? "").Trim();

    // ── Token cache ───────────────────────────────────────────────────────────
    // Key   = "appId:appKey" — using both fields avoids a collision if two devices
    //         share the same appId but have different appKey/appSecret credentials.
    // Value = (token string, Unix expiry timestamp in seconds)
    private static readonly Dictionary<string, (string Token, long Expire)> _tokenCache = new();
    private static readonly object _tokenLock = new();

    // Per-credential-set semaphore prevents stampede: when a token expires, only one
    // thread calls /Identity while others wait for it.
    private static readonly Dictionary<string, SemaphoreSlim> _tokenSemaphores = new();
    private static readonly object _semaphoreLock = new();

    private static string TokenKey(string appId, string appKey) => $"{appId}:{appKey}";

    // ─── SetGlobalCredentials ─────────────────────────────────────────────────

    /// <summary>
    /// Overrides the global TZone credential set with values loaded from the database
    /// (iot.tt19_api_config). Called once at ingest-service startup AFTER the DB is
    /// accessible. Only non-empty values replace the existing setting so env var
    /// overrides still take priority when both sources have a value.
    /// </summary>
    public static void SetGlobalCredentials(
        string? appId, string? appKey, string? appSecret, string? baseUrl = null)
    {
        if (!string.IsNullOrWhiteSpace(appId))     GlobalAppId  = appId!.Trim();
        if (!string.IsNullOrWhiteSpace(appKey))    GlobalAppKey = appKey!.Trim();
        if (!string.IsNullOrWhiteSpace(appSecret)) GlobalSecret = appSecret!.Trim();
        if (!string.IsNullOrWhiteSpace(baseUrl))   BaseUrl      = baseUrl!.Trim().TrimEnd('/');
    }

    // ─── ValidateConfig ───────────────────────────────────────────────────────

    /// <summary>
    /// Checks whether global TZone credentials are present in environment variables.
    ///
    /// Returns a warning message string if any credential is missing.
    /// Returns null if all three are set.
    ///
    /// This is NON-FATAL — if per-device credentials exist in iot.tt19_devices
    /// the service will still run normally. The warning is logged at startup so
    /// operators know which credential source is active.
    ///
    /// If BOTH global and per-device credentials are missing for a device, that
    /// device's API calls will fail with an authentication error at runtime.
    /// </summary>
    public static string? ValidateConfig()
    {
        if (string.IsNullOrWhiteSpace(GlobalAppId) ||
            string.IsNullOrWhiteSpace(GlobalAppKey) ||
            string.IsNullOrWhiteSpace(GlobalSecret))
        {
            return "Fleet: global API credentials missing " +
                   "(FLEET_APP_ID, FLEET_APP_KEY, FLEET_APP_SECRET). " +
                   "Per-device credentials from iot.tt19_devices will be used if available.";
        }
        return null;
    }

    // ─── GetToken (private — used internally by all API methods) ─────────────

    /// <summary>
    /// Returns a valid Bearer token for the given credential set.
    /// Fetches a new token from /Identity only when the cached one is expiring.
    ///
    /// How the cache works:
    ///   - The cache key is appId, so each TZone account has its own cached token.
    ///   - We check: now &lt; (expiry - 30s). The 30-second buffer prevents edge cases
    ///     where a token expires in the middle of an API call sequence.
    ///   - If expired or missing, a fresh /Identity call is made and the result
    ///     is stored back into the cache.
    ///
    /// TZone /Identity endpoint:
    ///   GET /Identity?appId={appId}&amp;appKey={appKey}&amp;appSecret={appSecret}
    ///   Response: { "status": 1, "body": { "token": "...", "expireTime": 1234567890 } }
    ///
    /// Throws InvalidOperationException if the API returns status != 1 (e.g. bad credentials).
    /// </summary>
    internal static async Task<string> GetTokenAsync(
        string appId, string appKey, string appSecret, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var cacheKey = TokenKey(appId, appKey);

        // Fast path: use cached token if still valid (no lock needed for read — worst case we
        // fall through to the semaphore path which will re-check under the semaphore)
        lock (_tokenLock)
        {
            if (_tokenCache.TryGetValue(cacheKey, out var cached) && now < (cached.Expire - 30))
                return cached.Token;
        }

        // Per-credential-set semaphore: only one thread refreshes a token at a time.
        // Others wait here and then get the freshly cached token on re-check.
        SemaphoreSlim sem;
        lock (_semaphoreLock)
        {
            if (!_tokenSemaphores.TryGetValue(cacheKey, out sem!))
                _tokenSemaphores[cacheKey] = sem = new SemaphoreSlim(1, 1);
        }

        await sem.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Re-check after acquiring: another thread may have already refreshed
            lock (_tokenLock)
            {
                if (_tokenCache.TryGetValue(cacheKey, out var cached) && now < (cached.Expire - 30))
                    return cached.Token;
            }

            var url  = $"{BaseUrl}/Identity?appId={Uri.EscapeDataString(appId)}&appKey={Uri.EscapeDataString(appKey)}&appSecret={Uri.EscapeDataString(appSecret)}";
            var resp = await Http.SendAsync(new HttpRequestMessage(HttpMethod.Get, url), ct).ConfigureAwait(false);
            var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var json = JObject.Parse(text);

            if ((int?)json["status"] != 1)
                throw new InvalidOperationException(
                    $"[Fleet-Client] TZone /Identity failed — check API credentials. Response: {json}");

            var body   = json["body"] as JObject;
            var token  = (string?)body?["token"]
                ?? throw new InvalidOperationException("[Fleet-Client] TZone /Identity returned no token.");
            var expire = (long?)body?["expireTime"] ?? (now + 3600);

            lock (_tokenLock) { _tokenCache[cacheKey] = (token, expire); }
            return token;
        }
        finally
        {
            sem.Release();
        }
    }

    // ─── ResolveCredentials (internal — used by all API methods) ─────────────

    /// <summary>
    /// Returns the credential set to use for an API call.
    ///
    /// Priority: per-device params (non-empty) → global environment variables.
    ///
    /// All public API methods in FleetClientRealtime.cs and FleetClientDevice.cs
    /// call this before building their HTTP request. It is the single place that
    /// implements the per-device vs global credential fallback logic.
    /// </summary>
    internal static (string id, string key, string secret) ResolveCredentials(
        string? appId, string? appKey, string? appSecret)
    {
        return (
            string.IsNullOrWhiteSpace(appId)     ? GlobalAppId  : appId!.Trim(),
            string.IsNullOrWhiteSpace(appKey)    ? GlobalAppKey : appKey!.Trim(),
            string.IsNullOrWhiteSpace(appSecret) ? GlobalSecret : appSecret!.Trim()
        );
    }
}
