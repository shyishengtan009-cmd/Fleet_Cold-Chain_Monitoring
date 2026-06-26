using System;
using System.IO;
using System.Threading.Tasks;
using FleetCore.Fleet;
using FleetCore.Fleet.Scheduling;
using FleetCore.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using QuestPDF.Fluent;

namespace FleetCore.Controllers;

/// <summary>
/// Cold-truck trip records — open/close/list and GPS route storage in iot.trips.
/// Two creation patterns: live (POST /open, ingest appends points, POST /{id}/close
/// finalizes distance) or batch (POST /save with the full trip + points up front).
/// POST bodies are read as raw JObject rather than [FromBody] for more control over
/// nullable/mixed-type fields in the trip_data blob.
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

    private bool TryGetOrgId(out int orgId)
    {
        orgId = 0;
        var claim = User.FindFirst("OrganizationId")?.Value;
        return claim != null && int.TryParse(claim, out orgId);
    }

    /// <summary>
    /// Trip list, newest-first — all devices in the org, or one if hardware_id is given.
    /// Does not include the GPS points blob; use GET /{id} for that.
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

    /// <summary>All trips for one device, including trip_data so the list view can show route stats.</summary>
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

    /// <summary>
    /// Single trip with full GPS points, for the route map view. Returns 403 (not 404)
    /// for both a missing ID and a foreign-org trip, so existence isn't leaked.
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

    /// <summary>Opens a live trip (start_time set, end_time null) — ingest appends GPS points as they arrive.</summary>
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

    /// <summary>Closes a trip, setting end_time and recalculating total_distance_km from the GPS points.</summary>
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

    /// <summary>Batch-creates a completed trip with full GPS data in one request; body is stored as-is in trip_data.</summary>
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

    /// <summary>
    /// Streams a PDF cold-chain trip report: summary, sensor stats, alarm events,
    /// and a capped readings table.
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

        var startIso = trip.TryGetValue("start_time", out var sv) ? sv?.ToString() : null;
        var endIso   = trip.TryGetValue("end_time",   out var ev) ? ev?.ToString() : null;

        var start = DateTimeOffset.TryParse(startIso, out var sd)
            ? sd.UtcDateTime
            : DateTime.UtcNow.AddHours(-24);

        // If trip is still open, use now as the end of the readings window
        var end = DateTimeOffset.TryParse(endIso, out var ed)
            ? ed.UtcDateTime
            : DateTime.UtcNow;

        // Up to 5000 readings loaded into memory at once (a 24h trip at 10s intervals
        // is ~8,640 points) — reduce the cap or stream row-by-row if that becomes an
        // issue under concurrent PDF requests.
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
