-- Auth: password hashing + session tokens for login flow

ALTER TABLE player_accounts
    ADD COLUMN IF NOT EXISTS password_hash TEXT NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS sessions (
    token      TEXT    PRIMARY KEY,
    actor_id   UUID    NOT NULL REFERENCES player_accounts(actor_id) ON DELETE CASCADE,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NOT NULL,
    last_activity_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX IF NOT EXISTS idx_sessions_actor_id ON sessions(actor_id);
CREATE INDEX IF NOT EXISTS idx_sessions_expires_at ON sessions(expires_at);

INSERT INTO schema_versions (version_number, description)
VALUES (8, 'Auth: password_hash + sessions table')
ON CONFLICT DO NOTHING;
