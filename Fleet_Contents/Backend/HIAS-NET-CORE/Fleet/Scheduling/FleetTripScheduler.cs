using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using FleetCore.Fleet;
using FleetCore.Fleet.Notifications;
using FleetCore.Repositories;
using Newtonsoft.Json.Linq;

namespace FleetCore.Fleet.Scheduling;

/// <summary>
/// Automatically starts and stops trips based on a device's configured daily schedule.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   CheckAndApplySchedule      — main entry point; called after every device poll
///   TrySendTripStartNotification — email when trip auto-starts
///   TrySendTripEndNotification   — email when trip auto-closes (with summary)
///   TrySendEndWarning            — email ~5 minutes before the trip ends
///   IsActiveDateRange            — checks effective_start / effective_end date window
///   IsActiveDay                  — checks repeat_days day-of-week filter
///
/// ── How auto-trip works ───────────────────────────────────────────────────────
/// The device's alarm_json settings contain a daily schedule:
///   auto_trip    = true                    — enables this feature
///   daily_start  = "08:00"                 — trip starts at this MYT time
///   daily_end    = "18:00"                 — trip ends at this MYT time
///   repeat_days  = ["Mon","Tue","Wed",...]  — which days (empty = every day)
///   effective_start = "2025-01-01"         — don't auto-start before this date
///   effective_end   = "2025-12-31"         — don't auto-start after this date
///
/// These same schedule fields are used for alarm filtering in FleetAlarmChecker —
/// alarms only fire during the same window as the auto-trip.
///
/// ── Behaviour per poll cycle ──────────────────────────────────────────────────
///   INSIDE window  → open a new trip if none is currently running
///                  → send trip-start notification (once per trip)
///   1–5 min before end → send "ending soon" warning (once per MYT day)
///   PAST end time  → close the active trip (if any)
///                  → send trip-ended notification with duration + distance
///   BEFORE window  → no action
///
/// ── Idempotency ───────────────────────────────────────────────────────────────
/// CheckAndApplySchedule is safe to call on EVERY poll cycle (every ~10 seconds).
/// All actions check the current DB state before acting:
///   - Trip start: only creates a trip if GetActiveTripForDevice() returns null
///   - Trip close: only closes if GetActiveTripForDevice() returns a value
///   - End warning: only sends once per day (tracked via _sched_warn alarm state)
///
/// ── Overnight schedules ───────────────────────────────────────────────────────
/// NOT SUPPORTED. daily_start must be earlier than daily_end (e.g. 08:00–18:00).
/// If start >= end, the method logs a warning and does nothing.
///
/// ── All times are MYT (UTC+8) ─────────────────────────────────────────────────
/// The server stores timestamps in UTC internally, but schedule config and all
/// notification times are displayed in MYT (Malaysia Time = UTC+8).
/// </summary>
public static class FleetTripScheduler
{
    // ─── CheckAndApplySchedule ────────────────────────────────────────────────

