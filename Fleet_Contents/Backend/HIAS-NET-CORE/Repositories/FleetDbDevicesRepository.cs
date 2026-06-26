using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using HIAS_NET_CORE.Context;
using HIAS_NET_CORE.Fleet;
using Npgsql;

namespace HIAS_NET_CORE.Repositories;

/// <summary>
/// Repository for the Fleet device registry (iot.tt19_devices).
///
/// ── What this file does ───────────────────────────────────────────────────────
///   GetDevicesByOrg      — list active devices for one organisation
///   GetDeviceCredentials — look up the TZone integer ID + API keys for one device
///   GetAllActiveDevices  — return every registered device (used by the ingest loop)
///   DeviceBelongsToOrg   — ownership check before any controller returns sensor data
///   GetByOrgId           — simpler list query without the device_int_id column
///   SeedDevice           — admin helper to pre-create an unregistered device row
///   UnregisterDevice     — detach a device from an organisation
///   RegisterDevice       — claim an unregistered device with an activation code
///
/// ── How device ownership works ────────────────────────────────────────────────
/// Each device row starts with organization_id = NULL.
/// An organisation "claims" a device by calling RegisterDevice() with the correct
/// activation_code. Once claimed, the device is locked to that org.
///
/// ── device_int_id explained ───────────────────────────────────────────────────
/// The TZone cloud API uses a numeric integer ID, NOT the hardware_id string.
/// Without device_int_id, the ingest service silently skips the device.
/// </summary>
public class FleetDbDevicesRepository
{
    private readonly DatabaseContext _databaseContext;

    // Cached per-process: once we know whether the archive table exists we
    // do not re-check on every call. Null = not yet checked.
    private static bool? _archiveExists;

    public FleetDbDevicesRepository(DatabaseContext context)
    {
        _databaseContext = context;
    }

    // ─── ArchiveExistsAsync ───────────────────────────────────────────────────

    private async Task<bool> ArchiveExistsAsync()
    {
        if (_archiveExists.HasValue) return _archiveExists.Value;
        const string sql = @"
SELECT 1 FROM information_schema.tables
WHERE table_schema = 'iot' AND table_name = 'tt19_data_archive'
LIMIT 1;";
        using var connection = _databaseContext.CreateConnection();
        var result = await connection.QueryFirstOrDefaultAsync<int?>(sql);
        _archiveExists = result.HasValue;
        return _archiveExists.Value;
    }

    // ─── GetDevicesByOrg ──────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active devices registered to the given organisation.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetDevicesByOrg(int orgId)
    {
        const string sql = @"
SELECT hardware_id   AS HardwareId,
       device_int_id AS DeviceIntId,
       label         AS Label,
       registered_at AS RegisteredAt,
       created_at    AS CreatedAt
FROM   iot.tt19_devices
WHERE  organization_id = @OrgId
  AND  is_active = TRUE
ORDER  BY registered_at DESC;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<DeviceDbRow>(sql, new { OrgId = orgId });
        return rows.Select(r => new Dictionary<string, object?>
        {
            ["hardware_id"]   = r.HardwareId,
            ["device_int_id"] = r.DeviceIntId,
            ["label"]         = r.Label,
            ["registered_at"] = r.RegisteredAt?.ToUniversalTime().ToString("o"),
            ["created_at"]    = r.CreatedAt?.ToUniversalTime().ToString("o")
        }).ToList();
    }

    // ─── GetDeviceCredentials ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the TZone integer device ID and per-device API credentials.
    /// Returns null if the device is not found, inactive, or has no device_int_id.
    /// </summary>
    public async Task<(long DeviceIntId, string? AppId, string? AppKey, string? AppSecret)?> GetDeviceCredentials(
        string hardwareId)
    {
        hardwareId = (hardwareId ?? "").Trim().ToUpperInvariant();
        if (hardwareId.Length == 0) return null;

        const string sql = @"
SELECT device_int_id AS DeviceIntId,
       app_id        AS AppId,
       app_key       AS AppKey,
       app_secret    AS AppSecret
FROM   iot.tt19_devices
WHERE  hardware_id    = @HardwareId
  AND  is_active      = TRUE
  AND  device_int_id IS NOT NULL
LIMIT 1;
";
        using var connection = _databaseContext.CreateConnection();
        var r = await connection.QueryFirstOrDefaultAsync<CredentialsDbRow>(
            sql, new { HardwareId = hardwareId });

        if (r is null) return null;
        return (r.DeviceIntId, r.AppId, r.AppKey, r.AppSecret);
    }

