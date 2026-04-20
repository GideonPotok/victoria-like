# Permissions And Command Safety Review

Date: 2026-04-26

Scope: Week 9, Days 41-45. This review covers the command authorization path, durable audit trail, simultaneous command conflicts, rate limits, and retry behavior.

## Current Command Safety Model

All gameplay commands enter through `POST /api/world/commands`. The endpoint resolves actor identity from a bearer session token first, then the legacy request-body actor ID fallback. The actor must map to a player account before a command is built.

Commands carry server-side metadata:

- `command_id`
- `actor_id`
- `country_id`
- `command_type`
- `target_ids`
- `submitted_tick`
- `received_at`
- `expected_world_tick`
- `idempotency_key`

The command queue processes commands deterministically by `submitted_tick`, then `received_at`, then `command_id`.

## Conflict Rules

The authoritative rules are documented in `docs/command_conflict_rules.md`.

Repeated tax changes are legal. The final tax rate is the last valid command in deterministic order.

Province construction is exclusive. A province may have only one active construction entry, and later construction commands for the same province are rejected with `ActiveConstructionConflict`.

Duplicate command IDs are rejected inside a tick batch. Duplicate submissions using the same command ID or actor-scoped idempotency key are deduplicated before queueing.

Commands with `expected_world_tick` older than one tick behind execution are rejected as `StaleClientState`.

## Rate Limits And Budgets

The command budget layer is in-memory and intentionally conservative for Month 3.

Per-account limits:

- Soft limit: 10 commands per 10 seconds
- Hard limit: 20 commands per 10 seconds
- Soft limit behavior: command is still queued and response status is `queued_soft_limited`
- Hard limit behavior: command is rejected with HTTP 429 before entering the tick queue

Per-country strategic cooldowns:

- `ChangeTaxRate`: 3 ticks
- `QueueBuilding`: 1 tick

Budget state is visible on `/admin` and in `/api/admin/summary` under `command_budgets`.

## Hardening Scenarios

Two users control different countries:

Covered by account-country ownership checks in command handlers. A command targeting a country or province outside the actor's controlled country is rejected.

One user attempts commands on another country:

`ChangeTaxRate` uses country ownership validation. `QueueBuilding` uses province ownership validation.

Duplicate command submits:

The command repository has a unique command ID constraint and an actor-scoped idempotency key index. Existing matching submissions are returned without queueing a second command.

Stale token command submits:

Bearer tokens are validated before command construction. Invalid or expired sessions return `401 Unauthorized` and do not enter the command queue.

Rapid repeated commands:

The budget layer logs soft-limit activity, rejects hard-limit traffic, and rejects commands still under country cooldown.

Reconnect and resubmit:

Clients can retry with the same `commandId` or `idempotencyKey`. The server returns the existing command record instead of queueing a duplicate.

## Verification

Automated coverage added:

- Deterministic command ordering
- Duplicate command ID rejection
- Stale client tick rejection
- Same-province construction conflict
- Soft command budget threshold
- Hard command budget rejection
- Strategic country cooldown
- Admin budget snapshot shape

Commands run:

```bash
dotnet test server/tests/VictoriaLike.Core.Tests/VictoriaLike.Core.Tests.csproj --no-restore
dotnet build server/src/VictoriaLike.Server/VictoriaLike.Server.csproj --no-restore
```

## Known Limits

The command budget state is currently in-memory. That is acceptable for the current single-server Month 3 architecture, but it will need persistence or Redis before horizontal scaling.

Rate-limited rejects are returned as structured responses but are not yet inserted into `command_log`, because the current repository is centered on persisted commands. If moderation-grade rejected-submit audit becomes necessary, add a dedicated rejected submission audit path.

The legacy body `actorId` fallback remains available for development compatibility. Production should require bearer sessions only.
