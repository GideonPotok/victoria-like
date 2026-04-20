-- Enrich command_log for full audit trail (Day 42)
ALTER TABLE command_log
ADD COLUMN IF NOT EXISTS country_id UUID,
ADD COLUMN IF NOT EXISTS target_ids JSONB,
ADD COLUMN IF NOT EXISTS rejection_reason_code VARCHAR(64);

-- Fast admin queries by country and rejection reason
CREATE INDEX IF NOT EXISTS idx_command_log_country ON command_log(country_id);
CREATE INDEX IF NOT EXISTS idx_command_log_rejection ON command_log(rejection_reason_code) WHERE rejection_reason_code IS NOT NULL;

INSERT INTO schema_versions (version_number, description)
VALUES (9, 'command_log: country_id, target_ids, rejection_reason_code')
ON CONFLICT DO NOTHING;
