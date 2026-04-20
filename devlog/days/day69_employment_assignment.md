# Day 69 Employment Assignment

Day 69 deliverable: employment and unemployment are assigned by the simulation instead of being static scenario values.

## Implemented

A deterministic employment assignment stage now runs before production.

- POP employment is reset each tick before assignment
- farm RGOs employ farmers
- mine/extraction RGOs employ laborers
- factories employ craftsmen and clerks up to level-based capacity
- artisans are treated as self-employed producers
- soldiers, clergy, bureaucrats, aristocrats, and capitalists are treated as state/service/property employed for Month 4
- remaining workers stay unemployed

## Capacity Rules

Month 4 uses deliberately simple capacity values:

- RGO base capacity is 4,000 workers per province
- province infrastructure increases RGO capacity by 25% per infrastructure point
- factory level provides 1,000 craftsmen jobs and 250 clerk jobs

These match the existing production scale: RGOs reach baseline output around 4,000 workers, and factories reach baseline output around 1,000 effective workers.

## Result

Day 69 is complete when:

- production stages read employment assigned by a single stage
- POP employed and unemployed counts are recalculated deterministically
- factories receive employed craftsmen and clerks from available POPs
- excess POP labor becomes unemployment
- focused employment tests pass
- server build passes
