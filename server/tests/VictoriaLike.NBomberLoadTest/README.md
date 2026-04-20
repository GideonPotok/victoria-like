# VictoriaLike NBomber Load Test

This project is the NBomber-based load test suite for the Victoria-like server.
It lives beside the legacy `VictoriaLike.LoadTest` harness and does not replace
or modify that reference implementation.

## Running

Start the server locally, then run:

```bash
dotnet run --project server/tests/VictoriaLike.NBomberLoadTest -- --profile=smoke --duration=30 --total-users=5 --auth-users=2
```

The default target is `http://localhost:5001`.

Two-player peaceful soak:

```bash
dotnet run --project server/tests/VictoriaLike.NBomberLoadTest -- --profile=two-player-soak
```

This profile defaults to two authenticated users for 30 minutes and uses the
peaceful economy command mix.

## Profiles

| Profile | Behavior |
| --- | --- |
| `smoke` | Websocket subscriber scenario only. |
| `baseline` | Websocket subscriber scenario plus authenticated command safety. |
| `stress` | Same scenario mix as baseline; tune user count and duration upward. |
| `soak` | Websocket subscribers plus authenticated command safety; tune duration upward. |
| `two-player-soak` | Two authenticated long-lived players with peaceful economy commands and strict acceptance gates. |
| `subscribers` | Websocket subscriber scenario only. |
| `commands` | Authenticated command safety scenario only. |
| `all` | Both initial scenarios. |

## Configuration

Options can be passed as `--key=value` or with environment variables.

| Option | Environment | Default |
| --- | --- | ---: |
| `--url` | `VICTORIA_NBOMBER_BASE_URL` | `http://localhost:5001` |
| `--total-users` | `VICTORIA_NBOMBER_TOTAL_USERS` | `20` |
| `--auth-users` | `VICTORIA_NBOMBER_AUTH_USERS` | `20` |
| `--duration` | `VICTORIA_NBOMBER_DURATION_SECONDS` | `120` |
| `--warmup` | `VICTORIA_NBOMBER_WARMUP_SECONDS` | `10` |
| `--reconnect` | `VICTORIA_NBOMBER_RECONNECT` | `true` |
| `--commands` | `VICTORIA_NBOMBER_COMMANDS` | `true` |
| `--duplicates` | `VICTORIA_NBOMBER_DUPLICATES` | `true` |
| `--stale-token` | `VICTORIA_NBOMBER_STALE_TOKEN` | `true` |
| `--command-interval` | `VICTORIA_NBOMBER_COMMAND_INTERVAL_SECONDS` | `20` |
| `--startup-stagger-ms` | `VICTORIA_NBOMBER_STARTUP_STAGGER_MS` | `150` |
| `--profile` | `VICTORIA_NBOMBER_PROFILE` | `baseline` |
| `--command-mix` | `VICTORIA_NBOMBER_COMMAND_MIX` | `full`, or `peaceful` for `two-player-soak` |
| `--credentials-file` | `VICTORIA_NBOMBER_CREDENTIALS_FILE` | empty |

Boolean values are true only when set to `true`, `yes`, or `1`.

## Credentials

When no credential file is supplied, the suite uses the same seeded accounts as
the legacy harness:

| Username | Password |
| --- | --- |
| `albion-player` | `alb123` |
| `bretoria-player` | `bre123` |

JSON credential file format:

```json
[
  { "username": "albion-player", "password": "alb123" },
  { "username": "bretoria-player", "password": "bre123" }
]
```

CSV credential file format:

```text
albion-player,alb123
bretoria-player,bre123
```

## Covered Behavior

- `POST /api/auth/login`.
- `GET /api/world/provinces` and owned province discovery.
- Optional stale-token validation through logout and `GET /api/auth/me`.
- Websocket connect to `/ws/world`, with bearer token query parameter for
  authenticated users.
- Topic subscription for `world_summary`, `country`, `market`, and optional
  `province:<provinceId>`.
- Message accounting for world, reconnect snapshot, market, country,
  subscription, and command-result messages.
- Optional intentional reconnect.
- Optional periodic gameplay command submission and duplicate retry. The command
  mix starts with `DeclareWar` when a foreign country is available, then samples
  `ChangeTaxRate`, `MoveArmy`, and `MakePeace` so soak runs exercise the new
  military command path as well as the older budget path.
- `two-player-soak` uses only peaceful economy commands: `ChangeTaxRate`,
  `ChangeStrataTax`, and `ChangeSpending`. It fails the websocket command loop
  if either player misses login/session setup, reconnect, subscription ack,
  reconnect snapshot, world updates, command submission, or records command HTTP
  infrastructure errors / failed command-result messages.

NBomber writes its normal reports under the default `reports` output folder.
