-- Normalised GPS points table for in-progress trips.
-- Replaces the O(N²) jsonb_set append pattern: each new GPS point is now an
-- O(1) INSERT here instead of a full rewrite of the trips.trip_data JSONB blob.
-- CloseTrip reads from this table, merges with legacy trip_data, then clears it.

CREATE TABLE IF NOT EXISTS iot.trip_points (
    id            BIGSERIAL        PRIMARY KEY,
    trip_id       BIGINT           NOT NULL REFERENCES iot.trips(id) ON DELETE CASCADE,
    ts            TIMESTAMPTZ      NOT NULL,
    lat           DOUBLE PRECISION NOT NULL,
    lng           DOUBLE PRECISION NOT NULL,
    temperature_c DOUBLE PRECISION
);

-- Primary access pattern: all points for a trip ordered by time
CREATE INDEX IF NOT EXISTS idx_trip_points_trip_ts
    ON iot.trip_points (trip_id, ts ASC);
