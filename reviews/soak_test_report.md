# Soak Test Report

Day 58 deliverable: full fake-client soak with external sampling.

## Run

Command:

```bash
SOAK_CLIENTS=40 SOAK_AUTH_CLIENTS=20 SOAK_DURATION_SECONDS=3600 infra/run-soak-with-sampling.sh
```

Run id: `20260427T023436Z`

Artifacts:

- Server log: `soak-runs/20260427T023436Z/server.log`
- Harness log: `soak-runs/20260427T023436Z/harness.log`
- Memory samples: `soak-runs/20260427T023436Z/server-memory.csv`
- Postgres write samples: `soak-runs/20260427T023436Z/postgres-writes.csv`
- Generated report: `soak-runs/20260427T023436Z/soak_test_report.md`

## Summary

| Signal | Result |
| --- | ---: |
| Duration | 3606s |
| Target clients | 40 |
| Authenticated clients | 20 |
| Anonymous clients | 20 |
| Completed clients | 40 |
| Harness errors | 0 |
| Total messages | 384,197 |
| Messages/client/minute | 159.8 |
| Total bandwidth | 62.15 MB |
| Bytes/client/minute | 26.5 KB |
| Mean observed tick | 1000ms |
| Tick drift | 0ms |
| Reconnect attempts | 40 |
| Reconnect successes | 40 |
| Reconnect success rate | 100% |
| Stale token attempts | 2 |
| Stale token rejected | 2 |
| HTTP command errors | 0 |

## External Samples

| Signal | Result |
| --- | ---: |
| Memory samples | 121 |
| Server RSS first | 230,512 KB |
| Server RSS last | 87,312 KB |
| Server RSS min | 87,312 KB |
| Server RSS max | 230,512 KB |
| Server RSS delta | -143,200 KB |
| DB xact commit delta | 114,454 |
| DB xact rollback delta | 0 |
| DB tuple insert delta | 2,859 |
| DB tuple update delta | 75,798 |
| DB tuple delete delta | 330 |
| Runtime server error/fatal log lines | 0 |
| Runtime thrown exception log lines | 0 |
| Shutdown cancellation error log lines | 2 |

Notes:

- The generated report counted `Server exception log lines` too broadly in the first version of the sampling script. ASP.NET debug logs include the literal text `FilterType:"exception"` for normal MVC filter planning, which is not a thrown exception.
- Actual thrown exception stack traces are represented by Serilog `@x` fields. The only `@x` entries in this run were two `OperationCanceledException` lines during process shutdown after the harness completed.
- Memory did not grow. RSS fell from about 230 MB after startup/build warmup to about 87 MB by the end of the hour.

## Message Fanout

| Message type | Messages | Total bytes | Avg payload |
| --- | ---: | ---: | ---: |
| `market_update` | 143,960 | 25.40 MB | 185 B |
| `world_update` | 144,037 | 23.08 MB | 168 B |
| `country_update` | 71,980 | 9.98 MB | 145 B |
| `command_result` | 24,100 | 3.68 MB | 160 B |
| `subscribed` | 80 | 13.7 KB | 175 B |
| `reconnect_snapshot` | 40 | 4.6 KB | 118 B |

## Command Safety

| Signal | Result |
| --- | ---: |
| Commands sent | 3,442 |
| Duplicate retries | 3,442 |
| HTTP accepted | 4,820 |
| HTTP rejected | 2,064 |
| HTTP errored | 0 |

Rejections are expected because duplicate retries, stale-token checks, rate limits, and conflict/idempotency paths are deliberately exercised.

## Result

Day 58 passed.

The hour-long 40-client soak did not show tick drift, memory growth, reconnect failure, command HTTP errors, or runtime server exceptions. The Day 59 follow-up was therefore cleanup/observability rather than emergency correctness work:

1. Expected shutdown `OperationCanceledException` paths now avoid error-level logs.
2. The soak sampling script's exception counting remains narrow to real thrown exceptions.
3. Generated soak reports now include DB write-rate rows in addition to raw deltas.

See `day59_bottleneck_fixes.md`.
