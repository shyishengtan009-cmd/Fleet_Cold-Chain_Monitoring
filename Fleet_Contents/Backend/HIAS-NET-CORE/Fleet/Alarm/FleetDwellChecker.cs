using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet.Notifications;
using HIAS_NET_CORE.Models.Fleet;
using HIAS_NET_CORE.Repositories;
using Newtonsoft.Json.Linq;

namespace HIAS_NET_CORE.Fleet.Alarm;

/// <summary>
/// Detects when a device has been stationary for longer than its configured limit
/// and fires a dwell alert (email + alarm log entry).
///
/// ── How dwell detection works ────────────────────────────────────────────────
/// On every new GPS reading, the checker compares the current lat/lng to an
/// "anchor" position stored in iot.tt19_dwell_state.
///
///   • If the device moved more than MOVE_THRESHOLD_M (100 m) from the anchor:
///       → Reset anchor to current position. Reset dwell clock.
///
///   • If the device is within 100 m of the anchor (stationary):
///       → Increment dwell duration = now - dwell_since_utc
///       → If dwell > threshold AND cooldown expired → fire alert
///
/// ── Thresholds ───────────────────────────────────────────────────────────────
/// Two-tier threshold system:
///   1. Device default  — alarm_json["dwell_max_minutes"]  (e.g. 15 min)
///      Applied when the truck is not near any named location.
///   2. Location override — iot.tt19_locations.max_dwell_min
///      Applied when the truck is within a named location's radius.
///      max_dwell_min = null → suppress all dwell alerts at this location (e.g. depot).
///
/// ── Cooldown ─────────────────────────────────────────────────────────────────
/// Uses the same email_cooldown_minutes as temperature alarms — reuses existing
/// device_level cooldown so the driver doesn't get spammed with multiple alerts.
/// Dwell alert cooldown stored in tt19_dwell_state.last_alert_sent_at.
///
/// ── Move threshold ───────────────────────────────────────────────────────────
/// 100 m dead-band absorbs GPS drift (typically ±30–50 m in open sky, up to
/// ±150 m in urban/indoor environments). A device genuinely parked will stay
/// within this band; a device that drove to a new location will exceed it.
/// </summary>
public static class FleetDwellChecker
{
    // GPS drift dead-band: must move more than this to reset the anchor
    private const double MoveThresholdM = 100.0;

    // ─── CheckDwell ───────────────────────────────────────────────────────────