    /// <summary>
    /// Main entry point. Checks the device's schedule and opens/closes trips as needed.
    /// Called by FleetIngestService after each successful device poll.
    ///
    /// Safe to call on every poll cycle — all actions are guarded by DB state checks.
    /// </summary>
    public static async Task CheckAndApplySchedule(
        string            hardwareId,
        FleetDbSettingsRepository   dbSettings,
        FleetDbAlarmStateRepository dbAlarmState,
        FleetDbTripsRepository      dbTrips,
        string            timezone             = "Asia/Kuala_Lumpur",
        long?             prefetchedTripId     = null)
    {
        // Load current alarm settings for this device
        var settings = await dbSettings.GetDeviceSettings(hardwareId);
        if (settings == null)
        {
            FleetLog.Warn($"[Fleet-Schedule] {hardwareId}: no device settings found — skipping.");
            return;
        }

        var alarmObj = settings["alarm_json"] as JObject;
        if (alarmObj == null || alarmObj.Count == 0)
        {
            FleetLog.Warn($"[Fleet-Schedule] {hardwareId}: alarm_json is empty — skipping.");
            return;
        }

        var tripObj = (settings["trip_json"] as JObject) ?? new JObject();

        // Feature gate: auto_trip must be explicitly enabled in Device Settings
        var autoTrip = alarmObj["auto_trip"]?.Value<bool>() ?? false;
        if (!autoTrip)
        {
            FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: auto_trip disabled — skipping.");
            return;
        }

        // Both daily_start and daily_end are required for auto-trip to work
        var dailyStartStr = alarmObj["daily_start"]?.ToString();
        var dailyEndStr   = alarmObj["daily_end"]?.ToString();

        if (string.IsNullOrWhiteSpace(dailyStartStr) || string.IsNullOrWhiteSpace(dailyEndStr))
        {
            FleetLog.Warn($"[Fleet-Schedule] {hardwareId}: daily_start or daily_end not set — skipping.");
            return;
        }

        if (!TimeSpan.TryParse(dailyStartStr, out var startTs) ||
            !TimeSpan.TryParse(dailyEndStr,   out var endTs))
        {
            FleetLog.Warn($"[Fleet-Schedule] {hardwareId}: could not parse " +
                          $"daily_start='{dailyStartStr}' or daily_end='{dailyEndStr}' — skipping.");
            return;
        }

        var startMin = (int)startTs.TotalMinutes;
        var endMin   = (int)endTs.TotalMinutes;

        // Overnight schedules not supported
        if (startMin >= endMin)
        {
            FleetLog.Warn($"[Fleet-Schedule] {hardwareId}: startMin={startMin} >= endMin={endMin} " +
                          "(overnight schedules are not supported) — skipping.");
            return;
        }

        // Current time in device local timezone
        var nowMyt = FleetTime.UtcToLocal(DateTime.UtcNow, timezone);
        var nowMin = nowMyt.Hour * 60 + nowMyt.Minute;

        FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: nowMyt={nowMyt:HH:mm} nowMin={nowMin} window={startMin}–{endMin}");

        // Date range and day-of-week filters
        if (!IsActiveDateRange(alarmObj, nowMyt))
        {
            FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: outside effective date range — skipping.");
            return;
        }
        if (!IsActiveDay(alarmObj, nowMyt))
        {
            FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: not an active day of week — skipping.");
            return;
        }

        // ── INSIDE the daily window ───────────────────────────────────────────
        if (nowMin >= startMin && nowMin < endMin)
        {
            // Reuse the trip ID fetched during the same poll cycle (IngestOneDevice) to
            // avoid a redundant DB round-trip. Fall back to a fresh query when not provided
            // (e.g. when called from tests or when the poll produced a duplicate reading).
            var activeTripId = prefetchedTripId ?? await dbTrips.GetActiveTripForDevice(hardwareId);
            if (activeTripId == null)
            {
                var slaMinutes = tripObj["trip_max_minutes"]?.Value<int?>();
                var newTripId  = await dbTrips.CreateOpenTrip(hardwareId, DateTime.UtcNow.ToString("O"), slaMinutes);
                FleetLog.Info($"[Fleet-Schedule] {hardwareId}: auto-started trip {newTripId} at {nowMyt:HH:mm} MYT.");
                await TrySendTripStartNotificationAsync(hardwareId, alarmObj, tripObj, nowMyt, newTripId, dailyEndStr).ConfigureAwait(false);
            }
            else
            {
                FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: trip {activeTripId} already active — no new trip created.");
            }

            // Send a "trip ending soon" warning when 1–5 minutes remain
            var minsUntilEnd = endMin - nowMin;
            if (minsUntilEnd >= 1 && minsUntilEnd <= 5)
                await TrySendEndWarning(hardwareId, alarmObj, tripObj, nowMyt, dailyEndStr, dbAlarmState, timezone);
        }
        // ── PAST the daily end time — close any open trip ─────────────────────
        else if (nowMin >= endMin)
        {
            var activeTripId = prefetchedTripId ?? await dbTrips.GetActiveTripForDevice(hardwareId);
            if (activeTripId.HasValue)
            {
                var tempMinC   = alarmObj["temp_min_c"]?.Value<double?>();
                var tempMaxC   = alarmObj["temp_max_c"]?.Value<double?>();
                var closedTrip = await dbTrips.CloseTrip(activeTripId.Value, DateTime.UtcNow.ToString("O"), tempMinC, tempMaxC);
                FleetLog.Info($"[Fleet-Schedule] {hardwareId}: auto-closed trip {activeTripId.Value} at {nowMyt:HH:mm} MYT.");
                if (closedTrip != null)
                    await TrySendTripEndNotificationAsync(hardwareId, alarmObj, tripObj, nowMyt, closedTrip, timezone).ConfigureAwait(false);
            }
            else
            {
                FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: past window — no active trip to close.");
            }
        }
        else
        {
            FleetLog.Debug($"[Fleet-Schedule] {hardwareId}: before daily window ({nowMin} < {startMin}) — no action.");
        }
    }

