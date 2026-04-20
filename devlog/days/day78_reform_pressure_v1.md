# Day 78 Reform Pressure V1

Day 78 deliverable: political/social reform pressure from POP conditions.

## Implemented

Monthly POP updates now recalculate country-level reform pressure after literacy, consciousness, militancy, and mobility changes.

Pressure is population-weighted and comes from:

- militancy
- consciousness
- unemployment share
- unmet needs

The result is stored in `SimulationMetrics.ReformPressureByCountry` and clamped to `0-100`.

## Result

Day 78 is complete when:

- each country receives a deterministic reform pressure score
- worse POP conditions increase pressure
- pressure remains clamped to invariant-friendly bounds
- focused reform-pressure tests pass
