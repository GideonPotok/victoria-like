# Day 74 Taxation And Budget

Day 74 deliverable: poor/middle/rich taxes plus education, military, and admin spending affect POPs and treasury.

## Implemented

Countries now support:

- `PoorTaxRate`
- `MiddleTaxRate`
- `RichTaxRate`
- `EducationSpending`
- `MilitarySpending`
- `AdministrationSpending`

The existing `TaxRate` remains as the compatibility fallback for scenarios and command paths that still set one national tax rate.

## Mechanics

`PopNeedsStage` now selects the effective tax rate by POP strata when wages are paid:

- poor: farmers, laborers, craftsmen, soldiers
- middle: clerks, clergy, bureaucrats, artisans
- rich: aristocrats, capitalists

`BudgetStage` now applies weekly spending costs and small funded-POP effects:

- education spending pays clergy/clerks and nudges literacy
- military spending pays soldiers and reduces soldier militancy
- administration spending pays bureaucrats and nudges consciousness
- spending costs reduce treasury and are reported in treasury-delta metrics

## Result

Day 74 is complete when:

- strata tax rates affect POP take-home income and treasury
- budget spending affects treasury
- education, military, and admin spending have visible POP effects
- focused budget tests pass
