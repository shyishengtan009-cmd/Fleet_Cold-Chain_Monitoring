using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FleetCore.Fleet;
using FleetCore.Fleet.Notifications;
using FleetCore.Repositories;
using FleetCore.Models.Fleet;
using Newtonsoft.Json.Linq;

namespace FleetCore.Fleet.Alarm;

/// <summary>
/// Checks a fresh sensor reading against the device's configured alarm thresholds
/// and sends ONE combined email notification per alarm event.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   CheckAndNotify   — main entry point; evaluates all sensor thresholds
///   CheckBatteryLevel — separate one-shot battery low alert (30%, 20%, 10%)
///
/// ── Debounce explained ────────────────────────────────────────────────────────
/// A sensor must exceed its threshold for N consecutive readings before an alarm
/// is fired. "N" is the debounce_count field in the device's alarm_json settings.
/// Default is 3 readings. This prevents a single noisy reading from triggering
/// a false alarm notification.
///
/// ── Device-level cooldown explained ──────────────────────────────────────────
/// Even if many sensors are simultaneously alarming, only ONE combined notification
/// is sent per device per cooldown window. The window length is email_cooldown_minutes
/// (default 30 minutes). This prevents alert fatigue / notification spam.
///
/// The cooldown is tracked using the special sensor key "_device" in iot.tt19_alarm_state.
/// The last_email_sent_at column holds the time of the last sent notification.
///
/// ── One combined message design ───────────────────────────────────────────────
/// Rather than sending separate emails per alarming field, all alarming fields are
/// bundled into one HTML email table that shows every sensor reading with status.
/// This gives the operator a complete picture in a single notification:
///   - ALARM fields shown in red with ⚠ status
///   - Normal fields shown in blue with ✓ OPTIMAL status
///
/// ── Schedule filtering ────────────────────────────────────────────────────────
/// Alarms only fire if the current MYT (Malaysia Time, UTC+8) time is within
/// the device's configured schedule window (daily_start, daily_end, repeat_days,
/// effective_start, effective_end). Outside the window, CheckAndNotify returns early.
///
/// ── Trip context ─────────────────────────────────────────────────────────────
/// When an alarm fires during an active trip, the notification includes a "Trip Context"
/// section: trip ID, elapsed time, distance covered, and a Google Maps link to the
/// location where the breach occurred. The trip context is loaded from the DB only
/// when a notification is confirmed to be sent (not on every poll cycle).
///
/// ── Related files ─────────────────────────────────────────────────────────────
/// - Fleet/Database/FleetDbAlarmStateRepository.cs  — per-sensor debounce + cooldown state
/// - Fleet/Database/FleetDbAlarmLogRepository.cs    — permanent record of fired alarms
/// - Fleet/Database/FleetDbEmailLogRepository.cs    — record of email send attempts
/// - Fleet/Database/FleetDbSettingsRepository.cs   — reads alarm_json for this device
/// - Fleet/Notifications/FleetEmailService.cs   — SMTP email delivery
/// </summary>
public static class FleetAlarmChecker
{
    // Default timezone kept for battery check path; alarm check receives per-device tz as parameter

    // ─── Field descriptors ────────────────────────────────────────────────────

    /// <summary>
    /// Describes one sensor field: its name, unit, current value, and configured thresholds.
    /// Used internally to build the alarm evaluation loop.
    /// </summary>
    private sealed record FieldSpec(
        string  Name,
        string  Unit,
        double? Value,
        double? Min,
        double? Max,
        int     Decimals);

    /// <summary>
    /// The result of evaluating one FieldSpec against its thresholds.
    /// AlarmType is either "ALARM" (threshold crossed) or "WARN" (pre-alarm warning zone).
    /// </summary>
    private sealed record FieldResult(
        FieldSpec Spec,
        string    AlarmType,
        double?   Threshold,
        string    Description);

    /// <summary>
    /// Snapshot of the active trip at the moment an alarm notification fires.
    /// Only fetched when a notification is about to be sent — not on every poll cycle.
    /// Including GPS coordinates so the notification can provide a direct map link.
    /// </summary>
    private sealed record TripContext(
        long    TripId,
        string  ElapsedStr,
        double  DistKm,
        double? Lat,
        double? Lng);

    // ─── EvaluateField (private) ──────────────────────────────────────────────

    /// <summary>
    /// Evaluates one sensor field against its min/max thresholds.
    /// Returns a FieldResult if the value is alarming or in the pre-alarm warning zone.
    /// Returns null if the value is within normal range.
    ///
    /// Pre-alarm logic: warns when value is within the top 20% of the max range
    /// (i.e. within 20% of (max - min) below the max threshold).
    /// </summary>
    private static FieldResult? EvaluateField(FieldSpec f, bool preAlarm)
    {
        if (f.Value == null) return null;
        var v = f.Value.Value;

        if (f.Max.HasValue && v >= f.Max.Value)
            return new FieldResult(f, "ALARM", f.Max,
                $"{f.Name} ALARM: {v.ToString($"F{f.Decimals}")} {f.Unit} exceeded max {f.Max.Value.ToString($"F{f.Decimals}")} {f.Unit}");

        if (f.Min.HasValue && v <= f.Min.Value)
            return new FieldResult(f, "ALARM", f.Min,
                $"{f.Name} ALARM: {v.ToString($"F{f.Decimals}")} {f.Unit} below min {f.Min.Value.ToString($"F{f.Decimals}")} {f.Unit}");

        if (preAlarm)
        {
            if (f.Max.HasValue)
            {
                var range  = f.Max.Value - (f.Min ?? 0.0);
                var warnAt = f.Max.Value - range * 0.2;
                if (v >= warnAt)
                    return new FieldResult(f, "WARN", f.Max,
                        $"{f.Name} WARNING: {v.ToString($"F{f.Decimals}")} {f.Unit} approaching max {f.Max.Value.ToString($"F{f.Decimals}")} {f.Unit}");
            }

            if (f.Min.HasValue)
            {
                // Use Max when available; fall back to min+10 so warnAt is always above min.
                // (min*2 was wrong for min=0 or negative min — gave zero/inverted range.)
                var range  = (f.Max ?? f.Min.Value + 10.0) - f.Min.Value;
                var warnAt = f.Min.Value + range * 0.2;
                if (v <= warnAt)
                    return new FieldResult(f, "WARN", f.Min,
                        $"{f.Name} WARNING: {v.ToString($"F{f.Decimals}")} {f.Unit} approaching min {f.Min.Value.ToString($"F{f.Decimals}")} {f.Unit}");
            }
        }

        return null;
    }

