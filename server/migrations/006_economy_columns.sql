-- Economy columns: province outputs/needs, country treasury, market supply/demand
-- Migration 006

ALTER TABLE provinces
    ADD COLUMN IF NOT EXISTS outputs_per_tick JSONB NOT NULL DEFAULT '{}',
    ADD COLUMN IF NOT EXISTS needs_fulfillment DECIMAL(5,4) NOT NULL DEFAULT 1.0;

ALTER TABLE countries
    ADD COLUMN IF NOT EXISTS treasury DECIMAL(14,2) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS tariff_rate DECIMAL(5,2) NOT NULL DEFAULT 0;

ALTER TABLE market_goods
    ADD COLUMN IF NOT EXISTS supply DECIMAL(12,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS demand DECIMAL(12,4) NOT NULL DEFAULT 0;

INSERT INTO schema_versions (version_number, description)
VALUES (6, 'Economy columns: province outputs/needs, country treasury, market supply/demand')
ON CONFLICT DO NOTHING;
