# Day 72 POP Purchasing

Day 72 deliverable: POPs earn income, pay taxes, buy needs, and update fulfillment from actual purchases.

## Implemented

`PopNeedsStage` now handles the weekly POP purchasing loop:

- POPs receive wage income based on class, size, literacy, and employment
- country tax rate is collected from gross POP income into the treasury
- POPs buy life, everyday, and luxury needs from province stockpiles
- purchases are limited by local supply and POP cash
- need fulfillment is calculated from purchased quantity rather than free consumption
- unmet needs still affect militancy

The earlier monthly cash placeholder was removed from `MonthlyPopUpdateStage`; that stage now keeps monthly literacy, militancy, consciousness, and promotion/demotion signaling.

## Result

Day 72 is complete when:

- POP cash changes from wages and purchases during weekly needs resolution
- country treasury receives POP income tax
- insufficient cash or supply lowers life/everyday/luxury fulfillment
- market consumption tracks purchased goods
- focused purchasing tests pass
