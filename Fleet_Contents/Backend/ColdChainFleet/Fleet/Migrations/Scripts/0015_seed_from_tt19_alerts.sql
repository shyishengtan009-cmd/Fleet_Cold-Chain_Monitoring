-- 0015: Seed iot.tt19_data from the legacy tt19_alerts table.
--
-- tt19_alerts was the primary storage table for the previous Fleet system,
-- combining sensor readings and alarm events into a single row. It is the
-- equivalent of cold_truck_alerts (0014) and holds the actual historical
-- readings for real (non-simulated) devices.
--
-- ON CONFLICT DO NOTHING is safe to re-run and will not overwrite any readings
-- already present in tt19_data (e.g. from 0014 or the new ingest service).
-- battery_pct cast integer → double precision to match tt19_data column type.
-- vibration_g and raw are left NULL (tt19_alerts has no equivalent columns).

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM   information_schema.tables
        WHERE  table_schema = 'iot'
          AND  table_name   = 'tt19_alerts'
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
        FROM  iot.tt19_alerts
        WHERE hardware_id IS NOT NULL
          AND ts          IS NOT NULL
        ORDER BY hardware_id, ts
        ON CONFLICT (hardware_id, ts) DO NOTHING;
    END IF;
END $$;
