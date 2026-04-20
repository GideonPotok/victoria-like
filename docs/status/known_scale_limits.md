# Known Scale Limits

Day 60 deliverable.

## Current Proven Envelope

The current local prototype has been exercised with:

- 40 fake clients
- 20 authenticated command-sending clients
- 20 anonymous subscription clients
- 3600 second harness duration
- world, country, market, and command-result fanout
- duplicate command retries
- stale-token attempts
- reconnect cycles

The run completed with:

- 0 harness errors
- 0 HTTP command errors
- 0ms observed tick drift
- 0 runtime thrown server exceptions
- no observed memory growth

## Not Yet Proven

The current system has not proven:

- hundreds or thousands of clients
- multiple server instances
- external load balancer behavior
- Redis-backed multi-node WebSocket fanout
- large POP table persistence
- large market goods sets
- cross-country/world-market trade
- production database durability under process or host failure
- cloud deployment latency
- Unity client bandwidth under real player navigation

## Watchpoints For Month 4

Month 4 will add persistent POPs and larger simulation state. Re-check:

- snapshot size
- migration time
- startup load time
- tick persist duration
- monthly POP stage duration
- DB tuple update rate
- admin endpoint response time for POP summaries
- WebSocket payload size once POP summaries are exposed

## Current Network Shape

The largest fanout contributors are:

- `market_update`
- `world_update`
- `country_update`

At current payload sizes, coalescing is not yet required. Revisit once Month 4 adds POP summaries to subscriptions.

## Current Persistence Shape

The Day 58 soak generated meaningful tuple update volume but no instability. This is acceptable for the current small world.

Do not optimize persistence prematurely. First Month 4 risk is not raw tick cadence; it is accidental row explosion from per-POP monthly writes.

## Practical Limit

Until Month 4 is implemented and re-soaked, treat the current practical envelope as:

```text
40 local fake clients for 1 hour with the current tiny economy model.
```

Everything above that remains unclaimed.
