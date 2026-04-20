# Day 71 Need Baskets

Day 71 deliverable: POP types have life, everyday, and luxury need demand.

## Implemented

A deterministic need profile catalog now assigns default Month 4 baskets by POP strata:

- poor POPs: farmers, laborers, craftsmen, soldiers
- middle POPs: clerks, clergy, bureaucrats, artisans
- rich POPs: aristocrats, capitalists

Each profile contains life, everyday, and luxury needs. Scenario-authored need categories still override defaults category-by-category, so existing focused scenarios can keep custom local baskets while omitted categories receive Month 4 defaults.

## Basket Goods

The content goods list now includes the Month 4 basket goods:

- life: `grain`, `clothes`
- everyday: `furniture`, `liquor`, `tools`
- luxury: `luxury_clothes`, `luxury_furniture`

Supporting raw and industrial goods are also registered for pricing and market summaries: `timber`, `cotton`, `fabric`, `steel`, and `cement`.

## Result

Day 71 is complete when:

- default need profiles exist for every Month 4 POP type
- loaded POPs with omitted needs receive life, everyday, and luxury demand
- scenario-authored needs remain supported
- market demand includes life, everyday, and luxury baskets
- focused need-profile tests pass
