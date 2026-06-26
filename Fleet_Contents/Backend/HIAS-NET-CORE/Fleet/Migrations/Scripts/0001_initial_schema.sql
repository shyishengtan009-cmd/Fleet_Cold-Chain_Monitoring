-- Fleet initial schema: core tables
-- Safe on a fresh database or an existing one (all statements use IF NOT EXISTS).

CREATE SCHEMA IF NOT EXISTS iot;

CREATE TABLE IF NOT EXISTS iot.tt19_devices (
    id              BIGSERIAL    PRIMARY KEY,
    hardware_id     TEXT         NOT NULL UNIQUE,
    device_int_id   BIGINT,
    label           TEXT,
    activation_code TEXT         NOT NULL DEFAULT '',
    organization_id INTEGER,
    registered_at   TIMESTAMPTZ,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    is_active       BOOLEAN      NOT NULL DEFAULT TRUE,
    app_id          TEXT,
    app_key         TEXT,
    app_secret      TEXT
);

CREATE TABLE IF NOT EXISTS iot.tt19_data (
    id            BIGSERIAL    PRIMARY KEY,
    hardware_id   TEXT         NOT NULL,
    ts            TIMESTAMPTZ  NOT NULL,
    temperature_c DOUBLE PRECISION,
    humidity_pct  DOUBLE PRECISION,
    light_lux     DOUBLE PRECISION,
    battery_pct   DOUBLE PRECISION,
    vibration_g   DOUBLE PRECISION,
    raw           JSONB,
    UNIQUE (hardware_id, ts)
);

CREATE TABLE IF NOT EXISTS iot.device_settings (
    hardware_id  TEXT        PRIMARY KEY,
    alarm_json   JSONB       NOT NULL DEFAULT '{}',
    trip_json    JSONB       NOT NULL DEFAULT '{}',
    updated_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.device_settings_history (
    id           BIGSERIAL    PRIMARY KEY,
    hardware_id  TEXT         NOT NULL,
    alarm_json   JSONB        NOT NULL DEFAULT '{}',
    trip_json    JSONB        NOT NULL DEFAULT '{}',
    saved_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.tt19_alarm_state (
    id                 BIGSERIAL    PRIMARY KEY,
    hardware_id        TEXT         NOT NULL,
    sensor             TEXT         NOT NULL,
    is_alarming        BOOLEAN      NOT NULL DEFAULT FALSE,
    alarm_started_at   TIMESTAMPTZ,
    last_alarmed_at    TIMESTAMPTZ,
    last_email_sent_at TIMESTAMPTZ,
    consecutive_count  INTEGER      NOT NULL DEFAULT 0,
    updated_at         TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    UNIQUE (hardware_id, sensor)
);

CREATE TABLE IF NOT EXISTS iot.tt19_alarm_log (
    id            BIGSERIAL    PRIMARY KEY,
    hardware_id   TEXT         NOT NULL,
    ts            TIMESTAMPTZ,
    alarm_type    TEXT         NOT NULL DEFAULT '',
    field         TEXT         NOT NULL DEFAULT '',
    value         DOUBLE PRECISION,
    threshold     DOUBLE PRECISION,
    message       TEXT,
    notify_phone  TEXT,
    whatsapp_sent BOOLEAN      NOT NULL DEFAULT FALSE,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.tt19_email_log (
    id            BIGSERIAL    PRIMARY KEY,
    hardware_id   TEXT         NOT NULL,
    sensor        TEXT,
    to_email      TEXT,
    description   TEXT,
    success       BOOLEAN      NOT NULL DEFAULT FALSE,
    error_message TEXT,
    alarm_log_id  BIGINT,
    created_at    TIMESTAMPTZ  NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.trips (
    id                BIGSERIAL        PRIMARY KEY,
    hardware_id       TEXT             NOT NULL,
    start_time        TIMESTAMPTZ      NOT NULL,
    end_time          TIMESTAMPTZ,
    total_distance_km DOUBLE PRECISION NOT NULL DEFAULT 0,
    points_count      INTEGER          NOT NULL DEFAULT 0,
    trip_data         JSONB            NOT NULL DEFAULT '{"points":[]}',
    created_at        TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.trucks (
    id         BIGSERIAL PRIMARY KEY,
    truck_name TEXT      NOT NULL UNIQUE,
    plate      TEXT
);

CREATE TABLE IF NOT EXISTS iot.truck_sensors (
    id          BIGSERIAL PRIMARY KEY,
    truck_id    BIGINT    NOT NULL REFERENCES iot.trucks(id),
    hardware_id TEXT      NOT NULL UNIQUE,
    sensor_name TEXT
);
