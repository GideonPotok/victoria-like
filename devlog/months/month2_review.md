# Victoria II MMO — Month 2 Review

_Date: 2026-04-25 | End of Day 40 / Week 8_

## Theme

"Make the world economically alive, then make it survivable for real users."

---

## What Was Built (Days 21–40)

### Week 5 — Smallest Possible Economy (Days 21–25)

| Component | Status |
|-----------|--------|
| `economy_mvp.md` — goods/buildings/pop/market design doc | Done |
| `GoodDefinition`, `MarketState`, `BuildingState`, `PopNeedProfile` domain models | Done |
| DB migrations 006 (economy columns), 007 (building queue) | Done |
| `goods.json` content file (grain, coal, iron, tools, fish) | Done |
| `JsonContentLoader` + `BalanceCsvLoader` for goods, productivity, wages, needs | Done |
| `ProvinceProductionStage` — buildings produce output per tick | Done |
| `NationalDistributionStage` — aggregate supply from provinces to national market | Done |
| `MarketPricingStage` — supply/demand price movement with clamping | Done |
| Market prices exposed in `GET /api/world/market` and realtime WS `market_update` | Done |
| Unity market panel in debug UI | Done |

### Week 6 — Pops, Needs, Command-Driven Economic Control (Days 26–30)

| Component | Status |
|-----------|--------|
| Synthetic pop counts per province in `CommandWorldStateMapper` | Done |
| `PopNeedsStage` — life needs (grain, fish) per 1000 pop; `NeedsFulfillment` tracked | Done |
| `BudgetStage` — tax rate drives disposable income; treasury updated each tick | Done |
| Treasury persisted in DB; exposed on `GET /api/world/countries` | Done |
| `QueueBuildingCommand` with province ownership + treasury check | Done |
| `BuildingConstructionStage` — countdown, completes, building added to province | Done |
| `GET /api/world/buildings/queue` endpoint | Done |

### Week 7 — Sessions, Reconnects, Selective Data Delivery (Days 31–35)

| Component | Status |
|-----------|--------|
| `accounts` + `sessions` table (migration 008) | Done |
| `Pbkdf2PasswordHasher`, `SessionRepository` | Done |
| `POST /api/auth/login`, `logout`, `me` | Done |
| Scenario seeder hashes passwords on first load | Done |
| `SubmitCommand` extracts actor from Bearer token; request-body fallback for dev | Done |
| WebSocket `/ws/world?token=` validates session; 401 on stale token | Done |
| `reconnect_snapshot` sent immediately on WS connect | Done |
| Per-connection `HashSet<string> Subscriptions`; subscribe/unsubscribe WS messages | Done |
| `BroadcastMarketUpdateAsync` filtered to "market" subscribers | Done |
| `SendCountryUpdateAsync` targeted per actor | Done |
| Unity `PlayerSession`, `AuthApiClient`, `WorldWebSocketClient` state machine | Done |
| `ConnectionDebugUI`, `WorldUIManager` refactored to WS events | Done |
| `Bootstrap` drives login → connect → snapshot flow | Done |

### Week 8 — Tools, Tests, Stability (Days 36–39)

| Component | Status |
|-----------|--------|
| `MarketHistoryService` — circular 20-tick buffer per good | Done |
| `GET /api/admin/market` — prices, deltas, history, top shortages, unmet needs | Done |
| `GET /api/admin/tick-profile` — per-stage timing breakdown | Done |
| `TickMetrics.StageDurationsMs` — every stage instrumented | Done |
| Tick-profile logged every 10 ticks via structured `ILogger` | Done |
| `VictoriaLike.LoadTest` console harness | Done |
| `FakeClient`, `LoadTestMetrics`, `LoadTestReport` | Done |
| Staggered startup, live status every 10s, reconnect test, command round-trip | Done |
| Tick interval drift statistics across all clients | Done |
| Npgsql pool size 50 + idle lifetime tuning | Done |
| Simulation logs demoted from Info to Debug (noise reduction) | Done |
| Tick drift guard — skips catch-up fires when tick overshoots interval | Done |

