-- Persist per-strata tax rates and per-category spending levels on countries.
-- Migration 016 (Day 81 / Week 17)
--
-- Until now, EducationSpending/MilitarySpending/AdministrationSpending and
-- PoorTaxRate/MiddleTaxRate/RichTaxRate lived only on the in-memory CountryState
-- and were re-seeded from the scenario on every load. This migration moves them
-- to the durable countries table so budget changes survive a restart.

ALTER TABLE countries
    ADD COLUMN IF NOT EXISTS poor_tax_rate DECIMAL(6,4) NOT NULL DEFAULT -1,
    ADD COLUMN IF NOT EXISTS middle_tax_rate DECIMAL(6,4) NOT NULL DEFAULT -1,
    ADD COLUMN IF NOT EXISTS rich_tax_rate DECIMAL(6,4) NOT NULL DEFAULT -1,
    ADD COLUMN IF NOT EXISTS education_spending DECIMAL(6,4) NOT NULL DEFAULT 0.5,
    ADD COLUMN IF NOT EXISTS military_spending DECIMAL(6,4) NOT NULL DEFAULT 0.5,
    ADD COLUMN IF NOT EXISTS administration_spending DECIMAL(6,4) NOT NULL DEFAULT 0.5;

INSERT INTO schema_versions (version_number, description)
VALUES (16, 'Country budget: per-strata tax rates and spending categories')
ON CONFLICT DO NOTHING;