    public static async Task CheckDwell(
        string            hardwareId,
        DateTime          ts,
        double            lat,
        double            lng,
        JObject           alarmObj,
        JObject           tripObj,
        FleetDbDwellRepository           dbDwell,
        FleetDbLocationsRepository?      dbLocations,
        FleetDbAlarmLogRepository?       alarmLog,
        FleetDbEmailLogRepository?       dbEmailLog,
        string                           timezone            = "Asia/Kuala_Lumpur",
        List<FleetLocationRow>?          prefetchedLocations = null,
        string?                          deviceLabel         = null)
    {
        // 1. Read dwell settings — if disabled or no threshold, skip
        var dwellEnabled = alarmObj["dwell_enabled"]?.Value<bool>() ?? false;
        if (!dwellEnabled) return;

        var dwellMaxMin = alarmObj["dwell_max_minutes"]?.Value<int?>();
        // No device-level default AND no location library → nothing to check
        // (location library may still provide a threshold, checked later)

        var emailEnabled  = alarmObj["email_enabled"]?.Value<bool>() ?? false;
        var notifyEmail   = alarmObj["notify_email"]?.ToString() ?? "";
        var cooldownMin   = alarmObj["email_cooldown_minutes"]?.Value<int>() ?? 240;

        // Trip metadata for the alert email
        var truckName     = tripObj["truck_name"]?.ToString() ?? deviceLabel ?? hardwareId;
        var startLocation = tripObj["start_location"]?.ToString() ?? "";
        var endLocation   = tripObj["end_location"]?.ToString() ?? "";

        // Skip stale readings — a reading more than 10 minutes old relative to the server clock
        // was buffered while the device was offline. Using it for dwell timing would make a truck
        // that drove through a dead zone appear to have been parked the entire offline gap.
        if ((DateTime.UtcNow - ts).TotalMinutes > 10)
        {
            FleetLog.Info($"[Fleet-Dwell] {hardwareId}: skipping stale reading (ts={ts:O}, age={(DateTime.UtcNow - ts).TotalMinutes:F1}m)");
            return;
        }

        // 2. Load current dwell state
        var state = await dbDwell.GetOrCreate(hardwareId);

        // 3. Determine if the device moved significantly from its anchor
        bool hasAnchor = state.AnchorLat.HasValue && state.AnchorLng.HasValue;
        double distM = hasAnchor
            ? HaversineM(state.AnchorLat!.Value, state.AnchorLng!.Value, lat, lng)
            : double.MaxValue;

        if (!hasAnchor || distM > MoveThresholdM)
        {
            // Device moved or first reading — reset anchor
            state.AnchorLat     = lat;
            state.AnchorLng     = lng;
            state.DwellSinceUtc = ts;
            await dbDwell.Save(state);
            return; // just started stationary period; no alert yet
        }

        // Device is still within the anchor zone — compute dwell duration
        var dwellSince   = state.DwellSinceUtc ?? ts;
        var dwellMinutes = (int)(ts - dwellSince).TotalMinutes;
        if (dwellMinutes < 1) return; // too short to care

        // 4. Check for a matching named location (location-specific threshold)
        FleetLocationRow? matchedLocation = null;
        int? effectiveThreshold = dwellMaxMin;

        if (dbLocations != null)
        {
            var allLocations = prefetchedLocations ?? await dbLocations.GetAllForDwellCheck();
            foreach (var loc in allLocations)
            {
                if (HaversineM(loc.Lat, loc.Lng, lat, lng) <= loc.RadiusM)
                {
                    matchedLocation = loc;
                    if (loc.MaxDwellMin == null)
                    {
                        // This location type suppresses dwell alerts entirely
                        FleetLog.Info($"[Fleet-Dwell] {hardwareId}: at '{loc.Name}' (no-limit location) — suppressed");
                        await dbDwell.Save(state);
                        return;
                    }
                    effectiveThreshold = loc.MaxDwellMin;
                    break;
                }
            }
        }

        if (effectiveThreshold == null) return; // no applicable threshold anywhere

        FleetLog.Info($"[Fleet-Dwell] {hardwareId}: dwell={dwellMinutes}m, threshold={effectiveThreshold}m, location={matchedLocation?.Name ?? "none"}");

        if (dwellMinutes < effectiveThreshold.Value) return; // not long enough yet

        // 5. Cooldown check
        var cooldownExpired = state.LastAlertSentAt == null
            || (ts - state.LastAlertSentAt.Value).TotalMinutes >= cooldownMin;

        if (!cooldownExpired)
        {
            FleetLog.Info($"[Fleet-Dwell] {hardwareId}: dwell alert suppressed — cooldown not expired");
            return;
        }

        // 6. Fire the alert
        var nowMyt  = FleetTime.UtcToLocal(ts, timezone);
        var message = matchedLocation != null
            ? $"[{truckName}] Stationary for {dwellMinutes} min at '{matchedLocation.Name}' (limit: {effectiveThreshold} min)"
            : $"[{truckName}] Stationary for {dwellMinutes} min (limit: {effectiveThreshold} min)";

        FleetLog.Warn($"[Fleet-Dwell] {hardwareId}: {message}");

        // Write to alarm log and push to SignalR
        if (alarmLog != null)
            await alarmLog.Insert(new FleetAlarmLogEntry
            {
                HardwareId = hardwareId,
                Ts         = ts,
                AlarmType  = "WARN",
                Field      = "dwell",
                Value      = dwellMinutes,
                Threshold  = effectiveThreshold.Value,
                Message    = message
            });

        _ = FleetAlarmPusher.Push(hardwareId, new
        {
            id          = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            hardware_id = hardwareId,
            ts          = ts.ToString("O"),
            alarm_type  = "WARN",
            field       = "dwell",
            value       = (double)dwellMinutes,
            threshold   = (double)effectiveThreshold.Value,
            message     = message,
            created_at  = DateTime.UtcNow.ToString("O")
        });

        // Send email
        bool emailSent = false;
        if (emailEnabled && !string.IsNullOrWhiteSpace(notifyEmail))
        {
            var html = BuildDwellHtml(hardwareId, nowMyt, truckName, dwellMinutes,
                effectiveThreshold.Value, lat, lng, matchedLocation, startLocation, endLocation);
            emailSent = await FleetEmailDispatch.SendAlarmToAll(
                notifyEmail, hardwareId, "dwell", 0, "", message, html).ConfigureAwait(false);

            if (emailSent)
                FleetLog.Info($"[Fleet-Dwell] {hardwareId}: dwell email sent to {notifyEmail}");

            if (dbEmailLog != null)
                await dbEmailLog.Insert(new FleetEmailLogEntry
                {
                    HardwareId  = hardwareId,
                    Sensor      = "dwell",
                    ToEmail     = notifyEmail,
                    Description = message,
                    Success     = emailSent
                });
        }

        // Only advance cooldown when the alarm log was written (email success or log-only).
        // Alarm log insert is unconditional above, so always update — but only after the
        // notification attempt so a failed email doesn't silently suppress the next alert.
        if (alarmLog != null || emailSent)
        {
            state.LastAlertSentAt = ts;
            await dbDwell.Save(state);
        }
    }

