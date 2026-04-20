# NBomber Load Test Migration Plan

This plan describes how to add a new NBomber-based load test suite while
leaving the existing `VictoriaLike.LoadTest` harness untouched.

The current harness should remain as the legacy/reference implementation. The
NBomber work should happen in a separate new test folder.

## Goals

- Add a modern load test suite with structured scenarios, thresholds, and
  reports.
- Preserve the current legacy harness exactly as-is.
- Reuse the existing domain behavior: login, province discovery, websocket
  subscription, command submission, duplicate retries, reconnects, and
  stale-token validation.
- Make performance regressions visible in CI by using explicit pass/fail
  criteria.
- Keep the test implementation understandable to .NET developers working in
  this repository.

## Proposed Location

Create a new sibling project:

```text
server/tests/VictoriaLike.NBomberLoadTest/
```

Do not move, rename, or rewrite:

```text
server/tests/VictoriaLike.LoadTest/
```

The legacy project can still be useful as a simple smoke/load harness and as a
behavioral reference while the NBomber suite is being built.

## Why NBomber

NBomber is a good fit because this load test is already written in C# and uses
custom HTTP plus websocket behavior. NBomber gives us the load-test machinery
without forcing the domain logic into another language or a GUI plan format.

Expected benefits:

- C# test code that fits the existing server/test stack.
- Named scenarios and steps.
- Load simulations such as warm-up, ramp-up, constant load, spike, and soak.
- Thresholds that can fail a run when latency, error rate, or custom metrics
  exceed limits.
- HTML/Markdown/CSV-style reporting.
- CI-friendly command-line execution.
- Support for custom protocol behavior, including HTTP and websocket flows.
- A path to distributed execution if one load generator is not enough.

## Project Shape

Recommended initial files:

```text
VictoriaLike.NBomberLoadTest/
  VictoriaLike.NBomberLoadTest.csproj
  Program.cs
  LoadTestConfig.cs
  VictoriaScenario.cs
  VictoriaClientSession.cs
  Metrics/
    VictoriaCounters.cs
  README.md
```

Suggested responsibilities:

- `Program.cs`: parse config, build NBomber scenarios, register thresholds, run.
- `LoadTestConfig.cs`: command-line/env configuration.
- `VictoriaScenario.cs`: NBomber scenario definitions and load profiles.
- `VictoriaClientSession.cs`: per-virtual-user state and protocol operations.
- `VictoriaCounters.cs`: custom counters for game-specific metrics.
- `README.md`: how to run locally and in CI.

## Behavior To Preserve

The NBomber suite should cover the same functional behavior as the legacy
harness before adding more advanced cases.

### Authenticated Setup

Each authenticated virtual user should:

1. `POST /api/auth/login`.
2. Store `token`, `actor_id`, and `controlled_country_id`.
3. `GET /api/world/provinces`.
4. Pick the first province whose `owner_id` matches `controlled_country_id`.

The initial version can reuse the same two known accounts:

| Username | Password |
| --- | --- |
| `england-player` | `eng123` |
| `france-player` | `fra123` |

Later, replace these with a configurable credential pool.

### Stale-Token Validation

For a configurable subset of authenticated users:

1. Log out with `POST /api/auth/logout`.
2. Reuse the old token against `GET /api/auth/me`.
3. Treat `401 Unauthorized` as the expected result.
4. Log in again before continuing to websocket traffic.

This should become an explicit named step with its own success/failure metric.

### Websocket Subscription

Each virtual user should connect to:

```text
/ws/world
```

Authenticated users should connect with:

```text
/ws/world?token=<token>
```

After connecting, send:

```json
{
  "type": "subscribe",
  "topics": [
    "world_summary",
    "country",
    "market",
    "province:<provinceId>"
  ]
}
```

Only include the `province:<provinceId>` topic when a province was found.

### Message Receive Loop

Track all websocket messages by type and size. Preserve special handling for:

| Message type | Metric |
| --- | --- |
| `world_update` | world update count, last tick, tick interval |
| `reconnect_snapshot` | reconnect snapshot count, last tick, tick interval |
| `market_update` | market update count, last tick |
| `country_update` | country update count, last tick |
| `subscribed` | subscription ack count |
| `command_result` | applied/rejected/failed result count |

The NBomber version should make websocket receive behavior a first-class part
of the scenario rather than hiding it behind a final console summary.

### Reconnect Test

Each virtual user should optionally perform one intentional reconnect:

1. Connect and subscribe.
2. Wait a randomized delay.
3. Close the websocket normally.
4. Reconnect.
5. Subscribe again.
6. Record reconnect attempt and success.

### Command Submission

Authenticated users should optionally submit periodic commands:

```http
POST /api/world/commands
Authorization: Bearer <token>
Content-Type: application/json
```

Command body:

```json
{
  "commandId": "<guid>",
  "idempotencyKey": "load-<clientId>-<guid>",
  "expectedWorldTick": <lastServerTick or null>,
  "commandType": "ChangeTaxRate",
  "payload": {
    "countryId": "<controlledCountryId>",
    "newTaxRate": <random 5..29>
  }
}
```

When duplicate testing is enabled, submit the exact same command body a second
time and record the duplicate retry result separately.

## Scenario Design

Start with two scenarios.

### Scenario 1: Websocket Subscribers

Purpose: measure fan-out, websocket stability, message volume, tick delivery,
and reconnect behavior.

Virtual user behavior:

1. Optional login.
2. Optional province discovery.
3. Connect websocket.
4. Subscribe.
5. Receive messages for the configured scenario duration.
6. Optionally reconnect once.

Suggested load profiles:

- Smoke: 1 to 5 users for 30 seconds.
- Baseline: ramp to 20 users, hold for 2 minutes.
- Stress: ramp to 100+ users, hold for 5 to 10 minutes.
- Soak: stable user count for 30+ minutes.

### Scenario 2: Authenticated Command Safety

Purpose: measure command endpoint behavior while websocket updates are active.

Virtual user behavior:

1. Login.
2. Load owned country/province context.
3. Connect websocket and subscribe.
4. Periodically submit `ChangeTaxRate`.
5. Optionally retry duplicate command body.
6. Record HTTP result and websocket `command_result`.

Suggested load profiles:

- Smoke: 2 authenticated users for 30 seconds.
- Baseline: 20 authenticated users, command every 20 seconds.
- Stress: gradually reduce command interval or increase users.

## Metrics To Capture

Use built-in NBomber metrics for request/step latency, success rate, failure
rate, and throughput. Add custom counters/gauges for game-specific behavior.

Game-specific metrics:

- Websocket connect attempts.
- Websocket connect failures.
- Reconnect attempts.
- Reconnect successes.
- Subscription messages sent.
- Subscription acknowledgements.
- Messages received by type.
- Bytes received by type.
- Last tick seen.
- Tick interval mean/p95/max.
- Time to first `world_update` or `reconnect_snapshot`.
- Commands sent.
- Command HTTP accepted/rejected/errored.
- Command results applied/rejected/failed.
- Duplicate retries.
- Duplicate retry expected rejections or idempotent acceptances.
- Stale-token attempts.
- Stale-token expected `401` results.

## Thresholds

Initial thresholds should be conservative and tuned after a few local baseline
runs. The first pass should catch obvious failures without being flaky.

Suggested starting thresholds:

| Area | Initial threshold |
| --- | --- |
| Websocket connect success | At least 99% |
| Reconnect success | At least 95% when reconnect test is enabled |
| Subscription ack rate | At least 99% |
| Stale-token expected rejection | 100% of stale-token attempts return `401` |
| HTTP command infrastructure errors | Less than 1% |
| Time to first world update | p95 under 5 seconds |
| Tick interval drift | Mean observed interval within 250 ms of 1000 ms |
| Unexpected websocket errors | 0 in smoke runs; low bounded rate in stress runs |

