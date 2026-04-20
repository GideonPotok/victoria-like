# Day 50 Restart Recovery Test Report

## Scope

Day 50 validates that the world can survive process death without losing its canonical state or reporting impossible post-restart data.

The recovery model under test is the Month 3 model documented in `persistence_recovery_model.md`: the server restores from the latest durable database state and named savepoints, but does not replay commands after a snapshot.

## Harness

Primary harness:

```bash
DAY50_SOAK_SECONDS=600 ./infra/test-restart-recovery.sh
```

Useful shorter local smoke run:

```bash
DAY50_SOAK_SECONDS=60 ./infra/test-restart-recovery.sh
```

The harness assumes the development server is already running at `BASE_URL`, defaulting to `http://localhost:5001`. It intentionally leaves process stop/start manual so it does not kill an unrelated local `dotnet` process.

## Test Flow

The Day 50 harness performs this sequence:

1. Waits for `/health`.
2. Seeds development passwords through `/dev/seed-passwords` when available.
3. Logs in as `england-player`.
4. Captures baseline state from:
   - `/dev/metrics`
   - `/api/world/summary`
   - `/api/admin/summary`
   - `/api/world/countries`
   - `/api/world/provinces`
   - `/api/world/buildings/queue`
   - `/api/world/market`
   - `/api/admin/commands`
5. Submits a tax command through `/api/world/commands`.
6. Resubmits the same command ID and idempotency key to verify duplicate retry handling.
7. Queues one building in an owned province.
8. Continues periodic tax commands during active ticks for the soak window.
9. Logs out and logs back in to verify reconnectable auth/session behavior.
10. Creates a named `day50-torture` savepoint through `/api/admin/snapshots`.
11. Prompts for manual server stop and restart.
12. Captures post-restart state from the same endpoints.
13. Fails if tick moves backward or post-restart invariant violations are present.
14. Writes a runtime report to `restart_recovery_test_report.runtime.md`.

## Pass Criteria

The run passes when all of the following are true:

- The restarted server answers `/health`.
- The world tick after restart is greater than or equal to the baseline tick.
- World summary is readable after restart.
- Country, province, market, and construction queue endpoints are readable after restart.
- The saved building queue is still valid after restart.
- The command audit endpoint is readable after restart.
- The named savepoint is listed in admin snapshot data.
- Admin invariant violations are zero after restart.
- Re-login after a simulated disconnect returns the same controlled country mapping.

## Fail Criteria

The run fails if any of the following occur:

- Server cannot restart cleanly.
- Tick count moves backward relative to the captured baseline.
- Admin invariant checks report violations after restart.
- Snapshot creation fails.
- Controlled country mapping is missing after re-login.
- Building queue or market state cannot be loaded after restart.
- Command audit data becomes unreadable.

## Current Automated Coverage

The supporting unit-level checks cover the core recovery pieces added in Days 46-49:

- Snapshot documents preserve named savepoints and construction queues.
- Snapshot validation rejects invalid ticks, invalid ownership references, invalid queue references, and missing building types.
- Startup validation rejects impossible loaded world state before accepting existing DB data.
- Invariant checks detect invalid owner references, invalid player-country mappings, negative market quantities, invalid price clamps, duplicate active construction, and invalid build ticks.

## Known Limits

- The shell harness does not own server process lifecycle; restart is manual by design.
- WebSocket reconnect is not automated unless a separate WebSocket client is used. The harness verifies HTTP session reconnect and post-restart endpoint recovery.
- Month 3 recovery does not replay command audit records after the latest snapshot. Commands accepted after a savepoint but before process death are only durable if they have already been persisted into canonical state.
- The test does not prove long-duration memory stability; that belongs to Day 58 soak testing.

## Expected Runtime Artifact

Live execution note: the full 10-20 minute Day 50 torture run is deferred. The harness and pass/fail report are ready, but we will run it later when the development server can be stopped and restarted manually.

After a live run, inspect:

```bash
cat restart_recovery_test_report.runtime.md
```

That runtime report contains the exact pre/post summaries, command responses, duplicate retry response, relogin response, and savepoint metadata for the local run.
