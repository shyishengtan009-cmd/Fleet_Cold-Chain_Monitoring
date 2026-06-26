using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using FleetCore.Context;
using FleetCore.Repositories;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

namespace FleetCore.Repositories;

/// <summary>
/// Manages the iot.trips table — GPS route records for cold-truck deliveries.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   SaveTrip              — create a completed trip from frontend-submitted data
///   ListTrips             — list trips with optional device/org filters
///   GetTrip               — load one trip by ID, including the GPS points blob
///   GetTripsForDevice     — list trips for one device (with GPS data)
///   CreateOpenTrip        — start a new in-progress trip (end_time = NULL)
///   GetActiveTripForDevice — check if a device currently has an open trip
///   GetActiveTripSummary  — lightweight summary of the open trip (no GPS blob)
///   AppendTripPoint       — add one GPS point to an open trip's route
///   CloseTrip             — finalise an open trip with end_time + recalculated distance
///
/// ── Table: iot.trips ─────────────────────────────────────────────────────────
/// Columns: id, hardware_id, start_time, end_time (NULL = in progress),
///          total_distance_km, points_count, trip_data (JSONB), created_at
///
/// The trip_data JSONB column holds the GPS route as:
///   { "points": [{"lat":3.1234, "lng":101.5678, "ts":"2025-01-01T06:00:00Z", "temp":4.5}, ...] }
///
/// ── How trips work ────────────────────────────────────────────────────────────
/// There are two trip creation patterns:
///
///   Pattern A — Live trip (recommended)
///     1. User clicks "Start Trip" → CreateOpenTrip() creates a row with end_time = NULL
///     2. Every poll cycle → AppendTripPoint() adds one GPS point to the JSONB array
///     3. User clicks "Stop Trip" → CloseTrip() recalculates distance and sets end_time
///
///   Pattern B — Post-trip upload
///     1. Frontend sends all GPS points at once → SaveTrip() inserts and filters in one step
///
/// ── GPS filtering explained ───────────────────────────────────────────────────
/// Raw GPS data is noisy. FilterTripPoints() removes:
///   - Points at 0,0 (no satellite lock)
///   - Points within 5 m of the previous point (stationary jitter)
///   - Points within 10 seconds of the previous point (duplicate bursts)
/// Distance is accumulated only for steps ≥ 10 m to avoid counting micro-jitter.
///
/// ── GetActiveTripSummary vs GetTripsForDevice ─────────────────────────────────
/// GetActiveTripSummary() returns just id + start_time + distance_km WITHOUT loading
/// the GPS blob. This is used by FleetAlarmChecker to enrich breach notifications
/// with trip context ("breach happened 12 km into trip #42") without the memory
/// overhead of loading potentially thousands of GPS points.
///
/// ── NOTE: DB table names ──────────────────────────────────────────────────────
/// Table name (iot.trips) is unchanged. Only C# / API names have been migrated
/// from "TT19" to "Fleet".
/// </summary>
public class FleetDbTripsRepository
{
    private readonly DatabaseContext _databaseContext;

    public FleetDbTripsRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── Private Dapper mapping class ────────────────────────────────────────

    private class TripDbRow
    {
        public long      Id                { get; set; }
        public string    HardwareId        { get; set; } = "";
        public DateTime  StartTime         { get; set; }
        public DateTime? EndTime           { get; set; }
        public double    TotalDistanceKm   { get; set; }
        public int       PointsCount       { get; set; }
        public string?   TripData          { get; set; }
        public DateTime  CreatedAt         { get; set; }
        public int?      SlaMinutes        { get; set; }
        public int?      MinutesLate       { get; set; }
        public int?      BreachMinutesHot  { get; set; }
        public int?      BreachMinutesCold { get; set; }
    }

    private class TripPreCloseRow
    {
        public string?   TripData   { get; set; }
        public int?      SlaMinutes { get; set; }
        public DateTime  StartTime  { get; set; }
    }

    private class TripSummaryRow
    {
        public long     Id              { get; set; }
        public DateTime StartTime       { get; set; }
        public double   TotalDistanceKm { get; set; }
    }

