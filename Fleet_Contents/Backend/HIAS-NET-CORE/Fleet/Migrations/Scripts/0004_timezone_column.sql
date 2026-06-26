-- Per-device timezone support.
-- Stores IANA timezone IDs (e.g. "Asia/Kuala_Lumpur") so each device's alarm
-- schedule and notification timestamps use the correct local time.

ALTER TABLE iot.tt19_devices
    ADD COLUMN IF NOT EXISTS timezone TEXT NOT NULL DEFAULT 'Asia/Kuala_Lumpur';
