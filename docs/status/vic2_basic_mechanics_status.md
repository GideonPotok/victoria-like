# Vic 2 Basic Mechanics Status

_Date: 2026-04-27 | End of Month 4 (Day 80)_

This is the implementation status of the scope locked in `vic2_basic_mechanics_mvp.md`.

Status legend:

- **Done** — implemented, tested, demoable.
- **v1** — implemented in primitive Month 4 form; expected to be tuned/expanded later.
- **Partial** — implemented but with a known gap (see `pop_known_issues.md`).
- **Deferred** — explicitly deferred per Month 4 scope.

---

## Data Model

| Item | Status |
|------|--------|
| `PopGroup` (id, province, size, type, strata, culture, religion, literacy, militancy, consciousness, cash, life/everyday/luxury fulfillment, employed, unemployed) | Done |
| Initial POP types (farmers, laborers, craftsmen, clerks, soldiers, aristocrats, capitalists, clergy, bureaucrats, artisans) | Done |
| Strata (poor/middle/rich) with default mapping | Done |
| Province additions (owner, market, RGO, POPs, culture, religion, literacy baseline) | Done |
| Validation rules (size, employment caps, strata, normalized literacy/militancy) | Done — enforced via `WorldInvariantChecker` |

## Production

| Item | Status |
|------|--------|
| RGOs per province (grain, coal, iron, timber, cotton) | v1 — `output = workers × per-worker × throughput` |
| Factory v1 (id, country/province, type, level, employment, inputs, output, profit) | Done |
| Initial factory types (cement, steel mill, fabric, clothes, tool) | v1 |
| Artisans v1 with stochastic switching (42-day average cadence, 2–6 month profit lookback) | Done |
| Player/state factory ownership | v1 — capitalist behavior deferred |

## Employment

| Item | Status |
|------|--------|
| Employment priority: RGO → factory → artisan → state-paid → unemployed | Done — `EmploymentAssignmentStage` |
| Employed/unemployed tracked per POP | Done |

## Needs

| Item | Status |
|------|--------|
| Life basket (grain, clothes) | Done |
| Everyday basket (furniture, liquor, tools) | Done |
| Luxury basket (luxury_clothes, luxury_furniture) | Done |
| Default-fill for omitted scenario need categories | Done |

## Market

| Item | Status |
|------|--------|
| National market accepts RGO/factory/artisan output | Done |
| Domestic supply satisfies POP and factory demand first | Done |
| Supply/demand price movement, finite + clamped | v1 — gradual smoothing, see issue P1 |
| World market | Deferred |

## Money & Taxes

| Item | Status |
|------|--------|
| POPs earn income, pay taxes, buy needs, retain cash | Done |
| Treasury accrues income tax | Done |
| Strata-aware poor/middle/rich tax | v1 — strata commands not exposed; flat `TaxRate` is the command-compatible fallback |
| Tariff income | v1 |

## Budget

| Item | Status |
|------|--------|
| Education spending pays clergy/clerks, nudges literacy | Done |
| Military spending pays soldiers, reduces militancy | Done |
| Administration spending pays bureaucrats, nudges consciousness | Done |
| Persistence of `EducationSpending`/`MilitarySpending`/`AdministrationSpending` across DB load | Partial — values reset to scenario defaults on load (see issue P2) |
| Spending sliders/commands | Deferred to Month 5 |

## Politics

| Item | Status |
|------|--------|
| Literacy drift | Done |
| Militancy drift | Done |
| Consciousness drift | Done |
| Promotion/demotion v1 (capped monthly transfers) | Done |
| Reform pressure score per country | Done — stored in `SimulationMetrics.ReformPressureByCountry` |
| Reform pressure exposed via API | Partial — admin/world endpoints do not yet surface it (see issue P3) |
| Optional placeholder reforms (weighted voting, minimum wage, school system) | Deferred |

## Inspection

| Item | Status |
|------|--------|
| Country POP summary (Unity) | Done — Day 79 |
| Province POP detail (Unity) | Done — Day 79 |
| POP type breakdown (Unity) | Done — Day 79 |
| Literacy/militancy/consciousness/unemployment summaries (Unity) | Done — Day 79 |
| RGO type and outputs per tick (Unity) | Done — Day 79 |
| Factory list per province (Unity) | Done — Day 79 |
| Treasury and tax rate (Unity) | Done |
| Market prices via WS subscription (Unity) | Done |
| Budget sliders / current spending levels (Unity) | Partial — only `tax_rate` exposed (see issue P2) |
| Reform pressure (Unity) | Partial — see issue P3 |
| Angry POPs list | Deferred |

## Excluded by Design

These remain deferred per `vic2_basic_mechanics_mvp.md`:

- Civic trust
- Real law rollout
- Chains of command
- Delayed implementation
- State capacity as a deep mechanic
- Disease, climate
- Advanced diplomacy, war, spheres
- Newspapers
- LLM diplomacy
- Deep capitalist AI
- World market
- Full migration model beyond crude internal movement
