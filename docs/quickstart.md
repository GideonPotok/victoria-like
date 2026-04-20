# Quickstart

This guide starts the local services and runs the Phase 1 Albion demo server.

## Prerequisites

- .NET SDK 10.0.106 or compatible latest feature roll-forward.
- Docker with Docker Compose.
- Unity 2023 LTS for the client.

## Start Infrastructure

```bash
make up
make test-connections
```

This starts PostgreSQL and Redis for local development.

## Run the Albion Demo Server

```bash
make run-albion
```

The server resets the local world and loads `server/content/scenarios/phase1-albion-server.json`.

Server URL:

```text
http://localhost:5001
```

Health check:

```bash
curl http://localhost:5001/health
```

## Run Tests

```bash
dotnet test server/VictoriaLike.Server.sln
```

## Open the Unity Client

Open `client-unity/v2` in Unity 2023 LTS.

The client is presentation-only. It should connect to the local server, inspect world state, and submit server-validated commands where the UI exposes them.

## Stop Services

```bash
make down
```
