-- Explicit POP strata for poor/middle/rich taxation and mobility
-- Migration 012

ALTER TABLE pop_groups
ADD COLUMN IF NOT EXISTS strata VARCHAR(32) NOT NULL DEFAULT 'poor';

ALTER TABLE pop_groups
DROP CONSTRAINT IF EXISTS pop_groups_strata_check;

ALTER TABLE pop_groups
ADD CONSTRAINT pop_groups_strata_check CHECK (strata IN ('poor', 'middle', 'rich'));

UPDATE pop_groups
SET strata = CASE
    WHEN pop_type IN ('clerks', 'clergy', 'bureaucrats', 'artisans') THEN 'middle'
    WHEN pop_type IN ('aristocrats', 'capitalists') THEN 'rich'
    ELSE 'poor'
END
WHERE strata IS NULL OR strata = 'poor';

INSERT INTO schema_versions (version_number, description)
VALUES (12, 'Explicit POP strata')
ON CONFLICT DO NOTHING;
