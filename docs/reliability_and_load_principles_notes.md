Victoria-Like Reliability and Load Principles Notes
===================================================

Purpose
-------

This file captures the reliability and performance-under-load principles already
visible in the project, plus advanced techniques and checklists that would make
the public story sharper.

The differentiator versus a classic single-player grand strategy game is not
"more features." It is that the simulation is being built like an online,
server-authoritative, inspectable system. Victoria 2 is loved for its emergent
political economy, but it was not designed as a load-tested, persistent,
multi-client, auditable service. This project can own that lane.


Core Reliability Thesis
-----------------------

The project is built around these rules:

1. The server owns the truth.
2. Player input is command intent, not direct state mutation.
3. Commands are ordered, validated, deduplicated, audited, and then executed.
4. Ticks are deterministic stages with measured durations.
5. Bad world state is rejected by invariants before it becomes durable truth.
6. Load is tested with black-box fake clients, not only unit tests.
7. Fanout, bandwidth, reconnects, stale tokens, and duplicate retries are
   measured as first-class gameplay infrastructure.
8. Admin/explanation tooling is part of reliability, because operators and
   players need to understand what happened.

This is the reliability story to put in public docs:

"Victoria-Like treats grand strategy simulation as a persistent distributed
system: commands are ordered, audited, idempotent, and replay-friendly; ticks
are profiled; world state is validated; clients reconnect; and load is measured
with fake players over HTTP/WebSocket."


Repo Evidence
-------------

Authoritative server and deterministic pipeline:

- docs/architecture.md defines the server as the single source of truth.
- docs/command_conflict_rules.md defines deterministic ordering:
  submitted_tick, received_at, command_id.
- PersistentWorldClockService runs a fixed tick loop and simulation stages.
- Economic stages are explicit: command processing, army movement, battle,
  construction, employment, production, distribution, pricing, POP needs,
  monthly POP update, and budget.
- The tick loop avoids catch-up storms: if a tick runs long, it schedules the
  next future slot rather than firing immediate back-to-back ticks.

Command safety:

- reviews/permissions_and_command_safety_review.md documents bearer-session
  identity, actor-country ownership checks, idempotency, stale client tick
  rejection, rate limits, and command cooldowns.
- CommandQueueService persists commands before enqueueing and sorts each batch
  deterministically before execution.
- CommandBudgetService applies per-account soft/hard limits and per-country
  strategic cooldowns.
- docs/command_conflict_rules.md distinguishes query/preview calls from
  authoritative execute calls.

Persistence and recovery:

- reviews/persistence_recovery.md documents latest-snapshot recovery,
  startup validation, named savepoints, command audit, and invariant checks.
- reviews/restart_recovery_test_report.md defines a restart harness and pass
  criteria around health, tick monotonicity, endpoint readability, savepoints,
  relogin, and zero invariant violations.
- PersistentWorldClockService saves periodic state, periodic snapshots, and a
  final save on shutdown.

Runtime invariants:

- WorldInvariantChecker is run after load and after simulation.
- If a loaded world or tick-mutated world fails invariants, the tick does not
  continue into normal persistence.
- The invariant model checks ownership, market quantities, price clamps,
  construction queue references, duplicate active construction, and player
  country mappings.

Load testing and fanout:

- reviews/fake_client_harness_v2.md documents a black-box load harness.
- FakeClient logs in, subscribes to WebSocket topics, sends tax commands,
  retries duplicate commands with the same command id/idempotency key, tests
  stale tokens, disconnects, and reconnects.
- LoadTestMetrics tracks messages, bytes, message type sizes, tick drift,
  reconnect success, command accept/reject/error counts, stale token rejection,
  duplicate retries, and time to first message.
- reviews/network_fanout_report.md measures bytes/client/minute and identifies
  market_update as the first likely oversized-delta risk.
- reviews/soak_test_report.md shows a 1-hour, 40-client soak with 384,197
  messages, 62.15 MB total bandwidth, 0ms tick drift, 100% reconnect success,
  0 HTTP command errors, 0 runtime server error/fatal lines, and no memory
  growth.

WebSocket safety:

- WorldWebSocketHub tracks connected clients, actor identity, and subscriptions.
- Country and command result messages target sockets for the actor, not all
  clients.
- Market updates are subscription-targeted.
- Sends are serialized per socket with a SemaphoreSlim SendLock.
- Failed sends remove dead sockets from the connection table.
- Anonymous clients get world summary by default; authenticated clients also
  get market subscription by default.

Observability:

