# Infrastructure

Local development environment setup and configuration.

## Quick Start

**One command to boot the local environment:**

```bash
make up
```

This starts PostgreSQL (5432) and Redis (6379) in Docker.

## Available Commands

```bash
make up                  # Start PostgreSQL and Redis
make down                # Stop and remove containers
make logs                # View container logs
make test-db             # Test database connectivity
make test-redis          # Test Redis connectivity
make test-connections    # Test both
make clean               # Remove containers and volumes
```

## Environment Files

- `.env.local` - Local development environment variables (used by docker-compose)
- `appsettings.json` - Server base configuration (production defaults)
- `appsettings.Development.json` - Server development overrides

## Configuration

### PostgreSQL
- Host: `localhost`
- Port: `5432`
- User: `victoria`
- Password: `victoria_dev_password`
- Database: `victoria_world`

Connection string: `Server=localhost;Port=5432;Database=victoria_world;User Id=victoria;Password=victoria_dev_password;`

### Redis
- Host: `localhost`
- Port: `6379`
- Connection string: `localhost:6379`

## Startup Script

For a guided startup (checks prerequisites, waits for health checks):

```bash
bash infra/startup.sh
```

## Load And Soak Scripts

Legacy fake-client harness:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest -- --url=http://localhost:5001 --clients=20 --duration=120
```

NBomber harness:

```bash
dotnet run --project server/tests/VictoriaLike.NBomberLoadTest -- --profile=smoke --duration=30 --total-users=5 --auth-users=2
```

Sampled NBomber soak wrapper:

```bash
bash infra/run-nbomber-soak-with-sampling.sh
```

Two-player peaceful soak:

```bash
NBOMBER_SOAK_PROFILE=two-player-soak bash infra/run-nbomber-soak-with-sampling.sh
```

The soak wrapper writes outputs under `nbomber-soak-runs/<run-id>/` and expects Docker, `curl`, `lsof`, and the local PostgreSQL/Redis container names described in the script.

## Docker Compose

The `docker-compose.yml` in the repo root defines both services with health checks and persistent volumes.

When you run `make up`, Docker:
1. Pulls images (redis:7-alpine, postgres:15-alpine)
2. Creates containers with named volumes for persistence
3. Exposes ports 5432 (PostgreSQL) and 6379 (Redis)
4. Waits for health checks before reporting "ready"

Data persists across restarts unless you run `make clean`.

## Troubleshooting

**"Cannot connect to the Docker daemon"**
- Start Docker Desktop (Mac/Windows) or `dockerd` (Linux)

**PostgreSQL won't start**
- Check port 5432 isn't already in use: `lsof -i :5432`
- Remove stuck container: `docker-compose down -v && make up`

**Redis won't start**
- Check port 6379 isn't already in use: `lsof -i :6379`
- Clear volume: `docker volume rm victoria_ii_redis_data && make up`

**Tests fail to connect**
- Ensure health checks have passed: `docker-compose ps` should show `Up`
- Wait a few more seconds: `sleep 5 && make test-connections`

