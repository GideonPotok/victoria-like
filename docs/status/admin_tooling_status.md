# Admin Tooling Status

Day 60 deliverable.

## Status

Admin tooling is sufficient for Month 3 exit.

The `/admin` dashboard and `/api/admin/*` endpoints now let us inspect health, commands, markets, provinces, countries, tick profile, and savepoints without direct database spelunking.

## Implemented Workflows

- world health overview
- active sessions and subscription counts
- command queue depth
- DB write counters
- recent command audit view
- command search/filtering
- rejected/failed command visibility
- market explanation by good
- country inspector
- province inspector
- tick stage timing
- named savepoint creation
- recent savepoint list
- invariant violation visibility

## Debugging Flow

For weird economy or command reports:

1. Check `/admin` health and invariant violations.
2. Check tick duration and persist stage timing.
3. Check command queue depth.
4. Search command audit by actor, country, command type, and tick range.
5. Inspect market pressure and clamp status.
6. Inspect affected country/province details.
7. Create a named savepoint before risky manual repair.

## Known Limits

- The dashboard is still a single static page.
- Market attribution is good enough for current goods but not a full economic debugger.
- WebSocket fanout volume is measured by the load harness, not yet exposed per topic in admin.
- DB write count is operation-count based in app metrics; external soak sampling adds database row deltas.
- POP inspection does not exist yet; that belongs to Month 4 Days 64 and 79.

## Month 4 Additions Needed

Add admin or Unity-visible inspection for:

- country POP summary
- province POP detail
- POP type distribution
- literacy summary
- militancy summary
- unemployment summary
- budget sliders/current values
- RGO/factory/artisan status

## Decision

Proceed to Month 4. Extend admin tooling around POP legibility as the simulation substrate expands.
