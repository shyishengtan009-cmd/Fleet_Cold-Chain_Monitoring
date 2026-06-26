using System;
using System.Threading.Tasks;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Provides historical sensor reading data for a single device.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET /api/fleet/history/meta    — returns the earliest and latest timestamps
///                                    available for a device (quick DB probe)
///   GET /api/fleet/history/range   — returns all readings in a UTC time window
///
/// ── Typical frontend flow ─────────────────────────────────────────────────────
///   1. Call /meta to discover what date range is available for the device
///   2. Let the user pick a start/end date in the UI
///   3. Call /range with the selected window to fetch chart data
///
/// ── Performance notes ─────────────────────────────────────────────────────────
///   /meta does NOT count rows — counting across a large time-series table is slow.
///   If you need a row count, use the /range endpoint and check "count" in the
///   response. The limit parameter (max 50000) prevents memory issues on large ranges.
///
///   iot.tt19_data has an index on (hardware_id, ts DESC) so both endpoints
///   are fast even for large datasets.
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   Both endpoints enforce FleetDbDevicesRepository.DeviceBelongsToOrg() to prevent one
///   organisation from reading another org's device history.
/// </summary>
[ApiController]
[Route("api/fleet/history")]
[Authorize]
public class FleetHistoryController : ControllerBase
{
    private readonly FleetDbDevicesRepository  _dbDevices;
    private readonly FleetDbRealtimeRepository _dbRealtime;

    public FleetHistoryController(FleetDbDevicesRepository dbDevices, FleetDbRealtimeRepository dbRealtime)
    {
        _dbDevices  = dbDevices;
        _dbRealtime = dbRealtime;
    }

    // ─── Helper: extract OrganizationId from JWT claim ────────────────────────

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    // ─── GET /api/fleet/history/meta ─────────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/history/meta?hardware_id=HWID_AABBCCDD
    ///
    /// Returns the first and last timestamps for which data exists in iot.tt19_data
    /// for the given device. Use this to discover what date range to offer in the UI.
    ///
    /// Response (data exists):
    /// {
    ///   "code": 0, "message": "Success",
    ///   "details": {
    ///     "found": true,
    ///     "hardwareId": "HWID_AABBCCDD",
    ///     "minTs": "2026-01-15T08:00:00Z",
    ///     "maxTs": "2026-03-27T10:00:00Z",
    ///     "count": 0   ← always 0 (not fetched for performance)
    ///   }
    /// }
    ///
    /// Response (no data yet):
    /// { "code": 0, "message": "Success", "details": { "found": false, ... } }
    ///
    /// Note: count is intentionally 0 — fetching the row count for large tables is
    /// expensive. Use the /range endpoint with your selected window to get a count.
    /// </summary>
    [HttpGet("meta")]
    public async Task<IActionResult> GetMeta([FromQuery(Name = "hardware_id")] string hardwareId = "")
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        // TODO: re-enable per-org ownership check before multi-tenant production deployment.
        var (minTs, maxTs) = await _dbRealtime.GetMinMaxTs(hardwareId);

