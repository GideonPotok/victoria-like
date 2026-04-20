# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What This Is

Victoria-like is a grand-strategy simulation game (inspired by Victoria II) built as an authoritative client-server system. The server owns all game state; the Unity client is presentation-only.

## Commands

### Infrastructure (Docker)
```bash
make up               # Start PostgreSQL and Redis
make down             # Stop containers
make clean            # Remove containers and volumes
make test-connections # Verify both services are reachable
```

### Running the Server
```bash
make run-tiny         # Full reset + start with tiny-2country scenario
make run-medium       # Full reset + start with medium-8country scenario
make reset-world      # Kill server + drop DB + delete snapshots (no restart)
```

### Tests
```bash
dotnet test server/VictoriaLike.Server.sln           # All tests
dotnet test server/tests/VictoriaLike.Core.Tests     # Unit/integration tests only
dotnet run --project server/tests/VictoriaLike.NBomberLoadTest  # Load test (server must be running)
```

Run a single test class:
```bash
dotnet test server/tests/VictoriaLike.Core.Tests --filter "FullyQualifiedName~ClassName"
```

### Build
```bash
dotnet build server/VictoriaLike.Server.sln
```

## Architecture

### Server (`server/`)

Two .NET projects:

**`VictoriaLike.Core`** — Pure C# simulation engine, no ASP.NET dependency, fully testable without a running server.
- `Core/` — Domain logic split by domain: `Pops/`, `Economy/`, `Buildings/`, `Military/`, `Countries/`, `World/`
- `Simulation/` — Fixed-tick pipeline stages (Input → Simulation → Metrics → Broadcast → Persistence)
- `Application/` — Commands, save/load, profiling
- `Data/` — Balance data loaders, JSON/CSV content definitions
- `Scenarios/` — Scenario loading

**`VictoriaLike.Server`** — ASP.NET Core host.
- `Api/` — REST controllers (World, Admin, Auth, Explain) and DTOs
- `Services/` — `PersistentWorldClockService` (fixed-tick loop), `CommandQueueService`, `WorldSnapshotService`, `AdminInspectorService`, `WorldWebSocketHub`
- `Data/` — EF Core migrations

### Tick Pipeline

Each tick (1 in-game day, default 1000ms wall-clock):
1. Input — dequeue and validate player commands
2. Simulation — update pops, markets, provinces
3. Metrics — compute aggregates
4. Broadcast — send deltas to connected clients via WebSocket
5. Persistence — snapshot to PostgreSQL (every 100 ticks)

### Client (`client-unity/`)

Unity 2023 LTS project. Scripts in `My project/Assets/Scripts/`:
- `Api/WorldApiClient.cs` — HTTP REST client
- `UI/WorldUIManager.cs` — UI presentation layer
- `Bootstrap.cs` — entry point

The client uses HTTP polling; WebSocket live updates are wired server-side and being integrated on the client.

### Infrastructure
- PostgreSQL: `postgresql://victoria:victoria_dev_password@localhost:5432/victoria_world`
- Redis: `redis://localhost:6379`
- Scenarios (JSON): `server/content/scenarios/`
- Snapshots: `server/src/VictoriaLike.Server/bin/Debug/net10.0/snapshots/`

### Utilities (`month8_utility/`)
Python scripts for economy analysis and balance modeling (offline, not part of the server build).

## Docs & Notes Layout

| Path | What's there |
|------|-------------|
| `docs/` | Living architecture docs: vision, scope, domain model, simulation pipeline, command rules, UI specs |
| `docs/design/` | MVP scope locks (economy, Vic2 mechanics) |
| `docs/status/` | Active known-issues trackers (economy, pops, networking, scale limits) |
| `docs/vic2_reference/` | Victoria II wiki reference scraped from paradoxwikis.com |
| `devlog/months/` | Month-by-month reviews, targets, and sprint plans |
| `devlog/weeks/` | Week-level plans |
| `devlog/days/` | Day-by-day implementation notes (day59–day79+) |
| `reviews/` | Technical audits, test reports, design reviews |
| `scratch/` | Raw session dumps (not authoritative) |

## Hard Rules (from `docs/architecture.md`)

- **Server is single source of truth.** No gameplay logic on the client.
- **Every player action is a command.** Commands are validated and executed server-side; client draft state is allowed but never authoritative.
- **Preview is not authority.** Explain/preview endpoints are advisory only — they cannot mutate world state.
- **Deterministic simulation.** Same seed + command replay = same world state.
- **No hidden RNG.** Randomness is seeded and logged via Serilog.
- **Tests don't touch the network.** `VictoriaLike.Core` is pure C#; all unit/integration tests run without a running server.
- **Durable truth is refreshable state.** Anything a player must see after reconnect belongs in server state or a fetchable DTO, not in a transient event stream.