    // ─── GetOrCreateState (private) ──────────────────────────────────────────

    /// <summary>
    /// Returns the in-memory alarm state for a sensor key, creating a default row
    /// if not already loaded. The returned row is owned by the <paramref name="states"/>
    /// dict so callers can mutate it — SaveAll persists the whole dict at cycle end.
    /// </summary>
    private static FleetAlarmStateRow GetOrCreateState(
        Dictionary<string, FleetAlarmStateRow> states, string hardwareId, string sensor)
    {
        if (states.TryGetValue(sensor, out var existing)) return existing;
        var row = new FleetAlarmStateRow { HardwareId = hardwareId, Sensor = sensor };
        states[sensor] = row;
        return row;
    }

    // ─── CheckAndNotify ───────────────────────────────────────────────────────

    /// <summary>
    /// Main alarm evaluation entry point. Called by FleetFetchRealtime after every
    /// new sensor reading is inserted into the database.
    ///
    /// Steps:
    ///   1. Load alarm settings from iot.device_settings
    ///   2. Check schedule window — return early if outside schedule
    ///   3. Evaluate all sensor fields against thresholds
    ///   4. Update per-field debounce counters in iot.tt19_alarm_state
    ///   5. Check device-level cooldown
    ///   6. Send combined email notification
    ///   7. Write alarm log rows (one per triggered field)
    /// </summary>
    public static async Task CheckAndNotify(
        string            hardwareId,
        DateTime          ts,
        double?           temperatureC,
        double?           humidityPct,
        double?           lightLux,
        double?           batteryPct,
        FleetDbSettingsRepository   dbSettings,
        FleetDbAlarmStateRepository dbAlarmState,
        double?           vibrationG   = null,
        double?           lat          = null,
        double?           lng          = null,
        FleetDbAlarmLogRepository?  alarmLog     = null,
        FleetDbEmailLogRepository?  dbEmailLog   = null,
        FleetDbTripsRepository?     dbTrips      = null,
        FleetDbDwellRepository?     dbDwell      = null,
        FleetDbLocationsRepository? dbLocations  = null,
        string            timezone     = "Asia/Kuala_Lumpur",
        long?             activeTripId = null,
        string?           deviceLabel  = null)
    {
        // Step 1: load alarm settings
        var settings = await dbSettings.GetDeviceSettings(hardwareId);
        if (settings == null) return;

        var alarmObj = settings["alarm_json"] as JObject;
        if (alarmObj == null || alarmObj.Count == 0) return;

        var tripObj = (settings["trip_json"] as JObject) ?? new JObject();

        // Step 2: schedule check — only fire alarms during configured hours/days
        if (!IsWithinSchedule(alarmObj, timezone)) return;

        // Batch-load all alarm state rows for this device in one query.
        // Per-sensor state is looked up in-memory; SaveAll writes everything at the end.
        var states = await dbAlarmState.GetAllForDevice(hardwareId);

        var preAlarm             = alarmObj["pre_alarm"]?.Value<bool>() ?? false;
        var notifyEmail          = alarmObj["notify_email"]?.ToString() ?? "";
        var emailEnabled         = alarmObj["email_enabled"]?.Value<bool>() ?? false;
        var debounceCount        = alarmObj["debounce_count"]?.Value<int>() ?? 3;
        var emailCooldownMinutes = alarmObj["email_cooldown_minutes"]?.Value<int>() ?? 240; // 4 h default ≈ 42 emails/week (2% of Brevo free tier)

        var snapshot = new SensorSnapshot(
            alarmObj, tripObj, deviceLabel,
            temperatureC, humidityPct, lightLux, batteryPct, vibrationG, preAlarm);

        var fieldSpecs = new[]
        {
            new FieldSpec("temperature", "°C",  temperatureC, snapshot.TempMin,  snapshot.TempMax,  1),
            new FieldSpec("humidity",    "%",   humidityPct,  snapshot.HumMin,   snapshot.HumMax,   1),
            new FieldSpec("light",       "lux", lightLux,     snapshot.LightMin, snapshot.LightMax, 0),
            new FieldSpec("battery",     "%",   batteryPct,   snapshot.BatMin,   null,              0),
            new FieldSpec("vibration",   "g",   vibrationG,   null,              snapshot.VibMax,   2),
        };

        // Step 3+4: evaluate all fields and update per-field debounce counters
        var validSensors  = new HashSet<string> { "temperature", "humidity", "light", "battery", "vibration" };
        var readyToNotify = new List<FieldResult>();

        foreach (var spec in fieldSpecs)
        {
            if (string.IsNullOrWhiteSpace(spec.Name) || !validSensors.Contains(spec.Name))
            {
                FleetLog.Warn($"[Fleet-Alarm] Skipped invalid sensor name: '{spec.Name}'");
                continue;
            }

            var result = EvaluateField(spec, preAlarm);
            FleetLog.Debug($"[Fleet-Alarm] {hardwareId}/{spec.Name}: value={spec.Value:F1}, alarm={result?.AlarmType ?? "none"}");

            var state = GetOrCreateState(states, hardwareId, spec.Name);

            if (result != null)
            {
                state.ConsecutiveCount++;
                state.LastAlarmedAt = DateTime.UtcNow;
                if (!state.IsAlarming)
                {
                    state.IsAlarming     = true;
                    state.AlarmStartedAt = DateTime.UtcNow;
                }

                FleetLog.Debug($"[Fleet-Alarm] {hardwareId}/{spec.Name}: consecutive={state.ConsecutiveCount}/{debounceCount}");

                if (state.ConsecutiveCount >= debounceCount)
                    readyToNotify.Add(result);
            }
            else
            {
                state.ConsecutiveCount = 0;
                state.IsAlarming       = false;
            }
            // State is mutated in-place in the dict; saved in batch at cycle end.
        }

        // Step 4b+4c: pre-fetch locations once — used by both dwell and geofence checks
        // so we avoid two cache lookups (GetAllForDwellCheck has a 60s MemoryCache).
        List<FleetLocationRow>? allLocations = null;
        if (lat.HasValue && lng.HasValue && dbLocations != null)
        {
            try { allLocations = await dbLocations.GetAllForDwellCheck(); }
            catch (Exception ex) { FleetLog.Warn($"[Fleet-Alarm] {hardwareId}: location prefetch error — {ex.Message}"); }
        }

        // Step 4b: dwell check runs independently — a truck parked too long with normal
        // sensor readings must still fire a dwell alert even when readyToNotify is empty.
        if (lat.HasValue && lng.HasValue && dbDwell != null)
        {
            try
            {
                await FleetDwellChecker.CheckDwell(
                    hardwareId, ts, lat.Value, lng.Value,
                    alarmObj, tripObj, dbDwell, dbLocations, alarmLog, dbEmailLog, timezone, allLocations,
                    deviceLabel);
            }
            catch (Exception ex)
            {
                FleetLog.Warn($"[Fleet-Alarm] {hardwareId}: dwell check error — {ex.Message}");
            }
        }

        // Step 4c: geofence check — fire ALARM when device enters a 'forbidden' zone.
        // Independent of sensor alarms; fires even when readyToNotify is empty.
        // Pass the already-loaded states dict so CheckGeofence can reuse it — avoids
        // a second GetAllForDevice query and lets SaveAll at the end of this method
        // persist geofence state mutations alongside sensor state mutations in one batch.
        if (lat.HasValue && lng.HasValue && dbLocations != null)
        {
            try
            {
                await FleetDwellChecker.CheckGeofence(
                    hardwareId, ts, lat.Value, lng.Value,
                    alarmObj, tripObj, dbLocations, dbAlarmState, alarmLog, dbEmailLog, timezone, allLocations,
                    sharedStates: states);
            }
            catch (Exception ex)
            {
                FleetLog.Warn($"[Fleet-Alarm] {hardwareId}: geofence check error — {ex.Message}");
            }
        }

        // Step 4d: cargo door open detection — fire when light spikes above threshold during an active trip.
        // Independent of other sensor alarms. Only fires if a trip is currently in progress.
        if (snapshot.DoorOpenLuxThreshold.HasValue && lightLux.HasValue && dbTrips != null)
        {
            try
            {
                await CheckDoorOpen(
                    hardwareId, ts, lightLux.Value,
                    snapshot.DoorOpenLuxThreshold.Value, debounceCount,
                    emailEnabled, notifyEmail,
                    alarmObj, snapshot, states, dbTrips, alarmLog, dbEmailLog, timezone, activeTripId);
            }
            catch (Exception ex)
            {
                FleetLog.Warn($"[Fleet-Alarm] {hardwareId}: door open check error — {ex.Message}");
            }
        }

        if (readyToNotify.Count == 0)
        {
            await dbAlarmState.SaveAll(states.Values);
            return;
        }

        // Step 5: device-level cooldown check — suppress if within cooldown window
        var deviceState     = GetOrCreateState(states, hardwareId, "_device");
        var cooldownExpired = deviceState.LastEmailSentAt == null
            || (DateTime.UtcNow - deviceState.LastEmailSentAt.Value).TotalMinutes >= emailCooldownMinutes;

        if (!cooldownExpired)
        {
            FleetLog.Info($"[Fleet-Alarm] {hardwareId}: notification suppressed — device cooldown not yet expired");
            await dbAlarmState.SaveAll(states.Values);
            return;
        }

        // Advance cooldown immediately in-memory — rate-limits even if email disabled or send fails.
        deviceState.LastEmailSentAt = DateTime.UtcNow;

        var nowMyt         = FleetTime.UtcToLocal(DateTime.UtcNow, timezone);
        var overallType    = readyToNotify.Any(r => r.AlarmType == "ALARM") ? "ALARM" : "WARN";
        var combinedFields = string.Join(", ", readyToNotify.Select(r => r.Spec.Name));
        var combinedDesc   = string.Join("; ", readyToNotify.Select(r => r.Description));

        // Fetch trip context (without loading GPS blob) — only when notification will fire
        var tripCtx = await TryGetActiveTripContext(hardwareId, lat, lng, dbTrips);

        // Step 6a: write alarm log first so we have IDs to link from email log
        var alarmLogIds = new Dictionary<string, long>();
        if (alarmLog != null)
        {
            foreach (var r in readyToNotify)
            {
                var alarmId = await alarmLog.Insert(new FleetAlarmLogEntry
                {
                    HardwareId = hardwareId,
                    Ts         = ts,
                    AlarmType  = r.AlarmType,
                    Field      = r.Spec.Name,
                    Value      = r.Spec.Value,
                    Threshold  = r.Threshold,
                    Message    = r.Description
                });
                if (alarmId > 0) alarmLogIds[r.Spec.Name] = alarmId;
            }
        }

        // Step 6b: send ONE combined email, then log the attempt with alarm_log_id correlation
        bool emailSent = false;
        if (emailEnabled && !string.IsNullOrWhiteSpace(notifyEmail))
        {
            var htmlBody = BuildHtmlBody(hardwareId, nowMyt, overallType, readyToNotify, snapshot, tripCtx, timezone);
            emailSent = await FleetEmailDispatch.SendAlarmToAll(
                notifyEmail, hardwareId, combinedFields, 0, "", combinedDesc, htmlBody).ConfigureAwait(false);

            if (emailSent)
                FleetLog.Info($"[Fleet-Alarm] {hardwareId}: combined email sent to {notifyEmail} for [{combinedFields}]");

            if (dbEmailLog != null)
                foreach (var r in readyToNotify)
                    await dbEmailLog.Insert(new FleetEmailLogEntry
                    {
                        HardwareId  = hardwareId,
                        Sensor      = r.Spec.Name,
                        ToEmail     = notifyEmail,
                        Description = r.Description,
                        Success     = emailSent,
                        AlarmLogId  = alarmLogIds.TryGetValue(r.Spec.Name, out var aid) ? aid : null
                    });
        }
        else if (!emailEnabled)
        {
            FleetLog.Info($"[Fleet-Alarm] {hardwareId}: email disabled in device settings");
        }
        else
        {
            FleetLog.Info($"[Fleet-Alarm] {hardwareId}: no email address configured");
        }

        _ = emailSent; // cooldown already saved above; variable retained for log clarity

        // Push one combined alarm event to any connected browser tabs
        _ = FleetAlarmPusher.Push(hardwareId, new
        {
            id           = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            hardware_id  = hardwareId,
            ts           = ts.ToString("O"),
            alarm_type   = overallType,
            field        = combinedFields,
            value        = (double?)null,
            threshold    = (double?)null,
            message      = combinedDesc,
            created_at   = DateTime.UtcNow.ToString("O")
        }).ContinueWith(
            t => FleetLog.Warn($"[Fleet-Alarm] {hardwareId}: SignalR push failed — {t.Exception?.GetBaseException().Message}"),
            TaskContinuationOptions.OnlyOnFaulted);

        // Persist all mutated alarm state rows in a single batch write.
        await dbAlarmState.SaveAll(states.Values);
    }

