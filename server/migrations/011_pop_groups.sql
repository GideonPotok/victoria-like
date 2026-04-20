-- Persistent POP groups for Month 4 Vic 2 substrate
-- Migration 011

CREATE TABLE IF NOT EXISTS pop_groups (
    id UUID PRIMARY KEY,
    province_id UUID NOT NULL REFERENCES provinces(id) ON DELETE CASCADE,
    size INT NOT NULL CHECK (size >= 0),
    pop_type VARCHAR(64) NOT NULL,
    strata VARCHAR(32) NOT NULL DEFAULT 'poor' CHECK (strata IN ('poor', 'middle', 'rich')),
    culture VARCHAR(128) NOT NULL,
    religion VARCHAR(128) NOT NULL,
    literacy DECIMAL(6, 4) NOT NULL DEFAULT 0 CHECK (literacy >= 0 AND literacy <= 1),
    militancy DECIMAL(6, 4) NOT NULL DEFAULT 0 CHECK (militancy >= 0 AND militancy <= 10),
    consciousness DECIMAL(6, 4) NOT NULL DEFAULT 0 CHECK (consciousness >= 0 AND consciousness <= 10),
    cash DECIMAL(18, 2) NOT NULL DEFAULT 0,
    life_needs_fulfillment DECIMAL(6, 4) NOT NULL DEFAULT 1 CHECK (life_needs_fulfillment >= 0 AND life_needs_fulfillment <= 1),
    everyday_needs_fulfillment DECIMAL(6, 4) NOT NULL DEFAULT 1 CHECK (everyday_needs_fulfillment >= 0 AND everyday_needs_fulfillment <= 1),
    luxury_needs_fulfillment DECIMAL(6, 4) NOT NULL DEFAULT 0 CHECK (luxury_needs_fulfillment >= 0 AND luxury_needs_fulfillment <= 1),
    employed_count INT NOT NULL DEFAULT 0 CHECK (employed_count >= 0),
    unemployed_count INT NOT NULL DEFAULT 0 CHECK (unemployed_count >= 0),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CHECK (employed_count + unemployed_count <= size)
);

CREATE INDEX IF NOT EXISTS idx_pop_groups_province ON pop_groups(province_id);
CREATE INDEX IF NOT EXISTS idx_pop_groups_type ON pop_groups(pop_type);
CREATE INDEX IF NOT EXISTS idx_pop_groups_culture ON pop_groups(culture);

INSERT INTO schema_versions (version_number, description)
VALUES (11, 'Persistent POP groups')
ON CONFLICT DO NOTHING;
