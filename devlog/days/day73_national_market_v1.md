# Day 73 National Market V1

Day 73 deliverable: domestic supply and demand move national market prices.

## Implemented

`MarketPricingStage` now records national market signals for every good:

- total domestic supply from province and country stockpiles
- total POP need demand
- price pressure as demand divided by supply
- unmet demand as demand above available supply

Prices now move gradually toward the scarcity target instead of snapping instantly. Each weekly tick can move a good by at most 15% of its previous price, bounded by the existing invariant range of `0.5` to `5x` base price.

## Result

Day 73 is complete when:

- scarce goods rise in price across ticks
- abundant goods fall in price across ticks
- market state exposes supply, demand, pressure, and unmet demand
- focused market-pricing tests pass
