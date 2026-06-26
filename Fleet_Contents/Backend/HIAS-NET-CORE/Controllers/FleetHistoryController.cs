using System;
using System.Threading.Tasks;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetCore.Controllers;

/// <summary>
/// Historical sensor readings for a single device. Typical flow: call /meta to
/// discover the available date range, then /range (raw) or /aggregated (bucketed)
/// for the chart data. iot.tt19_data is indexed on (hardware_id, ts DESC) so both
/// stay fast on large datasets.
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

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    /// <summary>
    /// Earliest/latest timestamp available for a device. count is always 0 here —
    /// counting rows across a large time-series table is expensive; use /range for that.
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

    /// <summary>Raw readings in a UTC window, oldest-first. Backs the line chart and history report.</summary>
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

    /// <summary>
    /// Bucketed min/max/avg aggregates (default hourly) — use instead of /range for
    /// windows over 24h, e.g. a 7-day query returns ~168 buckets instead of ~60k rows.
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
