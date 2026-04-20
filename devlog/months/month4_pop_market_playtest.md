# Month 4 POP Market Playtest

Day 75 deliverable: verify the Week 15 POP needs, purchasing, market, and budget loop.

## Scope

This playtest covers the current Month 4 substrate:

1. POPs have life, everyday, and luxury need baskets.
2. POPs receive income and pay taxes.
3. POPs buy needs from province stockpiles with cash.
4. National market supply and demand move prices gradually.
5. Budget policy changes treasury and funded POP state.

## Current Loop

The weekly simulation order now supports the Week 15 economy loop:

1. production creates raw, factory, and artisan goods
2. national distribution moves goods between province and country stockpiles
3. market pricing records supply, demand, pressure, and unmet demand
4. POP purchasing pays wages, collects taxes, buys needs, and updates fulfillment
5. monthly POP updates apply literacy, militancy, consciousness, and mobility signals
6. budget policy applies tariffs, spending costs, and funded POP effects

## Observed Behavior

Focused tests demonstrate the intended pressure points:

- omitted scenario need categories receive Month 4 default baskets
- life, everyday, and luxury needs all contribute to market demand
- scarce goods rise in price gradually instead of snapping immediately to the clamp
- abundant goods fall in price gradually
- POP cash limits purchases and lowers need fulfillment
- country treasury receives POP income tax
- poor, middle, and rich tax rates change take-home income by strata
- education spending pays clergy/clerks and nudges literacy
- military spending pays soldiers and reduces soldier militancy
- administration spending pays bureaucrats and nudges consciousness

## Validation

Command run:

```bash
dotnet test server/VictoriaLike.Server.sln
```

Result: passed, 55/55 tests.

Existing warnings remain:

- server package pruning warnings for several `Microsoft.Extensions.*` references
- existing nullable warnings in older domain/scenario classes
- existing xUnit analyzer warning in `SimulationSmokeTests`

## Known Limits

This is still Month 4 v1 behavior:

- no world market or import priority
- no savings-based promotion/demotion beyond event-log signals
- no factory owner dividend distribution
- no explicit strata tax command yet; `TaxRate` remains the command-compatible fallback
- price movement uses simple weekly smoothing and a bounded clamp

## Day 75 Result

Week 15 is functionally connected: POPs can be comfortable or deprived based on income, taxes, local supply, prices, and budget spending. This is sufficient to proceed to Week 16 political pressure work.
