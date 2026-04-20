# Month 4 Targets

_Days 61-80_

Theme: "Implement the basic Victoria 2 substrate before adding our novel state mechanics."

Month 4 answers one question:

> Can the game represent Victoria 2's core society and economy model in a primitive but working form?

This month is not about civic trust, law rollout realism, chains of command, disease, climate, newspapers, advanced diplomacy, war, or LLM diplomacy. Those belong after the basic society/economy model exists.

## Success Criteria

By the end of Day 80, the following should be demonstrably true:

1. Provinces contain persistent POP groups.
2. POPs have type, size, culture, religion, literacy, militancy, consciousness, cash, employment, unemployment, and needs fulfillment.
3. POPs work in RGOs, factories, artisan production, military, clergy, and bureaucracy categories.
4. POPs earn money, pay taxes, buy needs, and keep or lose cash.
5. RGOs, factories, and artisans produce goods into a national market.
6. National market prices respond to supply and demand.
7. Budget policy affects POP income, state payrolls, treasury, literacy, and militancy.
8. POPs promote or demote slowly under simple rules.
9. Basic reform pressure emerges from literacy, consciousness, militancy, unemployment, and poor needs fulfillment.
10. Unity and/or admin UI can inspect the Month 4 mechanics clearly.

## Sequencing

```text
Month 3: trustworthy MMO spine
Month 4: basic Vic 2-style POP/economy/politics mechanics
Month 5: Unity playable country loop + construction polish + simple war/diplomacy
Month 6+: novel state mechanics layered on top
```

## Week 13 — POP Foundation

| Day | Goal | Deliverable |
| --- | --- | --- |
| 61 | Define the Vic 2 minimum | `vic2_basic_mechanics_mvp.md` |
| 62 | POP data model | Provinces contain persistent `PopGroup` records |
| 63 | POP seeding and province setup | Authored scenario data loads POPs, RGO type, culture, religion, literacy |
| 64 | POP inspection API | Country/province/type/literacy/militancy/unemployment POP endpoints and debug UI |
| 65 | POP monthly tick | Monthly POP income, needs, literacy, militancy/consciousness, promotion/demotion stage |

Week 13 success metric: society exists as persistent data and can be inspected without database spelunking.

## Week 14 — Production and Employment

| Day | Goal | Deliverable |
| --- | --- | --- |
| 66 | RGO production | Provinces produce raw goods from employed farmers/laborers |
| 67 | Factory model v1 | Factories consume inputs and produce outputs |
| 68 | Artisans v1 | Artisan POPs produce simple goods outside factories |
| 69 | Employment assignment | Employment and unemployment are real and tracked |
| 70 | Production integration test | `month4_production_test.md` after a 12-month sim run |

Week 14 success metric: raw goods, factory goods, and artisan goods enter the market from POP labor.

## Week 15 — POP Needs, Market, and Money

| Day | Goal | Deliverable |
| --- | --- | --- |
| 71 | Need baskets | POP types have life, everyday, and luxury need demand |
| 72 | POP purchasing | POPs earn income, pay taxes, buy needs, and update fulfillment |
| 73 | National market v1 | Domestic market supply/demand and price movement drive scarcity |
| 74 | Taxation and budget | Poor/middle/rich taxes plus education, military, admin spending affect POPs and treasury |
| 75 | Market/POP playtest | `month4_pop_market_playtest.md` |

Week 15 success metric: POPs can become poor, comfortable, or deprived, and policy affects that.

## Week 16 — Basic Vic Politics and Social Movement Pressure

| Day | Goal | Deliverable |
| --- | --- | --- |
| 76 | Literacy, consciousness, militancy | Monthly political-pressure drift from education, literacy, needs, unemployment |
| 77 | Promotion/demotion v1 | POP mobility under small capped rules |
| 78 | Reform pressure v1 | Political/social reform pressure from POP conditions |
| 79 | Unity inspection pass | National POPs, budgets, market, province POPs, RGO/factory status visible |
| 80 | Month 4 review | `month4_review.md`, `vic2_basic_mechanics_status.md`, `pop_known_issues.md`, `month5_playable_slice_targets.md` |

Week 16 success metric: the simulation has basic Victoria-style political pressure, not just production numbers.

## Month 4 Demo Script

1. Start server.
2. Connect Unity.
3. Inspect country population.
4. Inspect province POP breakdown.
5. Inspect RGO workers and output.
6. Inspect a factory or artisan production source.
7. Change taxes.
8. Run 12 months.
9. Observe POP needs and militancy change.
10. Build or enable a factory.
11. Observe employment shift.
12. Observe market price movement.
13. Observe literacy and consciousness drift.

## Deferred Until Later

- Civic trust system
- Real law rollout
- Chain-of-command mechanics
- Delayed implementation
- Disease
- Climate
- Advanced diplomacy
- War
- Spheres
- Newspapers
- LLM diplomacy
- Full migration model beyond crude internal movement