    // ─── HaversineKm (private) ────────────────────────────────────────────────

    /// <summary>
    /// Calculates the great-circle distance in kilometres between two GPS coordinates
    /// using the Haversine formula. Used to accumulate total trip distance.
    ///
    /// Formula: https://en.wikipedia.org/wiki/Haversine_formula
    /// Earth radius constant: 6371 km
    /// </summary>
    private static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLng = (lng2 - lng1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ─── FilterTripPoints (private) ───────────────────────────────────────────

    /// <summary>
    /// Cleans a raw GPS point array and recalculates the true travel distance.
    ///
    /// Points are removed if:
    ///   - lat == 0 AND lng == 0  (device has no satellite lock)
    ///   - within 5 m of the previous retained point  (stationary jitter)
    ///   - within 10 seconds of the previous retained point  (burst duplicates)
    ///
    /// Distance is only accumulated for steps ≥ 10 m, to ignore micro-jitter
    /// that survives the position filter but isn't real movement.
    ///
    /// Returns the filtered JArray and the recalculated total distance in km.
    /// </summary>
    private static (JArray filtered, double distKm) FilterTripPoints(JArray? raw)
    {
        const double MIN_MOVE_DEG = 0.00005; // ≈ 5 m in degrees of lat/lng
        const double MIN_DIST_KM  = 0.010;   // 10 m minimum step for distance accumulation
        const int    MIN_SECS     = 10;       // minimum seconds between retained points

        var filtered = new JArray();
        double totalKm = 0;
        JObject? prev = null;
        DateTime prevTs = DateTime.MinValue;

        foreach (var token in raw ?? new JArray())
        {
            if (token is not JObject pt) continue;
            var lat = pt["lat"]?.ToObject<double>() ?? 0;
            var lng = pt["lng"]?.ToObject<double>() ?? 0;

            if (lat == 0 && lng == 0) continue; // no satellite lock — skip

            if (prev != null)
            {
                var pLat = prev["lat"]!.ToObject<double>();
                var pLng = prev["lng"]!.ToObject<double>();

                // Skip if too close to previous point (< 5 m by degree approximation)
                if (Math.Abs(lat - pLat) < MIN_MOVE_DEG && Math.Abs(lng - pLng) < MIN_MOVE_DEG)
                    continue;

                // Skip if timestamp too close to previous (< 10 s)
                if (DateTime.TryParse(pt["ts"]?.ToString(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var currTs)
                    && prevTs != DateTime.MinValue
                    && (currTs - prevTs).TotalSeconds < MIN_SECS)
                    continue;

                // Accumulate distance only for steps ≥ 10 m
                var km = HaversineKm(pLat, pLng, lat, lng);
                if (km >= MIN_DIST_KM) totalKm += km;

                if (currTs != DateTime.MinValue) prevTs = currTs;
            }
            else
            {
                DateTime.TryParse(pt["ts"]?.ToString(), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out prevTs);
            }

            filtered.Add(pt);
            prev = pt;
        }

        return (filtered, totalKm);
    }

    // ─── MapTripRow (private) ─────────────────────────────────────────────────

    /// <summary>
    /// Maps a typed Dapper row to the trip dictionary returned by all public methods.
    /// Set includeData = true to include the trip_data JSON string (GPS points).
    /// Set includeData = false for list views where the blob is not needed.
    /// </summary>
    private static Dictionary<string, object?> MapTripRow(TripDbRow r, bool includeData)
    {
        var row = new Dictionary<string, object?>
        {
            ["trip_id"]              = r.Id,
            ["hardware_id"]          = r.HardwareId,
            ["start_time"]           = r.StartTime.ToUniversalTime().ToString("o"),
            ["end_time"]             = r.EndTime.HasValue ? r.EndTime.Value.ToUniversalTime().ToString("o") : null,
            ["total_distance_km"]    = r.TotalDistanceKm,
            ["points_count"]         = r.PointsCount,
            ["created_at"]           = r.CreatedAt.ToUniversalTime().ToString("o"),
            ["sla_minutes"]          = r.SlaMinutes,
            ["minutes_late"]         = r.MinutesLate,
            ["breach_minutes_hot"]   = r.BreachMinutesHot,
            ["breach_minutes_cold"]  = r.BreachMinutesCold
        };

        if (includeData) row["trip_data"] = r.TripData ?? "{}";
        return row;
    }

    // ─── SaveTrip ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Saves a completed trip submitted by the frontend (Pattern B upload).
    /// GPS points in tripDataJson["points"] are filtered server-side before storing.
    /// If the frontend sent totalDistanceKm == 0, the server-recalculated value is used.
    ///
    /// Returns the saved trip row WITHOUT the GPS blob (use GetTrip() to load points).
    /// </summary>
    public async Task<Dictionary<string, object?>> SaveTrip(
        string hardwareId,
        string startTime,
        string? endTime,
        double totalDistanceKm,
        JObject tripDataJson)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) throw new ArgumentException("hardware_id is required");

        // Server-side GPS filtering: clean points and recalculate distance
        var rawPoints = tripDataJson["points"] as JArray;
        var (filteredPts, recalcKm) = FilterTripPoints(rawPoints);
        tripDataJson["points"] = filteredPts;
        if (totalDistanceKm < 0.001) totalDistanceKm = recalcKm;

        var pointsCount = filteredPts.Count;

        const string sql = @"
INSERT INTO iot.trips (hardware_id, start_time, end_time, total_distance_km, points_count, trip_data, created_at)
VALUES (@hardware_id, @start_time, @end_time, @total_distance_km, @points_count, @trip_data::jsonb, NOW())
RETURNING
    id                    AS Id,
    hardware_id           AS HardwareId,
    start_time            AS StartTime,
    end_time              AS EndTime,
    total_distance_km     AS TotalDistanceKm,
    points_count          AS PointsCount,
    created_at            AS CreatedAt,
    sla_minutes           AS SlaMinutes,
    minutes_late          AS MinutesLate,
    breach_minutes_hot    AS BreachMinutesHot,
    breach_minutes_cold   AS BreachMinutesCold;
";
        using var connection = _databaseContext.CreateConnection();
        var row = await connection.QuerySingleAsync<TripDbRow>(sql, new
        {
            hardware_id       = hardwareId,
            start_time        = DateTime.Parse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime(),
            end_time          = endTime != null ? (DateTime?)DateTime.Parse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime() : null,
            total_distance_km = totalDistanceKm,
            points_count      = pointsCount,
            trip_data         = tripDataJson.ToString(Formatting.None)
        });

        return MapTripRow(row, includeData: false);
    }

