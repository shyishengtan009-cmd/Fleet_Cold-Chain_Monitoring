using System;
using System.Threading.Tasks;
using Dapper;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;

namespace HIAS_NET_CORE.Repositories;

/// <summary>
/// Per-device GPS dwell (prolonged-stationary) tracking state.
///
/// One row per hardware_id. Tracks where the device anchored, when it
/// stopped moving, and when the last dwell alert was sent.
///
/// Table: iot.tt19_dwell_state
/// </summary>
public class FleetDwellStateRow
{
    public int       Id               { get; set; }
    public string    HardwareId       { get; set; } = "";
    public double?   AnchorLat        { get; set; }   // lat where stationary period started
    public double?   AnchorLng        { get; set; }   // lng where stationary period started
    public DateTime? DwellSinceUtc    { get; set; }   // when device last moved > threshold
    public DateTime? LastAlertSentAt  { get; set; }   // cooldown for dwell emails
}

public class FleetDbDwellRepository
{
    private readonly DatabaseContext _databaseContext;

    public FleetDbDwellRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    public async Task<FleetDwellStateRow> GetOrCreate(string hardwareId)
    {
        // Single round-trip: DO UPDATE with a no-op forces RETURNING to fire on conflict.
        const string sql = @"
INSERT INTO iot.tt19_dwell_state (hardware_id)
VALUES (@HardwareId)
ON CONFLICT (hardware_id) DO UPDATE SET hardware_id = EXCLUDED.hardware_id
RETURNING id,
          hardware_id        AS HardwareId,
          anchor_lat         AS AnchorLat,
          anchor_lng         AS AnchorLng,
          dwell_since_utc    AS DwellSinceUtc,
          last_alert_sent_at AS LastAlertSentAt;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            var row = await connection.QueryFirstOrDefaultAsync<FleetDwellStateRow>(sql, new { HardwareId = hardwareId });
            return row ?? new FleetDwellStateRow { HardwareId = hardwareId };
        }
        catch (Exception ex)
        {
            FleetLog.Error("[Fleet-Dwell] GetOrCreate failed.", ex);
            return new FleetDwellStateRow { HardwareId = hardwareId };
        }
    }

    public async Task Save(FleetDwellStateRow state)
    {
        const string sql = @"
UPDATE iot.tt19_dwell_state
SET    anchor_lat         = @AnchorLat,
       anchor_lng         = @AnchorLng,
       dwell_since_utc    = @DwellSinceUtc,
       last_alert_sent_at = @LastAlertSentAt,
       updated_at         = NOW()
WHERE  hardware_id = @HardwareId;
";
        try
        {
            using var connection = _databaseContext.CreateConnection();
            await connection.ExecuteAsync(sql, state);
        }
        catch (Exception ex)
        {
            FleetLog.Error($"[Fleet-Dwell] Save failed for {state.HardwareId}.", ex);
        }
    }
}
