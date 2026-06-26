using System;
using System.IO;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Fleet.Scheduling;
using HIAS_NET_CORE.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;

namespace HIAS_NET_CORE.Controllers;

/// <summary>
/// Manages cold-truck trip records — open, close, list, and save GPS routes.
///
/// ── What this controller does ─────────────────────────────────────────────────
///   GET  /api/fleet/trips            — list trips (all devices or one device)
///   GET  /api/fleet/trips/list       — trips for one device including trip_data
///   GET  /api/fleet/trips/{id}       — single trip with full GPS points payload
///   POST /api/fleet/trips/open       — create a new open trip (no end_time yet)
///   POST /api/fleet/trips/{id}/close — close an open trip and set end_time
///   POST /api/fleet/trips/save       — create a completed trip with GPS data
///                                      (Pattern B: post-trip batch upload)
///
/// ── Two trip creation patterns ────────────────────────────────────────────────
///   Pattern A — Live trip (typical usage):
///     1. POST /open         → creates trip with start_time, end_time = NULL
///     2. Ingest service appends GPS points automatically as readings arrive
///     3. POST /{id}/close   → sets end_time, finalises distance from stored points
///
///   Pattern B — Post-trip batch upload (legacy / manual):
///     POST /save with full trip data including GPS points array
///     The server stores the provided trip_data JSON blob as-is.
///
/// ── Organisation scoping ─────────────────────────────────────────────────────
///   - ListTrips: scoped by orgId via SQL JOIN
///   - GetTripsForDevice, GetTrip, OpenTrip, CloseTrip, SaveTrip:
///     device ownership verified via FleetDbDevicesRepository.DeviceBelongsToOrg()
///   - GetTrip also verifies ownership after loading the trip (trip → hardware_id → org check)
///
/// ── JSON body parsing ─────────────────────────────────────────────────────────
///   POST actions read the body as raw JSON (not [FromBody]) because JObject gives
///   more control over nullable fields and avoids System.Text.Json deserialization
///   issues with mixed-type or null values in the trip_data blob.
///
/// ── DB tables used ────────────────────────────────────────────────────────────
///   iot.trips — trip records with GPS route in trip_data JSON column
///   See FleetDbTripsRepository.cs for the full schema and helper method documentation.
/// </summary>
[ApiController]
[Route("api/fleet/trips")]
[Authorize]
public class FleetTripsController : ControllerBase
{
    private readonly FleetDbDevicesRepository  _dbDevices;
    private readonly FleetDbTripsRepository    _dbTrips;
    private readonly FleetDbSettingsRepository _dbSettings;
    private readonly FleetDbRealtimeRepository _dbRealtime;
    private readonly FleetDbAlarmLogRepository _dbAlarmLog;

    public FleetTripsController(
        FleetDbDevicesRepository  dbDevices,
        FleetDbTripsRepository    dbTrips,
        FleetDbSettingsRepository dbSettings,
        FleetDbRealtimeRepository dbRealtime,
        FleetDbAlarmLogRepository dbAlarmLog)
    {
        _dbDevices  = dbDevices;
        _dbTrips    = dbTrips;
        _dbSettings = dbSettings;
        _dbRealtime = dbRealtime;
        _dbAlarmLog = dbAlarmLog;
    }

