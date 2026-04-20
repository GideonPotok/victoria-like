# Current Status

Victoria-Like is a pre-alpha simulation project. The core value today is the server-side political economy substrate and its inspection paths, not a complete game.

## Implemented

- Fixed-tick server simulation with one in-game day per tick.
- Server-side command validation and command processing.
- POP groups with employment, unemployment, needs fulfillment, cash, literacy, militancy, consciousness, and promotion/demotion.
- RGO, factory, and artisan production.
- National market supply, demand, stockpiles, shortages, and price movement.
- Taxation, treasury, education spending, military spending, and administration spending.
- Political pressure drift and reform pressure metrics.
- Persistence, latest-snapshot recovery, PostgreSQL storage, Redis health checks.
- REST APIs, WebSocket world updates, admin inspection, and explanation services.
- Unity v2 inspection UI for countries, provinces, POPs, market prices, treasury, tax rate, RGO output, and factories.
- Tiny, medium, and Phase 1 Albion scenarios, including a server-compatible public demo scenario.
- xUnit simulation and server-adjacent tests.
- Fake-client and NBomber load harnesses.

## Partial

- Unity budget controls are still incomplete compared with the server-side budget model.
- Reform pressure exists in simulation metrics but is not fully surfaced through every player-facing path.
- Military, war, peace, and diplomacy exist in early forms and should be treated as prototype features until the public demo path proves them end to end.
- Multiplayer infrastructure exists, but the public launch should promise only the flows that are verified in the demo script.
- Market behavior is intentionally v1 and does not yet include a world market.

## Not Yet

- Full historical scenario.
- Deep diplomacy, spheres, and crisis systems.
- Advanced state capacity and law rollout.
- Disease, public health, climate, newspapers, or LLM diplomacy.
- Full modding API.
- Production-quality Unity UX.
- Balanced economy.

## Launch Principle

The public promise is narrow: this is an inspectable, server-authoritative political economy sandbox with real POP and market mechanics. It is not a finished game.
