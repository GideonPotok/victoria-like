# Good First Issues

A curated set of starter tasks drawn from the real backlog. Each issue is scoped small, points at concrete files, and lists acceptance criteria. Most of these can be done without understanding the full simulation engine.

Sourced from:
- [docs/starter_issues.md](starter_issues.md) — the original starter list
- [docs/status/economy_known_issues.md](status/economy_known_issues.md), [pop_known_issues.md](status/pop_known_issues.md), [networking_known_issues.md](status/networking_known_issues.md), [admin_tooling_status.md](status/admin_tooling_status.md) — active trackers with prioritized issues

Tag suggestion when opening these as GitHub issues: `good first issue` plus one of `content`, `simulation`, `Unity UI`, `docs`, `networking`, `balance`.

---

## Content & Scenarios

These touch only JSON/CSV data plus tests. No engine knowledge required.

### 1. Add a coal-and-tools industrial-bottleneck scenario

**Why:** the existing scenarios are either too small (`tiny-2country`) to show a real shortage cascade or too large (`medium-8country`) for a first-glance demo. A two-province scenario where coal is the choke point would make the "shortage → price → POP cash → militancy" loop visible in under five minutes of ticking.

**Files:** new file under `server/content/scenarios/`. Pattern after `phase1-albion-server.json` (smaller, hand-crafted, well-commented).

**Acceptance criteria:**
- [ ] One playable country, two provinces.
- [ ] One province has a `coal_mine` RGO, the other has a `tools_factory` factory.
- [ ] Tools production is starved of coal within ~30 ticks of game start.
- [ ] Scenario validates: `dotnet test server/VictoriaLike.Server.sln --filter "FullyQualifiedName~ScenarioLoader"`.
- [ ] `make run-<your-scenario>` target added to the Makefile, mirroring `run-albion`.
- [ ] A short note under `docs/demo_script.md` explaining what to watch for.

### 2. Add three new industrial goods with base prices

**Why:** the current set tops out at `cement`. Adding `glass`, `pulp`, and `paper` (or any three plausible chain extensions) gives factory chains room to grow.

**Files:** `server/content/goods.json`, scenarios that should trade them.

**Acceptance criteria:**
- [ ] Three new entries in `goods.json` with `id`, `displayName`, `basePrice`, `category`.
- [ ] At least one existing scenario adds market starting prices for the new goods.
- [ ] No regressions: `dotnet test server/VictoriaLike.Server.sln`.

### 3. Add a scenario review checklist for original / non-proprietary content

**Why:** the project will receive scenario PRs from Vic2 fans; we need a clear "what's OK to copy, what isn't" gate.

**Files:** new doc at `docs/scenario_review_checklist.md`. Link from `docs/modding_scenarios.md` and `.github/ISSUE_TEMPLATE/content_contribution.md`.

**Acceptance criteria:**
- [ ] Checklist covers: country names, province names, POP types, balance numbers, descriptive text.
- [ ] Explicitly bans copying from Paradox files and wikis with copyrighted prose.
- [ ] Permitted sources: historical atlases, Wikipedia (with citation), original research.

---

## Simulation Tests (xUnit)

These don't require any server / database — `VictoriaLike.Core.Tests` is pure C#.

### 4. Test: POP needs fulfillment under rising food prices

**Why:** the price → POP cash → unmet needs path is the central economic loop. There is no isolated regression test for "grain price doubles, POP grain fulfillment halves while cash holds, then drops."

**Files:** new test class under `server/tests/VictoriaLike.Core.Tests/`. Pattern after existing stage tests (e.g., `MonthlyPopUpdateStageTests`).

**Acceptance criteria:**
- [ ] Construct a fixture POP with known income and a known grain need.
- [ ] Advance the market price and run `PopNeedsStage`.
- [ ] Assert fulfillment drops monotonically as price rises until cash hits zero, then fulfillment crashes.
- [ ] Runs with `dotnet test server/VictoriaLike.Server.sln --filter "FullyQualifiedName~YourTestClass"`.

