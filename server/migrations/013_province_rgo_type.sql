-- Province RGO type for Month 4 POP/economy setup
-- Migration 013

ALTER TABLE provinces
ADD COLUMN IF NOT EXISTS rgo_type VARCHAR(64) NOT NULL DEFAULT 'grain_farm';

INSERT INTO schema_versions (version_number, description)
VALUES (13, 'Province RGO type')
ON CONFLICT DO NOTHING;
