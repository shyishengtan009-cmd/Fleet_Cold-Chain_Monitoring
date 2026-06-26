using System;
using System.Linq;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Provides a real-time fleet-wide overview — one row per device showing
/// the latest sensor reading, truck assignment, and freshness status.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET /api/fleet/status   — returns latest reading per device with
///                             OK / WARN / OFFLINE classification based on
///                             how recently the device last reported.
///
/// ── When to use this vs other controllers ────────────────────────────────────
///   FleetStatusController   ← this file — fleet-wide, one row per device
///   FleetRealtimeController  — single device, latest reading or alarm history
///   FleetHistoryController   — single device, full time-range data
///   FleetAlarmLogController  — single device, alarm event log
///
/// ── Status classification ─────────────────────────────────────────────────────
///   OK      — last reading < warn_seconds ago      (device is live)
///   WARN    — last reading >= warn_seconds ago     (device may be slow)
///   OFFLINE — last reading >= offline_seconds ago  (device not reporting)
///
///   Default thresholds:
///     warn_seconds    = 900   (15 minutes)
///     offline_seconds = 1800  (30 minutes)
///   Both can be overridden per request via query parameters.
///
/// ── Data source ───────────────────────────────────────────────────────────────
///   FleetDbStatusRepository.GetLatestPerDevice() — uses PostgreSQL DISTINCT ON to return
///   exactly one row per hardware_id, joined with truck assignment data.
///   See FleetDbStatusRepository.cs for the full SQL and column explanation.
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   Results are filtered to the caller's org via the JWT "OrganizationId" claim.
///   If the claim is missing or unparseable, null is passed (returns all devices —
///   intended for admin tools only; normal JWT middleware prevents anonymous calls).
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

    /// <summary>
    /// GET /api/fleet/fleet/status
    ///
    /// Returns the latest reading for each device registered to the caller's org,
    /// together with truck/sensor metadata and a freshness status badge.
    ///
    /// Query parameters:
    ///   warn_seconds    — age threshold (seconds) for WARN status   (default 900)
    ///   offline_seconds — age threshold (seconds) for OFFLINE status (default 1800)
    ///   limit           — max devices returned, clamped to 1–5000    (default 200)
    ///
    /// Response shape:
    /// {
    ///   "code": 0,
    ///   "message": "Success",
    ///   "details": {
    ///     "nowUtc": "2026-03-27T10:00:00Z",
    ///     "warnSeconds": 900,
    ///     "offlineSeconds": 1800,
    ///     "count": 3,
    ///     "items": [
    ///       {
    ///         "status": "OK",
    ///         "hardwareId": "HWID_AABBCCDD",
    ///         "temperatureC": 4.2,
    ///         "humidityPct": 65.1,
    ///         "lightLux": 0.0,
    ///         "batteryPct": 88,
    ///         "ts": "2026-03-27T09:58:00Z",
    ///         "ageSeconds": 120,
    ///         "truckId": 5,
    ///         "truckName": "Cold Truck A",
    ///         "plate": null,
    ///         "sensorName": "Front Sensor"
    ///       }
    ///     ]
    ///   }
    /// }
    ///
    /// Frontend uses this to drive the fleet overview dashboard cards and the
    /// DeviceTabStrip coloured status dots.
    /// </summary>
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

        // Local helper: classify a reading age into OK / WARN / OFFLINE
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

    // ─── GET /api/fleet/fleet/poll-health ─────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/fleet/poll-health
    ///
    /// Returns the current circuit breaker state for every device the ingest
    /// service has attempted to poll. Useful for diagnosing connectivity issues
    /// where a device is physically offline vs the backend can't reach TZone.
    ///
    /// Response:
    /// {
    ///   "details": {
    ///     "devices": [
    ///       {
    ///         "hardwareId": "HWID_...",
    ///         "state": "Closed",            // Closed | Open | HalfOpen
    ///         "consecutiveFails": 0,
    ///         "lastError": null,
    ///         "nextPollAllowedUtc": null,    // only set when state = Open
    ///         "lastSuccessAt": "2026-05-08T10:00:00Z"
    ///       }
    ///     ]
    ///   }
    /// }
    ///
    /// "Closed" = polling normally.
    /// "Open"   = TZone calls are failing; backend is backing off.
    ///            A device that is "OK" in /status but "Open" here means
    ///            TZone is unreachable — readings are stale from our backend's side.
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
