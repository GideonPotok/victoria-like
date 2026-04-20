-- Initial schema for Victoria-Like world server
-- Run this on fresh PostgreSQL database

CREATE TABLE IF NOT EXISTS world_state (
    id SERIAL PRIMARY KEY,
    tick_number BIGINT NOT NULL,
    world_timestamp TIMESTAMP NOT NULL,
    last_saved_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Only one active world state row (enforced by application logic)
CREATE UNIQUE INDEX idx_world_state_singleton
    ON world_state((1))
    WHERE id IS NOT NULL;

-- Track schema version
CREATE TABLE IF NOT EXISTS schema_versions (
    id SERIAL PRIMARY KEY,
    version_number INT NOT NULL UNIQUE,
    description TEXT NOT NULL,
    applied_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

-- Log all commands for auditability (added in Day 11)
CREATE TABLE IF NOT EXISTS command_log (
    id SERIAL PRIMARY KEY,
    command_id UUID NOT NULL UNIQUE,
    actor_id UUID NOT NULL,
    command_type VARCHAR(255) NOT NULL,
    payload JSONB NOT NULL,
    issued_at TIMESTAMP NOT NULL,
    status VARCHAR(50) NOT NULL DEFAULT 'accepted', -- accepted, applied, rejected, failed
    result_reason VARCHAR(500),
    applied_tick BIGINT,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_command_log_actor ON command_log(actor_id);
CREATE INDEX idx_command_log_type ON command_log(command_type);
CREATE INDEX idx_command_log_status ON command_log(status);
CREATE INDEX idx_command_log_tick ON command_log(applied_tick);

-- Record schema version
INSERT INTO schema_versions (version_number, description)
VALUES (1, 'Initial schema: world_state, command_log')
ON CONFLICT DO NOTHING;
