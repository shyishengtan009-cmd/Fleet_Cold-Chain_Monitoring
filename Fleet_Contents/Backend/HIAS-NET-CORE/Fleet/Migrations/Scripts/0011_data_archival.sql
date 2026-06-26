-- 0011: Monthly-partitioned sensor data archive + maintenance functions.
--
-- ── Why partition ─────────────────────────────────────────────────────────────
-- iot.tt19_data grows ~30M rows/year for a fleet of 100 devices (10 s polling).
-- Keeping all rows in one table causes O(N) query degradation after ~100M rows.
-- Solution: keep iot.tt19_data as a rolling 90-day live table; move older rows
-- into monthly partitions of iot.tt19_data_archive where they stay queryable
-- but don't slow down the poll-cycle hot path.
--
-- ── Architecture ──────────────────────────────────────────────────────────────
--   iot.tt19_data          → live table, 90-day rolling window (existing)
--   iot.tt19_data_archive  → RANGE-partitioned by ts (monthly)
--   archive_old_sensor_data(days) → moves rows in 5000-row batches (no long lock)
--   ensure_archive_partitions()   → creates next 3 months on demand
--   purge_old_archive(years)      → drops rows older than N years from archive
--
-- ── Partition naming ──────────────────────────────────────────────────────────
--   iot.tt19_data_archive_YYYY_MM  (e.g. iot.tt19_data_archive_2025_06)
--
-- ── Integration ───────────────────────────────────────────────────────────────
--   FleetDbCoreRepository.PurgeOldData() calls both archive functions hourly
--   (via DO $$ blocks so failures are silent if functions don't exist yet).

-- ── Archive parent table ──────────────────────────────────────────────────────
-- Mirrors iot.tt19_data schema (raw column was dropped in 0007).
-- id preserves the original BIGSERIAL value from tt19_data for auditability.

CREATE TABLE IF NOT EXISTS iot.tt19_data_archive (
    id            BIGINT           NOT NULL,
    hardware_id   TEXT             NOT NULL,
    ts            TIMESTAMPTZ      NOT NULL,
    temperature_c DOUBLE PRECISION,
    humidity_pct  DOUBLE PRECISION,
    light_lux     DOUBLE PRECISION,
    battery_pct   DOUBLE PRECISION,
    vibration_g   DOUBLE PRECISION,
    archived_at   TIMESTAMPTZ      NOT NULL DEFAULT NOW()
) PARTITION BY RANGE (ts);

-- Global index: propagates automatically to all child partitions (PG 10+).
CREATE INDEX IF NOT EXISTS idx_tt19_archive_hw_ts
    ON iot.tt19_data_archive (hardware_id, ts DESC);

-- ── Partition management function ────────────────────────────────────────────
-- Creates monthly child tables from 24 months ago through next 6 months.
-- Idempotent: skips months that already exist. Called at migration time and
-- from PurgeOldData so a new month's partition is always ready in advance.

CREATE OR REPLACE FUNCTION iot.ensure_archive_partitions()
RETURNS VOID LANGUAGE plpgsql AS $$
DECLARE
    first_month DATE := DATE_TRUNC('month', NOW()) - INTERVAL '24 months';
    last_month  DATE := DATE_TRUNC('month', NOW()) + INTERVAL '6 months';
    cur_month   DATE := first_month;
    next_month  DATE;
    part_name   TEXT;
BEGIN
    WHILE cur_month <= last_month LOOP
        next_month := cur_month + INTERVAL '1 month';
        part_name  := 'tt19_data_archive_' || TO_CHAR(cur_month, 'YYYY_MM');

        IF NOT EXISTS (
            SELECT 1 FROM pg_class c
            JOIN  pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'iot' AND c.relname = part_name
        ) THEN
            EXECUTE FORMAT(
                'CREATE TABLE iot.%I PARTITION OF iot.tt19_data_archive
                 FOR VALUES FROM (%L) TO (%L)',
                part_name, cur_month, next_month
            );
        END IF;

        cur_month := next_month;
    END LOOP;
END $$;

-- Create all needed partitions at migration time.
SELECT iot.ensure_archive_partitions();

-- ── Archival function ─────────────────────────────────────────────────────────
-- Moves rows older than days_to_keep from iot.tt19_data into iot.tt19_data_archive.
-- Processes in 5000-row batches using FOR UPDATE SKIP LOCKED so concurrent callers
-- don't compete and so no single transaction locks a large range of the live table.
-- Returns total rows archived (0 if nothing to do).

CREATE OR REPLACE FUNCTION iot.archive_old_sensor_data(days_to_keep INT DEFAULT 30)
RETURNS BIGINT LANGUAGE plpgsql AS $$
DECLARE
    cutoff     TIMESTAMPTZ := NOW() - (days_to_keep || ' days')::INTERVAL;
    batch_size INT         := 5000;
    archived   BIGINT      := 0;
    rows_moved INT;
BEGIN
    LOOP
        WITH moved AS (
            DELETE FROM iot.tt19_data
            WHERE id IN (
                SELECT id FROM iot.tt19_data
                WHERE  ts < cutoff
                ORDER  BY ts
                LIMIT  batch_size
                FOR UPDATE SKIP LOCKED
            )
            RETURNING id, hardware_id, ts, temperature_c, humidity_pct,
                      light_lux, battery_pct, vibration_g
        )
        INSERT INTO iot.tt19_data_archive
            (id, hardware_id, ts, temperature_c, humidity_pct,
             light_lux, battery_pct, vibration_g)
        SELECT id, hardware_id, ts, temperature_c, humidity_pct,
               light_lux, battery_pct, vibration_g
        FROM moved;

        GET DIAGNOSTICS rows_moved = ROW_COUNT;
        archived := archived + rows_moved;
        EXIT WHEN rows_moved < batch_size;
    END LOOP;

    RETURN archived;
END $$;

-- ── Compliance purge from archive ─────────────────────────────────────────────
-- Removes rows from the archive older than years_to_keep (default 7 years).
-- Partitioned DELETE is cheap — PostgreSQL can prune entire child tables.
-- Returns the number of rows deleted.

CREATE OR REPLACE FUNCTION iot.purge_old_archive(years_to_keep INT DEFAULT 7)
RETURNS BIGINT LANGUAGE plpgsql AS $$
DECLARE
    cutoff  TIMESTAMPTZ := NOW() - (years_to_keep || ' years')::INTERVAL;
    deleted BIGINT;
BEGIN
    DELETE FROM iot.tt19_data_archive WHERE ts < cutoff;
    GET DIAGNOSTICS deleted = ROW_COUNT;
    RETURN deleted;
END $$;
