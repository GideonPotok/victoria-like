# Month 3 Targets

_Date: 2026-04-25 | Days 41–60_

Theme: "A malicious or buggy client can be annoying, but not world-corrupting. The world can survive process death."

---

## Success Criteria

By the end of Day 60, the following must be demonstrably true:

1. A malicious or buggy client cannot corrupt world state (permissions + rate limits)
2. Server restart does not reset the world (snapshot recovery)
3. Admin can diagnose any weird economy or player action without spelunking logs
4. 20 fake clients run for 30 minutes without tick drift exceeding 100ms, memory growth, or exception floods

---

## Week 9 — Permissions, Authority, Command Safety (Days 41–45)

**Problem:** Command ownership checks are scattered across handlers. No structured command result. No rate limits.

| Day | Goal | Deliverable |
|-----|------|-------------|
| 41 | Formal command authorization layer | All commands go through centralized auth pipeline; structured `CommandResult` (accepted/rejected/queued/failed) |
| 42 | Command audit log | `CommandAuditRecord` with actor, country, command type, target IDs, submitted tick, executed tick, result, rejection reason |
| 43 | Simultaneous command conflict handling | Deterministic ordering; idempotency key deduplication for retried commands |
| 44 | Rate limits and command budgets | Per-account rate limits; per-country strategic cooldowns; soft vs. hard limits |
| 45 | Week 9 hardening test | `permissions_and_command_safety_review.md`; two-user cross-country command tests; duplicate submit; stale token; rapid spam; reconnect-resubmit |

**Week 9 success metric:** A malicious or buggy client can be annoying, but not world-corrupting.

---

## Week 10 — Persistence, Recovery, and State Integrity (Days 46–50)

**Problem:** Server restart resets the world. No state invariant enforcement. No explicit recovery model.

| Day | Goal | Deliverable |
|-----|------|-------------|
| 46 | Snapshot/savepoint system | Periodic world snapshot table; manual admin savepoint trigger; saves countries, provinces, markets, buildings, construction queues, account-country mappings |
| 47 | Restart recovery | Load latest snapshot on startup; restore world tick, construction queues, market state, account/country mappings; reject impossible partial state |
| 48 | Event replay decision | Written `persistence_recovery_model.md`; decision: snapshot-only vs. snapshot + post-snapshot replay; implement critical event replay only if manageable |
| 49 | State invariant checks | `StateInvariantChecker` runs during tick and after load; checks: non-negative stock, finite prices, valid province owners, treasury not NaN, construction progress within bounds |
| 50 | Recovery torture test | `restart_recovery_test_report.md`; 10–20 minute run, command during tick, disconnect clients, kill server, restart, verify pre/post state |

**Week 10 success metric:** The world can survive process death without becoming a liar.

---

## Week 11 — Better Admin Tools and Explainability (Days 51–55)

**Problem:** Admin inspection requires DB spelunking. Market behavior is not explainable from in-game tools.

| Day | Goal | Deliverable |
|-----|------|-------------|
| 51 | World inspector dashboard | One page: current tick, connected clients, active sessions, command queue depth, tick duration, DB write count, latest savepoint, active subscriptions |
| 52 | Command log viewer | Searchable/filterable admin command log; filter by account/country/command type/result/tick range; rejected commands shown clearly |
| 53 | Market explanation tool | Per-good: previous price, current price, equilibrium pressure, supply, demand, unmet demand, clamp applied, largest producer/consumer, last tick delta |
| 54 | Province/country inspector | Province detail: owner, pop, buildings, production, construction, local demand. Country: treasury, tax rate, account, active commands, market summary |
| 55 | Tooling review and cleanup | `admin_tooling_review.md`; clean admin endpoints; remove debug noise; add structured logs; "debugging weird economy" checklist |

**Week 11 success metric:** When the sim does something weird, you can investigate it in-game/admin instead of guessing.

---

## Week 12 — Load, Soak, and Vertical Slice Review (Days 56–60)

**Problem:** Current load test is a light 5–10 client smoke test. We don't know actual bandwidth, memory growth, or tick drift under sustained load.

| Day | Goal | Deliverable |
|-----|------|-------------|
| 56 | Fake client harness v2 | Simulate full flow: login, country assignment, subscriptions, periodic commands, disconnect/reconnect cycles, stale token attempts, duplicate retries. Target: 20 comfortable, stretch 50 |
| 57 | Bandwidth and update fanout measurement | `network_fanout_report.md`; bytes/client/minute, messages/client/minute by subscription type; identify oversized deltas; coalesce if needed |
| 58 | Soak test | `soak_test_report.md`; 30–60 min fake-client soak; monitor tick drift, memory growth, DB writes, reconnect success, command rejection rate, server exceptions |
| 59 | Fix top three bottlenecks | No new features; fix top 3 from soak; rerun smaller test; document remaining known issues |
| 60 | Month 3 milestone review | `month3_review.md`, `known_scale_limits.md`, `persistence_recovery_status.md`, `admin_tooling_status.md`, `month4_targets.md`; tagged milestone build |

**Day 60 demo script:**
1. Login as Country A
2. Login as Country B
3. Inspect different provinces and markets
4. Submit valid commands (tax change, queue building)
5. Submit invalid commands (wrong country, bad token)
6. Watch economy update in realtime
7. Disconnect and reconnect
8. Restart server; reconnect again; verify world state preserved
9. Inspect audit log and market explanation tool
10. Run load harness; show tick drift report

**Week 12 success metric:** The game is still tiny, but the online-world spine is no longer embarrassing.

---

## Top Carryover Issues from Month 2

These known issues from `economy_known_issues.md` and `networking_known_issues.md` must be resolved before or during Month 3:

| Issue | Day to Fix |
|-------|-----------|
| N1 — No command authorization layer | Day 41 |
| N2 — No structured command result | Day 41 |
| N3 — No server restart recovery | Day 47 |
| E2 — Market stock can go negative | Day 49 (invariant check) |
| E5 — No input goods consumption in production | Day 49 |
| E6 — Treasury can go negative without consequence | Day 41 (as part of QueueBuilding auth) |
| N7 — No idempotency guard | Day 43 |
| N8 — No rate limiting | Day 44 |

## Defer to Month 4

- Pop growth/migration/differentiation
- Multi-input production chains fully wired
- Trade between countries
- Multiple server instances / horizontal scaling
- WebSocket token delivery via subprotocol header (N5)
- Binary frames / compression