    // ─── TryGetActiveTripContext (private) ────────────────────────────────────

    /// <summary>
    /// Fetches a lightweight summary of the active trip (if any) to enrich alarm notifications.
    /// Returns null if no trip is in progress or if the DB lookup fails.
    ///
    /// Uses FleetDbTripsRepository.GetActiveTripSummary() which does NOT load the GPS blob,
    /// keeping the alarm path fast even on devices with long trip histories.
    /// </summary>
    private static async Task<TripContext?> TryGetActiveTripContext(string hardwareId, double? lat, double? lng, FleetDbTripsRepository? dbTrips)
    {
        if (dbTrips == null) return null;
        try
        {
            var summary = await dbTrips.GetActiveTripSummary(hardwareId);
            if (summary == null) return null;

            var (tripId, startIso, distKm) = summary.Value;

            string elapsedStr = "-";
            if (!string.IsNullOrEmpty(startIso)
                && DateTime.TryParse(startIso, null, DateTimeStyles.RoundtripKind, out var startUtc))
            {
                var elapsed = DateTime.UtcNow - startUtc;
                elapsedStr  = $"{(int)elapsed.TotalHours}h {elapsed.Minutes:D2}m";
            }

            return new TripContext(tripId, elapsedStr, distKm, lat, lng);
        }
        catch (Exception ex)
        {
            FleetLog.Warn($"[Fleet-Alarm] {hardwareId}: could not fetch trip context — {ex.Message}");
            return null;
        }
    }

