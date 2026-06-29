using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using FleetCore.Context; // DatabaseContext
using Npgsql;

namespace FleetCore.Repositories;

/// <summary>
/// Core database utilities shared by ALL Fleet database classes.
///
/// ── What this file does ───────────────────────────────────────────────────────
/// Every time a Fleet class needs to talk to PostgreSQL, it calls
/// FleetDbCoreRepository.OpenConnection() to get a ready NpgsqlConnection.
/// All SQL lives in the specific database files (FleetDbRealtimeRepository, FleetDbTripsRepository, etc.).
/// This file only handles:
///   - Storing the connection string (set once at startup)
///   - Opening a connection
///   - Creating performance indexes
///   - Small date/time and humidity helper methods shared by multiple files
///
/// ── Where the connection string comes from ────────────────────────────────────
/// All Fleet repos receive DatabaseContext via DI. DatabaseContext reads from
/// appsettings.json → DatabaseSettingsDev (bound via IOptions<DatabaseSettings>
/// in Program.cs) and builds an Npgsql connection string from those fields.
/// NEVER hardcode credentials in this file.
///
/// ── Database schema note ──────────────────────────────────────────────────────
/// All Fleet tables live in the PostgreSQL "iot" schema (e.g. iot.tt19_data).
/// Table names are NOT renamed here because that would require a DB migration.
/// Only the C# code names change.
/// </summary>
public static class FleetDbCoreRepository
{
    // ─── EnsureTables ─────────────────────────────────────────────────────────

