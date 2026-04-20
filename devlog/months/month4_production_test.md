# Month 4 Production Test

Day 70 deliverable: run a 12-month production integration test.

## Test Added

`ProductionIntegrationTests.TwelveMonthProductionRun_KeepsEconomyCoherent` runs the normal weekly simulation stage order for one in-game year:

1. advance date
2. assign employment
3. produce RGO goods
4. produce factory goods
5. produce artisan goods
6. distribute national stockpiles
7. update market prices
8. fulfill POP needs
9. run monthly POP updates when the weekly date lands on the first day of a month
10. update budget

The test checks each tick against `WorldInvariantChecker`.

## Coverage

The 12-month run verifies:

- RGOs produce raw `grain` and `iron`
- a steel mill consumes `coal` and `iron`
- artisans consume `timber` and produce `furniture`
- POP cash reserves change over the run
- unemployment exists after assignment
- produced goods enter market production and supply state
- prices remain inside invariant bounds for all active goods
- at least one monthly POP update occurs during the run

## Result

Command run:

```bash
dotnet test server/VictoriaLike.Server.sln --filter ProductionIntegrationTests
```

Result: passed, 1/1 tests.

Existing build warnings remain:

- server package pruning warnings for several `Microsoft.Extensions.*` references
- existing nullable warnings in older domain/scenario classes
- existing xUnit analyzer warning in `SimulationSmokeTests`

No Day 70-specific failures were found.