    // ─── GetAllActiveDevices ──────────────────────────────────────────────────

    /// <summary>
    /// Returns every active device across ALL organisations, including their
    /// TZone integer ID and per-device API credentials.
    /// Only devices with a valid device_int_id are returned.
    /// Called by FleetIngestService on each poll cycle.
    /// </summary>
    public async Task<List<(string HardwareId, long DeviceIntId, string? AppId, string? AppKey, string? AppSecret, string Timezone, string? Label)>> GetAllActiveDevices()
    {
        const string sql = @"
SELECT hardware_id   AS HardwareId,
       device_int_id AS DeviceIntId,
       app_id        AS AppId,
       app_key       AS AppKey,
       app_secret    AS AppSecret,
       COALESCE(timezone, 'Asia/Kuala_Lumpur') AS Timezone,
       label         AS Label
FROM   iot.tt19_devices
WHERE  is_active      = TRUE
  AND  device_int_id IS NOT NULL;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<ActiveDeviceDbRow>(sql);
        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.HardwareId))
            .Select(r => (r.HardwareId!, r.DeviceIntId, r.AppId, r.AppKey, r.AppSecret, r.Timezone ?? "Asia/Kuala_Lumpur", r.Label))
            .ToList();
    }

    // ─── GetTimezone ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the IANA timezone ID configured for the device.
    /// Falls back to "Asia/Kuala_Lumpur" if the column is NULL or the device is not found.
    /// Used by date-range queries so they window correctly for non-MYT deployments.
    /// </summary>
    public async Task<string> GetTimezone(string hardwareId)
    {
        const string sql = @"
SELECT COALESCE(timezone, 'Asia/Kuala_Lumpur')
FROM   iot.tt19_devices
WHERE  hardware_id = @HardwareId
LIMIT  1;
";
        using var connection = _databaseContext.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(sql, new { HardwareId = hardwareId })
            ?? "Asia/Kuala_Lumpur";
    }

    // ─── DeviceBelongsToOrg ───────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the given hardware_id is active AND belongs to orgId.
    /// Every controller that returns sensor data calls this first to prevent
    /// one organisation from seeing another org's device readings.
    /// </summary>
    public async Task<bool> DeviceBelongsToOrg(string hardwareId, int orgId)
    {
        const string sql = @"
SELECT COUNT(1)
FROM   iot.tt19_devices
WHERE  hardware_id     = @HardwareId
  AND  organization_id = @OrgId
  AND  is_active       = TRUE;
";
        using var connection = _databaseContext.CreateConnection();
        var count = await connection.QueryFirstOrDefaultAsync<int>(
            sql, new { HardwareId = hardwareId, OrgId = orgId });
        return count > 0;
    }

    // ─── GetByOrgId ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all devices (active OR inactive) registered to an organisation.
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetByOrgId(int organizationId)
    {
        const string sql = @"
SELECT hardware_id   AS HardwareId,
       label         AS Label,
       registered_at AS RegisteredAt,
       created_at    AS CreatedAt
FROM   iot.tt19_devices
WHERE  organization_id = @OrgId
ORDER  BY registered_at DESC;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<DeviceDbRow>(sql, new { OrgId = organizationId });
        return rows.Select(r => new Dictionary<string, object?>
        {
            ["hardware_id"]   = r.HardwareId,
            ["label"]         = r.Label,
            ["registered_at"] = r.RegisteredAt?.ToUniversalTime().ToString("o"),
            ["created_at"]    = r.CreatedAt?.ToUniversalTime().ToString("o")
        }).ToList();
    }

    // ─── GetDevicesSummary ────────────────────────────────────────────────────

    /// <summary>
    /// Unified device list: registry metadata LEFT JOIN latest telemetry reading.
    /// Every registered device appears regardless of whether it has telemetry yet.
    /// Uses a LATERAL subquery so the planner indexes tt19_data(hardware_id, ts DESC)
    /// per device row — O(devices) index seeks instead of a full scan + DISTINCT ON.
    ///
    /// Returned per row:
    ///   hardware_id, label, device_int_id, has_polling (device_int_id IS NOT NULL),
    ///   has_custom_creds (app_id IS NOT NULL), registered_at,
    ///   ts, temperature_c, humidity_pct, battery_pct, truck_name
    /// </summary>
    public async Task<List<Dictionary<string, object?>>> GetDevicesSummary(int orgId, int limit = 500)
    {
        limit = Math.Clamp(limit, 1, 5000);

        // The LATERAL subquery's source is archive-aware — iot.tt19_data is a 30-day
        // rolling window, so a device whose only history has aged into
        // iot.tt19_data_archive would otherwise show ts = NULL ("awaiting data")
        // forever even though it has genuinely reported before. Falls back to
        // tt19_data only if the archive table doesn't exist on this database.
        var hasArchive = await ArchiveExistsAsync();
        var latestSource = hasArchive ? @"
    (
        SELECT hardware_id, ts, temperature_c, humidity_pct, battery_pct
        FROM   iot.tt19_data
        UNION ALL
        SELECT hardware_id, ts, temperature_c, humidity_pct, battery_pct
        FROM   iot.tt19_data_archive
    )" : "iot.tt19_data";

        var sql = $@"
SELECT
    dev.hardware_id                                           AS HardwareId,
    dev.label                                                 AS Label,
    dev.device_int_id                                         AS DeviceIntId,
    dev.device_int_id IS NOT NULL                             AS HasPolling,
    dev.app_id        IS NOT NULL                             AS HasCustomCreds,
    dev.registered_at                                         AS RegisteredAt,
    latest.ts                                                 AS LastTs,
    latest.temperature_c                                      AS TemperatureC,
    latest.humidity_pct                                       AS HumidityPct,
    latest.battery_pct                                        AS BatteryPct,
    COALESCE(ds.trip_json->>'truck_name', dev.label) AS TruckName
FROM iot.tt19_devices dev
LEFT JOIN LATERAL (
    SELECT ts, temperature_c, humidity_pct, battery_pct
    FROM   {latestSource} src
    WHERE  src.hardware_id = dev.hardware_id
    ORDER  BY ts DESC
    LIMIT  1
) latest ON TRUE
LEFT JOIN iot.device_settings ds  ON ds.hardware_id  = dev.hardware_id
WHERE dev.organization_id = @OrgId
  AND dev.is_active        = TRUE
ORDER BY dev.registered_at ASC NULLS LAST
LIMIT @Limit;
";
        using var connection = _databaseContext.CreateConnection();
        var rows = await connection.QueryAsync<SummaryDbRow>(sql, new { OrgId = orgId, Limit = limit });
        return rows.Select(r => new Dictionary<string, object?>
        {
            ["hardware_id"]     = r.HardwareId,
            ["label"]           = r.Label,
            ["device_int_id"]   = r.DeviceIntId,
            ["has_polling"]     = r.HasPolling,
            ["has_custom_creds"]= r.HasCustomCreds,
            ["registered_at"]   = r.RegisteredAt?.ToUniversalTime().ToString("o"),
            ["ts"]              = r.LastTs?.ToUniversalTime().ToString("o"),
            ["temperature_c"]   = r.TemperatureC,
            ["humidity_pct"]    = r.HumidityPct,
            ["battery_pct"]     = r.BatteryPct,
            ["truck_name"]      = r.TruckName
        }).ToList();
    }

    // ─── UpdateDevice ─────────────────────────────────────────────────────────

    /// <summary>
    /// Updates mutable registration fields for a device the caller owns.
    /// Null parameters mean "leave unchanged"; pass an empty string to clear a field.
    ///
    /// device_int_id uses a special sentinel: long.MinValue means "not supplied"
    /// (omit from UPDATE), null means "clear to NULL", any other value means "set".
    /// </summary>
    public async Task<(bool Success, string? Error)> UpdateDevice(
        string  hardwareId,
        int     orgId,
        string? label,
        bool    clearLabel,
        long?   deviceIntId,
        bool    deviceIntIdSupplied,
        string? appId,
        string? appKey,
        string? appSecret,
        bool    clearCredentials)
    {
        hardwareId = (hardwareId ?? "").Trim().ToUpperInvariant();
        if (hardwareId.Length == 0) return (false, "hardware_id is required.");

        // Verify ownership first (fast single-row read)
        const string sqlCheck = @"
SELECT COUNT(1) FROM iot.tt19_devices
WHERE hardware_id = @HardwareId AND organization_id = @OrgId AND is_active = TRUE;
";
        using var connection = (NpgsqlConnection)_databaseContext.CreateConnection();
        await connection.OpenAsync();

        var owned = await connection.QueryFirstOrDefaultAsync<int>(
            sqlCheck, new { HardwareId = hardwareId, OrgId = orgId });
        if (owned == 0)
            return (false, "Device not found or not owned by your account.");

        // Build SET clause dynamically — only touch supplied fields
        var setClauses = new List<string>();
        var p          = new Dapper.DynamicParameters();
        p.Add("HardwareId", hardwareId);
        p.Add("OrgId",      orgId);

        if (clearLabel)
        {
            setClauses.Add("label = NULL");
        }
        else if (label != null)
        {
            setClauses.Add("label = @Label");
            p.Add("Label", label.Trim().Length == 0 ? null : label.Trim());
        }

        if (deviceIntIdSupplied)
        {
            if (deviceIntId == null)
                setClauses.Add("device_int_id = NULL");
            else
            {
                setClauses.Add("device_int_id = @DeviceIntId");
                p.Add("DeviceIntId", deviceIntId);
            }
        }

        if (clearCredentials)
        {
            setClauses.Add("app_id = NULL, app_key = NULL, app_secret = NULL");
        }
        else
        {
            if (appId != null)
            {
                setClauses.Add("app_id = @AppId");
                p.Add("AppId", appId.Trim().Length == 0 ? null : appId.Trim());
            }
            if (appKey != null)
            {
                setClauses.Add("app_key = @AppKey");
                p.Add("AppKey", appKey.Trim().Length == 0 ? null : appKey.Trim());
            }
            if (appSecret != null)
            {
                setClauses.Add("app_secret = @AppSecret");
                p.Add("AppSecret", appSecret.Trim().Length == 0 ? null : appSecret.Trim());
            }
        }

        if (setClauses.Count == 0)
            return (true, null); // Nothing to update — treat as success

        var sqlUpdate = $@"
UPDATE iot.tt19_devices
SET    {string.Join(", ", setClauses)}
WHERE  hardware_id     = @HardwareId
  AND  organization_id = @OrgId
  AND  is_active        = TRUE;
";
        var affected = await connection.ExecuteAsync(sqlUpdate, p);
        return affected > 0 ? (true, null) : (false, "Update did not match any device.");
    }

    // ─── SeedDevice ───────────────────────────────────────────────────────────

    /// <summary>
    /// Pre-creates an unregistered device row with a hardware_id + activation_code.
    /// Uses ON CONFLICT DO UPDATE so re-seeding with the same hardware_id is safe.
    /// </summary>
    public async Task<(bool Success, string? Error)> SeedDevice(
        string hardwareId, string activationCode, long? deviceIntId = null)
    {
        hardwareId     = (hardwareId     ?? "").Trim().ToUpperInvariant();
        activationCode = (activationCode ?? "").Trim();

        if (hardwareId.Length     == 0) return (false, "hardware_id is required.");
        if (activationCode.Length == 0) return (false, "activation_code is required.");

        const string sql = @"
INSERT INTO iot.tt19_devices (hardware_id, activation_code, device_int_id)
VALUES (@HardwareId, @ActivationCode, @DeviceIntId)
ON CONFLICT (hardware_id) DO UPDATE
    SET device_int_id = COALESCE(EXCLUDED.device_int_id, iot.tt19_devices.device_int_id);
";
        using var connection = _databaseContext.CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            HardwareId     = hardwareId,
            ActivationCode = activationCode,
            DeviceIntId    = deviceIntId
        });
        return (true, null);
    }

    // ─── UnregisterDevice ─────────────────────────────────────────────────────

    /// <summary>
    /// Detaches a device from an organisation by setting organization_id back to NULL.
    /// Only the owning org can unregister their own device.
    /// </summary>
    public async Task<(bool Success, string? Error)> UnregisterDevice(string hardwareId, int organizationId)
    {
        hardwareId = (hardwareId ?? "").Trim().ToUpperInvariant();
        if (hardwareId.Length == 0) return (false, "hardware_id is required.");

        const string sql = @"
UPDATE iot.tt19_devices
SET    organization_id = NULL,
       label           = NULL,
       registered_at   = NULL
WHERE  hardware_id     = @HardwareId
  AND  organization_id = @OrgId;
";
        using var connection = _databaseContext.CreateConnection();
        var affected = await connection.ExecuteAsync(
            sql, new { HardwareId = hardwareId, OrgId = organizationId });

        return affected > 0
            ? (true,  null)
            : (false, "Device not found or not owned by your account.");
    }

    // ─── RegisterDevice ───────────────────────────────────────────────────────

    /// <summary>
    /// Claims an unregistered device for an organisation.
    /// Uses SELECT FOR UPDATE inside a transaction so concurrent registration
    /// attempts for the same device are serialised — only one org can claim it,
    /// and the loser gets a clear "already registered" message rather than a
    /// generic race-condition error.
    /// </summary>
    public async Task<(bool Success, string? Error)> RegisterDevice(
        string hardwareId, string activationCode, int organizationId, string? label,
        string? appId = null, string? appKey = null, string? appSecret = null)
    {
        hardwareId     = (hardwareId     ?? "").Trim().ToUpperInvariant();
        activationCode = (activationCode ?? "").Trim();

        if (hardwareId.Length     == 0) return (false, "hardware_id is required.");
        if (activationCode.Length == 0) return (false, "activation_code is required.");

        using var connection = (NpgsqlConnection)_databaseContext.CreateConnection();
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        // Lock the row for the duration of this transaction — any concurrent
        // registration for the same hardware_id blocks here until we commit/rollback.
        const string sqlLock = @"
SELECT activation_code AS ActivationCode, organization_id AS ExistingOrgId
FROM   iot.tt19_devices
WHERE  hardware_id = @HardwareId
FOR UPDATE;
";
        var lookup = await connection.QueryFirstOrDefaultAsync<RegisterLookupRow>(
            sqlLock, new { HardwareId = hardwareId }, transaction: tx);

        if (lookup is null)
        {
            await tx.RollbackAsync();
            return (false, "Device not found. Please check the Device ID.");
        }

        if (!string.Equals(lookup.ActivationCode, activationCode, StringComparison.OrdinalIgnoreCase))
        {
            await tx.RollbackAsync();
            return (false, "Invalid Activation Code. Please check the code on your device label.");
        }

        if (lookup.ExistingOrgId.HasValue)
        {
            await tx.RollbackAsync();
            return (false, "This device is already registered to another account.");
        }

        const string sqlUpdate = @"
UPDATE iot.tt19_devices
SET    organization_id = @OrgId,
       label           = @Label,
       registered_at   = NOW(),
       app_id          = @AppId,
       app_key         = @AppKey,
       app_secret      = @AppSecret
WHERE  hardware_id     = @HardwareId
  AND  organization_id IS NULL;
";
        await connection.ExecuteAsync(sqlUpdate, new
        {
            HardwareId = hardwareId,
            OrgId      = organizationId,
            Label      = string.IsNullOrWhiteSpace(label)     ? null : label.Trim(),
            AppId      = string.IsNullOrWhiteSpace(appId)     ? null : appId!.Trim(),
            AppKey     = string.IsNullOrWhiteSpace(appKey)    ? null : appKey!.Trim(),
            AppSecret  = string.IsNullOrWhiteSpace(appSecret) ? null : appSecret!.Trim()
        }, transaction: tx);

        await tx.CommitAsync();
        return (true, null);
    }

    // ─── Private Dapper mapping classes ───────────────────────────────────────

    private class SummaryDbRow
    {
        public string?   HardwareId     { get; set; }
        public string?   Label          { get; set; }
        public long?     DeviceIntId    { get; set; }
        public bool      HasPolling     { get; set; }
        public bool      HasCustomCreds { get; set; }
        public DateTime? RegisteredAt   { get; set; }
        public DateTime? LastTs         { get; set; }
        public double?   TemperatureC   { get; set; }
        public double?   HumidityPct    { get; set; }
        public double?   BatteryPct     { get; set; }
        public string?   TruckName      { get; set; }
    }

    private class DeviceDbRow
    {
        public string?   HardwareId   { get; set; }
        public long?     DeviceIntId  { get; set; }
        public string?   Label        { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public DateTime? CreatedAt    { get; set; }
    }

    private class CredentialsDbRow
    {
        public long    DeviceIntId { get; set; }
        public string? AppId       { get; set; }
        public string? AppKey      { get; set; }
        public string? AppSecret   { get; set; }
    }

    private class ActiveDeviceDbRow
    {
        public string?  HardwareId  { get; set; }
        public long     DeviceIntId { get; set; }
        public string?  AppId       { get; set; }
        public string?  AppKey      { get; set; }
        public string?  AppSecret   { get; set; }
        public string?  Timezone    { get; set; }
        public string?  Label       { get; set; }
    }

    private class RegisterLookupRow
    {
        public string? ActivationCode { get; set; }
        public int?    ExistingOrgId  { get; set; }
    }
}
