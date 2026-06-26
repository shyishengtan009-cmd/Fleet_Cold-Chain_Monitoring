-- 0012: Phase 2 indexes — JSONB GIN, covering, and partial.
--
-- Fills gaps not covered by 0002_indexes.sql:
--
--   GIN on device_settings JSONB  → fast filtering on alarm fields (email_enabled,
--                                    debounce_count, daily_start, etc.) without
--                                    full table scan.
--
--   Covering index on trips        → avoids heap fetches for fleet overview + trip
--                                    list queries (include end_time, distance, count).
--
--   Partial index on open trips    → GetActiveTripForDevice is called every poll
--                                    cycle; open trips are <10 rows at any time.
--
--   Alarm log covering index       → alert history page queries use hardware_id +
--                                    ts DESC; INCLUDE avoids a second heap fetch.
--
--   Device org+active index        → fleet overview filters by org + is_active.

-- GIN indexes for JSONB field access in device_settings
CREATE INDEX IF NOT EXISTS idx_device_settings_alarm_json_gin
    ON iot.device_settings USING GIN (alarm_json);

CREATE INDEX IF NOT EXISTS idx_device_settings_trip_json_gin
    ON iot.device_settings USING GIN (trip_json);

-- Covering index for trip list and overview queries
-- Avoids heap fetch when only these columns are needed (common case)
CREATE INDEX IF NOT EXISTS idx_trips_hw_start_covering
    ON iot.trips (hardware_id, start_time DESC)
    INCLUDE (end_time, total_distance_km, points_count);

-- Partial index on open trips — only rows where end_time IS NULL qualify.
-- GetActiveTripForDevice is on the hot path (every poll cycle per device).
-- A partial index on a handful of rows is nearly constant-time.
CREATE INDEX IF NOT EXISTS idx_trips_open
    ON iot.trips (hardware_id)
    WHERE end_time IS NULL;

-- Covering alarm log index for alert history page
-- AlertPage requests GET /api/fleet/alarms?hw=X ordered by ts DESC
CREATE INDEX IF NOT EXISTS idx_alarm_log_hw_ts_covering
    ON iot.tt19_alarm_log (hardware_id, ts DESC)
    INCLUDE (alarm_type, field, value, threshold, message);

-- Device registry index for org-scoped queries
CREATE INDEX IF NOT EXISTS idx_devices_org_active
    ON iot.tt19_devices (organization_id, is_active)
    WHERE is_active = TRUE;

-- Email log index for correlation with alarm log (M1 audit fix from 0010)
CREATE INDEX IF NOT EXISTS idx_email_log_hw_created
    ON iot.tt19_email_log (hardware_id, created_at DESC);
