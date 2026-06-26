using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using HIAS_NET_CORE.Fleet;
using HIAS_NET_CORE.Fleet.Alarm;
using HIAS_NET_CORE.Repositories;

namespace HIAS_NET_CORE.Fleet.Ingest;

/// <summary>
/// Fetches one sensor reading from the TZone cloud and ingests it into PostgreSQL.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   IngestOneDevice — the core per-device ingest function, called by FleetIngestService
///                     on every poll cycle for each registered device
///
/// ── Per-cycle sequence (one device, one call) ─────────────────────────────────
///   1. Call FleetClient.GetRealtime()  — fetch the latest reading from TZone cloud
///   2. Parse sensor values             — temperature, humidity, light, battery, vibration, GPS
///   3. FleetDbRealtimeRepository.InsertRow()     — insert into iot.tt19_data (skips duplicates)
///   4. If inserted (new data only):
///        a. Log the new reading
///        b. FleetAlarmChecker.CheckAndNotify() — evaluate all threshold alarms
///        c. FleetAlarmChecker.CheckBatteryLevel() — separate low-battery alarm path
///        d. FleetDbTripsRepository.AppendTripPoint() — add GPS point to active trip (if any)
///
/// ── Why only run checks on "inserted" rows ────────────────────────────────────
/// The TZone API returns the same reading until the device transmits a new one.
/// InsertRow() returns false for duplicate (hardware_id, ts) pairs.
/// Running alarm checks on the same reading twice would cause duplicate notifications,
/// double-increment the debounce counter, and produce incorrect email cooldowns.
/// Checking only on insert means "one reading → one alarm evaluation → one notification".
///
/// ── GPS coordinate parsing ────────────────────────────────────────────────────
/// TZone returns latitude and longitude as a single comma-separated string in "latLng",
/// e.g. "3.1234,101.5678". This is split into separate lat/lng doubles.
/// Readings with lat == 0 AND lng == 0 are treated as "no satellite lock" and the
/// GPS point is NOT appended to the trip (FleetDbTripsRepository.AppendTripPoint checks this too).
///
/// ── Error handling ───────────────────────────────────────────────────────────
/// Exceptions from GetRealtime() propagate up to FleetIngestService, which catches
/// them per-device and logs them without stopping the service.
/// The trip-point append is wrapped in its own try/catch so a GPS error never
/// prevents the alarm check from running (alarms are more critical than GPS).
/// </summary>
public static class FleetFetchRealtime
{
    // ─── IngestOneDevice ──────────────────────────────────────────────────────

    /// <summary>
    /// Fetches and stores one sensor reading for the given device.
    ///
    /// Parameters:
    ///   deviceIntId — TZone integer ID (from iot.tt19_devices.device_int_id)
    ///   hardwareId  — physical device serial (primary key in iot.tt19_devices)
    ///   appId/appKey/appSecret — per-device TZone credentials (null = use global)
    ///
    /// Called by FleetIngestService.PollAllDevices() once per poll interval
    /// (default every 10 seconds).
    ///
    /// Throws if the TZone API call fails — the caller catches and logs per device.
    /// </summary>
    /// <returns>
    /// The active trip ID for this device at the time of this poll cycle,
    /// or null if no trip is in progress. Callers can pass this value on to
    /// FleetTripScheduler.CheckAndApplySchedule to avoid a second DB round-trip.
    /// Returns null when the reading was a duplicate (inserted = false) because
    /// the trip state was never queried in that case.
    /// </returns>
    public static async Task<long?> IngestOneDevice(
        long              deviceIntId,
        string            hardwareId,
        FleetDbRealtimeRepository   dbRealtime,
        FleetDbSettingsRepository   dbSettings,
        FleetDbAlarmStateRepository dbAlarmState,
        string?           appId       = null,
        string?           appKey      = null,
        string?           appSecret   = null,
        FleetDbAlarmLogRepository?  alarmLog    = null,
        FleetDbEmailLogRepository?  dbEmailLog  = null,
        FleetDbTripsRepository?     dbTrips     = null,
        FleetDbDwellRepository?     dbDwell      = null,
        FleetDbLocationsRepository? dbLocations  = null,
        string            timezone    = "Asia/Kuala_Lumpur",
        string?           deviceLabel = null)
    {
        // Step 1: fetch latest reading from TZone cloud
        var body = await FleetClient.GetRealtimeAsync(deviceIntId, appId, appKey, appSecret).ConfigureAwait(false);

        // Step 2: extract each sensor field from the flattened TZone response dict
        var ts       = FleetDbCoreRepository.TsFromBody(body);
        var rawTemp  = body.TryGetValue("temperature", out var t) ? t : null;
        var rawHum   = body.TryGetValue("humidity",    out var h) ? h : null;
        var rawLight = body.TryGetValue("light",       out var l) ? l : null;
        var rawBatt  = body.TryGetValue("battery",     out var b) ? b : null;
        var rawVib   = body.TryGetValue("vibration",   out var v) ? v : null;

        // Humidity from TZone is a 0–1 fraction; HumidityToPct multiplies by 100
        var humidityPct = FleetDbCoreRepository.HumidityToPct(rawHum);

        // Step 3: insert into iot.tt19_data — returns false for duplicate timestamps
        var inserted = await dbRealtime.InsertRow(
            hardwareId:   hardwareId,
            tsUtc:        ts,
            temperatureC: rawTemp,
            humidityPct:  humidityPct,
            lightLux:     rawLight,
            batteryPct:   rawBatt,
            vibrationG:   rawVib
        );

        if (inserted)
        {
            FleetLog.Info($"[Fleet] Inserted {hardwareId} at {ts:o}");

            // Cache last known reading for resiliency: status dashboard serves stale data
            // with a circuitOpen badge when the circuit breaker is OPEN for this device.
            FleetCache.Set($"fleet:last_reading:{hardwareId}", new
            {
                ts           = ts.ToString("o"),
                temperatureC = ToDouble(rawTemp),
                humidityPct,
                lightLux     = ToDouble(rawLight),
                batteryPct   = ToDouble(rawBatt),
                cachedAt     = DateTimeOffset.UtcNow.ToString("o")
            }, TimeSpan.FromHours(2));

            // Step 4a: parse GPS coordinates (used by both alarm context and trip append)
            double? lat = null, lng = null;
            var latLngRaw = body.TryGetValue("latLng", out var ll) ? ll?.ToString() : null;
            if (!string.IsNullOrWhiteSpace(latLngRaw))
            {
                var parts = latLngRaw!.Split(',');
                if (parts.Length == 2
                    && double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat)
                    && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng)
                    && !(parsedLat == 0 && parsedLng == 0)
                    && Math.Abs(parsedLat) <= 90 && Math.Abs(parsedLng) <= 180)
                {
                    lat = parsedLat;
                    lng = parsedLng;
                }
            }