    // ─── SensorSnapshot ───────────────────────────────────────────────────────

    /// <summary>
    /// Holds the current sensor values and their configured thresholds.
    /// Created once per CheckAndNotify call and passed to all message builders.
    /// Extracted from alarm_json (the JSONB settings column in iot.device_settings).
    /// </summary>
    private sealed class SensorSnapshot
    {
        public double? TempC    { get; }
        public double? HumPct   { get; }
        public double? LightLux { get; }
        public double? BatPct   { get; }
        public double? VibG     { get; }

        public double? TempMin  { get; }
        public double? TempMax  { get; }
        public double? HumMin   { get; }
        public double? HumMax   { get; }
        public double? LightMin { get; }
        public double? LightMax { get; }
        public double? BatMin   { get; }
        public double? VibMax   { get; }

        public bool PreAlarm { get; }

        // Cargo door detection — separate lux spike threshold (alarms only during an active trip)
        public double? DoorOpenLuxThreshold { get; }

        // Shipment / trip identity fields (from Device Settings → Trip tab → trip_json)
        public string TruckName     { get; }
        public string ShipmentId    { get; }
        public string StartLocation { get; }
        public string EndLocation   { get; }
        public int?   TripMaxMinutes { get; }

        // Schedule fields (from alarm_json)
        public string DailyStart    { get; }
        public string DailyEnd      { get; }

