# Roadmap

The roadmap is organized around visible deliverables, not vague system names.

## v0.1-prealpha: Playable Country Inspection Loop

- Public repo hygiene, license, contribution docs, and passing CI.
- Albion demo scenario with a documented run path.
- Unity can inspect a country, provinces, POP groups, market prices, treasury, and tax rate.
- Every Unity-fronted public use, including demos and docs, has an equivalent curl-based play path modeled on the Codex sessions in `playwithcurl.zip`.
- Player can run the server locally and watch POP/economy values change over time.
- Known gaps are documented honestly.

## v0.2: Explanation Tools and Medium Scenario

- Medium scenario becomes the recommended demo after Albion.
- Explanation endpoints and UI show why prices, needs fulfillment, treasury, and unemployment changed.
- More explicit player-facing status and warning text.
- Scenario/content docs are good enough for non-engine contributors.

## v0.3: Two-Player Persistent Multiplayer Slice

- Two players connect to the same persistent world.
- Country control authorization is clear and tested.
- Commands are isolated by player/country ownership.
- Reconnect and restart behavior is documented and covered by tests.

## v0.4: Better POP Purchasing and Market Behavior

- POP buying behavior becomes more expressive and easier to explain.
- Shortages, substitutions, income limits, and unmet needs produce clearer outcomes.
- Market explanation tools answer "why did this price change?" and "why did this POP suffer?"

## Later Research Tracks

- Diplomacy, spheres, and rank.
- State capacity and delayed implementation.
- Public health, disease, and demographic shocks.
- Newspapers and diegetic explanation layers.
- Richer military logistics and war goals.
