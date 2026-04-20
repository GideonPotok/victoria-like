# Week 13 POP Foundation Plan

Scope: Days 62-65.

This plan turns the Month 4 scope lock into implementation-level work for the POP foundation. Month 4 remains a basic Victoria 2 substrate, not the later novel state-mechanics design.

## Confirmed Mechanics

### POP Strata

POP class/strata is modeled explicitly.

```text
strata: poor | middle | rich
```

Default mapping:

- poor: farmers, laborers, craftsmen, soldiers
- middle: clerks, clergy, bureaucrats, artisans
- rich: aristocrats, capitalists

Taxes use strata. POP type remains separate and drives employment, production, and promotion/demotion.

### Month 4 Needs Basket

Use a small custom basket for Month 4.

```text
life needs:
- grain
- clothes

everyday needs:
- furniture
- liquor
- tools

luxury needs:
- luxury_clothes
- luxury_furniture
```

Supporting goods:

```text
raw:
- grain
- coal
- iron
- timber
- cotton

manufactured:
- fabric
- clothes
- furniture
- liquor
- tools
- luxury_clothes
- luxury_furniture
- steel
- cement
```

This is intentionally smaller than Vic2. It is enough to test labor, production, prices, POP purchasing, deprivation, and militancy.

### Promotion/Demotion

Use approximate Vic2-like factors:

- literacy
- consciousness
- employment/unemployment
- cash reserve
- available jobs
- state spending for soldiers, clergy, and bureaucrats

Rates should be tiny and capped. Exact Vic2 formulas are deferred.

### Market

Month 4 uses national-market-only pricing.

There is no world market, sphere market, trade priority system, or advanced import model in Month 4.

### Artisans

Artisans dynamically choose produced goods, but not every month.

Rules:

- each artisan POP has a current produced good
- reconsideration is stochastic
- mean time between reconsiderations is about 42 days per POP
- on reconsideration, choose based on observed producer profitability
- lookback window is random per reconsideration
- lookback window range: 2-6 months
- score candidate goods by average profit earned by producers of that good over the lookback window
- add inertia so the current good must be meaningfully beaten before switching

Month 4 data hooks:

```text
ArtisanState
- currentProducedGood
- daysUntilReconsider
- lastReconsideredAt
```

Month 4 market history hook:

```text
GoodProfitHistory
- month
- goodId
- averageProducerProfit
- producerCount
```

Implementation can start with persistent `currentProducedGood` and add the full switching logic on Day 68.

### Politics

For Month 4:

- consciousness mostly comes from literacy and needs awareness
- militancy mostly comes from unmet needs and unemployment
- satisfied needs reduce militancy slowly
- ideology, issues, parties, movements, and reform details are deferred

## Day 62 — POP Data Model

Implement persistent POP groups.

### Domain

```text
PopGroup
- id
- provinceId
- size
- popType
- strata
- culture
- religion
- literacy
- militancy
- consciousness
- cash
- lifeNeedsFulfillment
- everydayNeedsFulfillment
- luxuryNeedsFulfillment
- employedCount
- unemployedCount
```

### Validation

- `size >= 0`
- `popType`, `strata`, `culture`, and `religion` are non-empty
- `strata` is `poor`, `middle`, or `rich`
- `literacy` is `0.0-1.0`
- `militancy` is `0.0-10.0`
- `consciousness` is `0.0-10.0`
- need fulfillment fields are `0.0-1.0`
- `employedCount >= 0`
- `unemployedCount >= 0`
- `employedCount + unemployedCount <= size`
- explicit province POP sizes sum to province population

### Persistence

Create `pop_groups` table and load/save it with provinces.

Fresh scenarios without POPs must still load by creating a fallback farmer POP matching province population. Day 63 replaces fallback data with authored POP distributions.

### Snapshot

Snapshots include POP groups and reject invalid POP totals.

### Pass Criteria

- server build passes
- load-test build passes
- core tests pass
- existing tiny scenario loads and each province has at least one POP group
- loaded database world attaches POP groups to provinces

## Day 63 — POP Seeding and Province Setup

Author explicit POP data for every current province.

Each province should define:

- total population
- POP breakdown
- culture
- religion
- literacy baseline
- RGO type/proxy output
- owner country

The first authored scenario can remain small. The goal is not demographic accuracy; it is a legible spread of farmers, laborers, craftsmen, clerks, soldiers, clergy, bureaucrats, aristocrats, capitalists, and artisans.

## Day 64 — POP Inspection API

Expose:

- country POP summary
- province POP detail
- POP type summary
- strata summary
- literacy summary
- militancy summary
- consciousness summary
- unemployment summary

Admin/Unity should be able to inspect society without database queries.

## Day 65 — Monthly POP Tick

Create a monthly POP update stage.

Monthly stage includes early placeholders for:

- POP income
- POP needs
- literacy drift
- militancy drift
- consciousness drift
- promotion/demotion hooks

Exact formulas can be crude, but they must be deterministic, clamped, and documented.