### 5. Test: treasury changes after tax rate changes

**Why:** the `ChangeTaxRateCommandHandler` is the most-touched command in scenario / demo workflows. No focused round-trip test exists.

**Files:** `server/tests/VictoriaLike.Core.Tests/`.

**Acceptance criteria:**
- [ ] Submit `ChangeTaxRate` via the command pipeline against a tiny in-memory world.
- [ ] Run several ticks; assert treasury delta matches expected POP income × tax rate.
- [ ] Run a second command (lower rate); assert the rate change applies cleanly.

### 6. Test: scenario validation errors with missing goods

**Why:** the loader silently produces broken worlds today if a scenario references a good not in `goods.json`. A focused negative test forces the failure mode to be loud.

**Files:** `server/tests/VictoriaLike.Core.Tests/ScenarioLoader*Tests.cs`.

**Acceptance criteria:**
- [ ] Test scenario references `good_that_does_not_exist`.
- [ ] `ScenarioLoader` throws / returns a descriptive error rather than producing a world with dangling references.
- [ ] Error message names the offending good and the file path.

### 7. Regression test: restart recovery of budget settings

**Why:** [`docs/status/pop_known_issues.md`](status/pop_known_issues.md) P2 documented (and recently resolved) a bug where `EducationSpending` / `MilitarySpending` / `AdministrationSpending` / per-strata taxes reset to scenario defaults across restart. The `BudgetPersistenceRoundTripTests` cover the round trip but not the load-from-snapshot path.

**Files:** `server/tests/VictoriaLike.Core.Tests/`.

**Acceptance criteria:**
- [ ] Test seeds a world, mutates budget values, snapshots, loads from snapshot.
- [ ] Asserts every budget field round-trips exactly.

---

## Unity UI

You need Unity 2023 LTS open to do these. None require simulation engine changes.

### 8. Add sorting to the province list

**Why:** the province list in the country dashboard renders in scenario order today. Sorting by population / RGO output / unemployment makes the dashboard usable on the `medium-8country` scenario.

**Files:** `client-unity/v2/My project/Assets/Scripts/UI/`. Pattern after the existing list rendering.

**Acceptance criteria:**
- [ ] Sort dropdown above the province list (name / pop / RGO output / unemployment).
- [ ] Default sort is alphabetical.
- [ ] Sort persists when the player switches province and returns to the list.

### 9. Market price change indicator

**Why:** static prices are unreadable. A small ▲ / ▼ + delta vs the previous tick makes the market panel feel alive.

**Files:** Unity market panel UI script(s) under `client-unity/v2/My project/Assets/Scripts/`.

**Acceptance criteria:**
- [ ] Each market good row shows direction and percentage change vs the previous tick.
- [ ] Indicator hides when delta is near zero (avoid noise).
- [ ] Works on REST poll cadence — does **not** require WebSocket subscription work.

### 10. POP needs tooltip

**Why:** the POP panel shows `life: 0.39` etc. with no explanation of what "life" needs comprise. A tooltip listing the good breakdown closes the loop.

**Files:** Unity POP panel UI script(s).

**Acceptance criteria:**
- [ ] Hovering "life needs" shows: grain, clothes (current basket) and per-good fulfillment.
- [ ] Hovering "everyday needs" shows: furniture, liquor, tools.
- [ ] Hovering "luxury needs" shows: luxury_clothes, luxury_furniture.
- [ ] Tooltip data comes from the existing inspection DTOs.

### 11. Clearer connection-state messaging

**Why:** when the server is down, the client just shows stale data. A small badge ("connected", "reconnecting", "disconnected") near the top bar surfaces what's actually happening.

**Files:** Unity top bar / status UI under `client-unity/v2/My project/Assets/Scripts/`.

**Acceptance criteria:**
- [ ] Three explicit states with distinct visual treatments.
- [ ] State transitions are visible without restarting the client.
- [ ] No new server work required.

---

## Docs

### 12. POP-model comparison vs Victoria 2

