using System;
using System.Linq;
using System.Threading.Tasks;
using FleetCore.Fleet;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetCore.Controllers;

/// <summary>
/// Fleet-wide overview — one row per device with its latest reading and an
/// OK/WARN/OFFLINE freshness badge based on reading age (defaults: warn at 15min,
/// offline at 30min, both overridable per request).
/// </summary>
[ApiController]
[Route("api/fleet/fleet")]
[Authorize]
public class FleetStatusController : ControllerBase
{
    private readonly FleetDbStatusRepository _dbStatus;

    public FleetStatusController(FleetDbStatusRepository dbStatus)
    {
        _dbStatus = dbStatus;
    }

    /// <summary>Drives the fleet overview dashboard cards and the device status dots.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetFleetStatus(
        [FromQuery(Name = "warn_seconds")]    int warnSeconds    = 900,
        [FromQuery(Name = "offline_seconds")] int offlineSeconds = 1800,
        [FromQuery(Name = "limit")]           int limit          = 200)
    {
        warnSeconds    = Math.Max(1, warnSeconds);
        offlineSeconds = Math.Max(1, offlineSeconds);
        limit          = Math.Clamp(limit, 1, 5000);

        // TODO: re-enable per-org scoping before multi-tenant production deployment.
        // For now, all authenticated users see all fleet devices (orgId = null → no filter).
        // Previously used: User.FindFirst("OrganizationId") to scope by JWT claim,
        // but different login accounts had different org IDs, causing devices to disappear
        // after re-login with a non-fleet account. Removing the filter fixes the dev workflow.
        var nowUtc = DateTime.UtcNow;
        var rows   = await _dbStatus.GetLatestPerDevice(limit, null);

        string Classify(double ageSeconds)
        {
            if (ageSeconds >= offlineSeconds) return "OFFLINE";
            if (ageSeconds >= warnSeconds)    return "WARN";
            return "OK";
        }

        var allCircuits = FleetPollCircuitBreaker.AllStates;

        var items = rows.Select(r =>
        {
            // Parse the ISO-8601 timestamp stored in the row
            var tsStr  = r.TryGetValue("ts", out var tsObj) ? tsObj?.ToString() : null;
            int? ageSec = null;
            var status  = "OFFLINE";   // default if timestamp is missing or unparseable

            if (DateTimeOffset.TryParse(tsStr, out var dto))
            {
                var age = (nowUtc - dto.UtcDateTime).TotalSeconds;
                ageSec  = (int)Math.Max(0, Math.Round(age));
                status  = Classify(age);
            }

            var hwId       = r.TryGetValue("hardware_id", out var hwObj) ? hwObj?.ToString() ?? "" : "";
            var circuitOpen = allCircuits.TryGetValue(hwId, out var cb)
                && cb.State != FleetPollCircuitBreaker.CircuitState.Closed;

            return new
            {
                status,
                hardwareId   = hwId,
                temperatureC = r.TryGetValue("temperature_c", out var tc)  ? tc              : null,
                humidityPct  = r.TryGetValue("humidity_pct",  out var hm)  ? hm              : null,
                lightLux     = r.TryGetValue("light_lux",     out var ll)  ? ll              : null,
                batteryPct   = r.TryGetValue("battery_pct",   out var bp)  ? bp              : null,
                ts           = tsStr,
                ageSeconds   = ageSec,
                truckId      = r.TryGetValue("truck_id",      out var tid) ? tid             : null,
                truckName    = r.TryGetValue("truck_name",    out var tn)  ? tn?.ToString()  : null,
                plate        = r.TryGetValue("plate",         out var pl)  ? pl?.ToString()  : null,
                sensorName   = r.TryGetValue("sensor_name",   out var sn)  ? sn?.ToString()  : null,
                circuitOpen
            };
        }).ToList();

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                nowUtc         = FleetDbCoreRepository.ToIsoUtc(nowUtc),
                warnSeconds,
                offlineSeconds,
                count          = items.Count,
                items
            }
        });
    }

    /// <summary>
    /// Circuit breaker state per device. "Open" means the cloud API is failing and the
    /// backend is backing off — a device that's "OK" in /status but "Open" here means
    /// readings are stale on our side even though the device itself is fine.
    /// </summary>
    [HttpGet("poll-health")]
    public IActionResult GetPollHealth()
    {
        var states = FleetPollCircuitBreaker.AllStates
            .Select(kv => new
            {
                hardwareId          = kv.Key,
                state               = kv.Value.State.ToString(),
                consecutiveFails    = kv.Value.ConsecutiveFails,
                lastError           = kv.Value.LastError,
                lastErrorAt         = kv.Value.LastErrorAt == default
                    ? (string?)null
                    : kv.Value.LastErrorAt.ToString("o"),
                nextPollAllowedUtc  = kv.Value.State == FleetPollCircuitBreaker.CircuitState.Open
                    ? kv.Value.NextPollAllowed.ToString("o")
                    : (string?)null,
                lastSuccessAt       = kv.Value.LastSuccessAt == default
                    ? (string?)null
                    : kv.Value.LastSuccessAt.ToString("o")
            })
            .OrderBy(d => d.hardwareId)
            .ToList();

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new { count = states.Count, devices = states }
        });
    }
}
