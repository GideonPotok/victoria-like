# VictoriaLike Load Test

This project is a .NET console harness named `Victoria Fake Client Harness v2`.
It simulates many fake clients against a Victoria-like game server and reports
websocket, auth, reconnect, command, and bandwidth behavior.

The suite is implemented with plain .NET async code using `HttpClient` and
`ClientWebSocket`. It does not use a separate load-testing framework, does not
write result files, and does not currently enforce pass/fail thresholds.

## What It Exercises

The harness can run a mix of authenticated and anonymous clients. Each client
connects to the world websocket, subscribes to game topics, receives updates,
and optionally tests reconnect behavior. Authenticated clients can also log in,
submit commands, retry duplicate commands, and verify stale-token rejection.

Covered behavior:

- HTTP login and token use.
- Country/province discovery for authenticated clients.
- Websocket connection to `/ws/world`.
- Websocket topic subscription.
- World, country, market, reconnect snapshot, subscription ack, and command
  result message handling.
- One intentional reconnect per client.
- Periodic authenticated command submission.
- Duplicate command retry using the same idempotency key and command body.
- Stale-token invalidation after logout.
- Aggregate and per-client metrics reporting.

## Running

Default run:

```bash
dotnet run
```

Equivalent explicit run:

```bash
dotnet run -- --url=http://localhost:5001 --clients=20 --auth-clients=20 --duration=120
```

The older positional form is also supported:

```bash
dotnet run -- <url> <clients> <duration> [no-reconnect]
```

Example:

```bash
dotnet run -- http://localhost:5001 50 180 no-reconnect
```

## Configuration

Options use `--key=value` syntax. Boolean options are true only when set to
`true`, `yes`, or `1`.

| Option | Default | Meaning |
| --- | ---: | --- |
| `--url` | `http://localhost:5001` | Base HTTP URL for the server. The websocket URL is derived from this. |
| `--clients` | `20` | Total fake clients to start. |
| `--auth-clients` | `min(clients, 20)` | Number of clients that log in and act as players. Clamped between `0` and `clients`. |
| `--duration` | `120` | Run duration in seconds. |
| `--reconnect` | `true` | Whether each client intentionally disconnects and reconnects once. |
| `--commands` | `true` | Whether authenticated clients submit world commands. |
| `--duplicates` | `true` | Whether command submission immediately retries the exact same command body. |
| `--stale-token` | `true` | Whether the first two authenticated clients test logout/token invalidation. |
| `--command-interval` | `20` | Seconds between commands per authenticated client after initial delay. |
| `--startup-stagger-ms` | `150` | Milliseconds between starting each client. |

## Client Mix

The first `--auth-clients` clients are authenticated. Remaining clients are
anonymous websocket clients.

Authenticated clients rotate between these hard-coded accounts:

| Username | Password |
| --- | --- |
| `england-player` | `eng123` |
| `france-player` | `fra123` |

Authenticated client IDs look like:

```text
auth-00-england-player
auth-01-france-player
auth-02-england-player
```

Anonymous client IDs look like:

```text
anon-20
anon-21
```

Only the first two authenticated clients run the stale-token test.

## Authenticated Client Setup

Authenticated clients first log in:

```http
POST /api/auth/login
Content-Type: application/json

{
  "username": "...",
  "password": "..."
}
```

The login response is expected to contain:

```text
token
actor_id
controlled_country_id
```

The client then fetches province data:

```http
GET /api/world/provinces
```

It scans the returned array for the first province whose `owner_id` matches the
client's `controlled_country_id`. If found, that province is added to the
websocket subscription topics.

## Stale-Token Test

When enabled, the first two authenticated clients verify token invalidation:

1. Log in and receive a bearer token.
2. Log out:

   ```http
   POST /api/auth/logout
   Authorization: Bearer <token>
   ```

3. Reuse the same token:

   ```http
   GET /api/auth/me
   Authorization: Bearer <old-token>
   ```

4. Count the stale-token test as successful only if `/api/auth/me` returns
   `401 Unauthorized`.
5. Log in again to get a fresh token before websocket traffic begins.