    // ─── TrySendTripStartNotification (private) ───────────────────────────────

    /// <summary>
    /// Sends an email notification when a trip is automatically started.
    /// Called once per trip, immediately after CreateOpenTrip() succeeds.
    /// No-ops if all notification channels are disabled or have no address configured.
    /// </summary>
    internal static async Task TrySendTripStartNotificationAsync(
        string hardwareId, JObject alarmObj, JObject tripObj, DateTime nowMyt, long tripId, string scheduledEndStr)
    {
        var emailEnabled = alarmObj["email_enabled"]?.Value<bool>() ?? false;
        // trip_json notify_email takes priority (trip-specific address); alarm_json is the fallback
        var notifyEmail  = tripObj["notify_email"]?.ToString()?.Trim()
                        ?? alarmObj["notify_email"]?.ToString() ?? "";

        FleetLog.Info($"[Fleet-Trips] {hardwareId}: trip-start notify check — email_enabled={emailEnabled} email='{notifyEmail}'");

        if (!emailEnabled || string.IsNullOrWhiteSpace(notifyEmail))
        {
            FleetLog.Info($"[Fleet-Trips] {hardwareId}: trip-start notification skipped — email not enabled or no address.");
            return;
        }

        // Shipment / trip identity fields come from trip_json, not alarm_json
        var truckName      = tripObj["truck_name"]?.ToString()      ?? "";
        var shipmentId     = tripObj["shipment_id"]?.ToString()     ?? "";
        var startLocation  = tripObj["start_location"]?.ToString()  ?? "";
        var endLocation    = tripObj["end_location"]?.ToString()    ?? "";
        var tripMaxMinutes = tripObj["trip_max_minutes"]?.Value<int?>();

        FleetLog.Info($"[Fleet-Schedule] {hardwareId}: sending trip-start notification for trip {tripId}.");

        var summary = $"Trip started for device {hardwareId} at {nowMyt:HH:mm} MYT.";
        var html    = BuildTripStartHtml(hardwareId, nowMyt, scheduledEndStr,
                          truckName, shipmentId, startLocation, endLocation, tripMaxMinutes);
        await FleetEmailDispatch.SendAlarmToAll(
            notifyEmail, hardwareId, "trip_started", 0, "", summary, html).ConfigureAwait(false);
    }

    // ─── TrySendTripEndNotification (private) ─────────────────────────────────

