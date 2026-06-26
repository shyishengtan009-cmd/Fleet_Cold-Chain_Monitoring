namespace HIAS_NET_CORE.Fleet;

/// <summary>
/// Centralized limit constants for the Fleet module.
/// All query/page/export row caps live here — no more scattered 200/500/1000/5000 literals.
/// </summary>
public static class FleetLimits
{
    // History / realtime data
    public const int HistoryMaxRows    = 5_000;  // GET /History, GET /Realtime history range
    public const int RealtimeMaxRows   = 1_000;  // live readings returned to dashboard

    // Trips
    public const int TripListMaxRows   = 200;    // trip list per device
    public const int TripPointsMaxRows = 5_000;  // GPS points per trip export

    // Alarm / notification logs
    public const int AlarmLogMaxRows   = 500;    // alarm log page queries
    public const int EmailLogMaxRows   = 500;    // email log page queries

    // Devices
    public const int DeviceListMaxRows = 1_000;  // max devices returned in one call

    // Locations
    public const int LocationMaxRows   = 500;    // named location library

    // Export
    public const int ExportMaxRows     = 5_000;  // Excel / CSV export hard cap
}
