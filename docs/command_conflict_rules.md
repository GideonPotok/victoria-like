# Command Conflict Rules

Day 43 defines how simultaneous, duplicated, and stale gameplay commands are resolved.

## Ordering

Commands are processed in deterministic order:

1. `submitted_tick` ascending
2. `received_at` ascending
3. `command_id` ascending

The API sets `submitted_tick` from the authoritative world clock and `received_at` from server time when the command reaches the server.

This is VictoriaLike's lightweight equivalent of OpenRA's scheduled order
frames: the client may submit intent at any time, but the server owns the final
ordering slot in which gameplay commands are evaluated.

Not every client-visible event belongs in this ordered stream. Immediate UI
feedback, form state, and loading indicators stay local. Authoritative gameplay
changes enter the ordered pipeline.

## Idempotency

Clients may submit either:

- `commandId`: a client-generated UUID for an exact command retry
- `idempotencyKey`: a stable retry key scoped to the actor account

The server persists commands before queueing them. If a retry collides with an existing command ID or actor-scoped idempotency key, the existing command is returned and no second queue entry is created.

## Query and Execute

Preview/query calls do not reserve an ordering slot and do not create command
log entries. They are advisory reads over current authoritative state.

Only execute calls enter the deterministic command pipeline, consume command
budget, and produce authoritative audit records.

If a client uses preview/query before execution, the execute call must still be
validated against the world state at execution time.

## Repeated Tax Changes

Repeated tax changes are allowed. They are applied in deterministic command order, so the last ordered valid command wins. Each accepted or rejected command remains visible in the command audit log.

This is why the client should coalesce repeated edits before submission while
still expecting the server to decide the final accepted sequence.

## Province Construction

A province may have only one active construction queue entry. If two valid construction commands target the same province in the same tick, the first ordered command queues construction and later commands are rejected with `ActiveConstructionConflict`.

## Invalidated Commands

Commands are validated against the current world state at execution time. If an earlier command changes the state so a later command can no longer apply, the later command is rejected or failed through the normal command result path.

The client must treat this as an ordinary authoritative outcome, not as a
transport error. Earlier acceptance does not guarantee later applicability once
the command reaches execution.

## Nested Backend Operations

If a command handler needs subordinate operations, those operations should run
locally inside the handler rather than re-entering the external command queue.

Create separate queued commands only when the subordinate operation genuinely
needs its own ordering, retry, audit, or player-visible result lifecycle.

## Stale Client State

Clients may include `expectedWorldTick`. A command is rejected as `StaleClientState` when its expected tick is older than one tick behind the execution tick. Legacy commands without `expectedWorldTick` remain accepted for now.

## Replay and Recovery

Authoritative command ordering should remain inspectable enough to support:

- command audit review
- reconnect recovery after dropped clients
- future replay or deterministic re-simulation work

VictoriaLike does not need full RTS lockstep to benefit from these properties.
The practical requirement is that the server can explain what command was
accepted, in what order it executed, and why it was rejected or applied.
