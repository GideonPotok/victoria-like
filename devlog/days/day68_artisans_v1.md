# Day 68 Artisans v1

Day 68 deliverable: non-factory production exists.

## Implemented

Artisan POPs now have first-class production state.

- current produced good
- days until reconsideration
- last reconsidered date
- last-tick artisan profit

Artisans produce simple goods outside factories using deterministic recipes:

- `clothes` from `fabric`
- `furniture` from `timber`
- `tools` from `iron` and `coal`
- `liquor` from `grain`

The stage only uses goods known to the active world, so small scenarios can keep a reduced goods list.

## Production Behavior

Each tick:

- artisan POPs choose or keep a produced good
- employed artisans consume country stockpile inputs
- output enters the owning country's stockpile
- output contributes to market production history
- profit or input-shortage loss updates POP cash
- average producer profit is recorded by good and month

Artisan reconsideration averages roughly 42 days. When reconsidering, artisans score candidate goods from the recent 2-6 month producer profit window, with inertia for the current good to avoid constant churn.

## Persistence

Migration `015_artisans.sql` adds artisan columns to `pop_groups` and a `good_profit_history` table.

The server loads, saves, snapshots, restores, and validates artisan state through the normal world state path.

## Result

Day 68 is complete when:

- artisan POPs can produce non-factory goods
- artisan production consumes inputs when required
- profitable artisans gain cash
- unprofitable or input-starved artisans lose cash
- artisan produced-good choice persists
- producer profit history persists for switching
- focused production tests pass
- server build passes