The logout response itself is not checked for success. The test only verifies
that the old token is rejected afterward.

## Websocket Flow

Each client connects to:

```text
/ws/world
```

Authenticated clients pass the token as a query parameter:

```text
/ws/world?token=<token>
```

The websocket scheme is derived from `--url`:

```text
http://  -> ws://
https:// -> wss://
```

After connecting, each client sends a subscription message:

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

The `province:<provinceId>` topic is only included when the client found an
owned province during setup. Anonymous clients subscribe to `world_summary`,
`country`, and `market`.

## Message Handling

The client reads websocket text messages with an 8 KB receive buffer. It
supports fragmented websocket messages by appending chunks until
`EndOfMessage`.

Every message is counted by type and byte size. The harness has special
handling for:

| Message type | Handling |
| --- | --- |
| `world_update` | Reads `tick`, records last tick, counts a world update, and contributes to tick interval stats. |
| `reconnect_snapshot` | Treated like a world update for tick and timing metrics. |
| `market_update` | Counts a market update and records `tick` if present. |
| `country_update` | Counts a country update and records `tick` if present. |
| `subscribed` | Counts a subscription acknowledgement. |
| `command_result` | Reads `status`; `applied` counts as applied, `rejected` or `failed` counts as rejected. |

Unknown message types are still included in total message and bandwidth
metrics.

## Reconnect Test

When reconnect testing is enabled, each client schedules one intentional
disconnect 15 to 29 seconds after starting. It closes the websocket normally
with reason `reconnect test`, waits 500 ms, then reconnects and subscribes
again.

Any connection after the first is counted as a reconnect attempt. A successful
`ConnectAsync` after the first connection counts as a reconnect success.

## Command Submission

Only authenticated clients submit commands. If command submission is enabled,
each authenticated client sends its first command after a randomized initial
delay of 5 to 9 seconds, then repeats every `--command-interval` seconds.

Commands are sent to:

```http
POST /api/world/commands
Authorization: Bearer <token>
Content-Type: application/json
```

The command body is a `ChangeTaxRate` command:

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

If duplicate retry testing is enabled, the exact same command body is submitted
a second time immediately. This is meant to exercise idempotency and duplicate
command handling.

HTTP command responses are bucketed as:

| Status | Bucket |
| --- | --- |
| `2xx` | accepted |
| `401`, `403`, `409`, `422`, `429` | rejected |
| anything else, including synthetic status `0` after a send exception | errored |

## Runtime Status

Every 10 seconds the harness prints a compact progress line:

```text
[elapsed] msgs=<total> commands=<total> last_tick=<max tick> errors=<total>
```

Pressing Ctrl+C cancels the run cleanly and prints the final report using
whatever metrics have been collected.

## Final Report

At the end of the run, the report includes:

- Duration, target client count, and completed client count.
- Total messages, update counts, per-client average, and errors.
- Total received bandwidth.
- Bandwidth per client per minute.
- Messages per client per minute.
- Per-message-type message counts, total bytes, and average bytes.
- Observed tick interval mean and drift from a 1000 ms target.
- Login count, subscription topics requested, and subscription acknowledgements.
- Reconnect attempts, successes, and success rate.
- Command counts and HTTP accepted/rejected/errored buckets.
- Duplicate retry count.
- Stale-token attempts and stale-token rejections.
- Average and max time to first message.
- Per-client rows with auth status, messages, KB received, commands, duplicate
  retries, stale-token result, reconnect result, last tick, and time to first
  message.

## Notes and Limitations

- The suite is observational. It prints metrics but does not fail the process
  based on thresholds.
- "Time to first message" is set only when a `world_update` or
  `reconnect_snapshot` is processed, not literally any websocket message.
- The stale-token test does not check whether logout succeeded. It only checks
  whether the old token is rejected by `/api/auth/me`.
- Anonymous clients still subscribe to `country` and `market`; they simply do
  not submit commands or include a bearer token.
- Authenticated clients are limited to two hard-coded account identities,
  reused round-robin when more than two authenticated clients are requested.
- The project targets `net10.0`.
