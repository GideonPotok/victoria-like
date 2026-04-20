# Day 63 POP Seeding And Province Setup

Day 63 deliverable: authored scenario data loads POPs, RGO type, culture, religion, and literacy.

## Implemented

The tiny two-country scenario now includes explicit POP groups for every province.

Each province defines:

- total population
- `rgoType`
- output proxy for the existing production stage
- POP breakdown
- POP type
- strata, inferred from POP type unless explicitly set
- culture
- religion
- literacy baseline
- initial cash reserve

## Scenario File

Primary authored scenario:

```text
server/content/scenarios/tiny-2country.json
```

The scenario remains intentionally small:

- 2 countries
- 12 provinces
- 1 shared development market
- 10 POP groups per province

The demographic data is not intended to be historically exact. It is designed to exercise the Month 4 mechanics:

- rural farmer provinces
- mining laborer provinces
- urban craftsmen/clerks/artisans
- soldiers, clergy, bureaucrats
- small aristocrat/capitalist groups
- minority cultures in Wales and Brittany

## RGO Types

Provinces now persist `rgoType` so Day 66 can replace output proxies with RGO labor formulas.

Initial RGO types:

- `grain_farm`
- `coal_mine`
- `iron_mine`
- `timber_camp`

## Backward Compatibility

Scenarios without explicit POP groups still load.

Fallback behavior:

- create one `farmers` POP
- strata `poor`
- culture `primary`
- religion `secular`
- size equals province population
- default `rgoType` is `grain_farm`

## Result

Day 63 is complete when:

- existing scenario loads
- each province has explicit POP groups
- POP sizes sum to province population
- RGO type is non-empty for every province
- server build passes
- core tests pass