- reviews/admin_tooling_review.md documents /admin and /api/admin workflows.
- Admin endpoints expose health, tick, stage timing, sessions, subscriptions,
  command queue depth, DB writes, recent commands, savepoints, market
  explanation, province detail, country detail, and invariant violations.
- Useful structured fields include CommandId, ActorId, CountryId, CommandType,
  submitted_tick, expected_world_tick, idempotency_key, outcome status,
  rejection reason, stage durations, queue depth, connected clients,
  subscriptions, and DB write counters.


Principles and Expert Frameworks Reflected Here
-----------------------------------------------

1. Server-authoritative multiplayer

Expert frame:
Online games and distributed simulations keep authority on the server to
prevent cheating, resolve conflicts, and make outcomes reproducible.

Seen here:
Unity is presentation-only. Commands are submitted to the server, validated at
execution time, and applied inside the tick pipeline.

Public framing:
"The client can be wrong, late, or malicious; the server still produces the
canonical world."


2. Deterministic command scheduling

Expert frame:
RTS engines and distributed simulations avoid race-dependent outcomes by
placing player intent into ordered frames/ticks.

Seen here:
Commands sort by submitted_tick, received_at, and command_id. Repeated tax
commands have last-valid-command-wins semantics. Province construction has an
exclusive conflict rule.

Advanced technique to add later:
Persist an execution sequence number for each command and expose it in admin
views. That makes every dispute answerable: "this command ran before that one."


3. Idempotency everywhere user retries are possible

Expert frame:
Reliable distributed APIs assume clients will retry after timeouts. Retrying
must not duplicate side effects.

Seen here:
Commands carry command_id and actor-scoped idempotency_key. Duplicate retries
return the existing command instead of queueing another mutation.

Advanced technique to add later:
Add an Idempotency-Key header path alongside the body idempotency key so generic
HTTP tooling can participate.


4. Backpressure and rate shaping

Expert frame:
Reliable services degrade deliberately instead of accepting unbounded work.

Seen here:
CommandBudgetService has a 10-command/10s soft limit, 20-command/10s hard
limit, and strategic cooldowns for commands such as ChangeTaxRate, MoveArmy,
DeclareWar, and MakePeace.

Advanced technique to add later:
Move command budgets to Redis before horizontal scaling. Track budget decisions
as metrics: accepted, soft-limited, hard-limited, cooldown-rejected.


5. Invariant-driven simulation safety

Expert frame:
Complex simulations need executable invariants, not only tests. Invariants
catch impossible states at runtime and during recovery.

Seen here:
WorldInvariantChecker runs after load and after simulation. Invalid state is
reported and not blindly persisted.

Advanced technique to add later:
Classify invariants by severity:

- fatal: stop persistence/startup
- degraded: continue but alert
- informational: surface in admin only


6. Black-box load testing

Expert frame:
The most useful load tests exercise the system from the outside, through the
same protocols real users use.

Seen here:
Fake clients use HTTP and WebSocket. They do not require database access. They
login, subscribe, send commands, retry duplicates, test stale tokens, and
reconnect.

Advanced technique to add later:
Create named load profiles:

- smoke: 5 clients, 60s
- local-realistic: 40 clients, 10m
- fanout: 200 anonymous observers, 10m
- command-pressure: 50 authenticated clients, 10m
- soak: 100 mixed clients, 1h
- chaos-reconnect: clients disconnect/reconnect every 10-60s


7. Fanout budgeting

Expert frame:
Realtime systems fail when payload size and subscriber count multiply faster
than expected. Measure bytes, not only messages.

Seen here:
Network fanout reports track bytes/client/minute, message sizes by type, and
identify market_update as the first likely scaling bottleneck.

Advanced technique to add later:
Use a fanout budget per stream:

- world_update: target under 1 KB/client/tick
- market_update: target under 2 KB/subscriber/tick for small scenarios
- country_update: target under 1 KB/controlled actor/tick
- command_result: immediate but small, under 512 B

When a stream crosses budget, switch from full snapshots to deltas or
subscription partitions.


8. Reconnect as a normal case

Expert frame:
Networked games must treat disconnect/reconnect as ordinary behavior.

Seen here:
Fake clients intentionally disconnect and reconnect. The soak reports track
reconnect success rate. Auth session relogin is part of restart testing.

Advanced technique to add later:
Expose a reconnect contract:

- client reconnects with token
- server sends reconnect_snapshot
- client resubscribes or receives active subscription list
- client reconciles pending commands by command_id/idempotency_key


9. Observability as a gameplay feature

