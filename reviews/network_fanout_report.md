# Network Fanout Report

Day 57 deliverable: bandwidth and update fanout measurement.

## Harness Support

`server/tests/VictoriaLike.LoadTest` now records WebSocket payload bytes in addition to message counts:

- total bytes received
- bytes per client per minute
- messages per client per minute
- message count, total bytes, and average payload size by message type
- per-client received KB

Run:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest/VictoriaLike.LoadTest.csproj -- --url=http://localhost:5001 --clients=20 --duration=120
```

Stretch:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest/VictoriaLike.LoadTest.csproj -- --url=http://localhost:5001 --clients=50 --auth-clients=20 --duration=120
```

## Baseline Run

Source: 20-client fake harness run against `http://localhost:5001`, duration 124 seconds wall-clock.

| Signal | Result |
| --- | ---: |
| Target clients | 20 |
| Completed clients | 20 |
| Total messages | 7,780 |
| Messages/client/minute | 188.2 |
| World updates | 2,460 |
| Market updates | 2,380 |
| Country updates | 2,380 |
| Reconnect success | 20/20 |
| Command HTTP errors | 0 |
| Harness errors | 0 |
| Mean observed tick | 988ms |
| Tick drift | 12ms |

## Bandwidth Measurement

Source: 20-client fake harness run against `http://localhost:5001`, interrupted cleanly with Ctrl+C after 48 seconds.

| Signal | Result |
| --- | ---: |
| Total received | 479.3 KB |
| Bytes/client/minute | 30.2 KB |
| Messages/client/minute | 185.1 |
| Total messages | 2,953 |
| World updates | 970 |
| Market updates | 913 |
| Country updates | 913 |
| Harness errors | 0 |
| Mean observed tick | 971ms |
| Tick drift | 29ms |

| Message type | Messages | Total bytes | Avg payload |
| --- | ---: | ---: | ---: |
| `market_update` | 913 | 164.9 KB | 185 B |
| `world_update` | 930 | 152.6 KB | 168 B |
| `country_update` | 913 | 130.2 KB | 146 B |
| `command_result` | 117 | 18.3 KB | 160 B |
| `subscribed` | 40 | 8.7 KB | 223 B |
| `reconnect_snapshot` | 40 | 4.6 KB | 118 B |

## Fanout Shape

Current broadcast behavior:

- `world_update`: sent to every open socket.
- `market_update`: sent to sockets subscribed to `market`; authenticated fake clients subscribe by default.
- `country_update`: sent to every open socket for the actor that controls the changed country.
- `command_result`: sent to every open socket for the actor that submitted the command.
- `subscribed`: sent once after each subscription request.
- `reconnect_snapshot`: sent once when an authenticated client reconnects.

For the 20-auth-client baseline, the steady-state stream is roughly:

- 20 world messages per tick.
- 20 market messages per tick.
- 20 country messages per tick, split across the two actor accounts.

That is expected for the current tiny two-country scenario. There is no evidence yet that unauthenticated clients receive country-private updates.

## Oversized Delta Check

Known likely risks as the world grows:

- `world_update` includes `market_prices`; this is fine with one market and a small goods set, but it will grow with every globally visible good.
- `market_update` sends full `prices`, `supply`, and `demand` dictionaries on every tick, not only changed values.
- `country_update` is small today: country id, tax rate, and treasury.
- Province subscriptions are acknowledged by the hub, but there is not yet a dedicated recurring `province_update` stream in the observed harness output.

No coalescing is required for the current local scale. The measured total is only 30.2 KB/client/minute with 20 authenticated clients.

Top payload risk: `market_update` is already the largest byte consumer at 164.9 KB over the 48-second sample. It sends full `prices`, `supply`, and `demand` dictionaries on every tick, so it is the first stream to coalesce or delta-compress once the goods list, market count, or player count grows.

## Acceptance Notes

Day 57 is complete:

- bytes/client/minute measured
- messages/client/minute measured
- `world_update`, `market_update`, and `country_update` average payload sizes measured
- oversized delta risk identified
- no immediate coalescing change needed at current tiny-world scale
