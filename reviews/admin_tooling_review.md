# Admin Tooling Review

## Current Admin Workflows

Use `/admin` as the primary world inspector dashboard.

The dashboard now supports:

- World health overview: tick, world date, tick duration, connected clients, active sessions, active subscriptions, command queue depth, DB write counts, and health checks.
- Command investigation: searchable command audit by actor/account, country, command type, outcome, and submitted tick range.
- Rejection debugging: rejected and failed commands are surfaced clearly with rejection reason or outcome reason.
- Market explanation: per-good previous price, current price, delta, pressure, supply, demand, unmet demand, clamp status, largest producer, and largest consumer.
- Province inspection: owner, population/workforce, production, local demand, needs fulfillment, and construction queue.
- Country inspection: treasury, tax rate, controller account, province/population summary, active commands, and market summary.
- Snapshot operations: create a named savepoint and inspect recent savepoints.

## Admin Endpoints

- `GET /api/admin/summary`: health, tick, sessions, subscriptions, command queue, DB writes, recent commands, recent snapshots, invariant violations.
- `GET /api/admin/commands`: command audit with filters.
- `GET /api/admin/market`: market explanation data.
- `GET /api/admin/provinces/{provinceId}`: province detail inspector.
- `GET /api/admin/countries/{countryId}`: country detail inspector.
- `GET /api/admin/tick-profile`: tick stage timing.
- `POST /api/admin/snapshots`: create manual named savepoint.

## Debugging Weird Economy Checklist

1. Check `/admin` health and invariant violations first. If invariants fail, do not trust downstream economy values.
2. Check tick duration and `persist` stage time. Slow persistence can make command results feel delayed.
3. Check command queue depth. If commands are pending, the player may be waiting for the next tick.
4. Open Command Log Viewer and filter by actor/country. Confirm the command was accepted, rejected, applied, or failed.
5. For tax reports, filter `ChangeTaxRate` and inspect submitted tick, executed tick, and rejection reason.
6. For construction reports, filter `QueueBuilding` and inspect target province plus active construction conflicts.
7. Open Market Explanation. Compare pressure, unmet demand, and clamp status before assuming pricing math is broken.
8. Open Province Inspector for the relevant province. Check local production, local demand, needs fulfillment, and construction.
9. Open Country Inspector. Check treasury, tax rate, controlled account, active commands, and population footprint.
10. If the state looks impossible, create a named savepoint before attempting a fix.

## Structured Logging Notes

Useful structured log fields now emitted or available through admin data:

- `CommandId`, `ActorId`, `CountryId`, `CommandType`
- `submitted_tick`, `expected_world_tick`, `idempotency_key`
- command `outcome_status`, `outcome_reason`, `rejection_reason_code`
- tick stage durations, command queue depth, connected clients, subscriptions, and DB write counters

## Known Limits For Second Pass

- Market explanation uses current persisted supply/demand history and deterministic province demand estimates. It does not yet persist full producer/consumer attribution from the simulation tick.
- Country active commands are based on currently accepted audit records, not a richer command lifecycle index.
- The dashboard is a single static HTML page. Day 52-54 second pass should split large sections into clearer views if the UI becomes crowded.
- WebSocket subscription inspection is connection-level only. It does not yet show per-topic fanout volume.
- DB write count is operation-count based, not row-count based.

## Cleanup Status

- Admin endpoints are grouped under `/api/admin`.
- The Day 51 overview is now the first place to check world health.
- The Day 52 command viewer avoids raw log spelunking for player command reports.
- The Day 53 market view explains price movement with tested pressure/unmet-demand/clamp calculations and deterministic producer/consumer attribution.
- The Day 54 inspectors expose country/province state without opening the database.
