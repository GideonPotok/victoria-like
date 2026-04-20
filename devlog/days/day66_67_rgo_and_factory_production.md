# Day 66-67 RGO and Factory Production

Day 66 deliverable: provinces produce raw goods from employed farmers and laborers.

Day 67 deliverable: factories consume inputs and produce outputs.

## Implemented

RGO production now uses province `rgoType` and employed POP labor instead of total province population.

- farm RGOs use employed farmers
- mine and extraction RGOs use employed laborers
- output scales by employed workers and infrastructure
- output enters province stockpiles and market production history

Factory production now has a first-class `FactoryState`.

- factories belong to a country and can optionally be associated with a province
- factories have type, level, employed craftsmen, employed clerks, input goods, output good, output rate, cash reserve, and last-tick profit
- factories consume input goods from national stockpiles
- input shortages proportionally limit output
- output enters national stockpiles and market production history

## Persistence

Migration `014_factories.sql` adds the `factories` table.

The server loads, saves, snapshots, restores, and validates factories through the normal world state path.

## Scenario

`tiny-2country.json` now includes:

- an English steel mill in London
- a French fabric factory in Paris

## Result

Days 66 and 67 are complete when:

- RGO output depends on employed farmers/laborers
- factories consume inputs and produce output
- factory output participates in national market supply
- factories persist through database and snapshot paths
- focused production tests pass
- server build passes
- core tests pass
