# Day 59 Bottleneck Fixes

Day 59 scope: no new gameplay features. Use the Day 58 soak results to clean up the top observed operational issues and document remaining limits.

## Inputs

Primary soak run:

```bash
SOAK_CLIENTS=40 SOAK_AUTH_CLIENTS=20 SOAK_DURATION_SECONDS=3600 infra/run-soak-with-sampling.sh
```

Run id: `20260427T023436Z`

Source report: `soak_test_report.md`

## Findings

The soak did not expose an emergency performance bottleneck.

Key results:

- 40 clients completed a one-hour run.
- Harness errors: 0.
- HTTP command errors: 0.
- Reconnect success: 40/40.
- Tick drift: 0ms.
- Memory growth: none observed.
- Runtime server exceptions: 0.

The actionable Day 59 issues were observability and shutdown polish:

1. Expected shutdown cancellation was logged as server error noise.
2. The first soak sampler counted the string `exception` too broadly.
3. DB write volume was sampled but not expressed as a rate in generated reports.

## Fixes

### Graceful Shutdown Cancellation

Expected `OperationCanceledException` during server shutdown is no longer logged as an error for:

- economic tick cancellation
- world-state save cancellation
- building queue persistence cancellation

Unexpected exceptions still log at error level.

### Soak Exception Counting

The soak wrapper now counts real thrown exception logs narrowly with:

```text
"@x":
unhandled exception
```

This avoids false positives from framework debug messages that describe MVC exception filters but do not represent thrown runtime exceptions.

### DB Write-Rate Reporting

The generated soak report now includes:

- memory sample count
- DB sample count
- transaction commits per minute
- tuple inserts per minute
- tuple updates per minute
- tuple deletes per minute

This makes future Day 58/59 comparisons easier without manually calculating rates from CSV deltas.

## Remaining Known Issues

- DB write volume is acceptable at current scale, but tuple updates should be watched as Month 4 adds POP rows and monthly simulation stages.
- The load harness still samples process RSS externally rather than through an in-process metrics endpoint.
- The server remains single-process and single-node. The soak proves local stability, not horizontal scale.

## Result

Day 59 passed.

No coalescing or persistence rewrite is justified yet. The next scale watchpoint is Month 4 POP data volume, especially snapshot/persistence cost once provinces contain many `PopGroup` rows.
