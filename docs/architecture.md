# Architecture

## Overview

Victoria-like is structured as a client-server system:
- **Server**: Authoritative world simulation (.NET 10, PostgreSQL, Redis)
- **Client**: Unity-based presentation and input (C#, .NET runtime)
- **Infrastructure**: Docker-compose for local dev, scaled deployment patterns later

## Hard Rules

These are non-negotiable constraints that protect simulation integrity and testability.

### Server Authority
- **The server is the single source of truth.** All game state lives here.
- **No gameplay logic runs on the client.** The client is presentation only.
- **Every player action becomes a command.** Commands are validated and executed on the server.
- **Client draft state is allowed.** Unity may keep local UI state, selections,
  and unsynced draft intent, but never authoritative simulation outcomes.

### State Inspectability
- **Every important state change must be inspectable.** Logs show what changed, why, and when.
- **Deterministic simulation.** Same seed + command replay = same world state.
- **No hidden RNG.** Randomness is seeded and logged.

### Data Flow
```
Player Input (Client)
  ↓
Preview Query (optional, advisory only)
  ↓
Command (validated, sent to Server)
  ↓
Server Command Handler (executes, updates WorldState)
  ↓
Tick Pipeline (deterministic stages)
  ↓
State Delta (sent to Client)
  ↓
Presentation Update (Client renders)
```

## Directory Layout

```
/server
  /src/VictoriaLike.Core        # Simulation engine (pure C#, testable)
  /src/VictoriaLike.Server      # Server host (.NET 10, .NET runtime)
  /tests/VictoriaLike.Core.Tests
  /content                       # JSON/CSV game data
  docker-compose.yml             # Local services (PostgreSQL, Redis)

/client-unity
  /Assets                        # Unity project
  /Packages
  /ProjectSettings

/infra
  /docker                        # Container definitions
  /scripts                       # Deployment, setup

/docs
  vision.md                      # What we're building
  architecture.md               # This file
  non_goals_q1.md              # Q1 scope boundaries
```

## Simulation Pipeline

The server runs a fixed-tick loop:
1. **Input Stage** - Dequeue and validate player commands
2. **Simulation Stage** - Update world state (pops, markets, provinces)
3. **Metrics Stage** - Compute aggregates for inspection
4. **Broadcast Stage** - Send deltas to connected clients
5. **Persistence Stage** - Write snapshots (optional, per tick or interval)

Each stage is deterministic and logged.

Gameplay commands are evaluated in server-owned order inside this pipeline.
Client-side debounce, coalescing, and pending UI are useful UX layers, but they
do not choose the authoritative execution order or application tick.

## Technology Choices

| Layer | Tech | Why |
|-------|------|-----|
| Server Core | C# / .NET 10 | Testable, fast, deterministic |
| Server Host | .NET 10 + ASP.NET Core | Structured logging, health checks, hot reload in dev |
| Database | PostgreSQL | Rich queries for analytics, ACID guarantees |
| Cache | Redis | Fast state queries, pub/sub for broadcasting |
| Client | Unity + C# | Familiar to game devs, .NET runtime for code reuse |
| Local Dev | Docker Compose | One-command environment setup |

## Key Invariants

- **No state on the client.** All state is derived from server deltas.
- **Commands are the API.** The protocol between client and server is command messages.
- **Preview is not authority.** Optional query/preview endpoints may explain
  whether a command appears valid, what it may cost, or why it is blocked, but
  only the authoritative execute path can mutate world state.
- **One player intent maps to one authoritative command.** Internal helper
  logic may compose local validation and mutation steps, but should not
  implicitly enqueue additional network-visible commands from inside a command
  handler.
- **Durable truth is refreshable state.** Anything the player must still see
  after reconnect, reload, or late join belongs in authoritative state or a
  fetchable server DTO, not only in a transient event stream.
- **Client schedulers are UX infrastructure.** Debounce, coalescing, retry, and
  pending state may live on the client, but only as a transport-shaping layer
  ahead of authoritative command processing.
- **Logs are queryable.** Use structured logging (Serilog) so state changes can be traced.
- **Tests don't touch the network.** Core simulation is pure C#, testable without a server.

For practical Unity UI and command submission rules, see
`docs/unity-frontend-and-command-ui.md`.
