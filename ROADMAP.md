# Roadmap

This roadmap is honest about what is built, what is next, and where outside contributors can plug in. It is consolidated from [`docs/current_status.md`](docs/current_status.md), [`docs/roadmap.md`](docs/roadmap.md), [`docs/status/*`](docs/status/), and the month plans under [`devlog/months/`](devlog/months/).

## Today (Pre-Alpha)

The core substrate runs end-to-end. The list below is what a contributor can rely on as already implemented and tested.

**Simulation**
- Fixed-tick server simulation (1 in-game day per tick), command-validated, deterministic.
- POPs with strata, needs baskets (life / everyday / luxury), employment, unemployment, literacy, militancy, consciousness, promotion/demotion.
- RGOs (grain, coal, iron, timber, cotton), factories (cement, steel, fabric, clothes, tools), artisans v1 with stochastic switching.
- National market with supply/demand price movement (v1, untuned — see [pop_known_issues.md P1](docs/status/pop_known_issues.md)).
- Taxation, treasury, education / military / administration spending. Per-strata tax rates exist in the model.
- Reform pressure metric computed per country.

**Server**
- ASP.NET Core REST API + WebSocket hub on `:5001`.
- Persistence on PostgreSQL with restart recovery from latest snapshot.
- Redis health checks, structured logging via Serilog.
- Admin inspection endpoints (`/admin`), explanation services, command audit.

**Client**
- Unity v2 inspection client renders country dashboard, province POPs, market prices, treasury, tax rate, RGO output, factory list.
- REST polling for most views; WebSocket consumer wiring is in progress.

**Content**
- Three scenarios: `tiny-2country`, `phase1-albion-server`, `medium-8country`.
- Goods set: grain, fish, iron, coal, timber, cotton, fabric, clothes, furniture, liquor, tools, luxury_clothes, luxury_furniture, steel, cement.

**Testing & Ops**
- 98 xUnit tests covering loaders, simulation stages, command handling, persistence, explanation, invariants. CI runs them on push to `main`.
- NBomber + fake-client harnesses. Proven envelope: 40 fake clients × 1 hour on tiny economy, 0 errors / 0 tick drift ([known_scale_limits.md](docs/status/known_scale_limits.md)).
- Oracle Cloud Free Tier deployment runbook (scripts checked in, not yet executed — [docs/deployment/oracle-cloud.md](docs/deployment/oracle-cloud.md)).

## Versioned Milestones

Excerpted from [`docs/roadmap.md`](docs/roadmap.md). These are the deliverables — not feature ideas.

### v0.1-prealpha — Playable Country Inspection Loop

- Public repo hygiene, license, contribution docs, passing CI. **← this open-source pass**
- Albion demo scenario with a documented run path. **← shipped via `make run-albion`**
- Unity can inspect country, provinces, POP groups, market prices, treasury, tax rate. **← shipped at Day 79**
- Every Unity-fronted public flow has an equivalent `curl`-based path.
- Known gaps documented honestly. **← [`docs/current_status.md`](docs/current_status.md), [`docs/status/`](docs/status/)**

### v0.2 — Explanation Tools and Medium Scenario

- Medium scenario becomes the recommended demo after Albion.
- Explanation endpoints and UI show *why* prices, needs fulfillment, treasury, and unemployment changed.
- Player-facing status and warning text.
- Scenario / content docs are good enough for non-engine contributors.

### v0.3 — Two-Player Persistent Multiplayer Slice

- Two players connect to the same persistent world.
- Country control authorization is clear and tested.
- Commands are isolated by player / country ownership ([networking_known_issues.md N1](docs/status/networking_known_issues.md)).
- Reconnect and restart behavior is documented and covered by tests.

### v0.4 — Better POP Purchasing and Market Behavior

- POP buying behavior becomes more expressive and easier to explain.
- Shortages, substitutions, income limits, and unmet needs produce clearer outcomes.
- Market explanation tools answer "why did this price change?" and "why did this POP suffer?"

### Later Research Tracks