**Why:** Vic2 fans will ask "is this the Vic2 POP model?" out of the gate. A short doc that explicitly compares strata, needs baskets, promotion/demotion mechanics, and ideology/issue weights against vanilla Vic2 sets honest expectations.

**Files:** new doc at `docs/comparisons/pop_model_vs_vic2.md`. Reference [`docs/vic2_reference/gameplay_design_analysis.md`](vic2_reference/gameplay_design_analysis.md) for Vic2 mechanics.

**Acceptance criteria:**
- [ ] Side-by-side table of POP attributes in this project vs Vic2.
- [ ] Notes which Vic2 behaviors are explicitly out of scope (e.g., upper-house reform gate, ideology drift formula).
- [ ] Linked from the main README.

### 13. "Why did price change?" walkthrough with sample API output

**Why:** the explanation endpoints exist on the server but nobody has documented how to use them as a player or a contributor.

**Files:** new doc at `docs/walkthroughs/why_did_price_change.md`.

**Acceptance criteria:**
- [ ] Reproducible recipe starting from `make run-albion`.
- [ ] `curl` commands hitting the relevant explanation endpoints.
- [ ] Annotated sample JSON output.
- [ ] Linked from [`docs/modding_scenarios.md`](modding_scenarios.md) and the main README.

### 14. Glossary for POPs, RGOs, national markets, stockpiles, reform pressure

**Why:** every new contributor has to learn these terms by reading source.

**Files:** new doc at `docs/glossary.md`. Linked from the main README.

**Acceptance criteria:**
- [ ] Definitions for: POP, POP group, strata, needs basket, RGO, factory, artisan, national market, stockpile, treasury, reform pressure, militancy, consciousness, literacy.
- [ ] Each entry under 3 sentences with a pointer to where the concept lives in code.

### 15. Troubleshooting page for `:5001` port conflicts and Docker

**Why:** the single most common first-run failure is "port 5001 already in use" or a stale Docker container. The Makefile has `kill-server` and the `infra/README.md` has Docker tips, but nothing collects them.

**Files:** new doc at `docs/troubleshooting.md`.

**Acceptance criteria:**
- [ ] Sections for: port 5001 conflict (use `make kill-server`), stale Postgres container (`make clean && make up`), stale snapshots (`make reset-world`), pending migrations.
- [ ] Linked from the main README and the quickstart.

---

## Backend / Networking (Slightly Heavier)

These touch the command pipeline. Read [`docs/architecture.md`](architecture.md) first.

### 16. Idempotency key on command submission

**Why:** [`networking_known_issues.md`](status/networking_known_issues.md) N7 — a client that retries a command (network blip → no 202 received) double-applies. This is exactly the kind of bug that bites in a real soak.

**Files:** `server/src/VictoriaLike.Server/Api/` (DTOs + controller), `VictoriaLike.Core/Application/Commands/` (queue path).

**Acceptance criteria:**
- [ ] DTO accepts a client-supplied `idempotency_key` (UUID).
- [ ] Server deduplicates commands with the same key within a 60-second window.
- [ ] Duplicate submissions return the original `command_id` and HTTP status, not a new one.
- [ ] Test added in `VictoriaLike.Core.Tests`.

### 17. Sort province list by sort field server-side

**Why:** complement to Unity issue #8. The medium-8country scenario's province list is large enough that returning it sorted reduces client churn.

**Files:** `server/src/VictoriaLike.Server/Api/World/`.

**Acceptance criteria:**
- [ ] `GET /api/world/provinces?sort=name|population|owner` returns sorted results.
- [ ] Invalid sort key returns 400 with a clear error.
- [ ] Test added against the API surface.

---

## How To Pick One

1. Pick the area that interests you most.
2. Skim the linked status doc for additional context.
3. Open a draft PR early — the issue list above is opinionated about acceptance criteria, but the implementation path is yours.
4. Run `dotnet test server/VictoriaLike.Server.sln` before pushing.
5. PR template lives at [`.github/PULL_REQUEST_TEMPLATE.md`](../.github/PULL_REQUEST_TEMPLATE.md).
