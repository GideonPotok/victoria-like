-- Day 43: deterministic command ordering and retry idempotency.
ALTER TABLE command_log
ADD COLUMN IF NOT EXISTS submitted_tick BIGINT NOT NULL DEFAULT 0,
ADD COLUMN IF NOT EXISTS expected_world_tick BIGINT,
ADD COLUMN IF NOT EXISTS idempotency_key VARCHAR(128);

CREATE INDEX IF NOT EXISTS idx_command_log_ordering
    ON command_log(submitted_tick, received_at, command_id);

CREATE UNIQUE INDEX IF NOT EXISTS idx_command_log_actor_idempotency
    ON command_log(actor_id, idempotency_key)
    WHERE idempotency_key IS NOT NULL;

INSERT INTO schema_versions (version_number, description)
VALUES (10, 'command_log: conflict metadata and idempotency key')
ON CONFLICT DO NOTHING;
