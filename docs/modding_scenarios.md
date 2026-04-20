# Modding Scenarios

Scenarios live in `server/content/scenarios`.

The most useful examples are:

- `phase1-albion-server.json`: tiny Phase 1 server demo slice.
- `phase1-albion.json`: newer core simulation-loader demo slice.
- `tiny-2country.json`: older two-country test scenario.
- `medium-8country.json`: larger stress and gameplay scenario.

The canonical format notes live in `server/content/scenario-format.md`.

## Scenario Goals

Good early scenarios should be small, inspectable, and mechanically legible.

Prefer:

- 1-8 countries.
- A small number of provinces.
- A few obvious goods.
- POP groups with different jobs and conditions.
- One or two visible economic problems.

Avoid:

- Huge historical maps.
- Dozens of goods before the UI explains them.
- Data copied from proprietary games.
- Scenarios that require unfinished systems to be fun.

## Adding a Scenario

1. Create a JSON file under `server/content/scenarios`.
2. Define countries, markets, players, provinces, and POPs according to the supported format.
3. Use fictional names or original research. Do not copy proprietary data.
4. Add market prices for every good used by the scenario.
5. Run tests:

```bash
dotnet test server/VictoriaLike.Server.sln
```

## Public Demo Standard

A public demo scenario should let a new player answer:

- What country do I control?
- What do my provinces produce?
- Which POPs are doing well or badly?
- What goods are scarce or expensive?
- What changes after several ticks?
