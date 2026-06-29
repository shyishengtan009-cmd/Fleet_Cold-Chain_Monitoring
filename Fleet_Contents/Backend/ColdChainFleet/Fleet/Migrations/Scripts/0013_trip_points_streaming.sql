-- Migration 0013: Trip GPS Streaming
--
-- After this migration, CloseTrip no longer compiles trip_points rows into a
-- trip_data JSONB blob and no longer deletes them.  iot.trip_points becomes
-- the authoritative GPS store for all trips (open and closed alike).
--
-- Legacy trips closed before 0013 already had their GPS compiled into trip_data
-- and their trip_points rows deleted.  GetTrip() falls back to the JSONB blob
-- for those trips automatically (trip_points empty → use trip_data).
--
-- trip_points rows are purged only via ON DELETE CASCADE when the parent
-- iot.trips row is deleted (PurgeOldData removes trips older than 2 years).

-- Ensure the composite index exists so trip_points lookups by trip stay fast.
-- Harmless if already present from migration 0005.
CREATE INDEX IF NOT EXISTS idx_trip_points_trip_ts
    ON iot.trip_points (trip_id, ts ASC);
