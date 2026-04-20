# Victoria Server

The authoritative world simulation server (.NET 10 + ASP.NET Core).

> **Note on currency.** Sections below cover infrastructure, the tick clock, and health checks — these are accurate. The "Day 4" / "Day 5" sub-headers and the "No simulation logic yet" line are early-development notes; the simulation has since grown to include POPs, markets, budgets, persistence, command validation, WebSocket broadcast, and admin/explain endpoints. For current scope see [../docs/current_status.md](../docs/current_status.md) and [../docs/architecture.md](../docs/architecture.md).

## Structure

```
/src
  /VictoriaLike.Core        # Pure C# simulation engine (testable, no framework deps)
  /VictoriaLike.Server      # ASP.NET Core host (logging, health checks, network layer)
/tests
  /VictoriaLike.Core.Tests  # Unit and integration tests for simulation
/content
  *.json, *.csv             # Game balance data, scenario definitions
```

## Building

```bash
# Requires .NET 10 SDK
dotnet build server/VictoriaLike.Server.sln
```

## Running

### Prerequisites
- PostgreSQL running on localhost:5432 (start with `make up`)
- Redis running on localhost:6379 (start with `make up`)

### Start the Server
```bash
cd server
dotnet run --project src/VictoriaLike.Server
```

### Environment
- Development (default): Loud logs (Debug level), hot reload enabled
- Production: Quiet logs (Information level)

### Health Checks
- `GET /health` - Full status with all component checks
- `GET /health/ready` - Returns `{"ready": true}` if database and Redis are connected

Example:
```bash
curl http://localhost:5001/health
```

Response:
```json
{
  "status": "Healthy",
  "timestamp": "2026-04-20T21:00:00Z",
  "checks": [
    {
      "name": "database",
      "status": "Healthy",
      "description": null
    },
    {
      "name": "redis",
      "status": "Healthy",
      "description": null
    }
  ]
}
```

## Architecture

### Program.cs
Entry point that:
1. Configures Serilog for structured JSON logging
2. Registers health checks (PostgreSQL, Redis)
3. Validates database and Redis connectivity on startup
4. Maps health check endpoints
5. Runs the ASP.NET Core host

### appsettings.json / appsettings.Development.json
Configuration:
- Connection strings (PostgreSQL, Redis)
- Logging levels (Info for prod, Debug for dev)
- Server port and tick interval

### VictoriaLike.Core
Imported from `/server/src/VictoriaLike.Core`. The server uses the pure simulation engine for:
- State management (pops, markets, provinces)
- Simulation pipeline (deterministic tick orchestration)
- Queries (world summary, metrics)

## Persistent World Clock (Day 5)

The world clock state is automatically persisted to PostgreSQL and restored on startup.

### How Persistence Works

**Saving:**
- World state (tick number, world timestamp) is saved every 100 ticks by default
- Configurable via `Server:SaveIntervalTicks` in appsettings.json
- Final save happens on graceful shutdown

**Loading:**
- On startup, the server loads the most recent world state from `world_state` table
- If no state exists, starts fresh from 1800-01-01
- Tick count and date resume from persisted values

**Database Schema:**
```sql
CREATE TABLE world_state (
    id SERIAL PRIMARY KEY,
    tick_number BIGINT NOT NULL,
    world_timestamp TIMESTAMP NOT NULL,
    last_saved_at TIMESTAMP NOT NULL,
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP NOT NULL
);
```

### Testing Restart Recovery

```bash
# Start services
make up

# Build and run server
cd server
dotnet run --project src/VictoriaLike.Server

# In another terminal, watch metrics and note the tick count
curl http://localhost:5001/dev/metrics | jq '.tick_count'

# Wait a few seconds, check again
sleep 5 && curl http://localhost:5001/dev/metrics | jq '.tick_count'

# Stop server (Ctrl+C)
# Restart server
dotnet run --project src/VictoriaLike.Server

# Verify tick count resumed (should be same or higher)
curl http://localhost:5001/dev/metrics | jq '.tick_count'
```

**Or use the automated test script:**
```bash
bash infra/test-restart-recovery.sh
```

---

## Fixed-Tick Simulation Loop (Day 4)

The server runs a deterministic world clock with fixed-tick intervals (default 1 second).

### WorldClockService

- **Tick interval**: Configurable via `Server:TickIntervalMs` (default 1000ms)
- **Start time**: 1800-01-01 (game world date)
- **One tick = one day** in the world
- Runs continuously once the server starts

### Development Endpoints

In development mode (`ASPNETCORE_ENVIRONMENT=Development`), the following endpoints are available:

**Get tick metrics:**
```bash
curl http://localhost:5001/dev/metrics
```

Response:
```json
{
  "tick_count": 42,
  "tick_duration_ms": 45000,
  "world_timestamp": "1800-02-12",
  "tick_rate": 0.93,
  "is_paused": false
}
```

**Pause the clock:**
```bash
curl -X POST http://localhost:5001/dev/clock/pause
```

**Resume the clock:**
```bash
curl -X POST http://localhost:5001/dev/clock/resume
```

### Observing the Tick Loop

When you start the server, you'll see logs like:
```
[Debug] Tick 1 - World: 1800-01-02, Duration: 1001ms, Rate: 1.00 ticks/sec
[Debug] Tick 2 - World: 1800-01-03, Duration: 2002ms, Rate: 1.00 ticks/sec
...
[Info] Tick 10 checkpoint - Running 10100ms, World timestamp: 1800-01-11
```

The tick loop:
1. Waits for the next tick interval
2. Increments world date by 1 day
3. Logs tick metrics
4. Repeats indefinitely until shutdown

### Graceful Shutdown

When you stop the server (Ctrl+C):
1. Stops accepting new ticks
2. Completes any in-flight operations
3. Logs final metrics
4. Exits cleanly

### Architecture

**WorldClockService** (Services/WorldClockService.cs):
- Implements `IHostedService` (runs alongside ASP.NET Core)
- Thread-safe metrics via lock
- Pausable in dev mode
- Logs tick metrics continuously

**Integration** (Program.cs):
- Registered as singleton service
- Registered as hosted service (auto-starts with app)
- Exposed via /dev/metrics and /dev/clock/* endpoints

**Determinism**: Same tick interval = same world time advancement. No simulation logic yet, just the heartbeat.