    // ─── Helper: extract OrganizationId from JWT claim ────────────────────────

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    // ─── GET /api/fleet/trips ─────────────────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/trips?hardware_id=...&amp;limit=50
    ///
    /// Returns a list of trips. If hardware_id is provided, scoped to that device.
    /// If hardware_id is omitted, returns trips for all devices in the caller's org.
    /// Results are ordered by start_time DESC (newest first).
    ///
    /// Note: This endpoint does NOT return the full GPS points blob.
    ///       Use GET /api/fleet/trips/{id} to get the full trip with GPS data.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ListTrips(
        [FromQuery(Name = "hardware_id")] string? hardwareId = null,
        [FromQuery(Name = "limit")]       int     limit      = 50)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        // If a specific device is requested, verify ownership before filtering
        if (!string.IsNullOrWhiteSpace(hardwareId))
        {
            hardwareId = hardwareId.Trim();
            if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
                return StatusCode(403, new { code = 403, message = "Access denied." });
        }

        var trips = await _dbTrips.ListTrips(
            string.IsNullOrWhiteSpace(hardwareId) ? null : hardwareId,
            limit,
            orgId);

        return Ok(new { code = 0, message = "Success", details = new { count = trips.Count, trips } });
    }

    // ─── GET /api/fleet/trips/list ────────────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/trips/list?hardware_id=HWID_AABBCCDD
    ///
    /// Returns all trips for a specific device, including the trip_data JSON blob
    /// (GPS points, route metadata). Use this for the trip history list view.
    ///
    /// Unlike the main GET /trips, this endpoint includes the trip_data column so
    /// the frontend can display route statistics for each trip in the list.
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> GetTripsForDevice([FromQuery(Name = "hardware_id")] string hardwareId = "")
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return BadRequest(new { code = 400, message = "hardware_id is required" });

        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        if (!await _dbDevices.DeviceBelongsToOrg(hardwareId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        var trips = await _dbTrips.GetTripsForDevice(hardwareId);
        return Ok(new { code = 0, message = "Success", details = new { hardwareId, count = trips.Count, trips } });
    }

    // ─── GET /api/fleet/trips/{id} ────────────────────────────────────────────

    /// <summary>
    /// GET /api/fleet/trips/123
    ///
    /// Returns a single trip by its database ID, including the full GPS points
    /// payload in trip_data. Use this when the user clicks on a trip to view the
    /// route on the map.
    ///
    /// Returns 403 if the trip ID does not exist OR if the trip's device belongs
    /// to a different org than the caller. This is intentional — we don't reveal
    /// whether the ID exists for a foreign org.
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetTrip(long id)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        var trip = await _dbTrips.GetTrip(id);
        if (trip is null)
            return StatusCode(403, new { code = 403, message = "Access denied." });

        // Verify the trip's device belongs to the caller's org
        var tripHwId = trip.TryGetValue("hardware_id", out var hwObj) ? hwObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(tripHwId) || !await _dbDevices.DeviceBelongsToOrg(tripHwId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        return Ok(new { code = 0, message = "Success", details = trip });
    }

    // ─── POST /api/fleet/trips/open ───────────────────────────────────────────

    /// <summary>
    /// POST /api/fleet/trips/open
    ///
    /// Creates a new open trip (Pattern A — live trip). The trip has a start_time
    /// but no end_time. The ingest service will append GPS points automatically
    /// as new sensor readings arrive from the device.
    ///
    /// Body (JSON):
    /// {
    ///   "hardware_id": "HWID_AABBCCDD",
    ///   "start_time":  "2026-03-27T08:00:00Z"
    /// }
    ///
    /// Response:
    /// { "code": 0, "message": "Success", "details": { "trip_id": 42 } }
    ///
    /// Call POST /trips/{id}/close when the trip ends to set end_time and
    /// finalise the total distance from the accumulated GPS points.
    /// </summary>
    [HttpPost("open")]
    public async Task<IActionResult> OpenTrip()
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { code = 400, message = "Empty body" });

        JObject req;
        try { req = JObject.Parse(body); }
        catch { return BadRequest(new { code = 400, message = "Invalid JSON" }); }

        var hw        = req["hardware_id"]?.ToString()?.Trim() ?? "";
        var startTime = req["start_time"]?.ToString();

        if (string.IsNullOrWhiteSpace(hw))
            return BadRequest(new { code = 400, message = "hardware_id is required" });
        if (string.IsNullOrWhiteSpace(startTime))
            return BadRequest(new { code = 400, message = "start_time is required" });

        if (!await _dbDevices.DeviceBelongsToOrg(hw, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        try
        {
            // Load device settings once — shared between SLA extraction and notification
            int? slaMinutes = null;
            Dictionary<string, object?>? cachedSettings = null;
            try
            {
                cachedSettings = await _dbSettings.GetDeviceSettings(hw);
                if (cachedSettings != null)
                {
                    var tripObj = (cachedSettings["trip_json"] as JObject) ?? new JObject();
                    slaMinutes = tripObj["trip_max_minutes"]?.Value<int?>();
                }
            }
            catch { /* non-fatal — trip opens without SLA */ }

            var tripId = await _dbTrips.CreateOpenTrip(hw, startTime!, slaMinutes);

            // Fire-and-forget: send trip-start email without delaying the response.
            // Reuse the settings already loaded above to avoid a second DB call.
            var capturedSettings = cachedSettings;
            _ = Task.Run(async () =>
            {
                try
                {
                    var settings = capturedSettings ?? await _dbSettings.GetDeviceSettings(hw);
                    if (settings != null)
                    {
                        var alarmObj = (settings["alarm_json"] as JObject) ?? new JObject();
                        var tripObj  = (settings["trip_json"]  as JObject) ?? new JObject();
                        var nowMyt   = FleetTime.UtcToLocal(DateTime.UtcNow, null);
                        await FleetTripScheduler.TrySendTripStartNotificationAsync(
                            hw, alarmObj, tripObj, nowMyt, tripId, "Manual trip").ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    FleetLog.Warn($"[Fleet-Trips] {hw}: trip-start notification failed — {ex.Message}");
                }
            });

            return Ok(new { code = 0, message = "Success", details = new { trip_id = tripId } });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    // ─── POST /api/fleet/trips/{id}/close ────────────────────────────────────

    /// <summary>
    /// POST /api/fleet/trips/123/close
    ///
    /// Closes an open trip. Sets end_time and recalculates total_distance_km
    /// from the GPS points accumulated during the trip.
    ///
    /// Body (JSON):
    /// { "end_time": "2026-03-27T16:30:00Z" }
    ///
    /// Response on success:
    /// { "code": 0, "message": "Success", "details": { "trip_id": 123, "total_distance_km": 47.2, ... } }
    ///
    /// Returns 400 if the trip was already closed or does not exist.
    /// Returns 403 if the trip's device belongs to a different org.
    /// </summary>
    [HttpPost("{id:long}/close")]
    public async Task<IActionResult> CloseTrip(long id)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { code = 400, message = "Empty body" });

        JObject req;
        try { req = JObject.Parse(body); }
        catch { return BadRequest(new { code = 400, message = "Invalid JSON" }); }

        var endTime = req["end_time"]?.ToString();
        if (string.IsNullOrWhiteSpace(endTime))
            return BadRequest(new { code = 400, message = "end_time is required" });

        // Verify the trip's device belongs to the caller's org via trip → hardware_id
        var existing = await _dbTrips.GetTrip(id);
        if (existing is null)
            return StatusCode(403, new { code = 403, message = "Access denied." });

        var tripHwId = existing.TryGetValue("hardware_id", out var hwObj) ? hwObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(tripHwId) || !await _dbDevices.DeviceBelongsToOrg(tripHwId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        // Load settings once — shared between breach-stat extraction and notification
        double? tempMinC = null, tempMaxC = null;
        Dictionary<string, object?>? cachedSettings = null;
        try
        {
            cachedSettings = await _dbSettings.GetDeviceSettings(tripHwId!);
            if (cachedSettings != null)
            {
                var alarmObj = (cachedSettings["alarm_json"] as JObject) ?? new JObject();
                tempMinC = alarmObj["temp_min_c"]?.Value<double?>();
                tempMaxC = alarmObj["temp_max_c"]?.Value<double?>();
            }
        }
        catch { /* non-fatal — trip closes without breach stats */ }

        try
        {
            var result = await _dbTrips.CloseTrip(id, endTime!, tempMinC, tempMaxC);
            if (result is null)
                return BadRequest(new { code = 400, message = "Trip not found or already closed." });

            // Fire-and-forget: send trip-end email without delaying the response.
            // Reuse the settings already loaded above to avoid a second DB call.
            var capturedHwId     = tripHwId!;
            var capturedResult   = result;
            var capturedSettings = cachedSettings;
            _ = Task.Run(async () =>
            {
                try
                {
                    var settings = capturedSettings ?? await _dbSettings.GetDeviceSettings(capturedHwId);
                    if (settings != null)
                    {
                        var alarmObj = (settings["alarm_json"] as JObject) ?? new JObject();
                        var tripObj  = (settings["trip_json"]  as JObject) ?? new JObject();
                        var nowMyt   = FleetTime.UtcToLocal(DateTime.UtcNow, null);
                        await FleetTripScheduler.TrySendTripEndNotificationAsync(
                            capturedHwId, alarmObj, tripObj, nowMyt, capturedResult).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    FleetLog.Warn($"[Fleet-Trips] {capturedHwId}: trip-end notification failed — {ex.Message}");
                }
            });

            return Ok(new { code = 0, message = "Success", details = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    // ─── POST /api/fleet/trips/save ───────────────────────────────────────────

    /// <summary>
    /// POST /api/fleet/trips/save
    ///
    /// Creates a completed trip with full GPS data in one request (Pattern B).
    /// Use this when the trip data is uploaded as a batch after the trip ends,
    /// rather than being accumulated live during the trip.
    ///
    /// Body (JSON):
    /// {
    ///   "hardware_id":        "HWID_AABBCCDD",
    ///   "start_time":         "2026-03-27T08:00:00Z",
    ///   "end_time":           "2026-03-27T16:30:00Z",
    ///   "total_distance_km":  47.2,
    ///   "points": [
    ///     { "lat": 3.139, "lon": 101.686, "ts": "2026-03-27T08:00:00Z" },
    ///     ...
    ///   ]
    /// }
    ///
    /// The entire body is stored as the trip_data JSON blob in iot.trips.
    ///
    /// Response:
    /// { "code": 0, "message": "Success", "details": { "trip_id": 43, ... } }
    /// </summary>
    [HttpPost("save")]
    public async Task<IActionResult> SaveTrip()
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();
        if (string.IsNullOrWhiteSpace(body))
            return BadRequest(new { code = 400, message = "Empty body" });

        JObject tripData;
        try { tripData = JObject.Parse(body); }
        catch { return BadRequest(new { code = 400, message = "Invalid JSON" }); }

        var hw        = tripData["hardware_id"]?.ToString()?.Trim() ?? "";
        var startTime = tripData["start_time"]?.ToString();
        var endTime   = tripData["end_time"]?.ToString();
        var distKm    = tripData["total_distance_km"]?.ToObject<double>() ?? 0;

        if (string.IsNullOrWhiteSpace(hw))
            return BadRequest(new { code = 400, message = "hardware_id is required" });
        if (string.IsNullOrWhiteSpace(startTime))
            return BadRequest(new { code = 400, message = "start_time is required" });

        if (!await _dbDevices.DeviceBelongsToOrg(hw, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        try
        {
            var result = await _dbTrips.SaveTrip(hw, startTime!, endTime, distKm, tripData);
            return Ok(new { code = 0, message = "Success", details = result });
        }
        catch (Exception ex)
        {
            return BadRequest(new { code = 400, message = ex.Message });
        }
    }

    // ─── GET /api/fleet/trips/{id}/report.pdf ────────────────────────────────

    /// <summary>
    /// GET /api/fleet/trips/123/report.pdf
    ///
    /// Generates and streams a PDF cold-chain trip report for the given trip.
    ///
    /// The report contains:
    ///   - Trip summary (start/end time MYT, duration, distance)
    ///   - Sensor statistics (min/max/avg temperature, humidity, battery)
    ///   - Alarm events table (all alarms during the trip)
    ///   - Full sensor readings table (capped at 500 rows)
    ///
    /// Returns 403 if the trip does not exist or belongs to another org.
    /// Returns 400 if the trip has no sensor data (readings window is empty).
    /// </summary>
    [HttpGet("{id:long}/report.pdf")]
    public async Task<IActionResult> GetTripReport(long id)
    {
        if (!TryGetOrgId(out var orgId))
            return Unauthorized(new { code = 401, message = "Organisation not found in token." });

        var trip = await _dbTrips.GetTrip(id);
        if (trip is null)
            return StatusCode(403, new { code = 403, message = "Access denied." });

        var tripHwId = trip.TryGetValue("hardware_id", out var hwObj) ? hwObj?.ToString() : null;
        if (string.IsNullOrWhiteSpace(tripHwId) || !await _dbDevices.DeviceBelongsToOrg(tripHwId, orgId))
            return StatusCode(403, new { code = 403, message = "Access denied." });

        // ── Parse trip time window ────────────────────────────────────────────
        var startIso = trip.TryGetValue("start_time", out var sv) ? sv?.ToString() : null;
        var endIso   = trip.TryGetValue("end_time",   out var ev) ? ev?.ToString() : null;

        var start = DateTimeOffset.TryParse(startIso, out var sd)
            ? sd.UtcDateTime
            : DateTime.UtcNow.AddHours(-24);

        // If trip is still open, use now as the end of the readings window
        var end = DateTimeOffset.TryParse(endIso, out var ed)
            ? ed.UtcDateTime
            : DateTime.UtcNow;

        // ── Load supporting data in parallel ──────────────────────────────────
        // NOTE: up to 5000 readings are loaded into memory at once for PDF generation.
        // A 24-hour trip at 10 s intervals = ~8,640 points (capped here at 5000).
        // If memory becomes a concern under concurrent PDF requests, reduce the cap
        // or stream the PDF row-by-row instead of buffering everything upfront.
        var readingsTask  = _dbRealtime.GetRowsForRange(tripHwId, start, end, 5000);
        var alarmsTask    = _dbAlarmLog.GetByDateRange(tripHwId,  start, end, 2000);
        var settingsTask  = _dbSettings.GetDeviceSettings(tripHwId);

        await Task.WhenAll(readingsTask, alarmsTask, settingsTask);

        var readings = readingsTask.Result;
        var alarms   = alarmsTask.Result.Select(a => new Dictionary<string, object?>
        {
            ["ts"]         = a.Ts,
            ["field"]      = a.Field,
            ["alarm_type"] = a.AlarmType,
            ["value"]      = a.Value,
            ["threshold"]  = a.Threshold,
            ["message"]    = a.Message
        }).ToList();

        var settings  = settingsTask.Result;
        var truckName = settings?.TryGetValue("truck_name", out var tn) == true
            ? tn?.ToString() ?? ""
            : "";

        // ── Generate PDF ──────────────────────────────────────────────────────
        var doc = new FleetTripReportDocument
        {
            HardwareId = tripHwId,
            TruckName  = truckName,
            Trip       = trip,
            Readings   = readings,
            AlarmRows  = alarms
        };

        var pdfBytes = doc.GeneratePdf();

        var fileName = $"TripReport_{tripHwId}_{id}.pdf";
        return File(pdfBytes, "application/pdf", fileName);
    }
}
