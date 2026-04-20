-- Player ownership mapping for authoritative command validation
CREATE TABLE IF NOT EXISTS player_accounts (
    actor_id UUID PRIMARY KEY,
    username VARCHAR(255) NOT NULL UNIQUE,
    controlled_country_id UUID NOT NULL REFERENCES countries(id),
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_player_accounts_controlled_country
    ON player_accounts(controlled_country_id);

INSERT INTO schema_versions (version_number, description)
VALUES (5, 'Player accounts ownership mapping')
ON CONFLICT DO NOTHING;