---

## Architecture Decisions Made

1. **National market, not per-province markets** — one `MarketState` per world simplifies price signals and avoids arbitrage complexity until Month 3+.
2. **Tick pipeline as ordered stage list** — `ISimulationStage` list in `PersistentWorldClockService`; stages are stateless and run in sequence. Easy to add/reorder.
3. **In-memory command queue + tick-boundary application** — commands queue immediately (202 Accepted) and are drained atomically at tick start. Deterministic and auditable.
4. **Session tokens in Authorization: Bearer** — request-body `actorId` kept as dev fallback only; not used in production paths.
5. **Subscription-filtered WS fanout** — per-connection `HashSet<string>` prevents clients from receiving data they haven't subscribed to.
6. **Tick drift guard** — if a tick takes longer than the tick interval, the next fire is skipped rather than catching up, preventing runaway pile-ups under load.
7. **Circular history buffer in memory** — `MarketHistoryService` keeps 20 ticks per good in-process; no DB reads needed for admin market inspector.

---

## Month 2 Success Metric: Assessment

> "Can multiple players connect to a persistent world, issue meaningful economic commands, disconnect, reconnect, and keep seeing coherent server-owned outcomes?"

**Result: Yes, with caveats.**

- Multiple clients connect and receive live tick updates via WebSocket ✓
- Session auth works; commands are actor-bound ✓
- `ChangeTaxRate` and `QueueBuilding` affect live world state ✓
- Treasury, market prices, and construction queue update each tick ✓
- Reconnect restores subscriptions and delivers snapshot ✓
- Load test harness validates all of the above under fake client load ✓

**Caveats (see known_issues docs):**
- No formal command authorization layer — ownership checks are scattered
- No server restart recovery — world resets on process death
- Command outcomes are not formally structured (accepted/rejected/failed)
- No rate limiting or idempotency guards

---

## Demo Script (Alpha)

```bash
# 1. Start infrastructure
docker-compose up -d

# 2. Start server (auto-runs migrations, seeds scenario)
cd server && dotnet run --project src/VictoriaLike.Server

# 3. Login as Albion player
curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"player1","password":"password1"}' | jq .

# 4. Inspect world
curl -s http://localhost:5001/api/world/countries | jq .
curl -s http://localhost:5001/api/world/market | jq .
curl -s http://localhost:5001/api/world/buildings/queue | jq .

# 5. Change tax rate (use TOKEN from login)
curl -s -X POST http://localhost:5001/api/world/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"commandType":"ChangeTaxRate","payload":{"countryId":"albion","taxRate":18}}' | jq .

# 6. Queue a building
curl -s -X POST http://localhost:5001/api/world/commands \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"commandType":"QueueBuilding","payload":{"countryId":"albion","provinceId":"albion-1","buildingType":"farm"}}' | jq .

# 7. Watch economy update over time
watch -n 5 'curl -s http://localhost:5001/api/world/market | jq ".goods[] | {id, price}"'

# 8. Admin market inspector
curl -s http://localhost:5001/api/admin/market | jq .
curl -s http://localhost:5001/api/admin/tick-profile | jq .

# 9. WebSocket realtime (use TOKEN from login)
wscat -c "ws://localhost:5001/ws/world?token=$TOKEN"
# Send: {"type":"subscribe","topics":["market","country"]}
```

---

## State of the Codebase

- 8 database migrations applied cleanly
- All projects target `net10.0`; builds cleanly
- 16 simulation stage pipeline: command processing → production → distribution → pricing → pop needs → budget → construction → logging
- Load test harness in `server/tests/VictoriaLike.LoadTest`
- No server-restart recovery (known gap, Month 3 priority)
- No formal authorization pipeline (known gap, Month 3 priority)
