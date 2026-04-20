# Persistence & Recovery

_Model defined: Day 48 (2026-04-26) | Status as of: Day 60_

## Status

Persistence and restart recovery are sufficient for Month 3 exit.

The server can persist canonical world state, load existing database state on startup, and use savepoint snapshots for manual recovery workflows.

## Implemented

- Database migrations for core world state
- Persisted countries, provinces, markets, construction queue, sessions, accounts, command audit, and command outcomes
- Startup world load path and startup validation before accepting loaded state
- Periodic world state save and manual named savepoints
- Latest-savepoint restore path when the database world is empty
- Invariant checks for impossible loaded or tick-mutated state
- Restart recovery test harness

## Decision

Month 3 uses **latest snapshot only** for restart recovery.

The server does not replay gameplay commands after the selected snapshot. Command audit remains durable for debugging and moderation but is not currently an event-sourcing stream. This keeps recovery simple and explicit while the simulation is single-server and small.

## Durable Data Types

### Command Audit

Stores: command ID, actor ID, country ID, command type, target IDs, submitted tick, executed tick, result, rejection reason, idempotency key, and received time.

Used for debugging player reports, anti-cheat review, and moderation. Not used for restart state reconstruction.

### Savepoint Snapshot

The authoritative restart source when the database world is empty and `World:Snapshots:RestoreLatestOnStartup` is enabled.

Snapshot files include: world tick, world date, countries, provinces, markets, active building construction queues, and account-country mappings.

Manual savepoints: `POST /api/admin/snapshots` with body `{ "name": "before-tax-test" }`.

## Startup Recovery Flow

1. Run migrations.
2. If database world rows exist, validate and continue.
3. If database is empty and snapshot restore is enabled, load the latest valid snapshot.
4. Restore countries, provinces, markets, player mappings, active building queue, world tick, and world date.
5. If no database world and no snapshot exist, seed from scenario.

Invalid partial state fails startup rather than allowing the server to run a corrupted world.

## Invariants

Runtime invariant checks cover:

- Country tax rates are in bounds; treasury values are representable decimals
- Province owners point to valid countries; province/pop references are consistent
- Market quantities are non-negative; prices stay within clamps for known goods
- Building queue entries point to valid province/country IDs; progress within expected bounds
- Each province has at most one active construction queue entry
- Player account mappings point to valid countries

If tick-time invariants fail, that tick does not persist its mutated world state.

## Replay Policy

There is no command replay after snapshot. Commands accepted after the latest savepoint but before process death may be present in audit logs but absent from recovered world state — acceptable because snapshots are periodic and manual savepoints can be taken before risky tests.

Future replay path (not yet implemented):
1. Load latest snapshot.
2. Find audited commands accepted after snapshot tick.
3. Re-validate actor/country authority at replay time.
4. Re-apply only deterministic, idempotent commands.
5. Stop replay on invariant violation.

Implement only after every command has stable idempotency semantics.

## Known Limits

- Snapshot files are local filesystem artifacts (not object storage).
- Savepoint selection is simple latest-file ordering.
- No point-in-time database restore workflow.
- No post-snapshot command replay.
- Multi-node recovery not designed yet.
- Recovery not tested against very large POP datasets.

## Month 4 Risk

Month 4 POPs increase persistence volume. Before adding complex monthly POP mutation, measure: startup load time with seeded POPs, snapshot size, tick persist duration, monthly POP write count, and migration behavior on an existing local database.

**Decision:** Proceed to Month 4, but re-run soak after POP persistence and POP monthly ticks land.
