using System;
using System.Linq;
using System.Threading.Tasks;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Lightweight health-check endpoint for the Fleet subsystem.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET /api/fleet/health — returns DB connectivity status + last reading timestamp
///
/// ── Why this is public (AllowAnonymous) ──────────────────────────────────────
///   Health endpoints should be accessible without credentials so that:
///   - Azure / AWS load balancers can use it for health checks
///   - Uptime monitoring services (UptimeRobot, Pingdom) can probe it
///   - A developer can quickly check if the backend is alive with a browser
///
///   Only non-sensitive information is returned — DB "connected"/"unreachable"
///   and the timestamp of the most recent sensor reading. No org data is exposed.
///
/// ── What "connected" means ────────────────────────────────────────────────────
///   The health check runs a real SQL query for MAX(ts) across iot.tt19_data
///   (the 30-day rolling live table) and iot.tt19_data_archive (everything older).
///   This confirms:
///   a) The PostgreSQL connection string is valid and the server is reachable
///   b) The tables exist
///   c) The app can execute queries
///
///   Querying tt19_data alone would falsely report "no readings" once a device's
///   most recent reading ages past 30 days into the archive — checking both
///   tables avoids that false alarm. Falls back to tt19_data only if the archive
///   table doesn't exist on this database.
///
///   If any of these fail, the endpoint returns HTTP 503 with status "error".
///
/// ── Deployment usage ──────────────────────────────────────────────────────────
///   Configure Azure App Service health check to probe GET /api/fleet/health.
///   An HTTP 200 response means the app and DB are operational.
///   An HTTP 503 response means the DB is unreachable — investigate connection string.
/// </summary>
[ApiController]
[Route("api/fleet/health")]
[AllowAnonymous]
public class FleetHealthController : ControllerBase
{
    private readonly DatabaseContext _databaseContext;

    public FleetHealthController(DatabaseContext databaseContext)
    {
        _databaseContext = databaseContext;
    }

    /// <summary>
    /// GET /api/fleet/health
    ///
    /// Pings the PostgreSQL database and returns the timestamp of the most recent
    /// sensor reading across all devices.
    ///
    /// Response (healthy):
    /// {
    ///   "code":        0,
    ///   "status":      "ok",
    ///   "db":          "connected",
    ///   "lastReading": "2026-03-27T09:58:00Z",   ← null if no readings exist yet
    ///   "serverTime":  "2026-03-27T10:00:00Z"
    /// }
    ///
    /// Response (DB unreachable) — HTTP 503:
    /// {
    ///   "code":   503,
    ///   "status": "error",
    ///   "db":     "unreachable",
    ///   "error":  "connection refused"   ← the exception message
    /// }
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        try
        {
            // Run a real query to confirm the DB connection is live.
            // MAX(ts) gives the most recent reading timestamp — useful for monitoring
            // whether the ingest service is producing readings as expected.
            // Checks both the live table and the archive — tt19_data is a 30-day
            // rolling window, so a device's latest reading can legitimately have
            // already aged into tt19_data_archive even while ingest is healthy.
            using var connection = _databaseContext.CreateConnection();
            DateTime? lastTsRaw;
            try
            {
                lastTsRaw = await connection.ExecuteScalarAsync<DateTime?>(@"
SELECT MAX(ts) FROM (
    SELECT ts FROM iot.tt19_data
    UNION ALL
    SELECT ts FROM iot.tt19_data_archive
) combined;");
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
            {
                // Archive table doesn't exist on this database — fall back to the live table only.
                lastTsRaw = await connection.ExecuteScalarAsync<DateTime?>(
                    "SELECT MAX(ts) FROM iot.tt19_data;");
            }
            var lastTs = lastTsRaw.HasValue
                ? lastTsRaw.Value.ToUniversalTime().ToString("o")
                : (string?)null;

            return Ok(new
            {
                code        = 0,
                status      = "ok",
                db          = "connected",
                lastReading = lastTs,
                serverTime  = FleetDbCoreRepository.ToIsoUtc(DateTime.UtcNow)
            });
        }
        catch (Exception ex)
        {
            // Return 503 Service Unavailable so load balancers know to route
            // traffic away from this instance while the DB is unreachable.
            return StatusCode(503, new
            {
                code   = 503,
                status = "error",
                db     = "unreachable",
                error  = ex.Message
            });
        }
    }

    /// <summary>
    /// GET /api/fleet/health/metrics
    ///
    /// Returns operational metrics for the Fleet polling subsystem.
    /// Requires authentication — intended for admin/ops dashboards, not load balancers.
    ///
    /// Response:
    /// {
    ///   "pollCycles":          1234,        ← total poll cycles since service start
    ///   "lastPollDurationMs":  87,          ← how long the last full poll cycle took
    ///   "lastPollDeviceCount": 12,          ← devices polled in the last cycle
    ///   "secsSinceLastPoll":   4,           ← seconds elapsed since last cycle completed
    ///   "openCircuitBreakers": 0,           ← devices currently in exponential backoff
    ///   "totalDevicesTracked": 12,          ← total devices in circuit-breaker registry
    ///   "emailsEnqueued":      47,          ← alarm emails successfully queued since start
    ///   "emailsFailed":        0,           ← alarm emails that failed to queue since start
    ///   "cacheHits":           892,         ← settings/location cache hits since start
    ///   "cacheMisses":         14           ← settings/location cache misses since start
    /// }
    ///
    /// secsSinceLastPoll = -1 means no poll cycle has completed yet (service just started).
    /// </summary>
    [HttpGet("metrics")]
    [Authorize]
    public IActionResult GetMetrics()
    {
        var snap = FleetMetrics.Snapshot();
        return Ok(new
        {
            pollCycles          = snap.PollCycles,
            lastPollDurationMs  = snap.LastPollDurationMs,
            lastPollDeviceCount = snap.LastPollDeviceCount,
            secsSinceLastPoll   = snap.SecsSinceLastPoll,
            openCircuitBreakers = snap.OpenCircuitBreakers,
            totalDevicesTracked = snap.TotalDevicesTracked,
            emailsEnqueued      = snap.EmailsEnqueued,
            emailsFailed        = snap.EmailsFailed,
            cacheHits           = snap.CacheHits,
            cacheMisses         = snap.CacheMisses,
            circuitBreakers     = FleetPollCircuitBreaker.AllStates
                .Where(kvp => kvp.Value.State != FleetPollCircuitBreaker.CircuitState.Closed)
                .Select(kvp => new
                {
                    hardwareId     = kvp.Key,
                    state          = kvp.Value.State.ToString(),
                    consecutiveFails = kvp.Value.ConsecutiveFails,
                    nextRetryUtc   = kvp.Value.NextPollAllowed.ToString("o"),
                    lastError      = kvp.Value.LastError
                })
        });
    }
}
