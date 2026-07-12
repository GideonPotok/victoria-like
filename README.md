# Victoria-Like

> A pre-alpha, server-authoritative grand-strategy sandbox inspired by Victoria II's coupled POP, economy, and politics simulation. Built to be inspectable, testable, and moddable from day one.

[![.NET CI](https://github.com/GideonPotok/victoria-like/actions/workflows/dotnet.yml/badge.svg)](https://github.com/GideonPotok/victoria-like/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Victoria-Like is an open-source simulation about industrial society: POPs earn wages, buy goods, suffer shortages, switch jobs, and create political pressure through material conditions. The .NET server owns all world state; Unity presents it; the codebase is structured so you can read, run, and modify the simulation without reverse-engineering it.

> **Status:** pre-alpha. The core POP/economy/budget loop runs end to end; military, war, diplomacy, and the Unity client are early or partial. See [docs/current_status.md](docs/current_status.md) for the honest breakdown.

## Why Vic2 Fans Might Care

The project is inspired by the GVGOAT, Victoria 2:

- **POPs are the heart of society.** Income, needs, literacy, militancy, consciousness, and promotion/demotion all run per POP group.
- **Politics emerges from material conditions.** Reform pressure is a downstream metric of unmet needs and POP attitudes, not a hand-scripted event.
- **Markets, production, war, and budgets are linked and endogenous.** A factory shortage moves prices, which moves POP cash, which moves militancy, which moves reform pressure.

Additional principles:
- **The simulation is meant to be explainable.** Server-side explain/preview endpoints exist so the question "*why* did this price change?" has an answer.

Unlike Victoria II, though:
- **This architecture is online-native** Server-authoritative, deterministic, snapshot-recoverable, command-validated, WebSocket-broadcastable. Victoria II was a beloved single-player game; this is built like a small live service.


See [docs/for-victoria-2-fans.md](docs/for-victoria-2-fans.md) for more.

## What Works Today

- Server-authoritative fixed-tick simulation (1 in-game day per tick).
- Persistence + restart recovery on PostgreSQL, Redis health checks.
- REST API + WebSocket updates + admin and explanation endpoints.
- Three scenarios shipped: `tiny-2country`, `phase1-albion-server`, and `medium-8country`.
- xUnit coverage of loaders, simulation stages, command handling, persistence, explanation, and invariants.
- NBomber + fake-client soak/load harnesses.

## What works, but could be richer

- Unity v2 inspection UI for countries, provinces, POPs, market prices, treasury, tax rate, RGO output, and factories.
- POPs with needs, purchasing, employment, unemployment, literacy, militancy, consciousness, and promotion/demotion.
- RGO, factory, and artisan production feeding national markets.
- Market prices, shortages, taxation, budget spending, treasury changes.

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

## See It Running

The GIFs below are re-rendered from asciinema recordings (`agg --theme monokai`) against a live `make run-albion` server. The raw `.cast` file for each sits next to its GIF in [docs/assets/](docs/assets/) if you want to replay or re-render one yourself.

### 1. Bring the stack up

<details>
<summary><code>make up</code> → <code>make test-connections</code> → <code>make run-albion</code> — click to expand (~4 MB)</summary>

![Terminal recording: docker compose pulling Postgres and Redis, health-checking both, then starting the server against the Phase 1 Albion scenario](docs/assets/demo_server_setup.gif)

</details>

<sub>`make up` pulls and starts Postgres and Redis via Docker Compose, `make test-connections` confirms both are reachable, and `make run-albion` resets the world, seeds the Albion scenario, and starts the fixed-tick loop, ending on `Now listening on: http://0.0.0.0:5001`. Raw cast: [demo_server_setup.cast](docs/assets/demo_server_setup.cast).</sub>

### 2. Restart the server and resume from the last snapshot

<details>
<summary>Restart via <code>dotnet run</code>, then reset into <code>make run-medium</code> — click to expand (~10 MB)</summary>

![Terminal recording: restarting the server process, which reloads the last persisted snapshot at tick 875 and resumes ticking, then resetting into the 8-country medium scenario](docs/assets/demo_server_resume.gif)

</details>

<sub>Killing and restarting the server process — without a world reset — proves persistence: it logs `World restored from snapshot: tick 875` and picks the simulation back up exactly where it left off. The second half resets into `make run-medium` to show the same server on the larger 8-country scenario. Raw cast: [demo_server_resume.cast](docs/assets/demo_server_resume.cast).</sub>

### 3. Watch the economy tick live

![Terminal recording: a curl health check, then a countries query, then watching the countries endpoint every second as Albion's treasury ticks down](docs/assets/demo_second_terminal.gif)

<sub>`curl /health`, then `curl /api/world/countries | jq`, then `watch -n 1 "curl -s .../countries | jq ."` — the treasury moves every tick as POP wages, taxes, and spending settle. Raw cast: [demo_second_terminal.cast](docs/assets/demo_second_terminal.cast).</sub>

### 4. Inspect world state

![Terminal recording: querying world summary, countries, provinces, market prices, auto-generated events, buildings, armies, and wars endpoints](docs/assets/demo_second_terminal_world_state.gif)

<sub>A tour of the read-only world endpoints: `/api/world/summary`, `/countries`, `/provinces`, `/market` (per-good price/supply/demand), and `/events` — the server's own auto-generated alerts for a treasury deficit, a fish shortage, rising unemployment, and militancy — plus `/buildings/queue`, `/armies`, and `/wars`. Raw cast: [demo_second_terminal_world_state.cast](docs/assets/demo_second_terminal_world_state.cast).</sub>

### 5. Admin / ops view

![Terminal recording: querying admin summary, market, tick-profile, and per-country admin endpoints](docs/assets/demo_second_terminal_admin_views.gif)

<sub>`/api/admin/summary` (tick timing, health checks, snapshot history), `/api/admin/market`, `/api/admin/tick-profile`, and `/api/admin/countries/{id}` — the operational view used for debugging the simulation rather than playing it. Raw cast: [demo_second_terminal_admin_views.cast](docs/assets/demo_second_terminal_admin_views.cast).</sub>

### 6. Ask "why" — the explain endpoints

![Terminal recording: calling explain endpoints for a good's price, a country's budget, and a province's employment, each returning a human-readable list of contributing factors](docs/assets/demo_second_terminal_explain_endpoints.gif)

<sub>`/api/explain/good/grain`, `/api/explain/good/iron`, `/api/explain/country/{id}/budget`, and `/api/explain/province/{id}/employment` — each returns a `factors` list (supply vs. demand, price pressure, tax rates, spending) backing the headline number. This is the "why did this change?" answer the architecture is built to support — see [Preview is not authority](#architecture-hard-rules). Raw cast: [demo_second_terminal_explain_endpoints.cast](docs/assets/demo_second_terminal_explain_endpoints.cast).</sub>

### 7. Explain a single POP's needs

![Terminal recording: grabbing a province ID, pulling a POP ID out of the province inspect payload, then calling the explain endpoint for that POP's needs](docs/assets/demo_second_terminal_explain_endpoints_pop_needs.gif)

<sub>Same explain API, scoped to one POP group: grab a province ID, pull a `popId` out of `/api/world/provinces/{id}/inspect`, then call `/api/explain/pop/{id}/needs`. Raw cast: [demo_second_terminal_explain_endpoints_pop_needs.cast](docs/assets/demo_second_terminal_explain_endpoints_pop_needs.cast).</sub>

### 8. Grab IDs, then drill into the inspector

![Terminal recording: grabbing a country and province ID, then calling the country and province inspect endpoints, a budget preview, and construction options](docs/assets/demo_second_terminal_grab_ids_then_inspect.gif)

<sub>Grab a country and province ID from the list endpoints, then drill in: `/countries/{id}/inspect`, `/provinces/{id}`, `/provinces/{id}/inspect`, `/countries/{id}/budget-preview`, and `/provinces/{id}/construction-options`. Preview endpoints are read-only — they never mutate world state. Raw cast: [demo_second_terminal_grab_ids_then_inspect.cast](docs/assets/demo_second_terminal_grab_ids_then_inspect.cast).</sub>

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
