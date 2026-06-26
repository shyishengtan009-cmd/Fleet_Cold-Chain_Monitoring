using System.Threading.Tasks;
using Dapper;
using HIAS_NET_CORE.Context;

namespace HIAS_NET_CORE.Repositories;

/// <summary>
/// Reads the global TZone API credentials stored in iot.tt19_api_config.
///
/// This table predates the per-device credential columns on iot.tt19_devices.
/// It holds a single "global" TZone account used when no per-device credentials
/// are set. The ingest service loads these once at startup so the old DB-stored
/// config automatically takes effect without needing environment variables.
///
/// Credential resolution priority (FleetClientCore.ResolveCredentials):
///   1. Per-device  — iot.tt19_devices.app_id / app_key / app_secret
///   2. DB global   — iot.tt19_api_config (loaded here at startup)
///   3. Env vars    — FLEET_APP_ID / FLEET_APP_KEY / FLEET_APP_SECRET (last resort)
/// </summary>
public class FleetDbApiConfigRepository
{
    private readonly DatabaseContext _databaseContext;

    public FleetDbApiConfigRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    public record ApiConfig(string? Url, string AppId, string AppKey, string AppSecret);

    /// <summary>
    /// Returns the most recently created active row from iot.tt19_api_config,
    /// or null if the table is empty or no row has is_active = true.
    /// Safe to call even if the table does not exist (returns null).
    /// </summary>
    public async Task<ApiConfig?> GetActiveAsync()
    {
        const string sql = @"
SELECT url        AS Url,
       app_id     AS AppId,
       app_key    AS AppKey,
       app_secret AS AppSecret
FROM   iot.tt19_api_config
WHERE  is_active = TRUE
ORDER  BY created_at DESC
LIMIT  1;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<ApiConfig>(sql);
        }
        catch
        {
            return null;
        }
    }
}
