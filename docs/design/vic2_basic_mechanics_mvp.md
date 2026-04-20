# Vic 2 Basic Mechanics MVP

Day 61 deliverable: locked Month 4 scope.

Status: locked for Month 4.

This document is the scope boundary for Days 61-80. If a proposed feature does not directly help POPs work, earn, consume, change class, affect a national market, or create crude political pressure, it is deferred.

## Purpose

Month 4 implements the primitive Victoria 2 substrate. The goal is not a complete Victoria 2 clone and not the later Victoria successor design. The goal is a small, inspectable, server-authoritative model where POPs work, earn, consume, change, and create political pressure.

The key Month 4 question is:

> Can the game represent Victoria 2's core society/economy model in a primitive but working form?

## Include

Month 4 includes only:

- POPs
- need baskets
- literacy
- militancy
- consciousness
- employment and unemployment
- RGO production
- factory production
- artisan production
- national market
- POP purchasing
- poor, middle, and rich taxes
- basic government budget categories
- crude reform pressure
- Unity/admin inspection of the above

## Exclude

Month 4 explicitly excludes:

- civic trust
- real law rollout
- chains of command
- delayed implementation
- state capacity as a deep mechanic
- disease
- climate
- advanced diplomacy
- war
- spheres
- newspapers
- LLM diplomacy
- deep capitalist AI
- world market
- full migration model beyond crude internal movement

These exclusions are deliberate. They belong after the base society/economy substrate is visible and testable.

## Core Data Model

### PopGroup

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

Initial POP types:

- farmers
- laborers
- craftsmen
- clerks
- soldiers
- aristocrats
- capitalists
- clergy
- bureaucrats
- artisans

Some types can be stubs at first. The schema should support them from the beginning.

Initial strata:

- poor
- middle
- rich

Default mapping:

- poor: farmers, laborers, craftsmen, soldiers
- middle: clerks, clergy, bureaucrats, artisans
- rich: aristocrats, capitalists

Validation rules:

- `size >= 0`
- `employedCount >= 0`
- `unemployedCount >= 0`
- `employedCount + unemployedCount <= size`
- `strata` is `poor`, `middle`, or `rich`
- `literacy` is normalized from `0.0` to `1.0`
- `militancy` and `consciousness` use a `0.0` to `10.0` scale
- need fulfillment values are normalized from `0.0` to `1.0`
- a province with explicit POPs should have POP size totals matching province population

### Province Additions

Each province should define:

- owner country
- market or national market association
- total population derived from POPs
- RGO type
- POP breakdown
- primary culture
- optional minority culture
- religion
- literacy baseline

Example:

```text
Province: Lille
RGO: coal
POPs:
- 30,000 laborers
- 8,000 craftsmen
- 2,000 clerks
- 1,000 capitalists
- 1,500 clergy
```

## Production MVP

### RGOs

Each province has one RGO.

Initial RGO types:

- grain farm
- coal mine
- iron mine
- timber camp
- cotton farm, if needed

Employment:

- farmers work farms
- laborers work mines and extraction RGOs

Formula:

```text
output = employedWorkers * rgoOutputPerWorker * throughputModifier
```

Keep `throughputModifier` mostly `1.0` during Month 4.

### Factories

Factories exist at country or state level.

```text
Factory
- id
- countryId/stateId
- type
- level
- employedCraftsmen
- employedClerks
- inputGoods
- outputGood
- cash/profit optional
```

Initial factory types:

- cement factory
- steel mill
- fabric factory
- regular clothes factory
- tool factory, if tools are part of the goods set

Player/state ownership is acceptable for Month 4. Deep capitalist behavior is deferred.

### Artisans

Artisan POPs self-employ and produce simple goods.

Rules:

- buy inputs if needed
- produce simple goods
- earn income if profitable
- lose cash and risk demotion if unprofitable

Initial artisan goods:

- clothes
- furniture
- tools
- liquor or wine optional

## Employment MVP

Employment assignment priority:

