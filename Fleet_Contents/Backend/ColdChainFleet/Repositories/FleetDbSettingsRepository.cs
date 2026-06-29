using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Npgsql;
using FleetCore.Context;
using FleetCore.Fleet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FleetCore.Repositories;

/// <summary>
/// Repository for device settings stored in iot.device_settings.
///
/// ── What this file does ───────────────────────────────────────────────────────
///   GetDeviceSettings    — load alarm thresholds + trip config for a device
///   UpsertDeviceSettings — save (create or update) alarm + trip settings
///
/// ── Table: iot.device_settings ───────────────────────────────────────────────
/// Columns: hardware_id (PK), alarm_json (JSONB), trip_json (JSONB), updated_at
///
/// History: every save also inserts a row in iot.device_settings_history.
///
/// ── IMPORTANT: Return JObject directly ───────────────────────────────────────
/// These methods return JObject values inside the dictionary, NOT
/// ToObject<Dictionary<string,object?>> conversions.
/// Converting JObject → Dictionary produces System.Text.Json.JsonElement values
/// that break Newtonsoft serialization in the controller. Keep as JObject.
/// </summary>
public class FleetDbSettingsRepository
{
    private readonly DatabaseContext _databaseContext;

    // Settings are cached via FleetCache (shared singleton, see Fleet/FleetCacheService.cs).
    // TTL is intentionally short (30 s) to limit staleness after a settings save.
    // NOTE: FleetCache is process-local by default. Swap in a Redis-backed implementation
    // via FleetCache.UseImplementation() at startup to propagate invalidations across
    // multiple app instances.
    private static readonly TimeSpan _cacheTtl = TimeSpan.FromSeconds(30);

    public FleetDbSettingsRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── GetDeviceSettings ────────────────────────────────────────────────────

    /// <summary>
    /// Loads the alarm + trip settings for a device.
    /// Returns null if the device has no settings row yet.
    ///
    /// The returned dictionary keys:
    ///   hardware_id, truck_name, alarm_json (JObject), trip_json (JObject), updated_at
    /// </summary>
    public async Task<Dictionary<string, object?>?> GetDeviceSettings(string hardwareId)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) return null;

        var cacheKey = $"fleet:settings:{hardwareId}";
        if (FleetCache.TryGet<Dictionary<string, object?>>(cacheKey, out var hit))
            return CloneSettings(hit!);

        const string sql = @"
SELECT hardware_id  AS HardwareId,
       alarm_json::text AS AlarmJson,
       trip_json::text  AS TripJson,
       updated_at   AS UpdatedAt