    /// <summary>
    /// Sends an email notification when a trip is automatically closed.
    /// Includes a summary of trip duration, distance, and GPS point count.
    /// Called once per trip, immediately after CloseTrip() succeeds.
    /// No-ops if all notification channels are disabled or have no address configured.
    /// </summary>
    internal static async Task TrySendTripEndNotificationAsync(
        string hardwareId, JObject alarmObj, JObject tripObj, DateTime nowMyt, Dictionary<string, object?> closedTrip,
        string timezone = "Asia/Kuala_Lumpur")
    {
        var emailEnabled = alarmObj["email_enabled"]?.Value<bool>() ?? false;
        // trip_json notify_email takes priority (trip-specific address); alarm_json is the fallback
        var notifyEmail  = tripObj["notify_email"]?.ToString()?.Trim()
                        ?? alarmObj["notify_email"]?.ToString() ?? "";

        if (!emailEnabled || string.IsNullOrWhiteSpace(notifyEmail))
            return;

        // Shipment / trip identity fields come from trip_json, not alarm_json
        var truckName      = tripObj["truck_name"]?.ToString()      ?? "";
        var shipmentId     = tripObj["shipment_id"]?.ToString()     ?? "";
        var startLocation  = tripObj["start_location"]?.ToString()  ?? "";
        var endLocation    = tripObj["end_location"]?.ToString()    ?? "";
        var tripMaxMinutes = tripObj["trip_max_minutes"]?.Value<int?>();
        var dailyStart     = alarmObj["daily_start"]?.ToString()    ?? "";
        var dailyEnd       = alarmObj["daily_end"]?.ToString()      ?? "";

        // Extract summary fields from the closed-trip dictionary returned by CloseTrip()
        var tripId   = closedTrip.TryGetValue("trip_id",           out var tid) ? Convert.ToInt64(tid)    : 0L;
        var distKm   = closedTrip.TryGetValue("total_distance_km", out var d)   ? Convert.ToDouble(d)     : 0.0;
        var ptCount  = closedTrip.TryGetValue("points_count",      out var p)   ? Convert.ToInt32(p)      : 0;
        var startIso = closedTrip.TryGetValue("start_time",        out var s)   ? s?.ToString() ?? ""     : "";
        var endIso   = closedTrip.TryGetValue("end_time",          out var e)   ? e?.ToString() ?? ""     : "";

        // Calculate trip duration
        TimeSpan duration = TimeSpan.Zero;
        if (!string.IsNullOrEmpty(startIso) && !string.IsNullOrEmpty(endIso)
            && DateTime.TryParse(startIso, null, DateTimeStyles.RoundtripKind, out var startUtc)
            && DateTime.TryParse(endIso,   null, DateTimeStyles.RoundtripKind, out var endUtc))
        {
            duration = endUtc - startUtc;
        }

        var startMyt = string.IsNullOrEmpty(startIso) ? "-"
            : DateTime.TryParse(startIso, null, DateTimeStyles.RoundtripKind, out var su)
              ? FleetTime.UtcToLocal(su, timezone).ToString("HH:mm")
              : "-";

        var durationStr = duration == TimeSpan.Zero
            ? "-"
            : $"{(int)duration.TotalHours}h {duration.Minutes:D2}m";

        FleetLog.Info($"[Fleet-Schedule] {hardwareId}: sending trip-end notification for trip {tripId} " +
                      $"({durationStr}, {distKm:F2} km, {ptCount} pts).");

        var summary = $"Trip for device {hardwareId} ended at {nowMyt:HH:mm} MYT ({durationStr}, {distKm:F2} km).";
        var html    = BuildTripEndHtml(hardwareId, nowMyt, startMyt, durationStr, distKm, ptCount,
                          truckName, shipmentId, startLocation, endLocation, dailyStart, dailyEnd, tripMaxMinutes);
        await FleetEmailDispatch.SendAlarmToAll(
            notifyEmail, hardwareId, "trip_ended", 0, "", summary, html).ConfigureAwait(false);
    }

    // ─── TrySendEndWarning (private) ──────────────────────────────────────────

