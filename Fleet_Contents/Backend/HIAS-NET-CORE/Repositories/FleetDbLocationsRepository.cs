using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;

namespace HIAS_NET_CORE.Repositories;

/// <summary>
/// Fleet-wide named location library (depots, refuel stops, rest areas, customer sites).
///
/// Used by FleetDwellChecker to apply per-location dwell limits instead of the
/// device-level default. A location with max_dwell_min = null means "no limit here"
/// (e.g. the home depot) — dwell alerts are suppressed entirely at that location.
///
/// Table: iot.tt19_locations
/// </summary>
public class FleetLocationRow
{
    public int      Id          { get; set; }
    public int      OrgId       { get; set; }
    public string   Name        { get; set; } = "";
    public double   Lat         { get; set; }
    public double   Lng         { get; set; }
    public int      RadiusM     { get; set; } = 200;   // geofence radius in metres
    public int?     MaxDwellMin { get; set; }           // null = no alert here
    public string   Type        { get; set; } = "other"; // depot|refuel|rest|customer|other
    public DateTime CreatedAt   { get; set; }
}

public class FleetDbLocationsRepository
{
    private readonly DatabaseContext _databaseContext;

    private const string DwellCacheKey = "fleet:all_locations";
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(60);

    public FleetDbLocationsRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── GetAll ───────────────────────────────────────────────────────────────

    public async Task<List<FleetLocationRow>> GetAll(int orgId)
    {
        const string sql = @"
SELECT id, org_id AS OrgId, name, lat, lng, radius_m AS RadiusM,
       max_dwell_min AS MaxDwellMin, type, created_at AS CreatedAt
FROM iot.tt19_locations
WHERE org_id = @OrgId
ORDER BY name;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            var rows = await connection.QueryAsync<FleetLocationRow>(sql, new { OrgId = orgId });
            return rows.AsList();
        }
        catch (Exception ex)
        {
            FleetLog.Error("[Fleet-Locations] GetAll failed.", ex);
            return new List<FleetLocationRow>();
        }
    }

    // ─── GetAllForDwellCheck ──────────────────────────────────────────────────
    // No org filter — the dwell checker is an internal server-side process
    // that runs after a reading is already scope-checked at ingest time.

    public async Task<List<FleetLocationRow>> GetAllForDwellCheck()
    {
        if (FleetCache.TryGet<List<FleetLocationRow>>(DwellCacheKey, out var cached))
            return cached!;

        const string sql = @"
SELECT id, org_id AS OrgId, name, lat, lng, radius_m AS RadiusM,
       max_dwell_min AS MaxDwellMin, type, created_at AS CreatedAt
FROM iot.tt19_locations;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            var rows = await connection.QueryAsync<FleetLocationRow>(sql);
            var result = rows.AsList();
            FleetCache.Set(DwellCacheKey, result, _cacheTtl);
            return result;
        }
        catch (Exception ex)
        {
            FleetLog.Error("[Fleet-Locations] GetAllForDwellCheck failed.", ex);
            return new List<FleetLocationRow>();
        }
    }

    // ─── Insert ───────────────────────────────────────────────────────────────

    public async Task<FleetLocationRow?> Insert(FleetLocationRow row)
    {
        const string sql = @"
INSERT INTO iot.tt19_locations (org_id, name, lat, lng, radius_m, max_dwell_min, type)
VALUES (@OrgId, @Name, @Lat, @Lng, @RadiusM, @MaxDwellMin, @Type)
RETURNING id, org_id AS OrgId, name, lat, lng, radius_m AS RadiusM,
          max_dwell_min AS MaxDwellMin, type, created_at AS CreatedAt;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<FleetLocationRow>(sql, row);
            FleetCache.Remove(DwellCacheKey);
            return result;
        }
        catch (Exception ex)
        {
            FleetLog.Error("[Fleet-Locations] Insert failed.", ex);
            return null;
        }
    }

    // ─── Update ───────────────────────────────────────────────────────────────

    public async Task<FleetLocationRow?> Update(FleetLocationRow row)
    {
        const string sql = @"
UPDATE iot.tt19_locations
SET    name          = @Name,
       lat           = @Lat,
       lng           = @Lng,
       radius_m      = @RadiusM,
       max_dwell_min = @MaxDwellMin,
       type          = @Type
WHERE  id     = @Id
  AND  org_id = @OrgId
RETURNING id, org_id AS OrgId, name, lat, lng, radius_m AS RadiusM,
          max_dwell_min AS MaxDwellMin, type, created_at AS CreatedAt;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            var result = await connection.QueryFirstOrDefaultAsync<FleetLocationRow>(sql, row);
            FleetCache.Remove(DwellCacheKey);
            return result;
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-Locations] Update failed for id={row.Id}.", ex);
            return null;
        }
    }

    // ─── Delete ───────────────────────────────────────────────────────────────

    public async Task Delete(int id, int orgId)
    {
        const string sql = "DELETE FROM iot.tt19_locations WHERE id = @Id AND org_id = @OrgId;";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            await connection.ExecuteAsync(sql, new { Id = id, OrgId = orgId });
            FleetCache.Remove(DwellCacheKey);
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-Locations] Delete failed for id={id}.", ex);
        }
    }
}