FROM iot.device_settings
WHERE hardware_id = @HardwareId;
";
        using var connection = _databaseContext.CreateConnection();
        var r = await connection.QueryFirstOrDefaultAsync<SettingsDbRow>(
            sql, new { HardwareId = hardwareId });

        if (r is null) return null;

        var tripObj   = NormalizeJson(ParseJson(r.TripJson));
        var truckName = tripObj["truck_name"]?.ToString()
                     ?? tripObj["vehicle"]?.ToString()
                     ?? "";

        var result = new Dictionary<string, object?>
        {
            ["hardware_id"] = r.HardwareId,
            ["truck_name"]  = truckName,
            ["alarm_json"]  = NormalizeJson(ParseJson(r.AlarmJson)),
            ["trip_json"]   = tripObj,
            ["updated_at"]  = r.UpdatedAt?.ToUniversalTime().ToString("o")
        };

        FleetCache.Set(cacheKey, result, _cacheTtl);
        return CloneSettings(result);
    }

    // ─── UpsertDeviceSettings ─────────────────────────────────────────────────

    /// <summary>
    /// Saves (inserts or updates) device alarm + trip settings.
    /// Also writes a history record so previous settings are never lost.
    /// Uses a transaction so both the upsert and history insert are atomic.
    /// Returns the saved row in the same format as GetDeviceSettings.
    /// </summary>
    public async Task<Dictionary<string, object?>> UpsertDeviceSettings(
        string hardwareId, JObject alarmJson, JObject tripJson)
    {
        hardwareId = (hardwareId ?? "").Trim();
        if (hardwareId.Length == 0) throw new ArgumentException("hardware_id is required");

        ValidateAlarmJson(alarmJson);

        var alarmStr = NormalizeJson(alarmJson ?? new JObject()).ToString(Formatting.None);
        var tripStr  = NormalizeJson(tripJson  ?? new JObject()).ToString(Formatting.None);

        const string sqlUpsert = @"
INSERT INTO iot.device_settings (hardware_id, alarm_json, trip_json, updated_at)
VALUES (@HardwareId, @AlarmJson::jsonb, @TripJson::jsonb, NOW())
ON CONFLICT (hardware_id)
DO UPDATE SET alarm_json = EXCLUDED.alarm_json,
              trip_json  = EXCLUDED.trip_json,
              updated_at = NOW()
RETURNING hardware_id AS HardwareId,
          alarm_json::text AS AlarmJson,
          trip_json::text  AS TripJson,
          updated_at   AS UpdatedAt;
";
        const string sqlHistory = @"
INSERT INTO iot.device_settings_history (hardware_id, alarm_json, trip_json)
VALUES (@HardwareId, @AlarmJson::jsonb, @TripJson::jsonb);
";
        var param = new { HardwareId = hardwareId, AlarmJson = alarmStr, TripJson = tripStr };

        using var connection = (NpgsqlConnection)_databaseContext.CreateConnection();
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        var r = await connection.QueryFirstOrDefaultAsync<SettingsDbRow>(
            sqlUpsert, param, transaction: tx);

        await connection.ExecuteAsync(sqlHistory, param, transaction: tx);
        await tx.CommitAsync();

        var savedTrip      = NormalizeJson(ParseJson(r!.TripJson));
        var savedTruckName = savedTrip["truck_name"]?.ToString()
                          ?? savedTrip["vehicle"]?.ToString()
                          ?? "";

        var saved = new Dictionary<string, object?>
        {
            ["hardware_id"] = r.HardwareId,
            ["truck_name"]  = savedTruckName,
            ["alarm_json"]  = NormalizeJson(ParseJson(r.AlarmJson)),
            ["trip_json"]   = savedTrip,
            ["updated_at"]  = r.UpdatedAt?.ToUniversalTime().ToString("o")
        };

        FleetCache.Set($"fleet:settings:{hardwareId}", saved, _cacheTtl);
        return CloneSettings(saved);
    }

    // ─── InvalidateCache ──────────────────────────────────────────────────────

    /// <summary>
    /// Evicts the cached settings for a device immediately.
    /// Call this after deleting a device so the next poll doesn't serve stale data.
    /// </summary>
    public void InvalidateCache(string hardwareId)
    {
        FleetCache.Remove($"fleet:settings:{hardwareId}");
    }

    // ─── Private Dapper mapping class ─────────────────────────────────────────

    private class SettingsDbRow
    {
        public string?   HardwareId { get; set; }
        public string?   AlarmJson  { get; set; }
        public string?   TripJson   { get; set; }
        public DateTime? UpdatedAt  { get; set; }
    }

    // ─── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Validates alarm thresholds. Throws ArgumentException with a user-readable
    /// message so the controller can return 400 with that exact text.
    /// All checks are only applied when both endpoints of a range are present.
    /// </summary>
    private static void ValidateAlarmJson(JObject a)
    {
        double? Get(string key)
        {
            var tok = a[key];
            if (tok == null || tok.Type == JTokenType.Null) return null;
            return tok.Value<double?>();
        }
        string? GetStr(string key) => a[key]?.Value<string>();

        var tempMin = Get("temp_min_c");
        var tempMax = Get("temp_max_c");
        if (tempMin.HasValue && tempMax.HasValue && tempMin >= tempMax)
            throw new ArgumentException("Temp Min must be less than Temp Max.");

        var humMin = Get("humidity_min_pct");
        var humMax = Get("humidity_max_pct");
        if (humMin.HasValue && humMax.HasValue && humMin >= humMax)
            throw new ArgumentException("Humidity Min must be less than Humidity Max.");

        var luxMin = Get("light_min_lux");
        var luxMax = Get("light_max_lux");
        if (luxMin.HasValue && luxMax.HasValue && luxMin >= luxMax)
            throw new ArgumentException("Light Min must be less than Light Max.");

        var debounce = Get("debounce_count");
        if (debounce.HasValue && debounce < 1)
            throw new ArgumentException("Debounce count must be at least 1.");

        var cooldown = Get("email_cooldown_minutes");
        if (cooldown.HasValue && cooldown < 0)
            throw new ArgumentException("Email cooldown must be 0 or greater.");

        var interval = Get("reporting_interval_minutes");
        if (interval.HasValue && (interval < 1 || interval > 60))
            throw new ArgumentException("Reporting interval must be between 1 and 60 minutes.");

        var dwell = Get("dwell_max_minutes");
        if (dwell.HasValue && dwell < 1)
            throw new ArgumentException("Max parked time must be at least 1 minute.");

        var dailyStart = GetStr("daily_start");
        var dailyEnd   = GetStr("daily_end");
        if (!string.IsNullOrWhiteSpace(dailyStart) && !string.IsNullOrWhiteSpace(dailyEnd)
            && string.Compare(dailyStart, dailyEnd, StringComparison.Ordinal) >= 0)
            throw new ArgumentException("Daily Start must be earlier than Daily End.");
    }

    private static Dictionary<string, object?> CloneSettings(Dictionary<string, object?> src)
    {
        var clone = new Dictionary<string, object?>(src);
        if (clone["alarm_json"] is JObject aj) clone["alarm_json"] = (JObject)aj.DeepClone();
        if (clone["trip_json"]  is JObject tj) clone["trip_json"]  = (JObject)tj.DeepClone();
        return clone;
    }

    private static JObject ParseJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text == "{}") return new JObject();
        try   { return JObject.Parse(text); }
        catch { return new JObject(); }
    }

    /// <summary>
    /// Flattens single-element arrays in a JObject to their scalar values.
    /// Exception: "repeat_days" is always kept as an array.
    /// </summary>
    private static JObject NormalizeJson(JObject obj)
    {
        var clean = new JObject();
        foreach (var prop in obj.Properties())
        {
            var v = prop.Value;
            if (v.Type == JTokenType.Array)
            {
                var arr = (JArray)v;
                if (prop.Name == "repeat_days") { clean[prop.Name] = arr; continue; }
                if (arr.Count == 0)             { clean[prop.Name] = JValue.CreateNull(); continue; }
                if (arr.Count == 1)             { clean[prop.Name] = arr[0]; continue; }
                clean[prop.Name] = arr;
                continue;
            }
            clean[prop.Name] = v;
        }
        return clean;
    }
}