    // ─── ListTrips ────────────────────────────────────────────────────────────

    /// <summary>
    /// Lists trips with optional filters: by device, by organisation, or both.
    /// Does NOT include the GPS points blob — just metadata for list views.
    /// Limit is capped at 200 rows.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> ListTrips(
        string? hardwareId = null, int limit = 50, int? orgId = null)
    {
        limit = Math.Clamp(limit, 1, 200);

        // Single parameterized query: NULL parameters make the WHERE clause a no-op for that filter.
        const string sql = @"
SELECT id                    AS Id,
       hardware_id           AS HardwareId,
       start_time            AS StartTime,
       end_time              AS EndTime,
       total_distance_km     AS TotalDistanceKm,
       points_count          AS PointsCount,
       created_at            AS CreatedAt,
       sla_minutes           AS SlaMinutes,
       minutes_late          AS MinutesLate,
       breach_minutes_hot    AS BreachMinutesHot,
       breach_minutes_cold   AS BreachMinutesCold
FROM iot.trips
WHERE (@hardware_id::text IS NULL OR hardware_id = @hardware_id)
  AND (@org_id::int        IS NULL OR hardware_id IN (
           SELECT hardware_id FROM iot.tt19_devices WHERE organization_id = @org_id))
ORDER BY start_time DESC
LIMIT @limit;";

        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<TripDbRow>(sql, new
        {
            hardware_id = hardwareId,
            org_id      = orgId,
            limit
        });
        return rows.Select(r => MapTripRow(r, includeData: false)).ToList();
    }

    // ─── GetTrip ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads a single trip by its primary key ID, including the full GPS points blob.
    /// Returns null if no trip with that ID exists.
    /// Used by the frontend trip detail / map view.
    /// </summary>
    public async Task<Dictionary<string, object?>?> GetTrip(long tripId)
    {
        const string tripSql = @"
SELECT id                    AS Id,
       hardware_id           AS HardwareId,
       start_time            AS StartTime,
       end_time              AS EndTime,
       total_distance_km     AS TotalDistanceKm,
       points_count          AS PointsCount,
       trip_data::text       AS TripData,
       created_at            AS CreatedAt,
       sla_minutes           AS SlaMinutes,
       minutes_late          AS MinutesLate,
       breach_minutes_hot    AS BreachMinutesHot,
       breach_minutes_cold   AS BreachMinutesCold
FROM iot.trips WHERE id = @tripId;";

        using var connection = _databaseContext.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<TripDbRow>(tripSql, new { tripId });
        if (row is null) return null;

        // trip_points is authoritative for post-0013 trips (open or closed).
        // For legacy trips closed before migration 0013 the table is empty — fall back
        // to the trip_data JSONB blob that was compiled at close time.
        const string ptsSql = @"
SELECT ts::text AS Ts, lat AS Lat, lng AS Lng, temperature_c AS Temp
FROM iot.trip_points WHERE trip_id = @tripId ORDER BY ts ASC;";
        var pts = (await connection.QueryAsync<TripPointRow>(ptsSql, new { tripId })).AsList();
        if (pts.Count > 0)
        {
            var arr = new JArray();
            foreach (var p in pts)
                arr.Add(new JObject { ["lat"] = p.Lat, ["lng"] = p.Lng, ["ts"] = p.Ts, ["temp"] = p.Temp });
            row.TripData = new JObject { ["points"] = arr }.ToString(Formatting.None);
        }

        return MapTripRow(row, includeData: true);
    }

    // ─── GetTripsForDevice ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the most recent trips for a device, including the GPS points blob.
    /// Ordered by creation time descending (newest first).
    /// Limit is capped at 200.
    ///
    /// Returns metadata only — GPS blob is excluded to prevent 30 MB+ responses when
    /// a device has hundreds of trips. Use GetTrip(id) to load points for one trip.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetTripsForDevice(string hardwareId, int limit = 50)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) return new();

        const string sql = @"
