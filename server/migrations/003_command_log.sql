-- Add missing columns to command_log (migration 001 created the base table)
ALTER TABLE command_log
ADD COLUMN IF NOT EXISTS received_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
ADD COLUMN IF NOT EXISTS result_reason VARCHAR(500);

CREATE INDEX IF NOT EXISTS idx_command_log_received_at ON command_log(received_at);
