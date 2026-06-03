# Victoria-Like

> A pre-alpha, server-authoritative grand-strategy sandbox inspired by Victoria II's coupled POP, economy, and politics simulation. Built to be inspectable, testable, and moddable from day one.

[![.NET CI](https://github.com/GideonPotok/victoria-like/actions/workflows/dotnet.yml/badge.svg)](https://github.com/GideonPotok/victoria-like/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Victoria-Like is an open-source simulation about industrial society: POPs earn wages, buy goods, suffer shortages, switch jobs, and create political pressure through material conditions. The .NET server owns all world state; Unity presents it; the codebase is structured so you can read, run, and modify the simulation without reverse-engineering it.

> **Status:** pre-alpha. The core POP/economy/budget loop runs end to end; military, war, diplomacy, and the Unity client are early or partial. See [docs/current_status.md](docs/current_status.md) for the honest breakdown.

![Victoria-Like demo: watching Albion's treasury tick down via the REST API](docs/assets/demo.gif)

<sub>Above: `watch -n 1 "curl -s localhost:5001/api/world/countries | jq ."` against a live `make run-albion` server. The treasury moves each tick as POP wages, taxes, and spending settle. The raw [asciinema cast](docs/assets/demo.cast) is alongside the GIF if you want to re-render it.</sub>

## Why Vic2 Fans Might Care

The project is inspired by the parts of Victoria 2 that still feel structurally special — not the trappings:

- **POPs are the heart of society.** Income, needs, literacy, militancy, consciousness, and promotion/demotion all run per POP group.
- **Politics emerges from material conditions.** Reform pressure is a downstream metric of unmet needs and POP attitudes, not a hand-scripted event.
- **Markets, production, war, and budgets are linked.** A factory shortage moves prices, which moves POP cash, which moves militancy, which moves reform pressure.
- **The simulation is meant to be explainable.** Server-side explain/preview endpoints exist so the question "*why* did this price change?" has an answer.
- **The architecture is online-shaped from day one.** Server-authoritative, deterministic, snapshot-recoverable, command-validated, WebSocket-broadcastable. Victoria II was a beloved single-player game; this is built like a small live service.

See [docs/for-victoria-2-fans.md](docs/for-victoria-2-fans.md) for the longer pitch.

## What Works Today

- Server-authoritative fixed-tick simulation (1 in-game day per tick).
- POPs with needs, purchasing, employment, unemployment, literacy, militancy, consciousness, and promotion/demotion.
- RGO, factory, and artisan production feeding national markets.
- Market prices, shortages, taxation, budget spending, treasury changes.
- Persistence + restart recovery on PostgreSQL, Redis health checks.
- REST API + WebSocket updates + admin and explanation endpoints.
- Unity v2 inspection UI for countries, provinces, POPs, market prices, treasury, tax rate, RGO output, and factories.
- Three scenarios shipped: `tiny-2country`, `phase1-albion-server`, and `medium-8country`.
- xUnit coverage of loaders, simulation stages, command handling, persistence, explanation, and invariants.
- NBomber + fake-client soak/load harnesses.

What's **not** ready: full historical scenarios, deep diplomacy / spheres / crises, polished Unity UX, balanced economy, and the WebSocket integration on the client side (the server broadcasts; the Unity client still polls REST for most views). See [docs/current_status.md](docs/current_status.md) and the trackers under [docs/status/](docs/status/).

## 60-Second Quickstart

**Prerequisites**

- .NET SDK 10.0.106 or compatible feature roll-forward
- Docker with Docker Compose
- (Optional) Unity 2023 LTS for the client — server is fully usable via `curl` without it

**Run the Albion demo scenario**

```bash
make up           # start PostgreSQL + Redis
make run-albion   # reset world, load Phase 1 Albion demo, start ticking
```

The server listens on `http://localhost:5001`. In another terminal:

```bash
curl http://localhost:5001/health
curl http://localhost:5001/api/world/countries
curl http://localhost:5001/api/world/provinces
```

Other ready-to-run scenarios: `make run-tiny` (12-province test) and `make run-medium` (8-country stress slice).

**Stop everything**

```bash
make down
```

Full setup walkthrough: [docs/quickstart.md](docs/quickstart.md). Server-side REST/WebSocket reference: [server/README.md](server/README.md).

## Run the Tests

```bash
dotnet test server/VictoriaLike.Server.sln
```

Tests are pure C# — they do **not** require a running server, Docker, or any network. Touching the network from a test is against the architecture rules ([docs/architecture.md](docs/architecture.md)).

## Demo Slice

The Phase 1 server demo scenario is `server/content/scenarios/phase1-albion-server.json`: one playable country, a few provinces, staple goods, industrial inputs, POP groups, stockpiles, and visible economic pressure. It is intentionally small — the goal is to show the bones of the simulation, not to ship a finished game. See [docs/demo_script.md](docs/demo_script.md) for the demo flow.

## Repository Layout

```text
server/
  src/
    VictoriaLike.Core/       # Deterministic simulation and domain logic (pure C#)
    VictoriaLike.Server/     # ASP.NET Core API, WebSocket, persistence, auth, health
  tests/
    VictoriaLike.Core.Tests/ # xUnit simulation and server-adjacent tests
    VictoriaLike.LoadTest/   # Fake-client harness
    VictoriaLike.NBomberLoadTest/
  content/                   # Scenarios, goods, balance CSVs

client-unity/v2/             # Current Unity client (inspection-first)

docs/                        # Architecture, scope, status, design, roadmap, modding
devlog/                      # Curated development journal (history, not docs)
reviews/                     # Technical audits and test reports
infra/                       # Local infrastructure and soak scripts
scripts/                     # Deployment helpers (see docs/deployment/)
```

## Architecture (Hard Rules)

The server is the single source of truth. Unity is presentation and input only.

- No authoritative gameplay logic on the client.
- Every player action becomes a server-validated command.
- Preview/explain endpoints are advisory — they cannot mutate world state.
- Simulation is deterministic: same seed + command replay = same world state.
- Randomness is seeded and logged via Serilog; no hidden RNG.
- `VictoriaLike.Core` is pure C# — tests must not touch the network.
- Durable truth is refreshable state — anything a player must see after reconnect lives in server state or a fetchable DTO.

Full rules: [docs/architecture.md](docs/architecture.md).

## Contributing

You don't need to understand the whole engine to help. Good first contributions include scenario data, goods/building content, focused simulation tests, Unity UI polish, balance notes, and explanation text.

Start here:

1. [CONTRIBUTING.md](CONTRIBUTING.md) — coding guidelines and PR expectations
2. [docs/good-first-issues.md](docs/good-first-issues.md) — curated, scoped starter tasks with file pointers and acceptance criteria
3. [docs/modding_scenarios.md](docs/modding_scenarios.md) and [docs/modding_goods.md](docs/modding_goods.md) — adding content without engine knowledge
4. [ROADMAP.md](ROADMAP.md) — where the project is heading and where contributors can plug in

Please read [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) and report security issues per [SECURITY.md](SECURITY.md).

## Load and Soak Testing

The NBomber harness lives at `server/tests/VictoriaLike.NBomberLoadTest`. Standard soak:

```bash
# In one terminal: make run-albion (or run-tiny)
dotnet run --project server/tests/VictoriaLike.NBomberLoadTest -- \
    --profile=soak --duration=1800 --warmup=30
```

Two-player peaceful soak:

```bash
dotnet run --project server/tests/VictoriaLike.NBomberLoadTest -- --profile=two-player-soak
```

Knobs (`--duration`, `--warmup`, `--total-users`, `--auth-users`, `--command-interval`, `--command-mix={peaceful|full}`) and the sampled wrapper are documented in [infra/README.md](infra/README.md).

Current proven envelope: 40 fake clients × 1 hour on the tiny economy with 0 errors / 0 tick drift. Everything beyond that is unclaimed — see [docs/status/known_scale_limits.md](docs/status/known_scale_limits.md).

## Deployment

A self-contained Oracle Cloud Free Tier deployment handoff lives at [docs/deployment/oracle-cloud.md](docs/deployment/oracle-cloud.md). It is **not** yet executed — scripts and a runbook are checked in (`scripts/deploy-oracle.sh`, `scripts/Caddyfile.template`, `scripts/setup-systemd-unit.sh`) and reviewed, but no host has been provisioned. A contributor picking up cloud deployment should start there.

## License

MIT — see [LICENSE](LICENSE).
