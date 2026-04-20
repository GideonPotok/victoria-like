# Economy MVP — Month 2 Design Freeze

_Date: 2026-04-22_

## What this is

A minimal economic simulation that runs server-side every tick. Not a full Victoria II clone.
The goal is: prices move, treasuries fill and drain, players feel their commands mattering.

## What exists already (do not rebuild)

- `GoodDefinition` — id, displayName, basePrice, category
- `MarketState` — Prices, SupplyLastTick, DemandLastTick, ProductionLastTick, ConsumptionLastTick
- `ProvinceState` — OutputsPerTick (production rates), Stockpile, Infrastructure, PopulationIds
- `PopState` — Size, CashReserve, NeedsFulfillment, PopNeedProfile (Life/Everyday/Luxury)
- `CountryState` — Treasury, TaxRate
- `goods.json` — grain, fish, iron, coal, tools

## Goods (locked for month 2)

| id    | category   | base price |
|-------|------------|------------|
| grain | food       | 1.0        |
| fish  | food       | 1.2        |
| iron  | raw        | 2.4        |
| coal  | raw        | 2.1        |
| tools | industrial | 4.5        |

## What EXISTS in month 2 simulation

### Production
- Every province has `OutputsPerTick` — a dict of good → units produced per game-day tick
- Production is fixed per province; no tech modifiers, weather, or input chains
- Province is the unit of production (no building entities this month)
- All province output flows into the one shared market

### Market
- One national market (already seeded per scenario)
- Each tick: supply = sum of all province outputs that tick
- Demand = sum of pop life-need consumption across all provinces
- Price moves each tick based on supply/demand ratio:
  - `excess = (supply - demand) / max(demand, 0.01)`
  - `price_delta = clamp(excess * sensitivity, -0.10, +0.10) * base_price`
  - Prices floor at 5% of base price, ceiling at 400% of base price
- Market does not clear stock — no inventory accumulates (demand is satisfied or not)

### Pop needs and fulfillment
- Provinces have a population count (stored in DB)
- Life needs per tick per 1000 population: grain 0.5, fish 0.2
- Fulfillment = min(1.0, supply / demand) per good, averaged across life goods
- `NeedsFulfillment` stored on province aggregate (not individual pop entities yet)
- Unmet needs are visible in the market summary

### Treasury
- Each game-day tick: `revenue = population * (taxRate / 100) * 0.01`
- Simple flat income — no distinction by pop class this month
- Treasury accumulates; no spending mechanic yet (month 3 concern)

### Tick pipeline (server-side, runs every game-day tick)
1. **CommandProcessing** — apply queued player commands (already done)
2. **Production** — aggregate province outputs into market supply
3. **PopNeeds** — calculate life-need demand, compute fulfillment
4. **MarketClearing** — update prices based on supply/demand
5. **TaxCollection** — add to country treasury based on population and tax rate

## What does NOT exist in month 2

- Building entities (farms, mines, workshops are implicit in OutputsPerTick)
- Separate pop-class entities (Farmers, Laborers, Artisans)
- Everyday or luxury needs
- Tech modifiers or efficiency
- Trade between countries
- Tariffs
- Factory input chains (no iron → tools conversion)
- Military or war
- Diplomacy
- Transportation costs
- Population growth or migration
- Market inventory / stock accumulation

## Scenario changes needed

The `tiny-2country.json` scenario needs `outputsPerTick` on each province.
Suggested initial values (per province, per game-day tick):

England provinces → grain-heavy, some coal/iron
France provinces → grain-heavy, some iron

Example:
```json
"outputsPerTick": { "grain": 0.8, "coal": 0.3 }
```

Population is already in the scenario and DB.

## Persistence

New fields to persist to DB after each tick:
- `market_goods.price` — updated price per good (already has this column via seeding)
- `countries.treasury` — accumulating treasury
- `provinces.needs_fulfillment` — for admin visibility

No new tables needed for month 2.

## Success metric

After month 2 economy is live:
- Start server
- Prices should move over time as production and demand imbalance
- Submit a ChangeTaxRate command → treasury fills faster or slower
- Look at market summary → see which goods are short
- Prices should stabilize near base price if supply/demand are balanced

## What month 3 adds

- Buildings (farms, mines, workshops) with construction queue
- Building output modifies province OutputsPerTick
- Everyday needs
- Pop class differentiation
- Simple spending (infrastructure upkeep, military budget)
