-- Artisan production state and producer profit history
-- Migration 015

ALTER TABLE pop_groups
    ADD COLUMN IF NOT EXISTS artisan_produced_good VARCHAR(64),
    ADD COLUMN IF NOT EXISTS artisan_days_until_reconsider INT NOT NULL DEFAULT 0 CHECK (artisan_days_until_reconsider >= 0),
    ADD COLUMN IF NOT EXISTS artisan_last_reconsidered_at TIMESTAMP,
    ADD COLUMN IF NOT EXISTS artisan_profit_last_tick NUMERIC(18, 4) NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS good_profit_history (
    month_key VARCHAR(7) NOT NULL,
    good_id VARCHAR(64) NOT NULL,
    average_producer_profit NUMERIC(18, 4) NOT NULL DEFAULT 0,
    producer_count INT NOT NULL DEFAULT 0 CHECK (producer_count >= 0),
    PRIMARY KEY (month_key, good_id)
);

CREATE INDEX IF NOT EXISTS idx_good_profit_history_good ON good_profit_history(good_id);

INSERT INTO schema_versions (version_number, description)
VALUES (15, 'Artisans')
ON CONFLICT DO NOTHING;