- Diplomacy, spheres, and rank.
- State capacity and delayed implementation.
- Public health, disease, and demographic shocks.
- Newspapers and diegetic explanation layers.
- Richer military logistics and war goals.

## In Flight Right Now

Pulled from [`devlog/months/month5_8_weeks.md`](devlog/months/month5_8_weeks.md) and the Month 5 playable-slice plan. The simulation substrate is in place; the work in flight is making it actually playable through Unity.

- **Week 17 — Country dashboard:** treasury, budget, market summary, POP / unemployment / literacy summaries, top shortages, province list with sort/filter, selected province detail.
- **Week 18 — Budget and construction action loop:** wire budget sliders + construction commands through the validated command pipeline; expose effects.
- Following weeks: military and movement polish, scenario/balance iteration, explanation surface for Week 19+.

## Where Contributors Can Plug In

If you don't know where to start, read [`docs/good-first-issues.md`](docs/good-first-issues.md). Below is the bigger picture by area.

**Content & scenarios** — lowest barrier to entry. New goods, factory chains, balance CSVs, fresh tiny scenarios that highlight one shortage / one bottleneck. Docs in [`docs/modding_goods.md`](docs/modding_goods.md) and [`docs/modding_scenarios.md`](docs/modding_scenarios.md).

**Economy balance & tuning** — open issues with priority labels in [`docs/status/economy_known_issues.md`](docs/status/economy_known_issues.md) and [`docs/status/pop_known_issues.md`](docs/status/pop_known_issues.md). Concrete tasks: tune price elasticity (P1), enforce treasury floor (E6), add factory input buy-phase (E5), gate promotions on local job openings (P6).

**Server simulation** — sharpen stages in `VictoriaLike.Core`. Capitalist factory ownership (P5), per-strata tax commands (P4), POP grain split (P9). Tests live alongside the stages they exercise.

**Unity UI** — biggest under-built area. Sorting on province list, market price change indicators, POP needs tooltips, reform pressure readout once the API surface lands, replacement for `JsonUtility` to handle dictionary fields (P7) so per-good factory breakdowns can render.

**Networking & multiplayer prep** — `NetworkingKnownIssues` items N1 (centralized command authorization), N2 (structured command result types), N5 (token in URL → subprotocol header), N7 (idempotency key), N8 (rate limiting). v0.3 depends on these.

**Documentation** — POP-vs-Vic2 comparison notes, "why did price change?" walkthrough with screenshots, glossary, troubleshooting page for `:5001` port conflicts. See [`docs/starter_issues.md`](docs/starter_issues.md) for the existing curated list.

**Deployment** — [`docs/deployment/oracle-cloud.md`](docs/deployment/oracle-cloud.md) is the sealed handoff. Picking up the Oracle deploy, running through all eight steps, and reporting back what broke would be a high-impact contribution. The Fly.io path described in that doc is also genuinely uncommitted territory.

**Vic2 research** — historical research notes that compare a mechanic in this codebase against vanilla Vic2 (cited from the wiki) are valuable. The reference scrape lives under [`docs/vic2_reference/`](docs/vic2_reference/) and [`wiki/`](wiki/).

## What This Roadmap Is Missing (Contributors welcome)

- A full historical 1836 scenario.
- A balanced economy on large scenarios.
- Deep diplomacy, spheres, crises, or migration models.
- Production-quality Unity UX.
- Horizontal server scaling or thousands of concurrent clients.
- A stable public command protocol.


## Launch Principle

The public promise is narrow on purpose: this is an inspectable, server-authoritative political-economy sandbox with real POP and market mechanics. It is not a finished game. The launch claim worth making — taken from [`docs/reliability_and_load_principles_notes.md`](docs/reliability_and_load_principles_notes.md) — is the architecture, not the content depth:

> Victoria-like is a Victoria-inspired simulation built as an online service: authoritative server, deterministic command pipeline, idempotent retries, invariant-checked world state, restart recovery, WebSocket fanout measurement, and black-box fake-client soak tests.
