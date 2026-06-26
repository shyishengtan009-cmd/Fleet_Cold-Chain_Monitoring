ALTER TABLE iot.trips
  ADD COLUMN IF NOT EXISTS sla_minutes         INT,
  ADD COLUMN IF NOT EXISTS minutes_late        INT,
  ADD COLUMN IF NOT EXISTS breach_minutes_hot  INT,
  ADD COLUMN IF NOT EXISTS breach_minutes_cold INT;
