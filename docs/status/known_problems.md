# Known Problems — Day 15 Demo Snapshot (Historical)

_Date: 2026-04-21_

> **Historical.** This file captures the state of the Day 15 demo. Most issues listed below were resolved in subsequent months (commands now apply to world state, restart recovery exists, scenarios are wired through configuration, etc.). It is kept as a development artifact, not a current bug tracker. For active trackers, see [pop_known_issues.md](pop_known_issues.md), [economy_known_issues.md](economy_known_issues.md), [networking_known_issues.md](networking_known_issues.md), and [known_scale_limits.md](known_scale_limits.md).

## What Works

- **Server starts cleanly**: PostgreSQL + Redis via docker-compose, migrations auto-run, world seeds from scenario
- **World clock ticks**: 1 tick/sec, 1 game-day per tick, checkpoints logged every 10 ticks
- **REST API functional**:
  - `GET /api/world/countries` — returns countries with tax rates
  - `GET /api/world/provinces` — returns all 12 provinces with owner/market data
  - `POST /api/world/commands` — accepts commands, returns 202 with command ID
  - `GET /api/world/commands` — returns command audit history
  - `GET /health` — database + Redis health check

## Known Problems

### P1 — Commands accepted but not applied to world state

- **Symptom**: `POST /api/world/commands` returns accepted with HTTP 202, but `GET /api/world/countries` still shows old tax rate after multiple ticks.
- **Root cause**: `PersistentWorldClockService.ProcessPendingCommandsAsync` dequeues commands from the in-memory queue and logs them but does not call `CommandProcessingStage.ProcessCommandsAsync`. The command-to-world-state wire-up was simplified during Day 15 fixes to avoid WorldState structure mismatches.
- **Fix needed**: Load the current WorldState from DB at tick start, pass it to `CommandProcessingStage`, persist the mutated state. The `CommandProcessingStage` and `ChangeTaxRateCommandHandler` already work correctly in isolation.

### P2 — Migration 003/004 column conflicts

- **Symptom**: Applying all migrations to a fresh database fails if run naively (003 references column added in 001; 004 tried to re-add `applied_tick` which 001 already has).
- **Root cause**: Each day's migration was written without checking earlier schema.
- **Fix applied**: Converted 003/004 to use `ADD COLUMN IF NOT EXISTS` — now idempotent.

### P3 — Scenario path is fragile

- **Symptom**: Server fails on startup with `FileNotFoundException` for scenario file.
- **Root cause**: Path computed as `AppContext.BaseDirectory + "../../../../../content"` which is brittle — it depends on the build output depth (differs for `net8.0` vs `net10.0`).
- **Fix needed**: Add `World:ScenarioPath` to `appsettings.json` or embed scenario content as an embedded resource.

### P4 — Target framework mismatch

- **Symptom**: Build succeeds but runtime refuses to start: "You must install or update .NET to run this application" with net8.0 target.
- **Root cause**: NuGet packages were version 10.0.0 but `TargetFramework` was `net8.0`.
- **Fix applied**: Updated all `.csproj` files to `net10.0`.

### P5 — Unity client not tested against live server

- **Symptom**: Client build works in Unity Editor but end-to-end flow (spawn ProvinceListUI → click → submit command) not verified against local server.
- **Root cause**: Requires Unity Editor open with the scene loaded — can't automate from CLI.
- **Fix needed**: Manual test: start server, open Unity, verify BaseUrl = `http://localhost:5001`, hit Play.

## Demo Flow (What Was Verified)

```
1. docker-compose up -d               # start postgres + redis
2. dotnet run                         # server starts, seeds 2 countries 12 provinces
3. curl GET /api/world/countries      # England (10%), France (12%)
4. curl GET /api/world/provinces      # 12 provinces with correct owners
5. curl POST /api/world/commands      # ChangeTaxRate accepted → 202
6. curl GET /api/world/commands       # command shows status=accepted
7. curl GET /api/world/countries      # STILL 10% — P1 not fixed
```

Step 7 is the blocker for a complete demo.
