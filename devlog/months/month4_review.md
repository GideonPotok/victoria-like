# Victoria II MMO — Month 4 Review

_Date: 2026-04-27 | End of Day 80_

## Theme

"Implement the basic Victoria 2 substrate before adding our novel state mechanics."

The Month 4 question:

> Can the game represent Victoria 2's core society and economy model in a primitive but working form?

**Result: Yes, in v1 form.**

---

## What Was Built (Days 61–80)

### Week 13 — POP Foundation (Days 61–65)

| Component | Status |
|-----------|--------|
| `vic2_basic_mechanics_mvp.md` — locked Month 4 scope | Done |
| `PopGroup` domain model — type, strata, culture, religion, literacy, militancy, consciousness, cash, needs fulfillment, employment | Done |
| Persistent `pop_groups` table; provinces seed POP groups from scenario | Done |
| `ProvincePopGroupMapper` + `/api/world/provinces/{id}` POP fields | Done |
| `MonthlyPopUpdateStage` — monthly pop tick covering income, needs, literacy, militancy, consciousness, mobility | Done |

### Week 14 — Production and Employment (Days 66–70)

| Component | Status |
|-----------|--------|
| RGO production from province POPs (farmers/laborers) | Done |
| Factory model v1 (`FactoryState`, `FactoryProductionStage`) | Done |
| Artisan v1 with stochastic good reconsideration | Done |
| `EmploymentAssignmentStage` — RGO → factory → artisan → state-paid → unemployed | Done |
| `month4_production_test.md` after a 12-month sim run | Done |

### Week 15 — POP Needs, Market, and Money (Days 71–75)

| Component | Status |
|-----------|--------|
| `PopNeedProfileCatalog` — life/everyday/luxury baskets per POP type | Done |
| `PopNeedsStage` — POPs purchase needs, pay taxes, update fulfillment | Done |
| National market v1 with supply/demand price movement, clamped | Done |
| `BudgetStage` — poor/middle/rich tax + education/military/admin spending | Done |
| `month4_pop_market_playtest.md` | Done |

### Week 16 — Politics and Pressure (Days 76–80)

| Component | Status |
|-----------|--------|
| Political-pressure drift from literacy/needs/unemployment | Done |
| Promotion/demotion v1 — capped monthly POP transfers between classes | Done |
| Reform pressure v1 — `SimulationMetrics.ReformPressureByCountry` | Done |
| Unity inspection pass — admin DTOs extended; client surfaces national POPs, budget, RGO/factories, province POPs | Done |
| `month4_review.md`, `vic2_basic_mechanics_status.md`, `pop_known_issues.md`, `month5_playable_slice_targets.md` | Done |

---

## Month 4 Success Criteria

| # | Criterion | Status |
|---|-----------|--------|
| 1 | Provinces contain persistent POP groups | Met |
| 2 | POPs have type, size, culture, religion, literacy, militancy, consciousness, cash, employment, needs | Met |
| 3 | POPs work in RGOs, factories, artisans; soldiers/clergy/bureaucrats categories present | Met (state-paid POPs are paid by `BudgetStage`; full job markets are still v1) |
| 4 | POPs earn money, pay taxes, buy needs, keep or lose cash | Met |
| 5 | RGOs, factories, artisans produce into the national market | Met |
| 6 | National market prices respond to supply and demand | Met (gradual, clamped — see Known Issues) |
| 7 | Budget policy affects POP income, payrolls, treasury, literacy, militancy | Met (in simulation; **not exposed via inspection** — see pop_known_issues.md) |
| 8 | POPs promote or demote slowly under simple rules | Met |
| 9 | Reform pressure emerges from POP conditions | Met (in `SimulationMetrics`; not yet exposed via inspection) |
| 10 | Unity/admin can inspect Month 4 mechanics clearly | Mostly met — see Day 79 deliverable and gaps below |

---

## What Month 4 Proved

The simulation has a recognisable Victoria 2 substrate:

```text
POPs -> work -> income -> taxes -> needs -> market -> militancy/consciousness -> reform pressure
```

Each link in that chain runs every tick or month with deterministic, bounded behavior, and the chain holds under multi-month soak.

Province POPs grow, lose cash, become deprived, become militant, and shuffle classes under capped mobility rules. Markets respond gradually to scarcity instead of snapping to clamps. The country budget can be tuned to reward state-paid POPs or starve them. Reform pressure is a real number derived from POP conditions, not a placeholder.

## What Month 4 Did Not Prove

Month 4 did not deliver:

- A complete Victoria 2 clone — many secondary systems remain placeholders.
- World market or trade between countries.
- Per-strata budget commands (only `TaxRate` is wired through).
- Persistence of `EducationSpending`, `MilitarySpending`, `AdministrationSpending` across loads — they reset to scenario defaults.
- Reform pressure surfacing through admin or Unity endpoints.
- Capitalist factory ownership and dividend distribution.
- Full Unity playable loop — Month 5's job.

See `pop_known_issues.md` for the complete list of Month 4 gaps.

---

## Architecture Decisions Made

1. **Two-world split** — DB-loaded `WorldStateSnapshot` (Domain entities) feeds the simulation `WorldState` (Core/Pops/Countries) on every tick via `CommandWorldStateMapper`. The simulation owns runtime state; only a subset is persisted. This keeps Month 4 substrate testable without a schema change per stage.
2. **POP needs are catalog-driven** — `PopNeedProfileCatalog` defines life/everyday/luxury baskets per POP type with Month 4 default fallbacks. Adding a POP type does not require touching the needs stage.
3. **Stochastic artisan reconsideration** — artisans choose goods on a randomised cadence (avg 42 days) rather than every tick, matching the design in `vic2_basic_mechanics_mvp.md` and reducing churn.
4. **Mobility caps** — promotion/demotion transfers ≤0.1% of source POP per tick, with a one-person floor and source-non-empty guard. Province totals and employment counts stay stable under invariants.
5. **Reform pressure is a metric, not state** — pressure is recomputed each monthly tick from current POP signals; it is not stored on `Country` or persisted. Cheap to recompute, harder to inspect.

---

## Exit Decision

Move to Month 5.

Month 5 should turn the Month 4 substrate into a playable Unity country loop (see `month5_playable_slice_targets.md`). Month 5 must also retire the substrate's visibility gaps (budget categories, reform pressure, dictionary-typed JSON on the Unity side) before piling more UI on top.

Novel mechanics — civic trust, real law rollout, chains of command, disease, climate, newspapers, LLM diplomacy, world market — remain deferred per `month4_targets.md` and `vic2_basic_mechanics_mvp.md`.

## Milestone Build

```bash
dotnet build server/src/VictoriaLike.Server/VictoriaLike.Server.csproj --no-restore
dotnet test server/tests/VictoriaLike.Core.Tests/VictoriaLike.Core.Tests.csproj --no-restore
```

Result: build succeeds, 59/59 tests pass.

Milestone tag candidate after committing the closeout changes:

```text
month4-complete
```