Command rejection thresholds need care. Some `409`, `422`, or duplicate-related
responses may be expected depending on server semantics. The NBomber suite
should separate expected safety rejections from unexpected infrastructure
errors.

## Configuration

Support CLI args and environment variables so the same suite works locally and
in CI.

Recommended settings:

| Setting | Default |
| --- | --- |
| `BaseUrl` | `http://localhost:5001` |
| `TotalUsers` | `20` |
| `AuthenticatedUsers` | `20` |
| `DurationSeconds` | `120` |
| `WarmupSeconds` | `10` |
| `ReconnectEnabled` | `true` |
| `CommandsEnabled` | `true` |
| `DuplicateRetriesEnabled` | `true` |
| `StaleTokenEnabled` | `true` |
| `CommandIntervalSeconds` | `20` |
| `StartupStaggerMs` | `150` |
| `ScenarioProfile` | `baseline` |
| `CredentialsFile` | empty |

Credential handling should be improved in the NBomber version. Prefer a local
JSON or CSV credential file, environment variables, or test fixture data over
hard-coding additional accounts.

## Implementation Phases

### Phase 1: Project Scaffold

- Create `server/tests/VictoriaLike.NBomberLoadTest/`.
- Add a .NET console project.
- Add NBomber packages.
- Add config parsing.
- Add a minimal smoke scenario that starts and reports successfully.

### Phase 2: Port Legacy Behavior

- Port login.
- Port province discovery.
- Port websocket connect and subscribe.
- Port message receive handling.
- Port reconnect behavior.
- Port command submission and duplicate retry.
- Port stale-token validation.

The port should preserve the legacy behavior before changing semantics.

### Phase 3: Add NBomber Metrics

- Map protocol operations to named NBomber steps.
- Add custom counters for game-specific values.
- Add structured result reporting.
- Validate that report values match the legacy harness for small local runs.

### Phase 4: Add Thresholds

- Add smoke thresholds first.
- Add baseline thresholds after local calibration.
- Keep expected command rejections separate from unexpected failures.
- Make CI fail only on strong, meaningful performance or correctness signals.

### Phase 5: CI Integration

- Add a small smoke profile suitable for pull requests.
- Add a heavier baseline/stress profile for manual or scheduled runs.
- Publish NBomber reports as CI artifacts.
- Document how to run the suite against local and deployed servers.

### Phase 6: Scale-Out Options

- Evaluate whether a single generator process is enough.
- If not, add distributed execution or managed execution.
- Consider exporting metrics to the team's existing observability stack.

## Validation Against Legacy Harness

Before relying on the NBomber suite, run both harnesses against the same local
server using similar settings:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest -- --clients=20 --auth-clients=20 --duration=120
```

Then run the equivalent NBomber profile.

Compare:

- Total messages received.
- Messages per client per minute.
- Last tick seen.
- Reconnect success count.
- Commands sent.
- Duplicate retry count.
- Stale-token rejected count.
- Error count.

The values do not need to be identical, but they should be directionally
consistent. Any intentional behavior differences should be documented in the
new NBomber README.

## Open Questions

- What should the authoritative command duplicate behavior be: idempotent
  success, conflict, or already-applied result?
- Should anonymous clients subscribe to `country`, or should that become an
  authenticated-only topic in the new suite?
- Do we need a larger seeded account pool before meaningful command-load tests?
- What threshold should define acceptable tick drift under stress?
- Should the suite eventually drive multiple countries/provinces per scenario?
- Where should CI publish long-running load test reports?

## Non-Goals For The First Version

- Replacing or deleting the legacy harness.
- Building a full managed cloud load-testing pipeline.
- Modeling every gameplay command.
- Rewriting server behavior to satisfy the load test.
- Adding strict stress thresholds before baseline data exists.