        public SensorSnapshot(
            JObject alarm,
            JObject trip,
            string? deviceLabel,
            double? tempC, double? humPct, double? lightLux, double? batPct, double? vibG,
            bool preAlarm)
        {
            TempC    = tempC;
            HumPct   = humPct;
            LightLux = lightLux;
            BatPct   = batPct;
            VibG     = vibG;
            PreAlarm = preAlarm;

            TempMin  = alarm["temp_min_c"]?.ToObject<double?>();
            TempMax  = alarm["temp_max_c"]?.ToObject<double?>();
            HumMin   = alarm["humidity_min_pct"]?.ToObject<double?>();
            HumMax   = alarm["humidity_max_pct"]?.ToObject<double?>();
            LightMin = alarm["light_min_lux"]?.ToObject<double?>();
            LightMax = alarm["light_max_lux"]?.ToObject<double?>();
            BatMin   = alarm["battery_min_pct"]?.ToObject<double?>();
            VibMax   = alarm["vibration_g"]?.ToObject<double?>();

            DoorOpenLuxThreshold = alarm["door_open_lux_threshold"]?.ToObject<double?>();

            // Trip fields come from trip_json, not alarm_json
            TruckName      = trip["truck_name"]?.ToString()      ?? deviceLabel ?? "";
            ShipmentId     = trip["shipment_id"]?.ToString()     ?? "";
            StartLocation  = trip["start_location"]?.ToString()  ?? "";
            EndLocation    = trip["end_location"]?.ToString()    ?? "";
            TripMaxMinutes = trip["trip_max_minutes"]?.Value<int?>();

            DailyStart = alarm["daily_start"]?.ToString() ?? "";
            DailyEnd   = alarm["daily_end"]?.ToString()   ?? "";
        }
    }

    // ─── GetStatus (private) ──────────────────────────────────────────────────

    /// <summary>
    /// Returns a status string and display color for one sensor reading.
    /// Used by the HTML email table.
    ///
    /// Return values:
    ///   "⚠ ALARM (HIGH)" / red   — value at or above max threshold
    ///   "⚠ ALARM (LOW)"  / blue  — value at or below min threshold
    ///   "⚠ WARNING"      / amber — value in pre-alarm zone (top 20% of range)
    ///   "✓ OPTIMAL"       / blue  — value within normal range
    ///   "—"              / grey  — value is null (no data)
    /// </summary>
    private static (string status, string color) GetStatus(
        double? value, double? min, double? max, bool preAlarm)
    {
        if (value == null) return ("—", "#888888");
        var v = value.Value;
        if (max.HasValue && v >= max.Value) return ("⚠ ALARM (HIGH)", "#d32f2f");
        if (min.HasValue && v <= min.Value) return ("⚠ ALARM (LOW)",  "#1565c0");
        if (preAlarm)
        {
            if (max.HasValue)
            {
                var range  = max.Value - (min ?? 0.0);
                var warnAt = max.Value - range * 0.2;
                if (v >= warnAt) return ("⚠ WARNING (HIGH)", "#f57c00");
            }
            if (min.HasValue)
            {
                var range  = (max ?? min.Value + 10.0) - min.Value;
                var warnAt = min.Value + range * 0.2;
                if (v <= warnAt) return ("⚠ WARNING (LOW)", "#f57c00");
            }
        }
        return ("✓ OPTIMAL", "#1976d2");
    }

    // ─── BuildHtmlBody (private) ──────────────────────────────────────────────

    private static string BuildHtmlBody(
        string                    hardwareId,
        DateTime                  nowMyt,
        string                    overallType,
        IReadOnlyList<FieldResult> triggered,
        SensorSnapshot             s,
        TripContext?               tripCtx  = null,
        string                     timezone = "Asia/Kuala_Lumpur")
    {
        var titleColor     = overallType == "WARN" ? "#f57c00" : "#d32f2f";
        var triggeredNames = string.Join(", ",
            triggered.Select(r => char.ToUpper(r.Spec.Name[0]) + r.Spec.Name.Substring(1)));

        // Derive a human-readable timezone label from the IANA ID: "Asia/Kuala_Lumpur" → "Kuala Lumpur"
        var tzLabel = timezone.Contains('/') ? timezone.Split('/').Last().Replace('_', ' ') : timezone;

        var shipmentTable = FleetEmailTemplate.InfoTable([
            ("Truck",        s.TruckName),
            ("Shipment ID",  s.ShipmentId),
            ("From",         s.StartLocation),
            ("To",           s.EndLocation),
            ("Schedule",     !string.IsNullOrWhiteSpace(s.DailyStart) && !string.IsNullOrWhiteSpace(s.DailyEnd)
                                 ? $"{s.DailyStart} – {s.DailyEnd} ({tzLabel})" : null),
            ("Est. duration", s.TripMaxMinutes.HasValue ? $"{s.TripMaxMinutes.Value} min" : null),
        ]);

        var sb = new StringBuilder();
        sb.Append(FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt));
        sb.Append(FleetEmailTemplate.AlertParagraph(
            $"Exceeded: {string.Join(" &nbsp;|&nbsp; ", triggered.Select(r => r.Description))}", titleColor));
        sb.Append(shipmentTable);
        sb.Append(FleetEmailTemplate.SensorTableOpen());

        void AddRow(string label, double? val, string unit, double? min, double? max, int dec = 1)
        {
            var (status, color) = GetStatus(val, min, max, s.PreAlarm);
            var valStr = val != null ? $"{val.Value.ToString($"F{dec}")} {unit}" : "—";
            var minStr = min != null ? $"{min.Value.ToString($"F{dec}")} {unit}" : "—";
            var maxStr = max != null ? $"{max.Value.ToString($"F{dec}")} {unit}" : "—";
            sb.Append(FleetEmailTemplate.SensorTableRow(label, valStr, minStr, maxStr, status, color));
        }

