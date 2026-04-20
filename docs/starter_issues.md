# Starter Issues for Public Launch

Use this list to seed GitHub issues before announcing the repo.

## Content and Scenarios

1. Add three new industrial goods with base prices and categories.
2. Add a tiny two-province food-shortage scenario.
3. Add a tiny coal-and-tools industrial bottleneck scenario.
4. Add a fictional 4-country border scenario for diplomacy and war testing.
5. Expand `docs/modding_scenarios.md` with a complete minimal JSON example.
6. Add comments to balance docs explaining wages, productivity, and needs CSVs.
7. Create a scenario review checklist for original/non-proprietary content.

## Simulation and Tests

1. Add tests for POP needs fulfillment under rising food prices.
2. Add tests for factory input shortage behavior.
3. Add tests for treasury changes after tax rate changes.
4. Add tests for unemployment effects on militancy drift.
5. Add tests for scenario validation errors with missing goods.
6. Add a regression test for restart recovery of budget settings.
7. Add a test that explains why a market price changed after a tick.

## Unity UI

1. Add sorting to the province list.
2. Add a market price change indicator.
3. Add a POP needs tooltip.
4. Add clearer connection-state messaging.
5. Add a read-only reform pressure display once exposed by the API.
6. Add a compact country summary panel for recording demo clips.

## Docs and Research

1. Write a short comparison of this POP model to Victoria 2's POP model.
2. Document "why did price change?" with screenshots or sample API output.
3. Document how commands move from Unity to the server.
4. Add a glossary for POPs, RGOs, national markets, stockpiles, and reform pressure.
5. Write the launch post draft.
6. Add a troubleshooting page for PostgreSQL, Redis, and port 5001 conflicts.
