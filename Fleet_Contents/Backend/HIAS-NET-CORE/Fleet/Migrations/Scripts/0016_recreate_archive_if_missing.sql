-- 0016: Force-recreate iot.tt19_data_archive if it is missing on this database.
--
-- ── Why this migration exists ──────────────────────────────────────────────────
-- Migration 0011 created iot.tt19_data_archive (the rolling sensor-data archive),
-- but on at least one environment the table is missing even though 0011 is
-- recorded as applied in iot.schema_versions (e.g. the table was dropped after
-- the migration ran). Because FleetMigrationRunner skips any script already
-- recorded as applied, 0011 will never run again on that database — the table
-- stays missing forever without an explicit new migration.
--
-- This script is idempotent (CREATE TABLE/INDEX/FUNCTION IF NOT EXISTS /
-- OR REPLACE) so it is safe to run whether or not 0011 actually succeeded here.
-- It mirrors the table, index, and function definitions from 0011 exactly.

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

CREATE INDEX IF NOT EXISTS idx_tt19_archive_hw_ts
    ON iot.tt19_data_archive (hardware_id, ts DESC);

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

SELECT iot.ensure_archive_partitions();

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
