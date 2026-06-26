-- 0009: Remove dead schema objects that were part of the original design but are no longer used.
--
-- iot.trucks and iot.truck_sensors were created in 0001 but Fleet now stores
-- truck_name inside device_settings.trip_json — these tables have never been populated.
--
-- notify_phone and whatsapp_sent in iot.tt19_alarm_log were created in 0001 but
-- WhatsApp integration was permanently removed. These columns have always been NULL/FALSE.
--
-- All statements use IF EXISTS so this migration is safe to re-run on any DB state.

DROP TABLE IF EXISTS iot.truck_sensors;
DROP TABLE IF EXISTS iot.trucks;

ALTER TABLE iot.tt19_alarm_log DROP COLUMN IF EXISTS notify_phone;
ALTER TABLE iot.tt19_alarm_log DROP COLUMN IF EXISTS whatsapp_sent;