        AddRow("Temperature", s.TempC,    "°C",  s.TempMin,  s.TempMax,  1);
        AddRow("Humidity",    s.HumPct,   "%",   s.HumMin,   s.HumMax,   1);
        AddRow("Light",       s.LightLux, "lux", s.LightMin, s.LightMax, 0);
        AddRow("Battery",     s.BatPct,   "%",   s.BatMin,   null,       0);
        AddRow("Vibration",   s.VibG,     "g",   null,       s.VibMax,   2);
        sb.Append(FleetEmailTemplate.SensorTableClose());

        if (tripCtx != null)
        {
            var mapsLink = tripCtx.Lat.HasValue && tripCtx.Lng.HasValue
                ? $"<a href=\"https://www.google.com/maps?q={tripCtx.Lat.Value.ToString("F6", CultureInfo.InvariantCulture)},{tripCtx.Lng.Value.ToString("F6", CultureInfo.InvariantCulture)}\" style=\"color:#1976d2\">View on Google Maps</a>"
                : "GPS not available";
            sb.Append(FleetEmailTemplate.SectionHeader("🚛 Active Trip Context"));
            sb.Append(FleetEmailTemplate.InfoTable([
                ("Trip ID",           tripCtx.TripId.ToString()),
                ("Elapsed",           tripCtx.ElapsedStr),
                ("Distance covered",  $"{tripCtx.DistKm:F2} km"),
                ("Location at breach", mapsLink),
            ]));
        }