    /// <summary>
    /// Sends a "trip ending soon" warning approximately 5 minutes before the
    /// scheduled end time. Fires at most ONCE per MYT calendar day.
    ///
    /// The _sched_warn row in iot.tt19_alarm_state tracks whether the warning
    /// was already sent today (keyed on last_email_sent_at.Date in MYT).
    /// </summary>
    private static async Task TrySendEndWarning(
        string hardwareId, JObject alarmObj, JObject tripObj, DateTime nowMyt, string endTimeStr,
        FleetDbAlarmStateRepository dbAlarmState, string timezone = "Asia/Kuala_Lumpur")
    {
        var emailEnabled    = alarmObj["email_enabled"]?.Value<bool>()    ?? false;
        // trip_json notify_email takes priority (trip-specific address); alarm_json is the fallback
        var notifyEmail     = tripObj["notify_email"]?.ToString()?.Trim()
                           ?? alarmObj["notify_email"]?.ToString() ?? "";

        if (!emailEnabled || string.IsNullOrWhiteSpace(notifyEmail))
            return;

        // Check if warning already sent today (using _sched_warn alarm state row)
        var state       = await dbAlarmState.GetOrCreate(hardwareId, "_sched_warn");
        var warnedToday = state.LastEmailSentAt.HasValue
            && FleetTime.UtcToLocal(state.LastEmailSentAt.Value, timezone).Date == nowMyt.Date;

        if (warnedToday) return;

        // Shipment / trip identity fields come from trip_json, not alarm_json
        var warnTruckName     = tripObj["truck_name"]?.ToString()      ?? "";
        var warnShipmentId    = tripObj["shipment_id"]?.ToString()     ?? "";
        var warnStartLocation = tripObj["start_location"]?.ToString()  ?? "";
        var warnEndLocation   = tripObj["end_location"]?.ToString()    ?? "";

        var message = $"Trip for device {hardwareId} will end in ~5 minutes at {endTimeStr} (MYT).";
        FleetLog.Info($"[Fleet-Schedule] {hardwareId}: sending 5-minute end warning.");

        var warnHtml = BuildWarningHtml(hardwareId, nowMyt, endTimeStr,
                           warnTruckName, warnShipmentId, warnStartLocation, warnEndLocation);
        await FleetEmailDispatch.SendAlarmToAll(
            notifyEmail, hardwareId, "schedule_end_warning", 0, "", message, warnHtml).ConfigureAwait(false);

        // Mark warning as sent so we don't repeat it today
        state.LastEmailSentAt = DateTime.UtcNow;
        await dbAlarmState.Save(state);
    }

    // ─── IsActiveDateRange (private) ──────────────────────────────────────────

    /// <summary>
    /// Returns false if today's MYT date is outside the effective_start / effective_end
    /// date range configured in alarm_json. Returns true if dates are not configured.
    /// </summary>
    private static bool IsActiveDateRange(JObject alarmObj, DateTime nowMyt)
    {
        var effectiveStart = alarmObj["effective_start"]?.ToString();
        var effectiveEnd   = alarmObj["effective_end"]?.ToString();

        if (!string.IsNullOrWhiteSpace(effectiveStart)
            && DateTime.TryParse(effectiveStart, CultureInfo.InvariantCulture,
                                 DateTimeStyles.None, out var startDt)
            && nowMyt.Date < startDt.Date)
            return false;

        if (!string.IsNullOrWhiteSpace(effectiveEnd)
            && DateTime.TryParse(effectiveEnd, CultureInfo.InvariantCulture,
                                 DateTimeStyles.None, out var endDt)
            && nowMyt.Date > endDt.Date)
            return false;

        return true;
    }

    // ─── IsActiveDay (private) ────────────────────────────────────────────────

