CREATE TABLE IF NOT EXISTS building_queue (
    id UUID PRIMARY KEY,
    province_id UUID NOT NULL REFERENCES provinces(id),
    country_id UUID NOT NULL REFERENCES countries(id),
    building_type TEXT NOT NULL,
    ticks_remaining INT NOT NULL,
    queued_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

INSERT INTO schema_versions (version_number, description)
VALUES (7, 'Building queue table for construction commands')
ON CONFLICT DO NOTHING;
