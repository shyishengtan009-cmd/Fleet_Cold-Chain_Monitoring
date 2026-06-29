-- 0018: Downsample old archived sensor data so storage stays bounded on a
-- free-tier database (this demo runs on Neon's 0.5GB free plan), without
-- ever losing the ability to show a long-term trend.
--
-- ── Why ─────────────────────────────────────────────────────────────────────
-- FleetSimService writes 5 devices' worth of readings every 30s. Migration
-- 0011's archive/purge logic was sized for a real fleet on a paid database —
-- it keeps 7 years of full-density archived data, which alone would exceed a
-- 0.5GB free-tier cap within a year or two for this demo. Pure age-based
-- deletion isn't enough; density needs to shrink as data ages.
--
-- ── What this does ──────────────────────────────────────────────────────────
-- For archived rows older than `days_old`, keeps at most `keep_per_day` rows
-- per (hardware_id, calendar day), evenly spread across whatever hours that
-- day actually had data — not just the earliest N. Days with fewer rows than
-- the cap are left untouched entirely (NTILE naturally gives every row its
-- own bucket when there are fewer rows than buckets, so nothing is deleted).
--
-- ── How ─────────────────────────────────────────────────────────────────────
-- NTILE(keep_per_day) splits each (device, day) group into up to keep_per_day
-- buckets as evenly as possible by time order, then one representative row
-- (the earliest in each bucket) is kept and everything else in that day is
-- deleted. This is the standard time-series "downsampling" pattern.

CREATE OR REPLACE FUNCTION iot.downsample_archive(
    days_old     INT DEFAULT 15,
    keep_per_day INT DEFAULT 42
)
RETURNS BIGINT LANGUAGE plpgsql AS $$
DECLARE
    cutoff  TIMESTAMPTZ := NOW() - (days_old || ' days')::INTERVAL;
    deleted BIGINT;
BEGIN
    WITH bucketed AS (
        SELECT id, hardware_id, DATE_TRUNC('day', ts) AS day, ts,
               NTILE(keep_per_day) OVER (
                   PARTITION BY hardware_id, DATE_TRUNC('day', ts)
                   ORDER BY ts
               ) AS bucket
        FROM iot.tt19_data_archive
        WHERE ts < cutoff
    ),
    keepers AS (
        SELECT (ARRAY_AGG(id ORDER BY ts))[1] AS keep_id
        FROM bucketed
        GROUP BY hardware_id, day, bucket
    )
    DELETE FROM iot.tt19_data_archive
    WHERE ts < cutoff
      AND id NOT IN (SELECT keep_id FROM keepers);

    GET DIAGNOSTICS deleted = ROW_COUNT;
    RETURN deleted;
END;
$$;