    // ─── CheckGeofence ────────────────────────────────────────────────────────

    /// <summary>
    /// Fires an ALARM when a device enters any location whose type is 'forbidden'.
    ///
    /// Uses the same iot.tt19_alarm_state table as sensor alarms — key per zone is
    /// "geofence_{locationId}". IsAlarming is set on entry and cleared on exit so that:
    ///   - re-entry after exit always fires a fresh ALARM
    ///   - continuous presence inside the zone does NOT spam (fires once per entry)
    ///
    /// Email cooldown (email_cooldown_minutes from alarm_json) applies at the zone level
    /// as a secondary guard against rapid entry/exit cycling.
    ///
    /// Called from FleetAlarmChecker.CheckAndNotify (Step 4c), independent of sensor alarms.
    /// </summary>
    public static async Task CheckGeofence(
        string                      hardwareId,
        DateTime                    ts,
        double                      lat,
        double                      lng,
        JObject                     alarmObj,
        JObject                     tripObj,
        FleetDbLocationsRepository  dbLocations,
        FleetDbAlarmStateRepository dbAlarmState,
        FleetDbAlarmLogRepository?  alarmLog            = null,
        FleetDbEmailLogRepository?  dbEmailLog          = null,
        string                      timezone            = "Asia/Kuala_Lumpur",
        List<FleetLocationRow>?     prefetchedLocations = null,
        Dictionary<string, FleetAlarmStateRow>? sharedStates = null)
    {
        var emailEnabled = alarmObj["email_enabled"]?.Value<bool>() ?? false;
        var notifyEmail  = alarmObj["notify_email"]?.ToString() ?? "";
        var cooldownMin  = alarmObj["email_cooldown_minutes"]?.Value<int>() ?? 240;
        var truckName    = tripObj["truck_name"]?.ToString() ?? hardwareId;

        var allLocations   = prefetchedLocations ?? await dbLocations.GetAllForDwellCheck();
        var forbiddenZones = allLocations
            .Where(l => string.Equals(l.Type, "forbidden", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (forbiddenZones.Count == 0) return;

        // Use the caller's pre-loaded states dict when available to avoid a redundant
        // GetAllForDevice query (called from CheckAndNotify which already batch-loaded states).
        // Own the dict when called standalone so we can SaveAll at the end.
        var states    = sharedStates ?? await dbAlarmState.GetAllForDevice(hardwareId);
        var ownsStates = sharedStates == null;

        var nowMyt = FleetTime.UtcToLocal(ts, timezone);

        foreach (var zone in forbiddenZones)
        {
            var stateKey = $"geofence_{zone.Id}";
            var state    = GetOrCreateState(states, hardwareId, stateKey);
            var distM    = HaversineM(zone.Lat, zone.Lng, lat, lng);
            var inside   = distM <= zone.RadiusM;

            if (inside && !state.IsAlarming)
            {
                // Entry — fire immediate ALARM
                var cooldownOk = state.LastEmailSentAt == null
                    || (ts - state.LastEmailSentAt.Value).TotalMinutes >= cooldownMin;

                state.IsAlarming     = true;
                state.AlarmStartedAt = DateTime.UtcNow;
                state.LastAlarmedAt  = DateTime.UtcNow;

                var message = $"[{truckName}] Entered forbidden zone '{zone.Name}' ({(int)distM}m from centre, radius {zone.RadiusM}m)";
                FleetLog.Warn($"[Fleet-Geofence] {hardwareId}: {message}");

                if (alarmLog != null)
                    await alarmLog.Insert(new FleetAlarmLogEntry
                    {
                        HardwareId = hardwareId,
                        Ts         = ts,
                        AlarmType  = "ALARM",
                        Field      = stateKey,
                        Value      = Math.Round(distM, 0),
                        Threshold  = zone.RadiusM,
                        Message    = message
                    });

                _ = FleetAlarmPusher.Push(hardwareId, new
                {
                    id          = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    hardware_id = hardwareId,
                    ts          = ts.ToString("O"),
                    alarm_type  = "ALARM",
                    field       = stateKey,
                    value       = Math.Round(distM, 0),
                    threshold   = (double)zone.RadiusM,
                    message     = message,
                    created_at  = DateTime.UtcNow.ToString("O")
                });

                if (emailEnabled && !string.IsNullOrWhiteSpace(notifyEmail) && cooldownOk)
                {
                    var html      = BuildGeofenceHtml(hardwareId, nowMyt, truckName, zone.Name, (int)distM, zone.RadiusM, lat, lng);
                    var emailSent = await FleetEmailDispatch.SendAlarmToAll(
                        notifyEmail, hardwareId, stateKey, 0, "", message, html).ConfigureAwait(false);

                    if (emailSent)
                    {
                        FleetLog.Info($"[Fleet-Geofence] {hardwareId}: geofence email sent to {notifyEmail}");
                        state.LastEmailSentAt = ts;
                    }

                    if (dbEmailLog != null)
                        await dbEmailLog.Insert(new FleetEmailLogEntry
                        {
                            HardwareId  = hardwareId,
                            Sensor      = stateKey,
                            ToEmail     = notifyEmail,
                            Description = message,
                            Success     = emailSent
                        });
                }
            }
            else if (!inside && state.IsAlarming)
            {
                // Exit — silently reset so re-entry fires a fresh ALARM
                state.IsAlarming       = false;
                state.ConsecutiveCount = 0;
                FleetLog.Info($"[Fleet-Geofence] {hardwareId}: exited forbidden zone '{zone.Name}'");
            }
        }

        // Only persist here when we own the dict; when sharedStates was passed in,
        // the caller (CheckAndNotify) handles SaveAll after all checks complete.
        if (ownsStates)
            await dbAlarmState.SaveAll(states.Values);
    }

    // ─── GetOrCreateState (private) ──────────────────────────────────────────

    private static FleetAlarmStateRow GetOrCreateState(
        Dictionary<string, FleetAlarmStateRow> states, string hardwareId, string sensor)
    {
        if (states.TryGetValue(sensor, out var existing)) return existing;
        var row = new FleetAlarmStateRow { HardwareId = hardwareId, Sensor = sensor };
        states[sensor] = row;
        return row;
    }

    // ─── Haversine ────────────────────────────────────────────────────────────

    private static double HaversineM(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371000.0; // Earth radius in metres
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLng = (lng2 - lng1) * Math.PI / 180.0;
        var a    = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                 + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                 * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    // ─── BuildDwellHtml ───────────────────────────────────────────────────────

    private static string BuildDwellHtml(
        string            hardwareId,
        DateTime          nowMyt,
        string            truckName,
        int               dwellMinutes,
        int               threshold,
        double            lat,
        double            lng,
        FleetLocationRow? location,
        string            startLocation,
        string            endLocation)
    {
        var mapsLink = $"https://www.google.com/maps?q={lat.ToString("F6", CultureInfo.InvariantCulture)},{lng.ToString("F6", CultureInfo.InvariantCulture)}";
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.AlertParagraph(
                $"{truckName} has been stationary for {dwellMinutes} minutes (limit: {threshold} min).",
                "#f57c00")
            + FleetEmailTemplate.InfoTable([
                ("Truck",      truckName),
                ("Duration",   $"{dwellMinutes} min"),
                ("Limit",      $"{threshold} min"),
                ("Known stop", location != null ? $"{location.Name} ({location.Type})" : null),
                ("From",       string.IsNullOrWhiteSpace(startLocation) ? null : startLocation),
                ("To",         string.IsNullOrWhiteSpace(endLocation)   ? null : endLocation),
                ("Location",   $"<a href=\"{mapsLink}\" style=\"color:#1976d2\">View on Google Maps</a>"),
            ]);
        return FleetEmailTemplate.HtmlPage("⏱ Fleet DWELL ALERT — Stationary Too Long", "#f57c00", body);
    }

    // ─── BuildGeofenceHtml ────────────────────────────────────────────────────

    private static string BuildGeofenceHtml(
        string   hardwareId,
        DateTime nowMyt,
        string   truckName,
        string   zoneName,
        int      distM,
        int      radiusM,
        double   lat,
        double   lng)
    {
        var mapsLink = $"https://www.google.com/maps?q={lat.ToString("F6", CultureInfo.InvariantCulture)},{lng.ToString("F6", CultureInfo.InvariantCulture)}";
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.AlertParagraph($"{truckName} entered forbidden zone '{zoneName}'.", "#b71c1c")
            + FleetEmailTemplate.InfoTable([
                ("Truck",       truckName),
                ("Zone",        zoneName),
                ("Zone radius", $"{radiusM} m"),
                ("Distance",    $"{distM} m from zone centre"),
                ("Location",    $"<a href=\"{mapsLink}\" style=\"color:#1976d2\">View on Google Maps</a>"),
            ]);
        return FleetEmailTemplate.HtmlPage("🚫 Fleet GEOFENCE VIOLATION", "#b71c1c", body);
    }
}
