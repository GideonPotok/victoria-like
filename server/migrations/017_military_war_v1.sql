-- Week 20: basic army stacks, movement, battle v1, and war/peace state.

CREATE TABLE IF NOT EXISTS wars (
    id UUID PRIMARY KEY,
    attacker_country_id UUID NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    defender_country_id UUID NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    started_at TIMESTAMP NOT NULL,
    ended_at TIMESTAMP NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT wars_distinct_countries CHECK (attacker_country_id <> defender_country_id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_wars_one_active_pair
    ON wars (
        LEAST(attacker_country_id, defender_country_id),
        GREATEST(attacker_country_id, defender_country_id)
    )
    WHERE is_active;

CREATE TABLE IF NOT EXISTS army_stacks (
    id UUID PRIMARY KEY,
    country_id UUID NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    location_province_id UUID NOT NULL REFERENCES provinces(id) ON DELETE CASCADE,
    destination_province_id UUID NULL REFERENCES provinces(id) ON DELETE SET NULL,
    movement_ticks_remaining INT NOT NULL DEFAULT 0,
    soldier_count INT NOT NULL DEFAULT 0,
    morale DECIMAL(6,4) NOT NULL DEFAULT 1,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT army_stacks_nonnegative_movement CHECK (movement_ticks_remaining >= 0),
    CONSTRAINT army_stacks_nonnegative_soldiers CHECK (soldier_count >= 0),
    CONSTRAINT army_stacks_morale_bounds CHECK (morale >= 0 AND morale <= 1)
);

CREATE INDEX IF NOT EXISTS idx_army_stacks_country ON army_stacks(country_id);
CREATE INDEX IF NOT EXISTS idx_army_stacks_location ON army_stacks(location_province_id);

CREATE TABLE IF NOT EXISTS battle_reports (
    id TEXT PRIMARY KEY,
    war_id UUID NOT NULL REFERENCES wars(id) ON DELETE CASCADE,
    province_id UUID NOT NULL REFERENCES provinces(id) ON DELETE CASCADE,
    winner_army_id UUID NOT NULL,
    loser_army_id UUID NOT NULL,
    winner_country_id UUID NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    loser_country_id UUID NOT NULL REFERENCES countries(id) ON DELETE CASCADE,
    occurred_at TIMESTAMP NOT NULL,
    winner_casualties INT NOT NULL DEFAULT 0,
    loser_casualties INT NOT NULL DEFAULT 0,
    winner_morale_after DECIMAL(6,4) NOT NULL DEFAULT 0,
    loser_morale_after DECIMAL(6,4) NOT NULL DEFAULT 0,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT battle_reports_nonnegative_winner_casualties CHECK (winner_casualties >= 0),
    CONSTRAINT battle_reports_nonnegative_loser_casualties CHECK (loser_casualties >= 0),
    CONSTRAINT battle_reports_winner_morale_bounds CHECK (winner_morale_after >= 0 AND winner_morale_after <= 1),
    CONSTRAINT battle_reports_loser_morale_bounds CHECK (loser_morale_after >= 0 AND loser_morale_after <= 1)
);

CREATE INDEX IF NOT EXISTS idx_battle_reports_war ON battle_reports(war_id);
CREATE INDEX IF NOT EXISTS idx_battle_reports_province ON battle_reports(province_id);

INSERT INTO schema_versions (version_number, description)
VALUES (17, 'Military v1: army stacks, movement, and war state')
ON CONFLICT DO NOTHING;
