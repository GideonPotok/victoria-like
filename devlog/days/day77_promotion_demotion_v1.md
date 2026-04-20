# Day 77 Promotion Demotion V1

Day 77 deliverable: POP mobility under small capped rules.

## Implemented

`MonthlyPopUpdateStage` now performs tiny deterministic monthly POP transfers when existing promotion or demotion conditions fire.

Promotion candidates:

- farmers/laborers promote into craftsmen
- craftsmen promote into clerks
- clerks promote into capitalists

Demotion risks:

- capitalists/aristocrats demote into clerks
- clerks/artisans demote into craftsmen
- craftsmen demote into laborers

Transfers are capped at 0.1% of the source POP per monthly tick, with a minimum of one person and source POPs kept non-empty. If the target POP class does not already exist in the province, the stage creates one with inherited political state and default Month 4 needs.

## Result

Day 77 is complete when:

- promotion/demotion changes POP sizes, not just event logs
- province population totals remain stable
- employment counts remain clamped after mobility
- focused mobility tests pass
