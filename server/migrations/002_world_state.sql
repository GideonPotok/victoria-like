-- World state tables for countries, provinces, and markets
-- Migration 002

CREATE TABLE IF NOT EXISTS countries (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    tag VARCHAR(3) NOT NULL UNIQUE,
    tax_rate INT NOT NULL DEFAULT 10 CHECK (tax_rate >= 0 AND tax_rate <= 100),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_countries_tag ON countries(tag);

CREATE TABLE IF NOT EXISTS markets (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL UNIQUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS market_goods (
    id SERIAL PRIMARY KEY,
    market_id UUID NOT NULL REFERENCES markets(id) ON DELETE CASCADE,
    good_name VARCHAR(255) NOT NULL,
    price DECIMAL(10, 2) NOT NULL,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    UNIQUE(market_id, good_name)
);

CREATE INDEX idx_market_goods_market ON market_goods(market_id);

CREATE TABLE IF NOT EXISTS provinces (
    id UUID PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    owner_id UUID NOT NULL REFERENCES countries(id),
    market_id UUID NOT NULL REFERENCES markets(id),
    population INT NOT NULL DEFAULT 1000 CHECK (population >= 100),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_provinces_owner ON provinces(owner_id);
CREATE INDEX idx_provinces_market ON provinces(market_id);
CREATE INDEX idx_provinces_name ON provinces(name);

-- Record schema version
INSERT INTO schema_versions (version_number, description)
VALUES (2, 'World state: countries, markets, provinces')
ON CONFLICT DO NOTHING;
