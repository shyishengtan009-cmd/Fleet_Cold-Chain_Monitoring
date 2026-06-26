using System;
using System.Linq;
using System.Threading.Tasks;
using FleetCore.Context;
using FleetCore.Fleet;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Dapper;

namespace FleetCore.Controllers;

/// <summary>
/// Health check for load balancers/uptime monitors. Public on purpose — no org
/// data is exposed, just DB connectivity and the latest reading timestamp.
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
    /// Operational metrics for the polling subsystem — admin/ops use, not load balancers.
    /// secsSinceLastPoll is -1 if no poll cycle has completed yet.
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
