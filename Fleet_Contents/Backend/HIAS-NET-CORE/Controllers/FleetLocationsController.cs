using System;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// CRUD API for the fleet-wide named location library.
///
/// Locations are shared across all devices in an organisation. They are used by
/// the dwell-detection system to apply per-location alert thresholds
/// (e.g. longer grace period at a refuelling stop than at an unknown roadside).
///
///   GET    /api/fleet/locations           — list all locations for the caller's org
///   POST   /api/fleet/locations/save      — create (id=0) or update (id>0) a location
///   DELETE /api/fleet/locations/{id}      — delete a location
/// </summary>
[ApiController]
[Route("api/fleet/locations")]
[Authorize]
public class FleetLocationsController : ControllerBase
{
    private readonly FleetDbLocationsRepository _dbLocations;

    public FleetLocationsController(FleetDbLocationsRepository dbLocations)
    {
        _dbLocations = dbLocations;
    }

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    private ContentResult Json(object payload)
    {
        var json = JsonConvert.SerializeObject(payload);
        return Content(json, "application/json");
    }

    // ─── GET /api/fleet/locations ─────────────────────────────────────────────

    [HttpGet("")]
    public async Task<IActionResult> GetLocations()
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        var locations = await _dbLocations.GetAll(orgId);
        return Json(new { code = 0, message = "Success", details = new { locations } });
    }

    // ─── POST /api/fleet/locations/save ──────────────────────────────────────

    public class SaveLocationRequest
    {
        public int     id           { get; set; }
        public string? name         { get; set; }
        public double  lat          { get; set; }
        public double  lng          { get; set; }
        public int     radius_m     { get; set; } = 200;
        public int?    max_dwell_min { get; set; }
        public string? type         { get; set; } = "other";
    }

    [HttpPost("save")]
    public async Task<IActionResult> SaveLocation([FromBody] SaveLocationRequest req)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (string.IsNullOrWhiteSpace(req.name))
            return BadRequest(new { code = 400, message = "name is required" });

        if (req.radius_m < 50 || req.radius_m > 10000)
            return BadRequest(new { code = 400, message = "radius_m must be 50–10000 m" });

        var row = new FleetLocationRow
        {
            Id          = req.id,
            OrgId       = orgId,
            Name        = req.name.Trim(),
            Lat         = req.lat,
            Lng         = req.lng,
            RadiusM     = req.radius_m,
            MaxDwellMin = req.max_dwell_min,
            Type        = req.type ?? "other"
        };

        FleetLocationRow? saved = req.id > 0
            ? await _dbLocations.Update(row)
            : await _dbLocations.Insert(row);

        if (saved == null)
            return StatusCode(500, new { code = 500, message = "Failed to save location." });

        return Json(new { code = 0, message = "Success", details = new { location = saved } });
    }

    // ─── DELETE /api/fleet/locations/{id} ────────────────────────────────────

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteLocation(int id)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        await _dbLocations.Delete(id, orgId);
        return Json(new { code = 0, message = "Success" });
    }
}
