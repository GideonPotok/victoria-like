-- Add outcome tracking to command_log table
ALTER TABLE command_log
ADD COLUMN IF NOT EXISTS outcome_status VARCHAR(50),
ADD COLUMN IF NOT EXISTS outcome_reason TEXT,
ADD COLUMN IF NOT EXISTS applied_at TIMESTAMP;

-- Create index for status filtering
CREATE INDEX IF NOT EXISTS idx_command_log_outcome_status ON command_log(outcome_status);
