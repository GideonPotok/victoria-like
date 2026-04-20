# Unity Command Pipeline Research Plan

This plan sequences deeper research into multiplayer command pipelines, then
collapses the ideal research track into a practical seven-day plan for updating
`docs/unity-frontend-and-command-ui.md`.

The goal is to improve future Unity frontend work and the backend layer that
directly supports it. The output should be a durable set of principles,
decision frameworks, and implementation guidance for command UX, command
scheduling, server responses, and stress testing.

## Research Sources, Most to Least Relevant

1. OpenRCT2 Game Actions
   - Why: closest fit to VictoriaLike. It is a management sim with user actions,
     structured action results, multiplayer-safe execution, and a useful
     `Query`/`Execute` split.
   - Source: https://github-wiki-see.page/m/OpenRCT2/OpenRCT2/wiki/Game-actions

2. OpenRCT2 Multiplayer
   - Why: shows how management-sim game actions are networked and synchronized
     across clients.
   - Source: https://github-wiki-see.page/m/OpenRCT2/OpenRCT2/wiki/Multiplayer

3. OpenRA Order Processing and Networking
   - Why: strong deterministic command pipeline: orders, delayed execution
     frames, server distribution, sync checks, replayability.
   - Source: https://deepwiki.com/OpenRA/OpenRA/2.3-order-processing-and-networking

4. Unity Boss Room and Netcode Guidance
   - Why: Unity-native guidance for server authority, input buffering, transient
     RPC-style events, persistent replicated state, and late-join state.
   - Sources:
     - https://mp-docs.dl.it.unity3d.com/netcode/1.7.1/learn/rpcvnetvar/
     - https://docs-multiplayer.unity3d.com/netcode/1.9.1/learn/rpcnetvarexamples/

5. Unity Lockstep Examples
   - Why: useful mostly as contrast. Full lockstep is likely too heavy for
     VictoriaLike's Unity dashboard, but buffering, tick-indexed intent,
     backpressure, and replay concepts are relevant.

## Thirty-Day Ideal Research Track

### Days 1-7: OpenRCT2 Management-Sim Action Model

1. Map OpenRCT2 `GameAction`, `Query`, `Execute`, result objects, and nested
   actions.
2. Study OpenRCT2 action result taxonomy: success, error, cost, position, and
   UI-facing messages.
3. Study OpenRCT2 multiplayer action flow: who sends actions, who executes, and
   how clients stay consistent.
4. Study the nested-action rule: avoid accidental duplicate network actions.
5. Translate OpenRCT2 action principles into VictoriaLike command vocabulary.
6. Draft a VictoriaLike `Query`/`Execute` backend proposal.
7. Update the Unity frontend doc with command lifecycle, expected frontend
   behavior, and structured outcomes.

### Days 8-14: OpenRA Deterministic Order Pipeline

8. Map OpenRA orders, `OrderManager`, delayed frames, and deterministic order.
9. Study command scheduling: immediate orders vs delayed gameplay orders.
10. Study sync hashes, desync detection, and replay implications.
11. Study the server role: validation, ordering, and broadcast.
12. Compare OpenRA ordering to `docs/command_conflict_rules.md`.
13. Identify which OpenRA concepts are overkill for VictoriaLike.
14. Update docs with deterministic command ordering and replay/recovery
    principles.

### Days 15-21: Unity-Native Multiplayer Patterns

15. Study Boss Room's server-authoritative architecture.
16. Extract the RPC vs persistent-state decision rule.
17. Study input buffering: when full input history matters.
18. Study late-join behavior: why durable state must be refreshable, not only
    event-driven.
19. Translate Boss Room patterns to VictoriaLike's HTTP/WebSocket split.
20. Update docs with an event-vs-state decision framework for Unity.
21. Update docs with UI Toolkit binding and view-model boundaries.

### Days 22-30: VictoriaLike Design Synthesis and Tests

22. Study lockstep examples for input delay, buffered commands, and tick-indexed
    intent.
23. Decide which lockstep ideas apply: buffering and backpressure yes, client
    simulation no.
24. Study idempotency and retry patterns for command APIs.
25. Design a command scheduler: debounce, coalescing, retry, and cancellation.
26. Define VictoriaLike `ClientCommandScheduler` principles.
27. Define backend response contracts: accepted, rejected, failed, pending, and
    retry metadata.
28. Design stress tests for rapid clicks, reconnects, duplicate retries, and
    cooldown responses.
29. Collapse findings into final doc edits and a future implementation backlog.
30. Final review across architecture, command conflict, Unity frontend, and
    load-test docs.

## Collapsed Seven-Day Plan

### Day 1: OpenRCT2 Deep Dive

Focus on `GameAction`, `Query`, `Execute`, structured results, and nested
actions.

Status: completed on 2026-04-28.

Doc output:
- Commands should support preview/query when UI needs button enablement, cost,
  or reason text.
- Game-level command outcomes should be structured and UI-readable.

### Day 2: OpenRA Deep Dive

Focus on deterministic ordering, scheduled execution, immediate vs delayed
orders, replay, and desync lessons.

Status: completed on 2026-04-28.

Doc output:
- Frontend commands enter an authoritative ordered pipeline.
- Client scheduling improves UX but does not decide truth.
- Separate immediate local UI feedback from authoritative command execution.

### Day 3: Unity Boss Room and Netcode Deep Dive

Focus on transient event vs persistent state, input history vs latest desired
state, and late-join state refresh.

Status: completed on 2026-04-28.

Doc output:
- Add a decision table for event, state, draft, command, and inspection fetch.
- Clarify when the client should preserve input history versus only latest
  desired state.

### Day 4: VictoriaLike Command Scheduler Design

Define the client-side framework:
- draft state
- debounce
- coalescing
- one in-flight command per logical field
- retry on `retryAfterTicks`
- cancellation or replacement of stale desired values

Status: completed on 2026-04-28.

Doc output:
- Add recommended scheduler responsibilities and lifecycle.

### Day 5: Backend Contract Design

Define command-facing backend expectations:
- structured responses
- stable rejection reasons
- retry metadata
- idempotency keys
- possible future `QueryCommand` or preview endpoints

Doc output:
- Add backend contract requirements for frontend-safe command handling.

### Day 6: Collapsed Stress-Test Design

Replace a large stress matrix with a few focused tests:
- rapid budget clicks coalesce into latest desired value
- cooldown responses retry without Unity Console error spam
- reconnect/refresh preserves authoritative state and clears stale pending UI

Doc output:
- Add a small test matrix to the Unity frontend doc or load-test notes.

### Day 7: Documentation Pass

Update `docs/unity-frontend-and-command-ui.md` with the final researched
principles. Cross-link any needed changes from:
- `docs/architecture.md`
- `docs/command_conflict_rules.md`
- load-test docs, if stress testing guidance becomes concrete

## Decision Framework To Add To The Frontend Guidelines

Future Unity/frontend command work should answer these questions:

- Is this persistent state or a transient event?
- Is this a discrete action or an adjustable desired value?
- Does the player care about the history of inputs or only the latest desired
  state?
- Should the UI ask "can I do this?" before executing?
- Can this command be safely retried with an idempotency key?
- What should happen if the server says "retry after N ticks"?
- What server DTO refresh is needed after success?
- What is the correct UX for pending, rejected, failed, and disconnected?

## Expected Final Output

The research should result in:

- a stronger `docs/unity-frontend-and-command-ui.md`
- optional updates to `docs/architecture.md`
- optional updates to `docs/command_conflict_rules.md`
- a concrete command scheduler implementation backlog
- a small stress-test plan for frontend command interaction
