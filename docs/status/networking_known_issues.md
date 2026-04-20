# Networking Known Issues

_Date: 2026-04-25 | End of Month 2 (Day 40)_

---

## Active Issues

### N1 — No command authorization layer; ownership checks are scattered

**Symptom:** `ChangeTaxRateCommandHandler` checks country ownership inline. `QueueBuildingCommandHandler` checks ownership inline. No centralized authorization pipeline exists. Any new command handler must remember to check ownership itself.

**Impact:** Easy to introduce a command handler that skips authorization. No audit trail of rejected-before-enqueue attempts. No rate limiting before the command reaches the queue.

**Fix needed:** Centralized `CommandAuthorizer` that validates actor identity, country ownership, and basic rate limits before a command enters the queue. Every command goes through one pipeline (Month 3 Day 41).

**Priority:** Critical — security and correctness gap.

---

### N2 — Commands have no structured result shape

**Symptom:** `POST /api/world/commands` returns `accepted` with HTTP 202 but the command may later be rejected during tick processing. The client has no way to distinguish "accepted and applied", "accepted but rejected at tick", and "failed during execution" without polling the command history endpoint.

**Impact:** Unity client cannot show meaningful feedback. Race conditions between command submission and outcome are invisible.

**Fix needed:** Define a `CommandResult` discriminated union: `accepted | queued | rejected | failed_execution`. Route all outcomes through `ICommandOutcomeRecorder` and push `command_result` WS events to the submitting client (this exists but isn't always called).

**Priority:** High — player experience gap, and necessary before Day 41.

---

### N3 — No server restart recovery

**Symptom:** `PersistentWorldClockService` saves world state to DB periodically (every N ticks). But `WorldInitializationService` reinitializes from the scenario file on every startup, not from the last saved snapshot.

**Impact:** Server process death resets the world to initial scenario state. All economic progress, construction queues, and treasury values are lost.

**Fix needed:** On startup, check if a saved world snapshot exists. If it does, load it. Only fall back to scenario seeding if no snapshot is found (Month 3 Day 47).

**Priority:** Critical for any real deployment.

---

### N4 — WebSocket fanout serializes per-connection sends

**Symptom:** `BroadcastWorldUpdateAsync` sends to all connections sequentially inside a loop, each awaiting its own `SemaphoreSlim` send lock. With 20+ clients, late-connecting clients receive the world update noticeably after early-connecting clients.

**Impact:** Tick update delivery has O(N) latency tail for large N. Under the load test harness at 20 clients, tick drift was measurably higher than with 5 clients.

**Fix needed:** Serialize the message once, then `Task.WhenAll` the per-connection sends. The existing send lock per socket is correct; the outer broadcast just needs parallelism. Already partially addressed in `BroadcastMarketUpdateAsync` but not in `BroadcastWorldUpdateAsync`.

**Priority:** Medium — correctness is fine, performance degrades at scale.

---

### N5 — Stale Bearer token in WebSocket URL is a credential leak risk

**Symptom:** Session token is passed as a URL query parameter: `/ws/world?token=`. Query parameters appear in server access logs, proxy logs, and browser history.

**Impact:** Tokens are logged in plaintext by any middleware that logs request URLs (e.g., ASP.NET request logging, nginx access log).

**Fix needed:** Accept the token via the WebSocket handshake `Sec-WebSocket-Protocol` subprotocol header, or require a short-lived one-time WebSocket upgrade token obtained from a separate REST endpoint.

**Priority:** Low for localhost dev; High before any real deployment.

---

### N6 — Subscription state is not durable

**Symptom:** Client subscriptions are stored in `Connection.Subscriptions` (in-process `HashSet<string>`). If the server restarts or the connection drops-and-reconnects, subscriptions are not restored automatically — the client must re-send subscribe messages.

**Impact:** After reconnect, a client that subscribed to "market" will receive `reconnect_snapshot` but no further `market_update` messages until it re-subscribes. The Unity `WorldWebSocketClient` does re-send subscriptions on reconnect, but this isn't guaranteed for all client types.

**Fix needed:** Either persist subscription preferences per session in the DB, or document that clients must re-subscribe on every reconnect and enforce this in the Unity client. Currently the Unity client does re-subscribe correctly, but this is not tested in the load harness.

**Priority:** Low — Unity client handles it; only matters for future client types.

---

### N7 — No duplicate command / idempotency guard

**Symptom:** A client that retries a command (e.g., due to network timeout before receiving 202) will enqueue the same command twice. Both will execute at the next tick boundary.

**Impact:** Duplicate tax changes would apply twice. Duplicate building queues would charge treasury twice and queue two constructions.

**Fix needed:** Clients should include a client-generated `idempotency_key` on each command. Server deduplicates by key within a sliding window (e.g., last 60 seconds). (Month 3 Day 43).

**Priority:** Medium — requires bad timing and network conditions to trigger; not yet a problem at dev scale.

---

### N8 — Rate limiting is not implemented

**Symptom:** A single authenticated client can submit an unbounded number of commands per second. The in-memory command queue has no cap.

**Impact:** A buggy or malicious client can flood the command queue, causing tick processing to take arbitrarily long.

**Fix needed:** Per-account command rate limit (e.g., 10 commands per second soft, 30 hard). Per-country strategic command cooldowns (e.g., tax rate change once per 5 ticks). (Month 3 Day 44).

**Priority:** Medium — not a problem at dev scale; critical before open access.

---

## Won't Fix (Month 3)

- **WebSocket compression** — bandwidth is not a bottleneck at current scale; revisit when fanout measurement (Day 57) identifies oversized payloads.
- **Binary WebSocket frames** — JSON text is fine; optimization without a measured problem.
- **Multiple server instances** — single-process world clock is a deliberate design choice for Month 3; horizontal scaling requires distributed clock coordination and is deferred.