    /// <summary>
    /// Returns false if repeat_days is configured and today's MYT day-of-week
    /// is not in the list. Returns true if repeat_days is empty or absent
    /// (meaning "active every day").
    ///
    /// Day abbreviations: "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"
    /// </summary>
    private static bool IsActiveDay(JObject alarmObj, DateTime nowMyt)
    {
        var repeatDays = alarmObj["repeat_days"];
        if (repeatDays?.Type != JTokenType.Array) return true;

        var days = ((JArray)repeatDays).Select(d => d.ToString()).ToList();
        if (days.Count == 0) return true;

        var todayAbbr = nowMyt.DayOfWeek switch
        {
            DayOfWeek.Monday    => "Mon",
            DayOfWeek.Tuesday   => "Tue",
            DayOfWeek.Wednesday => "Wed",
            DayOfWeek.Thursday  => "Thu",
            DayOfWeek.Friday    => "Fri",
            DayOfWeek.Saturday  => "Sat",
            DayOfWeek.Sunday    => "Sun",
            _                   => ""
        };

        return days.Contains(todayAbbr, StringComparer.OrdinalIgnoreCase);
    }

    // ─── Email HTML builders (private) ────────────────────────────────────────

    private static string BuildWarningHtml(
        string hardwareId, DateTime nowMyt, string endTimeStr,
        string truckName, string shipmentId, string startLocation, string endLocation)
    {
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.InfoTable([
                ("Truck", truckName), ("Shipment ID", shipmentId),
                ("From", startLocation), ("To", endLocation),
            ])
            + FleetEmailTemplate.AlertParagraph(
                $"This trip will end automatically in approximately 5 minutes at {endTimeStr} (MYT).", "#f57c00")
            + FleetEmailTemplate.FooterNote("The device will stop recording when the scheduled end time is reached. No action is required.");
        return FleetEmailTemplate.HtmlPage("⏰ Fleet Trip Ending Soon", "#f57c00", body);
    }

    private static string BuildTripStartHtml(
        string hardwareId, DateTime nowMyt, string scheduledEndStr,
        string truckName, string shipmentId, string startLocation, string endLocation,
        int? tripMaxMinutes)
    {
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.InfoTable([
                ("Date",                  $"{nowMyt:dd MMM yyyy}"),
                ("Start time (MYT)",      $"{nowMyt:HH:mm}"),
                ("Scheduled end (MYT)",   scheduledEndStr),
                ("Est. duration",         tripMaxMinutes.HasValue ? $"{tripMaxMinutes.Value} min" : null),
                ("Truck",                 truckName),
                ("Shipment ID",           shipmentId),
                ("From",                  startLocation),
                ("To",                    endLocation),
            ])
            + FleetEmailTemplate.FooterNote("GPS recording and temperature monitoring are now active.");
        return FleetEmailTemplate.HtmlPage("🚛 Fleet Trip Started", "#1976d2", body);
    }

    private static string BuildTripEndHtml(
        string hardwareId, DateTime nowMyt,
        string startMyt, string durationStr, double distKm, int ptCount,
        string truckName, string shipmentId, string startLocation, string endLocation,
        string dailyStart, string dailyEnd, int? tripMaxMinutes)
    {
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.InfoTable([
                ("Date",                $"{nowMyt:dd MMM yyyy}"),
                ("Truck",               truckName),
                ("Shipment ID",         shipmentId),
                ("From",                startLocation),
                ("To",                  endLocation),
                ("Schedule",            !string.IsNullOrWhiteSpace(dailyStart) && !string.IsNullOrWhiteSpace(dailyEnd)
                                            ? $"{dailyStart} – {dailyEnd} (MYT)" : null),
                ("Start time (MYT)",    startMyt),
                ("End time (MYT)",      $"{nowMyt:HH:mm}"),
                ("Duration",            durationStr),
                ("Est. duration",       tripMaxMinutes.HasValue ? $"{tripMaxMinutes.Value} min" : null),
                ("Distance",            $"{distKm:F2} km"),
                ("GPS points recorded", $"{ptCount}"),
            ])
            + FleetEmailTemplate.FooterNote("Trip recording has been automatically closed by the schedule.");
        return FleetEmailTemplate.HtmlPage("🏁 Fleet Trip Ended", "#388e3c", body);
    }
}
