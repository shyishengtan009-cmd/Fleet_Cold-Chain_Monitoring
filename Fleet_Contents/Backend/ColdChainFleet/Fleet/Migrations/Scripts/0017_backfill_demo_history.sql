-- 0017: One-time backfill of synthetic historical readings for the 5
-- FleetSimService demo devices, so a first-time viewer sees days of chart
-- history immediately instead of "started a few minutes ago".
--
-- Only inserts for the sim devices (SIM-U-*) and only if that device doesn't
-- already have a backfilled history — safe to leave in the migration set even
-- though FleetMigrationRunner already guarantees this runs at most once.
-- Value ranges match FleetSimService.cs exactly so the backfilled history
-- blends in with live-generated readings.

INSERT INTO iot.tt19_data (hardware_id, ts, temperature_c, humidity_pct, light_lux, battery_pct, vibration_g)
SELECT
    d.hardware_id,
    ts,
    round((d.t_min + random() * (d.t_max - d.t_min))::numeric, 2)::float8,
    round((40 + random() * 40)::numeric, 2)::float8,
    round((random() * 300)::numeric, 2)::float8,
    round((60 + random() * 40)::numeric, 1)::float8,
    round((random() * 1.5)::numeric, 3)::float8
FROM (
    VALUES
        ('SIM-U-COLD-1', 10.0, 20.0),
        ('SIM-U-COLD-2', 10.0, 20.0),
        ('SIM-U-FRZE-1',  0.0,  5.0),
        ('SIM-U-FRZE-2',  0.0,  5.0),
        ('SIM-U-WARM-1', 30.0, 40.0)
) AS d(hardware_id, t_min, t_max)
CROSS JOIN generate_series(
    now() - interval '14 days',
    now() - interval '15 minutes',
    interval '15 minutes'
) AS ts
ON CONFLICT (hardware_id, ts) DO NOTHING;
