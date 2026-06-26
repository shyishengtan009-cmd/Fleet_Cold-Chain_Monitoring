-- 0010: Add FK constraints for Fleet tables where referential integrity is safe.
--
-- Only device_settings and tt19_alarm_state get constraints:
--   • device_settings   — settings must belong to a real device; cascade on delete.
--   • tt19_alarm_state  — debounce state must belong to a real device; cascade on delete.
--
-- Historical tables (tt19_data, tt19_alarm_log, tt19_email_log, trips) deliberately
-- receive NO FK constraints — historical records must survive device re-registration
-- and org changes. Enforcing a FK there would block unregistering a device that has
-- any sensor history.
--
-- NOT VALID skips validation of existing rows (avoids a full table scan + lock on UAT).
-- To validate existing rows after the fact: VALIDATE CONSTRAINT <name>;
--
-- Wrapped in existence checks (unlike when this script was first written) so it's
-- safe to re-run on a database where these constraints already exist but the
-- schema_versions row recording this migration as applied is somehow missing
-- (manual intervention, a restored backup, etc.) — without this guard, a re-run
-- would fail with "constraint already exists" and permanently block every
-- migration numbered after this one.

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_device_settings_hwid'
    ) THEN
        ALTER TABLE iot.device_settings
            ADD CONSTRAINT fk_device_settings_hwid
            FOREIGN KEY (hardware_id) REFERENCES iot.tt19_devices(hardware_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'fk_alarm_state_hwid'
    ) THEN
        ALTER TABLE iot.tt19_alarm_state
            ADD CONSTRAINT fk_alarm_state_hwid
            FOREIGN KEY (hardware_id) REFERENCES iot.tt19_devices(hardware_id)
            ON DELETE CASCADE
            NOT VALID;
    END IF;
END $$;
