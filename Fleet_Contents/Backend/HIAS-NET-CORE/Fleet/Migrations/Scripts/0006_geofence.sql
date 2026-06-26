-- Adds an index on iot.tt19_locations.type to make forbidden-zone queries fast.
-- The type column already exists as TEXT (values: depot|refuel|rest|customer|other).
-- 'forbidden' is a new valid value — a zone that fires an ALARM on any device entry.
-- No schema change is required; the index is all that's needed for performance.

CREATE INDEX IF NOT EXISTS ix_tt19_locations_type ON iot.tt19_locations (type);