Expert frame:
For complex systems, explainability reduces support load and improves trust.

Seen here:
Admin endpoints explain market pressure, command outcomes, province state,
country state, stage timing, queues, sessions, and snapshots.

Public framing:
"The simulation is not a black box. The server can explain why prices moved,
why commands were rejected, and whether the world state is valid."


10. Recovery over perfection

Expert frame:
Early systems benefit from simple, explicit recovery models rather than
premature event sourcing.

Seen here:
The project currently uses latest snapshot recovery. Command audit is durable
for debugging but not yet replayed after snapshots.

Advanced technique to add later:
Promote to replay only after every command has deterministic, idempotent
semantics and all command handlers are replay-safe.


Reliability Checklists
----------------------

Public launch reliability checklist:

[ ] Fresh clone builds with the documented .NET SDK.
[ ] dotnet test server/VictoriaLike.Server.sln passes.
[ ] make up starts PostgreSQL and Redis.
[ ] make run-albion starts the server from a clean local DB.
[ ] /health is healthy.
[ ] /api/admin/summary is readable.
[ ] Unity connects to the local server.
[ ] Demo scenario can run for 10 minutes without tick drift or errors.
[ ] Fake client smoke run completes with 0 harness errors.
[ ] No tracked .env, bin, obj, scratch, crash, or log clutter.
[ ] README does not overclaim unfinished multiplayer/war/diplomacy features.


Command reliability checklist:

[ ] Every gameplay mutation enters through a command.
[ ] Command has command_id, actor_id, country_id, command_type, target_ids,
    submitted_tick, received_at, expected_world_tick, and idempotency_key.
[ ] Actor is authenticated through bearer token for production paths.
[ ] Command validates country/province ownership.
[ ] Command validates against current world state at execution time.
[ ] Duplicate command_id returns the existing command.
[ ] Duplicate actor-scoped idempotency_key returns the existing command.
[ ] Expected-world-tick staleness is rejected or explicitly accepted.
[ ] Conflict rule is documented for the command type.
[ ] Outcome is recorded and visible in admin tooling.
[ ] Tests cover accepted, rejected, duplicate, stale, and unauthorized cases.


Tick reliability checklist:

[ ] Tick stages run in a stable explicit order.
[ ] Each stage has timing recorded.
[ ] Command batch is sorted deterministically.
[ ] Seeded randomness uses world seed plus deterministic tick/date input.
[ ] Invariants run after load and after simulation.
[ ] Invalid worlds are not persisted as healthy state.
[ ] Slow ticks do not create catch-up storms.
[ ] Broadcast duration is measured separately from simulation duration.
[ ] DB write counts are exposed to admin.
[ ] Save/snapshot cadence is documented.


WebSocket and fanout checklist:

[ ] Every message type has an intended audience.
[ ] Private country updates are actor-targeted.
[ ] Command results are actor-targeted.
[ ] Market updates require subscription.
[ ] Dead sockets are removed.
[ ] Sends are serialized per socket.
[ ] Payload bytes are measured by message type.
[ ] Bytes/client/minute is reported.
[ ] Reconnect success rate is reported.
[ ] Time to first message is reported.
[ ] Oversized streams have an identified delta/coalescing plan.


Soak checklist:

[ ] Define client count, authenticated count, duration, and command interval.
[ ] Exercise login, subscriptions, commands, duplicate retries, stale tokens,
    disconnects, and reconnects.
[ ] Sample server RSS externally.
[ ] Sample DB commits, rollbacks, inserts, updates, deletes.
[ ] Count real thrown exceptions, not incidental log text.
[ ] Report tick drift.
[ ] Report command HTTP errors separately from expected rejections.
[ ] Report reconnect success rate.
[ ] Report bytes by message type.
[ ] Save run artifacts under an ignored directory.
[ ] Write a human-readable report after the run.


Recovery checklist:

[ ] Server starts from existing DB state.
[ ] Startup validates loaded world before accepting it.
[ ] Empty DB can seed from scenario.
[ ] Latest snapshot restore path is tested.
[ ] Manual named savepoints work.
[ ] Tick count never moves backward after restart.
[ ] Health endpoint recovers after restart.
[ ] Country, province, market, construction, command audit, and admin endpoints
    remain readable after restart.
[ ] Re-login preserves controlled country mapping.
[ ] Invariant violations are zero after restart.


Advanced Techniques to Add
--------------------------

1. Service-level objectives for the simulation

Define public SLOs for pre-alpha:

- 99% of ticks complete under 1000ms in the Albion demo.
- 95% of WebSocket clients receive first message under 2s locally.
- 0 command HTTP 5xx responses in a 40-client, 10-minute local run.
- 100% duplicate command retries are deduplicated.
- 100% stale-token attempts are rejected.
- 0 fatal invariant violations in accepted demo scenarios.


2. RED and USE metrics

Apply service monitoring frameworks:

RED for request/command paths:

- Rate: commands submitted per second
- Errors: 4xx expected rejects, 5xx unexpected failures
- Duration: command enqueue latency and execution tick delay

USE for resources:

- Utilization: CPU, memory, DB connection pool, WebSocket connections
- Saturation: command queue depth, pending sends, DB write backlog
- Errors: failed sends, DB errors, invariant violations


3. Queueing model

Track command lifecycle timestamps:

- received_at
- persisted_at
- enqueued_at
- dequeued_at
- executed_at
- outcome_sent_at

Then compute:

- API receive-to-persist latency
- persist-to-execute latency
- execute-to-broadcast latency
- total command round-trip latency


4. Replay-readiness score

Before adding command replay, score each command handler:

- deterministic
- idempotent
- validates authority during replay
- has stable target IDs
- does not depend on wall-clock time
- does not emit duplicate external side effects
- covered by replay-style tests

Only replay commands that pass the score.


5. Property-based and fuzz tests

For simulation stages:

- Generate random valid worlds.
- Apply random valid/invalid command sequences.
- Assert invariants after every tick.
- Shrink failures to a minimal world and command list.

Good targets:

- market quantities never negative
- prices stay clamped and finite
- POP employed + unemployed never exceeds size
- province ownership references always resolve
- one active construction per province
- country treasury remains representable


6. Delta compression and subscription partitions

When market_update grows too large:

- send only changed goods
- split market subscriptions by market id
- split world summary from market data
- add client-requested detail levels
- batch low-priority streams every N ticks
- use sequence numbers so clients can detect missed deltas and request snapshot


7. Backpressure-aware broadcasting

Current sends are per-socket serialized. Later, add:

- per-connection bounded send queues
- drop/coalesce policy for stale world/market updates
- always deliver command_result messages
- disconnect slow consumers after threshold
- expose slow consumer count in admin

Policy example:

- command_result: never drop
- reconnect_snapshot: never drop
- country_update: keep latest
- market_update: keep latest per market
- world_update: keep latest


8. Chaos drills

Add scripted drills:

- kill server during active commands
- kill server during snapshot save
- drop Redis
- drop PostgreSQL
- reconnect all clients at once
- submit duplicate command storm
- load malformed scenario
- force invariant violation in test-only path

Each drill should have expected behavior and pass/fail criteria.


9. Golden-run regression

For deterministic simulation:

- seed a known scenario
- run N ticks with a fixed command script
- serialize canonical outputs
- compare against a golden checksum

This catches accidental simulation drift.


10. Capacity envelope documents

For every public milestone, publish measured limits:

- scenario size
- client count
- authenticated client count
- tick duration p50/p95/p99
- bytes/client/minute
- DB writes/minute
- memory delta over soak
- largest message type
- known bottleneck

This creates credibility because the project says what it can and cannot do.


Recommended Public Documentation Page
-------------------------------------

Add docs/reliability_and_scale.md later with this structure:

1. Why reliability matters for grand strategy
2. Server authority and deterministic command ordering
3. Idempotency, retries, stale clients, and conflict rules
4. Tick pipeline and invariants
5. Persistence, snapshots, and restart recovery
6. WebSocket fanout and bandwidth accounting
7. Fake-client and soak testing
8. Admin explainability and observability
9. Current measured envelope
10. Known limits and next scaling work


Best Launch Claim
-----------------

Use this carefully:

"Victoria-Like is not just a Victoria-inspired simulation. It is a Victoria-like
simulation built as an online service: authoritative server, deterministic
command pipeline, idempotent retries, invariant-checked world state, restart
recovery, WebSocket fanout measurement, and black-box fake-client soak tests."

That is the genuinely interesting technical distinction.


What Not To Claim Yet
---------------------

Do not claim:

- production scale
- horizontal scalability
- replay-based recovery
- anti-cheat completeness
- stable public protocol
- balanced economy under large scenarios
- proven thousands of clients

Claim what is real:

- small-world server authority
- command audit and idempotency
- deterministic ordering
- explicit conflict rules
- invariant checks
- restart recovery model
- black-box fake-client harness
- measured 40-client, 1-hour soak
- measured fanout bandwidth
- admin explainability

