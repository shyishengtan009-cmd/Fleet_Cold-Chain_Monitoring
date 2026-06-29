-- Dwell detection state and named locations (depots, customer sites, etc.)

CREATE TABLE IF NOT EXISTS iot.tt19_dwell_state (
    id                 BIGSERIAL        PRIMARY KEY,
    hardware_id        TEXT             NOT NULL UNIQUE,
    anchor_lat         DOUBLE PRECISION,
    anchor_lng         DOUBLE PRECISION,
    dwell_since_utc    TIMESTAMPTZ,
    last_alert_sent_at TIMESTAMPTZ,
    updated_at         TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS iot.tt19_locations (
    id            BIGSERIAL        PRIMARY KEY,
    org_id        INTEGER          NOT NULL,
    name          TEXT             NOT NULL,
    lat           DOUBLE PRECISION NOT NULL,
    lng           DOUBLE PRECISION NOT NULL,
    radius_m      INTEGER          NOT NULL DEFAULT 200,
    max_dwell_min INTEGER,
    type          TEXT             NOT NULL DEFAULT 'other',
    created_at    TIMESTAMPTZ      NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS idx_tt19_locations_org
    ON iot.tt19_locations (org_id);
