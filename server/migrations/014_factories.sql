-- Factory model v1 for Month 4 production
-- Migration 014

CREATE TABLE IF NOT EXISTS factories (
    id UUID PRIMARY KEY,
    country_id UUID NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    province_id UUID REFERENCES provinces(id) ON DELETE SET NULL,
    type VARCHAR(64) NOT NULL,
    level INT NOT NULL DEFAULT 1,
    employed_craftsmen INT NOT NULL DEFAULT 0,
    employed_clerks INT NOT NULL DEFAULT 0,
    input_goods JSONB NOT NULL DEFAULT '{}',
    output_good VARCHAR(64) NOT NULL,
    output_per_tick NUMERIC(18, 4) NOT NULL DEFAULT 0,
    cash_reserve NUMERIC(18, 2) NOT NULL DEFAULT 0,
    profit_last_tick NUMERIC(18, 2) NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_factories_country ON factories(country_id);
CREATE INDEX IF NOT EXISTS idx_factories_province ON factories(province_id);
CREATE INDEX IF NOT EXISTS idx_factories_type ON factories(type);

INSERT INTO schema_versions (version_number, description)
VALUES (14, 'Factories')
ON CONFLICT DO NOTHING;
