# Fake Client Harness v2

Day 56 deliverable: reusable fake client suite.

## Target Run

Run 20 fake clients for two minutes:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest -- --clients=20 --duration=120
```

This defaults to:

- 20 authenticated clients, cycling between `england-player` and `france-player`.
- Login and country assignment.
- WebSocket world/country/market/province subscriptions.
- Periodic tax command submission.
- Duplicate command retry using the same command ID and idempotency key.
- Stale-token attempt on the first real account instances.
- Disconnect/reconnect cycle.

## Stretch Run

Run 50 fake clients for two minutes:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest -- --clients=50 --auth-clients=20 --duration=120
```

The remaining clients are anonymous WebSocket observers. This keeps command pressure reasonable while still testing update fanout.

## Options

- `--url=http://localhost:5001`
- `--clients=20`
- `--auth-clients=20`
- `--duration=120`
- `--reconnect=true`
- `--commands=true`
- `--duplicates=true`
- `--stale-token=true`
- `--command-interval=20`
- `--startup-stagger-ms=150`

Backward-compatible positional form still works:

```bash
dotnet run --project server/tests/VictoriaLike.LoadTest -- http://localhost:5001 20 120
```

## Report Signals

The harness reports:

- Message totals by world, market, and country updates.
- Total WebSocket bytes, bytes per client per minute, and messages per client per minute.
- Message count, total bytes, and average payload size by WebSocket message type.
- Time to first message.
- Observed tick interval drift.
- Login count.
- Subscription topic requests and acknowledgements.
- Reconnect attempts and success rate.
- Commands sent, accepted, rejected, and errored.
- Duplicate retry count.
- Stale token attempts and expected rejection count.
- Per-client summary.

## Notes

The harness is intentionally HTTP/WebSocket black-box oriented. It does not require direct database access and should be run against a local development server with the tiny two-country scenario initialized.