SELECT id                    AS Id,
       hardware_id           AS HardwareId,
       start_time            AS StartTime,
       end_time              AS EndTime,
       total_distance_km     AS TotalDistanceKm,
       points_count          AS PointsCount,
       created_at            AS CreatedAt,
       sla_minutes           AS SlaMinutes,
       minutes_late          AS MinutesLate,
       breach_minutes_hot    AS BreachMinutesHot,
       breach_minutes_cold   AS BreachMinutesCold
FROM iot.trips
WHERE hardware_id = @hardwareId
ORDER BY created_at DESC
LIMIT @limit;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<TripDbRow>(sql, new { hardwareId, limit = Math.Clamp(limit, 1, 200) });
        return rows.Select(r => MapTripRow(r, includeData: false)).ToList();
    }

    // ─── CreateOpenTrip ───────────────────────────────────────────────────────

    /// <summary>
    /// Creates an in-progress trip row with end_time = NULL (Pattern A — live trip).
    /// Called when the user clicks "Start Trip" on the Real-Time Monitoring page.
    /// Returns the new trip's database ID so subsequent AppendTripPoint() calls
    /// know which row to update.
    /// </summary>
    public async Task<long> CreateOpenTrip(string hardwareId, string startTime, int? slaMinutes = null)
    {
        const string sql = @"
INSERT INTO iot.trips (hardware_id, start_time, total_distance_km, points_count, trip_data, created_at, sla_minutes)
VALUES (@hardwareId, @startTime, 0, 0, '{""points"":[]}'::jsonb, NOW(), @slaMinutes)
RETURNING id;";
        using var connection = _databaseContext.CreateConnection();
        return await connection.QuerySingleAsync<long>(sql, new
        {
            hardwareId,
            startTime  = DateTime.Parse(startTime, null, System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime(),
            slaMinutes = (object?)(slaMinutes.HasValue ? (int?)slaMinutes.Value : null)
        });
    }

    // ─── GetActiveTripForDevice ───────────────────────────────────────────────

    /// <summary>
    /// Returns the ID of the most recent open trip (end_time IS NULL) for a device.
    /// Returns null if the device has no in-progress trip.
    ///
    /// Used by FleetIngestService to decide whether to call AppendTripPoint() on
    /// each new sensor reading.
    /// </summary>
    public async Task<long?> GetActiveTripForDevice(string hardwareId)
    {
        const string sql = @"
SELECT id FROM iot.trips
WHERE hardware_id = @hardwareId AND end_time IS NULL
ORDER BY created_at DESC LIMIT 1;";
        using var connection = _databaseContext.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<long?>(sql, new { hardwareId });
    }

    // ─── GetActiveTripSummary ─────────────────────────────────────────────────

    /// <summary>
    /// Returns a lightweight summary of the active open trip: id, start_time (ISO UTC),
    /// and total_distance_km.
    ///
    /// IMPORTANT: This does NOT load the GPS points blob (trip_data). It is intentionally
    /// lightweight so FleetAlarmChecker can enrich alarm notifications with trip context
    /// (e.g. "alarm at km 12.3 into trip #42") without the memory overhead of loading
    /// potentially thousands of GPS points.
    ///
    /// Returns null if the device has no active open trip.
    /// </summary>
    public async Task<(long tripId, string startTimeIso, double distKm)?> GetActiveTripSummary(string hardwareId)
    {
        const string sql = @"
SELECT id                AS Id,
       start_time        AS StartTime,
       total_distance_km AS TotalDistanceKm
FROM iot.trips
WHERE hardware_id = @hardwareId AND end_time IS NULL
ORDER BY created_at DESC LIMIT 1;";
        using var connection = _databaseContext.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<TripSummaryRow>(sql, new { hardwareId });
        if (row is null) return null;

        // AppendTripPoint now maintains total_distance_km incrementally (O(1) per poll),
        // so the column is accurate for all trips opened after migration 0005.
        // LEGACY: trips started before 0005 still have total_distance_km = 0. The fallback
        // below loads ALL trip_points to compute distance — O(N) per alarm check cycle.
        // This only matters for pre-0005 trips that are still open. Once those trips are
        // closed the fallback is never hit again. New trips always use the incremental path.
        double distKm = row.TotalDistanceKm;
        if (distKm < 0.001)
        {
            const string ptsSql = @"
SELECT lat AS Lat, lng AS Lng FROM iot.trip_points
WHERE trip_id = @tripId AND lat <> 0 AND lng <> 0
ORDER BY ts ASC;";
            var pts = (await connection.QueryAsync<TripPointRow>(ptsSql, new { tripId = row.Id })).AsList();
            double accum = 0;
            for (int i = 1; i < pts.Count; i++)
                accum += HaversineKm(pts[i - 1].Lat, pts[i - 1].Lng, pts[i].Lat, pts[i].Lng);
            distKm = Math.Round(accum, 2);
        }

        return (row.Id, row.StartTime.ToUniversalTime().ToString("o"), distKm);
    }

    // ─── AppendTripPoint ──────────────────────────────────────────────────────

    /// <summary>
    /// Appends one GPS point to iot.trip_points (O(1) per point).
    ///
    /// Previously used jsonb_set on the trip_data JSONB blob, which was O(N) per
    /// append → O(N²) total for an 8-hour trip (2,880 points ≈ 4M total bytes written).
    /// Now each point is a separate row — indexed by trip_id + ts.
    ///
    /// CloseTrip reads all rows from trip_points, filters them, writes the final
    /// cleaned route into trips.trip_data, and deletes the live point rows.
    /// </summary>
    public async Task AppendTripPoint(long tripId, double lat, double lng, double? temp, string ts)
    {
        if (lat == 0 && lng == 0) return;

        const string insertSql = @"
INSERT INTO iot.trip_points (trip_id, ts, lat, lng, temperature_c)
VALUES (@tripId, @ts::timestamptz, @lat, @lng, @temp)
ON CONFLICT DO NOTHING;";
        using var connection = _databaseContext.CreateConnection();
        var affected = await connection.ExecuteAsync(insertSql, new { tripId, ts, lat, lng, temp });

        // Maintain a running total_distance_km on the trips row so GetActiveTripSummary
        // can read it directly without loading all trip_points (O(N) → O(1) per alarm).
        if (affected > 0)
        {
            const string prevSql = @"
SELECT lat AS Lat, lng AS Lng FROM iot.trip_points
WHERE trip_id = @tripId AND ts < @ts::timestamptz AND lat <> 0 AND lng <> 0
ORDER BY ts DESC LIMIT 1;";
            var prev = await connection.QueryFirstOrDefaultAsync<TripPointRow>(prevSql, new { tripId, ts });
            if (prev != null)
            {
                var delta = HaversineKm(prev.Lat, prev.Lng, lat, lng);
                if (delta >= 0.010) // same 10 m threshold as FilterTripPoints MIN_DIST_KM
                    await connection.ExecuteAsync(
                        "UPDATE iot.trips SET total_distance_km = total_distance_km + @delta WHERE id = @tripId;",
                        new { tripId, delta });
            }
        }
    }

    // ─── CloseTrip ────────────────────────────────────────────────────────────

    /// <summary>
    /// Finalises an open trip: sets end_time, recalculates the filtered distance,
    /// and updates points_count.
    ///
    /// GPS DUAL-STORAGE NOTE: Two GPS data paths exist because the iot.trip_points table
    /// was added after trips were already in production. Both are merged at close time:
    ///   • iot.trip_points rows  — new path (post-migration 0005). One row per GPS point.
    ///   • trips.trip_data JSONB — legacy path (pre-0005). A JSON array of GPS points.
    /// New trips only ever write to trip_points. The JSONB merge is kept so that any trips
    /// still open from before 0005 close correctly. trip_points rows are deleted after close.
    /// iot.trips.trip_data JSONB remains the authoritative GPS blob after the trip is closed
    /// and is used by the PDF trip report.
    ///
    /// Returns the closed trip metadata row (no GPS blob).
    /// Returns null if no open trip exists with that ID (e.g. already closed).
    /// </summary>
    public async Task<Dictionary<string, object?>?> CloseTrip(
        long    tripId,
        string  endTime,
        double? tempMinC = null,
        double? tempMaxC = null)
    {
        using var connection = (Npgsql.NpgsqlConnection)_databaseContext.CreateConnection();
        await connection.OpenAsync();

        // Steps 1a-1c: load and merge GPS points (reads outside transaction — no writes yet)
        const string pointsSql = @"
SELECT ts::text AS Ts, lat AS Lat, lng AS Lng, temperature_c AS Temp
FROM iot.trip_points
WHERE trip_id = @tripId
ORDER BY ts ASC;";
        var livePoints = (await connection.QueryAsync<TripPointRow>(pointsSql, new { tripId })).AsList();

        var preClose = await connection.QueryFirstOrDefaultAsync<TripPreCloseRow>(
            @"SELECT trip_data::text AS TripData, sla_minutes AS SlaMinutes, start_time AS StartTime
              FROM iot.trips WHERE id = @tripId AND end_time IS NULL;",
            new { tripId });
        if (preClose is null) return null;

        JArray legacyPoints;
        try { legacyPoints = JObject.Parse(preClose.TripData ?? "{}")["points"] as JArray ?? new JArray(); }
        catch { legacyPoints = new JArray(); }

        var mergedRaw = new List<JToken>();
        foreach (var p in livePoints)
            mergedRaw.Add(new JObject { ["lat"] = p.Lat, ["lng"] = p.Lng, ["ts"] = p.Ts, ["temp"] = p.Temp });
        foreach (var p in legacyPoints)
            mergedRaw.Add(p);
        var merged = new JArray(mergedRaw.OrderBy(t => t["ts"]?.ToString()));

        // Step 2 — compute temperature breach duration from ALL merged points (before GPS filter)
        int breachMinutesHot = 0, breachMinutesCold = 0;
        if (tempMinC.HasValue || tempMaxC.HasValue)
        {
            DateTime? prevTs = null;
            foreach (var token in merged)
            {
                if (token is not JObject pt) continue;
                var tsStr = pt["ts"]?.ToString();
                if (string.IsNullOrEmpty(tsStr)
                    || !DateTime.TryParse(tsStr, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var currTs)) continue;
                var tempVal = pt["temp"]?.ToObject<double?>();

                if (prevTs.HasValue && tempVal.HasValue)
                {
                    var intervalMin = (int)Math.Max(0, Math.Round((currTs - prevTs.Value).TotalMinutes));
                    if (intervalMin > 0 && intervalMin < 120) // cap at 2 h to ignore huge gaps
                    {
                        if (tempMaxC.HasValue && tempVal.Value >= tempMaxC.Value) breachMinutesHot  += intervalMin;
                        if (tempMinC.HasValue && tempVal.Value <= tempMinC.Value) breachMinutesCold += intervalMin;
                    }
                }
                prevTs = currTs;
            }
        }

        // Step 3 — filter GPS noise and recalculate distance
        var (filteredPts, distKm) = FilterTripPoints(merged);

        // Step 4 — compute SLA overage
        var endDt      = DateTime.Parse(endTime, null, System.Globalization.DateTimeStyles.RoundtripKind | System.Globalization.DateTimeStyles.AssumeUniversal).ToUniversalTime();
        int? minutesLate = null;
        if (preClose.SlaMinutes.HasValue && preClose.SlaMinutes.Value > 0)
        {
            var actualMinutes = (endDt - preClose.StartTime.ToUniversalTime()).TotalMinutes;
            minutesLate = Math.Max(0, (int)Math.Round(actualMinutes - preClose.SlaMinutes.Value));
        }

        // Step 5 — UPDATE trips metadata only.
        // trip_points rows are intentionally kept: iot.trip_points is now the authoritative
        // GPS store (migration 0013). trip_data JSONB is no longer written for new trips;
        // legacy trips (closed before 0013) retain their JSONB blob for the fallback path.
        const string updateSql = @"
UPDATE iot.trips
SET end_time            = @endTime,
    total_distance_km   = @distKm,
    points_count        = @ptsCount,
    breach_minutes_hot  = @breachHot,
    breach_minutes_cold = @breachCold,
    minutes_late        = @minutesLate
WHERE id = @tripId AND end_time IS NULL
RETURNING
    id                    AS Id,
    hardware_id           AS HardwareId,
    start_time            AS StartTime,
    end_time              AS EndTime,
    total_distance_km     AS TotalDistanceKm,
    points_count          AS PointsCount,
    created_at            AS CreatedAt,
    sla_minutes           AS SlaMinutes,
    minutes_late          AS MinutesLate,
    breach_minutes_hot    AS BreachMinutesHot,
    breach_minutes_cold   AS BreachMinutesCold;";

        var updateParams = new
        {
            tripId,
            endTime     = endDt,
            distKm,
            ptsCount    = filteredPts.Count,
            breachHot   = breachMinutesHot  > 0 ? (int?)breachMinutesHot  : null,
            breachCold  = breachMinutesCold > 0 ? (int?)breachMinutesCold : null,
            minutesLate = minutesLate
        };

        TripDbRow? row = await connection.QueryFirstOrDefaultAsync<TripDbRow>(updateSql, updateParams);

        return row is null ? null : MapTripRow(row, includeData: false);
    }

    private class TripPointRow
    {
        public string  Ts   { get; set; } = "";
        public double  Lat  { get; set; }
        public double  Lng  { get; set; }
        public double? Temp { get; set; }
    }
}
