-- Performance indexes for the core Fleet tables.

-- Speed up GetLatestRow and GetRowsForRange (called every poll cycle)
CREATE INDEX IF NOT EXISTS idx_tt19_data_hw_ts
    ON iot.tt19_data (hardware_id, ts DESC);

-- Speed up GetActiveTripForDevice and fleet overview queries
CREATE INDEX IF NOT EXISTS idx_tt19_trips_hw
    ON iot.trips (hardware_id);

-- Speed up GetDeviceSettings (called on every alarm evaluation cycle)
CREATE INDEX IF NOT EXISTS idx_tt19_device_settings_hw
    ON iot.device_settings (hardware_id);