            // Fetch active trip once — used by both GPS append (4d) and door alarm (inside CheckAndNotify)
            long? activeTripId = null;
            if (dbTrips != null)
            {
                try { activeTripId = await dbTrips.GetActiveTripForDevice(hardwareId); }
                catch (Exception ex) { FleetLog.Error($"[Fleet] GetActiveTripForDevice error for {hardwareId}.", ex); }
            }

            // Step 4b: evaluate alarm thresholds against settings in iot.device_settings
            await FleetAlarmChecker.CheckAndNotify(
                hardwareId, ts,
                ToDouble(rawTemp), humidityPct, ToDouble(rawLight), ToDouble(rawBatt),
                dbSettings, dbAlarmState,
                ToDouble(rawVib), lat, lng, alarmLog, dbEmailLog, dbTrips,
                dbDwell, dbLocations, timezone, activeTripId, deviceLabel);

            // Step 4c: separate low-battery alarm path (uses different thresholds/cooldown)
            await FleetAlarmChecker.CheckBatteryLevel(hardwareId, ts, ToDouble(rawBatt),
                dbSettings, dbAlarmState, alarmLog, dbEmailLog, timezone);

            // Step 4d: append GPS point to the active open trip (reuse activeTripId fetched above)
            try
            {
                if (activeTripId.HasValue && lat.HasValue && lng.HasValue && dbTrips != null)
                    await dbTrips.AppendTripPoint(activeTripId.Value, lat.Value, lng.Value, ToDouble(rawTemp), ts.ToString("O"));
            }
            catch (Exception ex)
            {
                FleetLog.Error($"[Fleet] AppendTripPoint error for {hardwareId}.", ex);
            }

            // Return the active trip ID so FleetIngestService can pass it on to
            // FleetTripScheduler.CheckAndApplySchedule — avoids a second GetActiveTripForDevice
            // DB call on the same poll cycle.
            return activeTripId;
        }
        else
        {
            FleetLog.Debug($"[Fleet] Skipped duplicate reading for {hardwareId}");
            // Reading was a duplicate; trip state was never queried — return null so
            // CheckAndApplySchedule will fetch it independently if needed.
            return null;
        }
    }

    // ─── ToDouble (private) ───────────────────────────────────────────────────

    /// <summary>
    /// Safely converts any object value from the TZone response dictionary to a nullable double.
    /// Returns null if the value is null, empty, or cannot be parsed as a number.
    /// Used to convert raw sensor strings (e.g. "23.5") to typed numbers.
    /// </summary>
    private static double? ToDouble(object? v)
    {
        if (v == null) return null;
        try   { return Convert.ToDouble(v, CultureInfo.InvariantCulture); }
        catch { return null; }
    }
}