        return FleetEmailTemplate.HtmlPage($"⚠ Fleet {overallType} — {triggeredNames}", titleColor, sb.ToString());
    }

    // ─── CheckDoorOpen (private) ──────────────────────────────────────────────

    /// <summary>
    /// Fires a DOOR_OPEN alarm when the light sensor spikes above the configured
    /// door_open_lux_threshold AND the device has an active open trip.
    ///
    /// "Door open" logic: a sharp lux spike during a cold-truck trip likely means
    /// the cargo door was opened. This alerts managers to unauthorised access or
    /// cold-chain breaks caused by door events.
    ///
    /// Uses the same per-field debounce state (key "door_open") as sensor alarms so
    /// the alert doesn't fire on every poll cycle — it must exceed the threshold for
    /// debounce_count consecutive readings before the alarm fires.
    ///
    /// Fires independently of the main sensor alarm cooldown — it uses its own
    /// device-level state key "_device_door" so that a door event is never suppressed
    /// by an unrelated sensor alarm that already consumed the main cooldown.
    /// </summary>
    private static async Task CheckDoorOpen(
        string            hardwareId,
        DateTime          ts,
        double            lightLux,
        double            threshold,
        int               debounceCount,
        bool              emailEnabled,
        string            notifyEmail,
        JObject           alarmObj,
        SensorSnapshot    snapshot,
        Dictionary<string, FleetAlarmStateRow> states,
        FleetDbTripsRepository      dbTrips,
        FleetDbAlarmLogRepository?  alarmLog,
        FleetDbEmailLogRepository?  dbEmailLog,
        string            timezone,
        long?             prefetchedTripId = null)
    {
        // Only fire during an active trip — use pre-fetched ID to avoid a second DB call
        var activeTripId = prefetchedTripId ?? await dbTrips.GetActiveTripForDevice(hardwareId);
        if (activeTripId == null) return;

        // State looked up from the pre-loaded dict — no DB read; SaveAll persists at cycle end.
        var state = GetOrCreateState(states, hardwareId, "door_open");

        if (lightLux >= threshold)
        {
            state.ConsecutiveCount++;
            if (!state.IsAlarming)
            {
                state.IsAlarming     = true;
                state.AlarmStartedAt = DateTime.UtcNow;
            }
            state.LastAlarmedAt = DateTime.UtcNow;

            FleetLog.Info($"[Fleet-Door] {hardwareId}: lux={lightLux:F0} >= threshold={threshold:F0}, consecutive={state.ConsecutiveCount}/{debounceCount}");

            if (state.ConsecutiveCount >= debounceCount)
            {
                var message = $"Cargo door open detected: light {lightLux:F0} lux exceeds threshold {threshold:F0} lux during trip #{activeTripId.Value}";

                // Separate cooldown state per device so door events aren't suppressed by sensor cooldown
                var doorDevState    = GetOrCreateState(states, hardwareId, "_device_door");
                var doorCooldownMin = alarmObj["email_cooldown_minutes"]?.Value<int>() ?? 240;
                var cooldownExpired = doorDevState.LastEmailSentAt == null
                    || (DateTime.UtcNow - doorDevState.LastEmailSentAt.Value).TotalMinutes >= doorCooldownMin;

                if (cooldownExpired)
                {
                    // Advance cooldown in-memory before sending — persisted by SaveAll at cycle end.
                    doorDevState.LastEmailSentAt = DateTime.UtcNow;

                    if (alarmLog != null)
                        await alarmLog.Insert(new FleetAlarmLogEntry
                        {
                            HardwareId = hardwareId,
                            Ts         = ts,
                            AlarmType  = "ALARM",
                            Field      = "door_open",
                            Value      = lightLux,
                            Threshold  = threshold,
                            Message    = message
                        });

                    _ = FleetAlarmPusher.Push(hardwareId, new
                    {
                        id          = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        hardware_id = hardwareId,
                        ts          = ts.ToString("O"),
                        alarm_type  = "ALARM",
                        field       = "door_open",
                        value       = lightLux,
                        threshold   = threshold,
                        message     = message,
                        created_at  = DateTime.UtcNow.ToString("O")
                    }).ContinueWith(
                        t => FleetLog.Warn($"[Fleet-Door] {hardwareId}: SignalR push failed — {t.Exception?.GetBaseException().Message}"),
                        TaskContinuationOptions.OnlyOnFaulted);

                    if (emailEnabled && !string.IsNullOrWhiteSpace(notifyEmail))
                    {
                        var nowMyt = FleetTime.UtcToLocal(DateTime.UtcNow, timezone);
                        var htmlBody = BuildDoorOpenHtml(hardwareId, nowMyt, lightLux, threshold, activeTripId.Value);
                        var emailSent = await FleetEmailDispatch.SendAlarmToAll(
                            notifyEmail, hardwareId, "door_open", 0, "", message, htmlBody).ConfigureAwait(false);

                        FleetLog.Info($"[Fleet-Door] {hardwareId}: door open email sent={emailSent} to {notifyEmail}");

                        if (dbEmailLog != null)
                            await dbEmailLog.Insert(new FleetEmailLogEntry
                            {
                                HardwareId  = hardwareId,
                                Sensor      = "door_open",
                                ToEmail     = notifyEmail,
                                Description = message,
                                Success     = emailSent
                            });
                    }
                }
            }
        }
        else
        {
            // Light back below threshold — reset debounce so next event can fire again
            if (state.IsAlarming || state.ConsecutiveCount > 0)
            {
                state.ConsecutiveCount = 0;
                state.IsAlarming       = false;
            }
        }
        // State mutations are tracked in the shared dict; no individual Save here.
    }

    private static string BuildDoorOpenHtml(string hardwareId, DateTime nowMyt, double lux, double threshold, long tripId)
    {
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.AlertParagraph(
                $"Light sensor detected {lux:F0} lux (threshold: {threshold:F0} lux). " +
                $"Cargo door may have been opened during trip #{tripId}.", "#f57c00")
            + FleetEmailTemplate.InfoTable([
                ("Sensor Reading",  $"{lux:F0} lux"),
                ("Door Threshold",  $"{threshold:F0} lux"),
                ("Active Trip",     $"#{tripId}"),
                ("Alert Time",      nowMyt.ToString("dd MMM yyyy HH:mm") + " MYT"),
            ]);
        return FleetEmailTemplate.HtmlPage("🚪 Fleet DOOR OPEN ALERT", "#f57c00", body);
    }

    // ─── CheckBatteryLevel ────────────────────────────────────────────────────

    /// <summary>
    /// Fires a one-shot battery low alert when the battery crosses below 30%, 20%, or 10%.
    ///
    /// "One-shot" means: the alert fires ONCE when the battery first drops below a threshold,
    /// and resets when the battery recovers above that threshold. It does NOT re-fire on
    /// every subsequent low reading (that would cause continuous spam).
    ///
    /// The three thresholds (30, 20, 10%) can all fire independently. A device at 8% will
    /// have fired alerts at all three thresholds at different points in time.
    ///
    /// State per threshold is stored in iot.tt19_alarm_state under keys like "battery_30",
    /// "battery_20", "battery_10".
    ///
    /// The alarm log row (iot.tt19_alarm_log) is written even if email fails —
    /// this ensures the frontend browser notification poller can still pick up the alert.
    /// </summary>
    public static async Task CheckBatteryLevel(
        string            hardwareId,
        DateTime          ts,
        double?           batteryPct,
        FleetDbSettingsRepository   dbSettings,
        FleetDbAlarmStateRepository dbAlarmState,
        FleetDbAlarmLogRepository?  alarmLog   = null,
        FleetDbEmailLogRepository?  dbEmailLog = null,
        string            timezone   = "Asia/Kuala_Lumpur")
    {
        if (batteryPct == null) return;
        var batt = batteryPct.Value;

        var settings = await dbSettings.GetDeviceSettings(hardwareId);
        if (settings == null) return;

        var alarmObj = settings["alarm_json"] as JObject;
        if (alarmObj == null) return;

        if (!IsWithinSchedule(alarmObj, timezone)) return;

        var notifyEmail  = alarmObj["notify_email"]?.ToString()     ?? "";
        var emailEnabled = alarmObj["email_enabled"]?.Value<bool>() ?? false;

        var nowMyt = FleetTime.UtcToLocal(DateTime.UtcNow, timezone);

        // Batch-load all battery threshold states in one query instead of N GetOrCreate round-trips.
        var states      = await dbAlarmState.GetAllForDevice(hardwareId);
        var dirtyStates = new List<FleetAlarmStateRow>();

        foreach (var threshold in _batteryThresholds)
        {
            var stateKey = $"battery_{threshold}";
            if (!states.TryGetValue(stateKey, out var state))
                state = new FleetAlarmStateRow { HardwareId = hardwareId, Sensor = stateKey };

            if (batt <= threshold && !state.IsAlarming)
            {
                // Crossed below threshold for the first time — fire one-shot alert
                state.IsAlarming     = true;
                state.AlarmStartedAt = DateTime.UtcNow;
                state.LastAlarmedAt  = DateTime.UtcNow;
                dirtyStates.Add(state);

                var message = $"Battery LOW: {batt:F0}% on device {hardwareId} (threshold: {threshold}%)";
                FleetLog.Warn($"[Fleet-Battery] {hardwareId}: {message}");

                // Write alarm log first — id is passed to email log for correlation
                long alarmLogId = 0;
                if (alarmLog != null)
                    alarmLogId = await alarmLog.Insert(new FleetAlarmLogEntry
                    {
                        HardwareId = hardwareId,
                        Ts         = ts,
                        AlarmType  = "ALARM",
                        Field      = stateKey,
                        Value      = batt,
                        Threshold  = threshold,
                        Message    = message
                    });

                _ = FleetAlarmPusher.Push(hardwareId, new
                {
                    id          = -DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    hardware_id = hardwareId,
                    ts          = ts.ToString("O"),
                    alarm_type  = "ALARM",
                    field       = stateKey,
                    value       = batt,
                    threshold   = (double)threshold,
                    message     = message,
                    created_at  = DateTime.UtcNow.ToString("O")
                }).ContinueWith(
                    t => FleetLog.Warn($"[Fleet-Battery] {hardwareId}: SignalR push failed — {t.Exception?.GetBaseException().Message}"),
                    TaskContinuationOptions.OnlyOnFaulted);

                // Send email alert
                if (emailEnabled && !string.IsNullOrWhiteSpace(notifyEmail))
                {
                    var html      = BuildBatteryHtml(hardwareId, nowMyt, batt, threshold);
                    var emailSent = await FleetEmailDispatch.SendAlarmToAll(
                        notifyEmail, hardwareId, stateKey, 0, "", message, html).ConfigureAwait(false);
                    if (emailSent)
                        FleetLog.Info($"[Fleet-Battery] {hardwareId}: email sent to {notifyEmail}");
                    if (dbEmailLog != null)
                        await dbEmailLog.Insert(new FleetEmailLogEntry { HardwareId = hardwareId, Sensor = stateKey, ToEmail = notifyEmail, Description = message, Success = emailSent, AlarmLogId = alarmLogId > 0 ? alarmLogId : null });
                }
            }
            else if (batt > threshold && state.IsAlarming)
            {
                // Battery recovered above threshold — reset state so next drop fires again
                state.IsAlarming       = false;
                state.ConsecutiveCount = 0;
                dirtyStates.Add(state);
                FleetLog.Info($"[Fleet-Battery] {hardwareId}: battery recovered above {threshold}% — alert reset");
            }
        }

        if (dirtyStates.Count > 0)
            await dbAlarmState.SaveAll(dirtyStates);
    }

    // Battery thresholds in descending order — evaluated independently
    private static readonly int[] _batteryThresholds = { 30, 20, 10 };

    private static string BuildBatteryHtml(string hardwareId, DateTime nowMyt, double batt, int threshold)
    {
        var body = FleetEmailTemplate.DeviceHeader(hardwareId, nowMyt)
            + FleetEmailTemplate.AlertParagraph($"Battery level {batt:F0}% is below the {threshold}% threshold.", "#d32f2f")
            + FleetEmailTemplate.BatteryTableOpen()
            + FleetEmailTemplate.BatteryTableRow(batt, threshold)
            + FleetEmailTemplate.BatteryTableClose()
            + FleetEmailTemplate.FooterNote("Please charge or replace the device battery promptly.");
        return FleetEmailTemplate.HtmlPage("🔋 Fleet BATTERY LOW ALERT", "#d32f2f", body);
    }

    // ─── IsWithinSchedule (private) ───────────────────────────────────────────

    /// <summary>
    /// Returns true if the current MYT time is within the device's alarm schedule.
    ///
    /// Schedule fields in alarm_json (all optional):
    ///   effective_start  — date string (YYYY-MM-DD), don't fire before this date
    ///   effective_end    — date string (YYYY-MM-DD), don't fire after this date
    ///   repeat_days      — array e.g. ["Mon","Wed","Fri"], empty = every day
    ///   daily_start      — HH:MM, don't fire before this time
    ///   daily_end        — HH:MM, don't fire after this time
    ///
    /// All times are interpreted in MYT (UTC+8).
    /// If all schedule fields are empty, alarms fire at any time.
    /// </summary>
    private static bool IsWithinSchedule(JObject alarmObj, string timezone = "Asia/Kuala_Lumpur")
    {
        var nowMyt = FleetTime.UtcToLocal(DateTime.UtcNow, timezone);

        // Effective date range check
        var effectiveStart = alarmObj["effective_start"]?.ToString();
        var effectiveEnd   = alarmObj["effective_end"]?.ToString();

        if (!string.IsNullOrWhiteSpace(effectiveStart)
            && DateTime.TryParse(effectiveStart, CultureInfo.InvariantCulture, DateTimeStyles.None, out var startDt)
            && nowMyt.Date < startDt.Date)
            return false;

        if (!string.IsNullOrWhiteSpace(effectiveEnd)
            && DateTime.TryParse(effectiveEnd, CultureInfo.InvariantCulture, DateTimeStyles.None, out var endDt)
            && nowMyt.Date > endDt.Date)
            return false;

        // Day-of-week check
        var repeatDays = alarmObj["repeat_days"];
        if (repeatDays?.Type == JTokenType.Array)
        {
            var days = ((JArray)repeatDays).Select(d => d.ToString()).ToList();
            if (days.Count > 0)
            {
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
                if (!days.Contains(todayAbbr, StringComparer.OrdinalIgnoreCase))
                    return false;
            }
        }

        // Daily time window check
        var dailyStart = alarmObj["daily_start"]?.ToString();
        var dailyEnd   = alarmObj["daily_end"]?.ToString();

        if (!string.IsNullOrWhiteSpace(dailyStart)
            && TimeSpan.TryParse(dailyStart, out var startTime)
            && nowMyt.TimeOfDay < startTime)
            return false;

        if (!string.IsNullOrWhiteSpace(dailyEnd)
            && TimeSpan.TryParse(dailyEnd, out var endTime)
            && nowMyt.TimeOfDay > endTime)
            return false;

        return true;
    }
}