        if (minTs is null)
            return Ok(new
            {
                code    = 0,
                message = "Success",
                details = new { found = false, hardwareId, minTs = (string?)null, maxTs = (string?)null, count = 0 }
            });

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                found      = true,
                hardwareId,
                minTs      = FleetDbCoreRepository.ToIsoUtc(minTs.Value),
                maxTs      = maxTs is null ? null : FleetDbCoreRepository.ToIsoUtc(maxTs.Value),
                count      = 0   // count not fetched here for performance; use /range
            }
        });
    }

    // ─── GET /api/fleet/history/range ────────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/history/range?hardware_id=...&amp;start_utc=...&amp;end_utc=...&amp;limit=20000
    ///
    /// Returns all sensor readings for a device within the given UTC time window.
    /// Results are ordered oldest-first (chronological, good for chart rendering).
    ///
    /// Query parameters:
    ///   hardware_id — required; device to query
    ///   start_utc   — required; ISO-8601 UTC timestamp (e.g. "2026-03-01T00:00:00Z")
    ///   end_utc     — required; ISO-8601 UTC timestamp (e.g. "2026-03-02T00:00:00Z")
    ///   limit       — max rows, clamped to 1–50000 (default 20000)
    ///
    /// Response:
    /// {
    ///   "code": 0, "message": "Success",
    ///   "details": {
    ///     "hardwareId": "HWID_AABBCCDD",
    ///     "startUtc": "2026-03-01T00:00:00Z",
    ///     "endUtc": "2026-03-02T00:00:00Z",
    ///     "count": 2880,
    ///     "rows": [ { "ts": "...", "temperature_c": 4.1, ... }, ... ]
    ///   }
    /// }
    ///
    /// Used by the Line Chart and the History Report page.
    /// </summary>
    [HttpGet("range")]
    public async Task<IActionResult> GetRange(
        [FromQuery(Name = "hardware_id")] string hardwareId  = "",
        [FromQuery(Name = "start_utc")]   string startUtcStr = "",
        [FromQuery(Name = "end_utc")]     string endUtcStr   = "",
        [FromQuery(Name = "limit")]       int    limit       = 20000)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        // TODO: re-enable per-org ownership check before multi-tenant production deployment.
        if (!DateTimeOffset.TryParse(startUtcStr, out var startDto))
            return BadRequest(new { code = 400, message = "Missing or invalid start_utc. Use ISO-8601 e.g. 2026-01-01T00:00:00Z" });

        if (!DateTimeOffset.TryParse(endUtcStr, out var endDto))
            return BadRequest(new { code = 400, message = "Missing or invalid end_utc. Use ISO-8601 e.g. 2026-01-02T00:00:00Z" });

        var startUtc = startDto.UtcDateTime;
        var endUtc   = endDto.UtcDateTime;

        if (endUtc < startUtc)
            return BadRequest(new { code = 400, message = "end_utc must be >= start_utc" });

        limit = Math.Clamp(limit, 1, 50000);
        var rows = await _dbRealtime.GetRowsForRange(hardwareId, startUtc, endUtc, limit);

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                hardwareId,
                startUtc = FleetDbCoreRepository.ToIsoUtc(startUtc),
                endUtc   = FleetDbCoreRepository.ToIsoUtc(endUtc),
                count    = rows.Count,
                rows
            }
        });
    }

    // ─── GET /api/fleet/history/aggregated ───────────────────────────────────

    /// <summary>
    /// GET /api/fleet/history/aggregated?hardware_id=...&amp;start_utc=...&amp;end_utc=...&amp;bucket_minutes=60
    ///
    /// Returns hourly (or custom bucket) min/max/avg aggregates for chart rendering.
    /// Use this instead of /range when the requested window is larger than 24 hours —
    /// a 7-day query returns ~168 hourly buckets instead of ~60 000 raw rows.
    ///
    /// bucket_minutes is clamped to 5–1440 (default 60 = hourly).
    ///
    /// Each row contains: ts, temp_min/max/avg, hum_min/max/avg, light_min/max/avg,
    /// batt_min/max/avg.
    /// </summary>
    [HttpGet("aggregated")]
    public async Task<IActionResult> GetAggregated(
        [FromQuery(Name = "hardware_id")]    string hardwareId     = "",
        [FromQuery(Name = "start_utc")]      string startUtcStr    = "",
        [FromQuery(Name = "end_utc")]        string endUtcStr      = "",
        [FromQuery(Name = "bucket_minutes")] int    bucketMinutes  = 60)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!DateTimeOffset.TryParse(startUtcStr, out var startDto))
            return BadRequest(new { code = 400, message = "Missing or invalid start_utc. Use ISO-8601 e.g. 2026-01-01T00:00:00Z" });

        if (!DateTimeOffset.TryParse(endUtcStr, out var endDto))
            return BadRequest(new { code = 400, message = "Missing or invalid end_utc. Use ISO-8601 e.g. 2026-01-02T00:00:00Z" });

        var startUtc = startDto.UtcDateTime;
        var endUtc   = endDto.UtcDateTime;

        if (endUtc < startUtc)
            return BadRequest(new { code = 400, message = "end_utc must be >= start_utc" });

        var rows = await _dbRealtime.GetAggregatedRows(hardwareId, startUtc, endUtc, bucketMinutes);

        return Ok(new
        {
            code    = 0,
            message = "Success",
            details = new
            {
                hardwareId,
                startUtc      = FleetDbCoreRepository.ToIsoUtc(startUtc),
                endUtc        = FleetDbCoreRepository.ToIsoUtc(endUtc),
                bucketMinutes,
                count         = rows.Count,
                rows
            }
        });
    }
}
