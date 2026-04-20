# Day 65 Monthly POP Tick

Day 65 deliverable: POPs change over time on a monthly cadence.

## Implemented

The core simulation now includes `MonthlyPopUpdateStage`.

The stage runs only when the in-world date is the first day of a month. It is deterministic and clamps all mutable POP values to invariant-safe ranges.

Monthly updates currently cover:

- POP cash reserve drift from crude monthly income minus priced needs cost
- life, everyday, and luxury needs fulfillment tracking
- literacy drift toward simple class-specific targets
- militancy drift from unmet needs and recovery under stable needs
- consciousness drift from literacy and hardship
- promotion and demotion event-log hooks for later employment/class migration work

## Persistence

The server tick pipeline now persists monthly POP state back to `pop_groups`:

- size
- cash
- literacy
- militancy
- consciousness
- life needs fulfillment
- everyday needs fulfillment
- luxury needs fulfillment

Province-level needs fulfillment remains persisted as the average of province POP fulfillment.

## Validation

World invariants now reject invalid POP cash, literacy, militancy, consciousness, and per-need fulfillment values.

## Result

Day 65 is complete when:

- monthly POP updates run on month boundaries only
- POP economic and social fields change deterministically over time
- POP updates are clamped to valid ranges
- POP updates persist through the server database path
- focused monthly POP tests pass
- server build passes
- core tests pass
