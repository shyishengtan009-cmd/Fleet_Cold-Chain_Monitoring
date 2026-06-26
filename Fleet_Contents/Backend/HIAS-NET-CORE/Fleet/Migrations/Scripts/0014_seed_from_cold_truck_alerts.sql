-- 0014: Seed iot.tt19_data from the legacy cold_truck_alerts table.
--
-- cold_truck_alerts was the storage table for the previous Fleet system.
-- It combined sensor readings and alarm events into a single row, so every
-- row already contains a full sensor snapshot (temperature_c, humidity_pct,
-- light_lux, battery_pct).
--
-- The new ingest service writes readings to iot.tt19_data (separate from alarm
-- events). Without this migration, devices that were only ever recorded in
-- cold_truck_alerts appear invisible in the new Fleet status and history views
-- because those views INNER JOIN on iot.tt19_data.
--
-- This script copies the sensor columns from cold_truck_alerts into tt19_data.
-- vibration_g and raw are left NULL (cold_truck_alerts has no equivalent columns).
-- battery_pct is cast from integer → double precision to match tt19_data's type.
-- ON CONFLICT DO NOTHING ensures the migration is safe to re-run and will not
-- overwrite any readings already ingested by the new service.
--
-- Wrapped in a DO block so the script is safe on databases that never had the
-- old Fleet system (cold_truck_alerts does not exist → graceful no-op).

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM   information_schema.tables
        WHERE  table_schema = 'iot'
          AND  table_name   = 'cold_truck_alerts'
    ) THEN
        INSERT INTO iot.tt19_data
            (hardware_id, ts, temperature_c, humidity_pct, light_lux, battery_pct)
        SELECT DISTINCT ON (hardware_id, ts)
            hardware_id,
            ts,
            temperature_c,
            humidity_pct,
            light_lux,
            battery_pct::double precision
        FROM  iot.cold_truck_alerts
        WHERE hardware_id IS NOT NULL
          AND ts          IS NOT NULL
        ORDER BY hardware_id, ts
        ON CONFLICT (hardware_id, ts) DO NOTHING;
    END IF;
END $$;