1. Existing RGO workers remain if space exists.
2. Factories hire craftsmen and clerks.
3. Artisans self-employ.
4. Soldiers, clergy, and bureaucrats are paid by the state.
5. Remaining POP members are unemployed.

Track:

- employed count
- unemployed count
- workplace type
- wage or income

## Needs MVP

Need tiers:

```text
life needs:
- grain
- clothes

everyday needs:
- furniture
- liquor
- tools

luxury needs:
- luxury clothes
- luxury furniture placeholder
```

Month 4 uses a small custom basket:

- life: `grain`, `clothes`
- everyday: `furniture`, `liquor`, `tools`
- luxury: `luxury_clothes`, `luxury_furniture`

## Artisan Switching MVP

Artisans dynamically choose goods, but not every month.

Rules:

- each artisan POP has a current produced good
- each artisan POP reconsideration is stochastic, averaging about 42 days
- on reconsideration, choose a good based on observed producer profitability
- the profit lookback window is random per reconsideration
- the lookback window ranges from 2 to 6 months
- candidates are scored by average profit for producers of that good over the lookback window
- the current good gets inertia so artisans do not churn constantly

Implementation hooks:

```text
ArtisanState
- currentProducedGood
- daysUntilReconsider
- lastReconsideredAt

GoodProfitHistory
- month
- goodId
- averageProducerProfit
- producerCount
```

The full artisan switching implementation belongs to Day 68. Day 62 should keep the POP model compatible with it.

## Day 61 Lock

Month 4 starts with this order:

1. POP persistence and seeded province society.
2. POP inspection.
3. Monthly POP stage.
4. Production and employment.
5. Needs, market, money, and taxes.
6. Literacy, militancy, consciousness, promotion/demotion, and reform pressure.

Month 4 does not start with advanced state mechanics. Basic Victoria-style substrate comes first.

Each POP type can have different quantities, but the first implementation should prefer clarity over exact historical balancing.

## Monthly POP Tick

Daily tick remains for:

- production
- market price update
- construction progress, if still daily

Monthly tick handles:

- POP income
- taxes
- POP needs purchasing
- literacy drift
- militancy drift
- consciousness drift
- promotion/demotion

Effects:

```text
low life needs fulfillment -> militancy rises
high needs fulfillment -> militancy falls slowly
literacy + education spending -> literacy rises slowly
literacy + needs awareness -> consciousness rises slowly
unemployment -> militancy rises
cash + literacy + job openings -> possible promotion
poverty + unemployment -> possible demotion
```

## National Market MVP

The national market should:

- receive goods from RGOs, factories, and artisans
- satisfy POP and factory demand from domestic supply first
- optionally use imports as a placeholder
- move prices based on supply and demand
- keep prices finite and clamped

Do not implement full world market in Month 4.

## Budget MVP

Budget controls:

- poor tax
- middle tax
- rich tax
- education spending
- military spending
- administration spending

Effects:

- taxes reduce POP disposable income
- taxes increase treasury
- education spending pays clergy and supports literacy drift
- military spending pays soldiers
- administration spending pays bureaucrats
- unpaid or underpaid state POPs lose fulfillment and may become more militant

## Reform Pressure MVP

Track:

```text
politicalReformPressure
socialReformPressure
```

Inputs:

- average militancy
- average consciousness
- literacy
- poor needs failure
- unemployment

Optional placeholder reforms:

- weighted voting
- minimum wage
- school system

Effects can be crude. The important part is that POP conditions create visible pressure.

## Inspection Requirements

Server/admin/Unity should expose:

- country POP summary
- province POP detail
- POP type summary
- literacy summary
- militancy summary
- unemployment summary
- angry POPs list
- national population
- budget sliders/current budget policy
- market prices
- RGO status
- factory status
- artisan summary

Month 4 is successful only if the simulation is legible.

## Non-Goals

Do not optimize for perfect balance, historical detail, or deep political simulation yet. Month 4 is about getting the substrate working end to end:

```text
POPs -> work -> income -> taxes -> needs -> market -> militancy/consciousness -> pressure
```