    /// <summary>
    /// Creates all Fleet tables if they do not already exist.
    /// Superseded by FleetMigrationRunner — do NOT call this.
    /// Use FleetMigrationRunner.RunAsync() (called automatically by FleetIngestService at startup).
    /// </summary>
    [Obsolete("Use FleetMigrationRunner.RunAsync() instead. EnsureTables bypasses migration versioning and may create tables in a state incompatible with numbered migration scripts.")]
    public static async Task EnsureTables(DatabaseContext databaseContext)
    {
        const string sql = @"
CREATE SCHEMA IF NOT EXISTS iot;

CREATE TABLE IF NOT EXISTS iot.tt19_devices (
    id              BIGSERIAL    PRIMARY KEY,
    hardware_id     TEXT         NOT NULL UNIQUE,
    device_int_id   BIGINT,
    label           TEXT,
    activation_code TEXT         NOT NULL DEFAULT '',
    organization_id INTEGER,
    registered_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    app_id          TEXT,
    app_key         TEXT,
    app_secret      TEXT
);

CREATE TABLE IF NOT EXISTS iot.tt19_data (
    id            BIGSERIAL    PRIMARY KEY,
    hardware_id   TEXT         NOT NULL,
    ts            TIMESTAMPTZ  NOT NULL,
    temperature_c DOUBLE PRECISION,
    humidity_pct  DOUBLE PRECISION,
    light_lux     DOUBLE PRECISION,
    battery_pct   DOUBLE PRECISION,
    vibration_g   DOUBLE PRECISION,
    raw           JSONB,
    UNIQUE (hardware_id, ts)
);

CREATE TABLE IF NOT EXISTS iot.device_settings (
    hardware_id  TEXT         PRIMARY KEY,
    alarm_json   JSONB        NOT NULL DEFAULT '{}',
    trip_json    JSONB        NOT NULL DEFAULT '{}',
    updated_at   TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.device_settings_history (
    id           BIGSERIAL    PRIMARY KEY,
    hardware_id  TEXT         NOT NULL,
    alarm_json   JSONB        NOT NULL DEFAULT '{}',
    trip_json    JSONB        NOT NULL DEFAULT '{}',
    saved_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.tt19_alarm_state (
    id                 BIGSERIAL    PRIMARY KEY,
    hardware_id        TEXT         NOT NULL,
    sensor             TEXT         NOT NULL,
    is_alarming        BOOLEAN      NOT NULL DEFAULT FALSE,
    alarm_started_at   TIMESTAMPTZ,
    last_alarmed_at    TIMESTAMPTZ,
    last_email_sent_at TIMESTAMPTZ,
    consecutive_count  INTEGER      NOT NULL DEFAULT 0,
    updated_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (hardware_id, sensor)
);

CREATE TABLE IF NOT EXISTS iot.tt19_alarm_log (
    id            BIGSERIAL    PRIMARY KEY,
    hardware_id   TEXT         NOT NULL,
    ts            TIMESTAMPTZ,
    alarm_type    TEXT         NOT NULL DEFAULT '',
    field         TEXT         NOT NULL DEFAULT '',
    value         DOUBLE PRECISION,
    threshold     DOUBLE PRECISION,
    message       TEXT,
    notify_phone  TEXT,
    whatsapp_sent BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.tt19_email_log (
    id            BIGSERIAL    PRIMARY KEY,
    hardware_id   TEXT         NOT NULL,
    sensor        TEXT,
    to_email      TEXT,
    description   TEXT,
    success       BOOLEAN      NOT NULL DEFAULT FALSE,
    error_message TEXT,
    alarm_log_id  BIGINT,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.trips (
    id                BIGSERIAL    PRIMARY KEY,
    hardware_id       TEXT         NOT NULL,
    start_time        TIMESTAMPTZ  NOT NULL,
    end_time          TIMESTAMPTZ,
    total_distance_km DOUBLE PRECISION NOT NULL DEFAULT 0,
    points_count      INTEGER      NOT NULL DEFAULT 0,
    trip_data         JSONB        NOT NULL DEFAULT '{""points"":[]}',
    created_at        TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.trucks (
    id          BIGSERIAL    PRIMARY KEY,
    truck_name  TEXT         NOT NULL UNIQUE,
    plate       TEXT
);

CREATE TABLE IF NOT EXISTS iot.truck_sensors (
    id           BIGSERIAL    PRIMARY KEY,
    truck_id     BIGINT       NOT NULL REFERENCES iot.trucks(id),
    hardware_id  TEXT         NOT NULL UNIQUE,
    sensor_name  TEXT
);

-- Column migrations: safe to run on existing tables
ALTER TABLE iot.tt19_data ADD COLUMN IF NOT EXISTS vibration_g DOUBLE PRECISION;
ALTER TABLE iot.tt19_devices ADD COLUMN IF NOT EXISTS timezone TEXT NOT NULL DEFAULT 'Asia/Kuala_Lumpur';

CREATE TABLE IF NOT EXISTS iot.tt19_dwell_state (
    id                 BIGSERIAL    PRIMARY KEY,
    hardware_id        TEXT         NOT NULL UNIQUE,
    anchor_lat         DOUBLE PRECISION,
    anchor_lng         DOUBLE PRECISION,
    dwell_since_utc    TIMESTAMPTZ,
    last_alert_sent_at TIMESTAMPTZ,
    updated_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.tt19_locations (
    id            BIGSERIAL    PRIMARY KEY,
    org_id        INTEGER      NOT NULL,
    name          TEXT         NOT NULL,
    lat           DOUBLE PRECISION NOT NULL,
    lng           DOUBLE PRECISION NOT NULL,
    radius_m      INTEGER      NOT NULL DEFAULT 200,
    max_dwell_min INTEGER,
    type          TEXT         NOT NULL DEFAULT 'other',
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tt19_locations_org
    ON iot.tt19_locations (org_id);

-- Live GPS points for in-progress trips — replaces O(N²) jsonb_set append
CREATE TABLE IF NOT EXISTS iot.trip_points (
    id            BIGSERIAL    PRIMARY KEY,
    trip_id       BIGINT       NOT NULL REFERENCES iot.trips(id) ON DELETE CASCADE,
    ts            TIMESTAMPTZ  NOT NULL,
    lat           DOUBLE PRECISION NOT NULL,
    lng           DOUBLE PRECISION NOT NULL,
    temperature_c DOUBLE PRECISION
);
";
        using var conn = (NpgsqlConnection)databaseContext.CreateConnection();
        await conn.OpenAsync();
        using var cmd  = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── EnsureIndexes ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates performance indexes on the existing Fleet tables.
    /// Safe to call every time the service starts — all indexes use IF NOT EXISTS.
    ///
    /// Superseded by FleetMigrationRunner (scripts 0002_indexes.sql, 0005_trip_points.sql).
    /// FleetIngestService now calls FleetMigrationRunner.RunAsync() instead.
    /// Kept for reference and emergency manual use only.
    /// </summary>
    public static async Task EnsureIndexes(DatabaseContext databaseContext)
    {
        const string sql = @"
-- Speed up GetLatestRowForHardware and GetRowsForHardwareRange (called every poll cycle)
CREATE INDEX IF NOT EXISTS idx_tt19_data_hw_ts
    ON iot.tt19_data (hardware_id, ts DESC);

-- Speed up GetActiveTripForDevice and fleet overview queries
CREATE INDEX IF NOT EXISTS idx_tt19_trips_hw
    ON iot.trips (hardware_id);

-- Speed up GetDeviceSettings — called on every alarm evaluation cycle
CREATE INDEX IF NOT EXISTS idx_tt19_device_settings_hw
    ON iot.device_settings (hardware_id);

-- Speed up AppendTripPoint and CloseTrip reads
CREATE INDEX IF NOT EXISTS idx_trip_points_trip_ts
    ON iot.trip_points (trip_id, ts ASC);
";
        using var conn = (NpgsqlConnection)databaseContext.CreateConnection();
        await conn.OpenAsync();
        using var cmd  = new NpgsqlCommand(sql, conn);
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── PurgeOldData ─────────────────────────────────────────────────────────

    /// <summary>
    /// Deletes rows older than the retention window from Fleet tables.
    /// Called once per hour from FleetIngestService to prevent unbounded growth.
    ///
    ///   iot.tt19_data          — 90 days  (sensor readings; oldest data has low value)
    ///   iot.tt19_alarm_log     — 365 days (alarm history kept 1 year for compliance)
    ///   iot.tt19_email_log     — 365 days
    ///   iot.device_settings_history — 365 days
    /// </summary>
    public static async Task PurgeOldData(DatabaseContext databaseContext)
    {
        const string sql = @"
-- Ensure next 3 monthly archive partitions exist before archiving
DO $$ BEGIN PERFORM iot.ensure_archive_partitions(); EXCEPTION WHEN undefined_function THEN NULL; END $$;

-- Archive sensor readings older than 15 days into iot.tt19_data_archive (partitioned).
-- Runs only when migration 0011 has been applied; silent no-op otherwise.
-- (15 days, not the original 30 — this demo runs on a free-tier database with a
-- 0.5GB storage cap; see migration 0018 for why density needs to shrink sooner.)
DO $$ BEGIN PERFORM iot.archive_old_sensor_data(15); EXCEPTION WHEN undefined_function THEN NULL; END $$;

-- Downsample archived data older than 15 days to at most 42 rows/device/day,
-- evenly spread across the day. Runs only when migration 0018 has been applied.
DO $$ BEGIN PERFORM iot.downsample_archive(15, 42); EXCEPTION WHEN undefined_function THEN NULL; END $$;

-- Safety net: delete any sensor readings older than 90 days not yet archived
DELETE FROM iot.tt19_data
WHERE ts < NOW() - INTERVAL '90 days';

-- Alarm + email logs — 1 year for compliance
DELETE FROM iot.tt19_alarm_log
WHERE created_at < NOW() - INTERVAL '365 days';

DELETE FROM iot.tt19_email_log
WHERE created_at < NOW() - INTERVAL '365 days';

-- Settings history — 1 year
DELETE FROM iot.device_settings_history
WHERE saved_at < NOW() - INTERVAL '365 days';

-- Completed trips older than 2 years (trip_data JSONB and trip_points go with it via CASCADE)
DELETE FROM iot.trips
WHERE end_time IS NOT NULL AND end_time < NOW() - INTERVAL '730 days';

-- Dwell state for devices that are no longer active — prevents stale anchor accumulation
DELETE FROM iot.tt19_dwell_state
WHERE hardware_id NOT IN (
    SELECT hardware_id FROM iot.tt19_devices WHERE is_active = TRUE
);

-- Geofence alarm state rows for deleted locations (key pattern 'geofence_%')
DELETE FROM iot.tt19_alarm_state
WHERE sensor LIKE 'geofence_%'
  AND CAST(SUBSTRING(sensor FROM 10) AS BIGINT) NOT IN (
      SELECT id FROM iot.tt19_locations
  );

-- Purge archive rows older than 7 years (compliance window)
DO $$ BEGIN PERFORM iot.purge_old_archive(7); EXCEPTION WHEN undefined_function THEN NULL; END $$;
";
        using var conn = (NpgsqlConnection)databaseContext.CreateConnection();
        await conn.OpenAsync();
        using var cmd = new NpgsqlCommand(sql, conn);
        cmd.CommandTimeout = 180; // large deletes can be slow
        await cmd.ExecuteNonQueryAsync();
    }

    // ─── Shared helper methods ────────────────────────────────────────────────

    /// <summary>
    /// Converts a humidity value to a percentage (0–100 scale).
    ///
    /// Why this is needed:
    ///   Some TZone sensors report humidity as a fraction (e.g. 0.65 = 65%).
    ///   Others report it already as a percentage (e.g. 65.0 = 65%).
    ///   This method normalises both formats to 0–100%.
    /// </summary>
    public static double? HumidityToPct(object? value)
    {
        if (value is null) return null;
        try
        {
            var v = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return v <= 1.0 ? v * 100.0 : v;   // 0.65 → 65.0,  65.0 → 65.0
        }
        catch { return null; }
    }

    /// <summary>
    /// Extracts the reading timestamp from a TZone sensor body dictionary.
    ///
    /// TZone sends timestamps as Unix epoch seconds in either "createTime" or "rtc".
    /// If neither is present (unexpected API format), falls back to DateTime.UtcNow.
    /// </summary>
    public static DateTime TsFromBody(IDictionary<string, object?> body)
    {
        foreach (var key in new[] { "createTime", "rtc" })
        {
            if (body.TryGetValue(key, out var v) && v is not null)
            {
                try
                {
                    var unix = Convert.ToInt64(v, CultureInfo.InvariantCulture);
                    return DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime;
                }
                catch { /* ignore — try next key */ }
            }
        }
        return DateTime.UtcNow;
    }

    /// <summary>
    /// Converts a DateTime to an ISO-8601 UTC string (e.g. "2026-03-01T08:00:00.0000000Z").
    /// Always marks the kind as UTC before formatting.
    /// </summary>
    public static string ToIsoUtc(DateTime dt)
        => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
                   .ToString("o", CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts a DB object (DateTime or string) to an ISO-8601 UTC string.
    /// Returns null if the input is null or DBNull.
    /// Used when reading DateTime columns from Npgsql — they may come back as
    /// unspecified-kind DateTime values that need to be tagged as UTC first.
    /// </summary>
    public static string? ObjToIsoUtc(object? dtObj)
    {
        if (dtObj is DateTime dt)
        {
            if (dt.Kind == DateTimeKind.Unspecified)
                dt = DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            return ToIsoUtc(dt.ToUniversalTime());
        }
        return dtObj?.ToString();
    }
}
