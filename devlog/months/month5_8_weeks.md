Yes. Here’s the clean **Month 5–7 week-by-week plan**, assuming 4 weeks per month and continuing the day numbering after Month 4.

Big sequencing:

```text
Month 5: make the country loop playable through Unity.
Month 6: harden, explain, scale-test, and prepare deeper systems.
Month 7: introduce the first real deeper economy upgrade, carefully.
```

This preserves the MMO-first principle from the original plan: server-authoritative world, validated commands, durable persistence, subscriptions/deltas, reconnect, and admin/explanation tooling rather than client-side “fake gameplay.” The original early plan explicitly treated Unity as a consumer of server truth and emphasized command validation, snapshots, subscriptions, durable state, and multi-client coherence. 

Command-pipeline note:
Work in GitHub issues `#2` through `#8` should be treated as upstream guidance for the Unity-facing parts of this plan. When there is any ambiguity about command UX, reconnect behavior, event vs durable state, or command response shape, follow [docs/unity-frontend-and-command-ui.md](/Users/gideonpotok/repos/victoria_ii/docs/unity-frontend-and-command-ui.md) first. Use [docs/unity-command-pipeline-research-plan.md](/Users/gideonpotok/repos/victoria_ii/docs/unity-command-pipeline-research-plan.md) as the rationale and research-history doc, not the primary implementation checklist.

---

# Month 5 — Playable Unity Country Loop

**Theme:** Make the Month 4 Vic substrate playable through Unity.

**Month 5 success test:**
A player can open Unity, select a country, inspect POPs/economy/budget, change policy, build production, move armies, fight a simple war, and play for 30 minutes without admin-only tools.

Month 4 already has the right foundation: explicit POP strata, needs baskets, national-market-only pricing, artisans, employment, literacy/militancy/consciousness, and deliberately deferred deeper politics/world-market systems. 

## Week 17 — Country dashboard and playable inspection

**Goal:** Unity becomes the main way to understand your country.

Build:

```text
- country dashboard v1
- treasury, tax rates, spending sliders/readouts
- national market summary
- POP summary by strata/type
- unemployment summary
- literacy/militancy/consciousness summary
- top shortages and unmet needs
- province list with sort/filter
- selected province detail panel
```

Server/API work:

```text
- country summary DTO
- province economy DTO
- POP summary DTO
- budget summary DTO
- market warning DTO
```

Unity work:

```text
- dashboard screen
- country header
- budget panel
- market panel
- POP panel
- province drilldown
```

Tests:

```text
- country dashboard API maps correct treasury/budget/POP values
- province detail matches server state
- Unity does not need admin endpoints for normal inspection
```

Pipeline guidance:

```text
- This week should adopt the frontend doc's event vs state vs inspection-fetch split.
- Country and province panels should be rendered from authoritative DTOs, not from transient command-result events.
- Focused detail panels should prefer explicit inspection fetches over overloading the WebSocket with every deep field.
- Refresh and reconnect should be expected to rebuild this screen correctly from authoritative state alone.
```

**Deliverable:**
You can select a country and understand its economy/POPs in Unity.

**Do not add:** utility optimizer, civic trust, newspapers, disease, climate. This is UI over the Month 4 substrate.

---

## Week 18 — Budget, construction, and economic action loop

**Goal:** The player can change the economy and see consequences.

Build:

```text
- budget controls wired through validated server commands
- poor/middle/rich tax controls
- education/military/admin spending controls
- construction/building UI polish
- expand/build factory command polish
- RGO/factory/artisan status views
- queue status and completion feedback
```

Server work:

```text
- validate budget commands
- validate construction commands
- expose spending effect summaries
- expose construction queue in country/province views
```

Unity work:

```text
- budget slider UI with predicted/observed effects
- construction panel
- building cards
- “why unavailable?” messages
- completion notifications
```

Tests:

```text
- tax/spending commands are authorized
- budget changes affect treasury and POPs
- construction changes production capacity after completion
- invalid construction is rejected cleanly
```

Pipeline guidance:

```text
- This week is directly shaped by issues #2 through #8.
- Tax and spending controls should use latest-desired-value scheduling semantics, not direct button-to-command wiring.
- Construction should stay on the discrete-action path: immediate submit, explicit pending state, no silent auto-retry unless idempotency is clear.
- "Why unavailable?" messaging should come from structured command responses or future query/preview support, not ad hoc client guesses.
- Predicted effects are advisory UI only; committed values must come back from authoritative state.
- Cooldowns, retry metadata, and rejections should be normal UI outcomes, not Unity Console error noise.
```

**Deliverable:**
The player can change taxes/spending, build/expand production, and observe employment, prices, and POP conditions react.

---

## Week 19 — Event feed and player-facing economy feedback

**Goal:** The game tells the player what changed.

Build event feed v1:

```text
Budget events:
- taxes changed
- spending changed
- treasury warning

POP events:
- unmet life needs rising
- unemployment rising
- militancy warning
- literacy improvement

Market events:
- shortage
- price spike
- input bottleneck
- artisan production switch

Construction events:
- building queued
- building completed
- factory input shortage

Province events:
- province economy changed materially
- POP hardship increased/decreased
```

Unity work:

```text
- event feed panel
- event severity levels
- click event → relevant country/province/market panel
- monthly digest summary
```

Server work:

```text
- event generation stage
- event persistence or rolling event log
- event subscription topic
- event DTOs
```

Tests:

```text
- event generated for major price/need/unemployment changes
- events are not spammy
- event feed survives reconnect or reload
```

Pipeline guidance:

```text
- Event feed items are transient UX unless explicitly backed by a durable event log.
- The feed must not become the only source of truth for construction status, budget state, or province state.
- Clicking an event should navigate to a panel that can rebuild itself from authoritative DTOs or focused fetches.
- Reconnect should preserve durable summaries and optionally recent history, but may legitimately drop one-shot visual notifications.
```

**Deliverable:**
The player no longer needs to stare at tables to notice the country is going sideways. The game says, “Hey, grain is expensive, London craftsmen are unhappy, and your steel mill is short on coal.”

---

## Week 20 — Basic armies, movement, battle v1, war/peace v1

**Goal:** The first playable country slice includes military/diplomatic action.

Build:

```text
- army stack model
- army location
- army movement command
- movement ETA
- battle v1
- casualties/morale/simple outcome
- war declaration command
- peace command
- war state persistence
- simple diplomacy panel
```

Keep it brutally simple:

```text
- no front system
- no supply network
- no mobilization depth
- no naval logistics
- no delayed ceasefires yet
- no deep diplomacy
```

Unity work:

```text
- army stack display
- select army
- move army
- battle indicator
- war status panel
- peace button
```

Tests:

```text
- army movement persists
- invalid movement rejected
- battle resolves deterministically
- war/peace state survives restart
- two countries cannot have contradictory war state
```

Pipeline guidance:

```text
- Army movement, war declaration, and peace are discrete commands, not scheduler-managed adjustable fields.
- These commands should use the same structured outcome handling introduced for construction: accepted, rejected, failed, and player-safe messages.
- Immediate client feedback may show selection, pressed state, pending badge, or ETA request, but authoritative movement and war state must come from the server pipeline.
- If any action benefits from preflight validation, prefer explicit query/preview support over hidden client-only gating.
```

**Month 5 final playtest:**

```text
1. Start server.
2. Open Unity.
3. Select country.
4. Inspect POPs/economy/budget.
5. Change taxes/spending.
6. Queue/complete building.
7. Watch employment/prices/POP conditions respond.
8. Move army.
9. Start and resolve simple war.
10. Play 30 minutes without admin tools.
```

**Month 5 review artifacts:**

```text
month5_review.md
playable_slice_known_issues.md
unity_country_loop_report.md
war_v1_known_issues.md
month6_targets.md
```

---

# Month 6 — Hardening, Legibility, Medium Scenario, Utility Shadow

**Theme:** Make the Month 5 playable loop explainable, repeatable, multiplayer-safe, and ready for deeper systems.

Month 6 should not unleash the full utility economy. It should run it in shadow mode. The original MMO plan repeatedly emphasizes tooling, partial updates, reconnects, fake clients, and “why did this happen?” visibility; that spirit stays here. 

## Week 21 — Explanation tools and playtest repair

**Goal:** Every major player-visible change has a “why.”

Build explanation endpoints:

```text
/api/explain/good/{goodId}
/api/explain/pop/{popId}/needs
/api/explain/province/{provinceId}/employment
/api/explain/country/{countryId}/budget
/api/explain/war/{warId}
/api/explain/battle/{battleId}
```

Examples:

```text
Grain rose because:
- demand exceeded supply
- stockpile was low
- price pressure was high
- weekly clamp limited movement

POP militancy rose because:
- life needs fell
- unemployment rose
- taxes reduced take-home pay
```

Unity work:

```text
- “why?” buttons on market, POP, budget, province, battle panels
- tooltip explanations
- monthly “what changed?” summary
```

Tests:

```text
- explanation output matches market/pop/budget data
- no null explanation for core gameplay states
- explanation endpoints are not too slow
```

Pipeline guidance:

```text
- Keep command-outcome explanations separate from world-state explanations, but make them feel consistent in the UI.
- Rejected-command messages should reuse the same stable rejection codes and player-facing phrasing established by the command-pipeline work.
- "Why did this happen?" views should assume the source of truth is authoritative state plus explain endpoints, not remembered transient UI state.
```

**Deliverable:**
You can answer “why did this happen?” from Unity, not just from admin.

---

## Week 22 — Medium scenario and bigger data pass

**Goal:** Move beyond tiny-2country without pretending to be full-world.

Build a medium scenario:

```text
- 6–10 countries
- 50–100 provinces
- 1–3 national/regional markets, depending on how brave you feel
- enough RGOs/factories/artisans to create bottlenecks
- at least one plausible land war pair
- at least one overseas/separated region if possible
```

Server work:

```text
- scenario loader performance check
- POP seeding helpers
- factory/RGO content validation
- market assignment validation
- snapshot size measurements
```

Unity work:

```text
- map navigation improvement
- province search/filter
- country selection polish
- subscription sanity under more provinces
```

Tests:

```text
- medium scenario loads
- 12-month simulation test passes
- invariants pass
- startup/load/save duration measured
```

Pipeline guidance:

```text
- Subscription sanity under a larger scenario should continue to follow the doc's rule: WebSocket for live authoritative updates, HTTP for focused inspections and recovery fetches.
- Do not respond to scale pain by pushing every inspectable detail continuously.
- Snapshot size and panel cost should be evaluated with the assumption that deep screens may fetch focused DTOs on demand.
```

**Deliverable:**
A medium scenario runs one simulated year and exposes real UI/navigation/performance pain.

---

## Week 23 — Multiplayer playtest and reconnect hardening

**Goal:** Get back to the MMO premise.

Build:

```text
- 2-player country-control playtest
- concurrent command handling checks
- reconnect/subscription restoration for Unity
- conflict handling for simultaneous construction/budget/army commands
- command result UI
- better rejected-command messages
```

Test script:

```text
Player A controls Britain.
Player B controls France.
Both inspect markets and POPs.
Both change budgets.
Both build production.
One starts war or diplomatic action.
Both disconnect/reconnect.
World remains coherent.
```

Server work:

```text
- command audit cleanup
- actor/country permission edge cases
- duplicate/retry behavior for Month 5 commands
- war command authorization
```

Tests:

```text
- wrong-country commands rejected
- duplicate commands idempotent
- reconnect restores country/province/market subscriptions
- concurrent commands resolve predictably
```

Pipeline guidance:

```text
- This week is the second major consumer of issues #2 through #8 after Week 18.
- Reconnect behavior should clear stale client scheduler timers and rebuild pending UI conservatively from authoritative state and any durable command history.
- Duplicate commands should be treated idempotently where supported; retry behavior should be explicit and bounded.
- Simultaneous construction, budget, and army actions should all surface structured command outcomes rather than generic transport errors.
- Command-result UI should distinguish rejected game-rule outcomes from actual network or auth failures.
```

**Deliverable:**
Two players can meaningfully inhabit the same world without the sim getting weird or the UI losing its mind.

---

## Week 24 — Utility shadow v0 and POP-heavy soak

**Goal:** Prepare the deeper economy without letting it control the game yet.

Build `UtilityShadowEvaluator`:

```text
For selected POPs, compute:
- current utility
- hypothetical preferred bundle at current prices
- top desired purchases
- top desired sales, if production inventory exists
- cash reserve pressure
- goods that look overpriced/underpriced to the POP
```

Important: **shadow only.** The real purchasing system still runs Month 4/5 rules.

Admin/Unity readout:

```text
Current behavior:
- bought grain/clothes/tools by needs rules

Utility shadow says:
- would prefer more cash reserve
- would buy fewer luxury goods at current price
- would strongly bid for grain
```

Scale/soak:

```text
- 40 fake clients again
- 2–4 Unity clients if possible
- medium scenario
- 60–90 minute run
- budget/construction/army/war commands
- measure tick drift, memory, DB writes, WebSocket payloads
```

Pipeline guidance:

```text
- Include command-UX traffic in the soak: repeated budget edits, construction spam, reconnect cycles, and multi-client command churn.
- Measure whether scheduler coalescing materially reduces command volume and WebSocket/UI noise.
- Watch for stale pending UI after reconnect, duplicate retries, and oversized payloads caused by pushing data that should remain fetch-on-demand.
```

The scale watch is important because the prior MMO plan relied on subscriptions and partial replication, not brute-force whole-world sync. 

**Deliverables:**

```text
utility_shadow_report.md
month6_soak_report.md
unity_bandwidth_report.md
pop_persistence_report.md
month7_go_no_go.md
```

**Month 6 exit gate:**

```text
- Can 2 players play 60 minutes?
- Can medium scenario run 12 simulated months?
- Can we explain core economy changes?
- Are POP-heavy ticks stable?
- Is utility shadow sane enough to control one narrow slice next month?
```

---

# Month 7 — First Deeper Economy Upgrade

**Theme:** Let the utility model control a narrow part of the economy, while preserving playability.

This is the first month where the fancy POP utility planning starts touching live simulation. Still: no stocks yet, no scrip yet, no newspapers, no disease, no climate. Tiny spoonfuls of goblin math, not the whole cauldron.

The stability principles from the POP/MMO notes matter here: use diminishing returns, partial adjustment, clamps, bounded randomness, localized shocks, shallow formulas, and strong explainability. 

## Week 25 — Utility-driven private purchasing v1

**Goal:** Replace fixed POP purchasing for one narrow slice.

Scope options:

Best first scope:

```text
poor POP private purchasing only
life + everyday goods only
one country or small scenario first
```

Do not start with all POPs/all goods/all countries.

Build:

```text
- PopUtilityParams by strata/type
- utility terms for life/everyday/luxury reserves
- utility term for cash reserve
- affordability constraints
- supply constraints
- deterministic greedy optimizer
- purchase action log
```

Rules:

```text
- POP can buy private consumption goods
- POP can keep cash
- POP cannot buy production inventory
- no stocks yet
- no credit/debt yet
- no sellable private goods yet
```

Tests:

```text
- poor POP buys life goods first under scarcity
- POP keeps some cash when prices are bad
- luxury does not dominate poor consumption
- output is deterministic
- no negative cash
- no negative stockpile
```

**Deliverable:**
One controlled POP segment purchases by utility rather than fixed needs.

---

## Week 26 — Utility purchasing expansion and player-facing explanations

**Goal:** Expand to all POP strata and make it understandable.

Build:

```text
- poor/middle/rich utility profiles
- type-specific modifiers if needed
- luxury reserve term
- cash/bullion reserve term
- utility result explanation
- per-POP purchase summary
```

Unity/explanation:

```text
POP bought grain because:
- high marginal life utility
- price affordable
- local supply available

POP skipped luxury furniture because:
- marginal utility lower than cash reserve
- price high
```

Tests:

```text
- rich POPs buy more luxury at same price/income
- poor POPs preserve life/everyday bias
- tax changes alter purchasing through cash
- price spikes change bundle composition
```

Pipeline guidance:

```text
- Any new player-facing controls or explanations added here should still follow the established event/state/query/command split.
- The explanation UI should integrate with the same authoritative refresh model used by earlier dashboard work.
```

**Deliverable:**
Utility purchasing can run for all POP types in tiny/medium scenario, with explanations.

---

## Week 27 — Production inventory and ask behavior v1

**Goal:** Start the real bid/ask bridge, but only for sellable production inventory.

Build:

```text
- POP/producer production inventory state
- inventory utility term
- sell decision:
  keep inventory vs sell for cash
- ask threshold calculation
- market signal aggregation from sell intent
```

Initial constraints:

```text
- production inventory can be sold
- production inventory cannot be bought as production inventory
- private goods cannot be sold yet
- no stock assets yet
```

This preserves the conceptual model we liked: private reserves are household goods; production inventory is a sellable buffer; cash is liquid; later stocks become another asset object.

Market integration:

```text
- current price still drives actual transactions
- ask pressure feeds market explanation
- optional: price pressure begins using utility-derived desired demand/supply
```

Tests:

```text
- POP sells inventory when price is high enough
- POP keeps inventory when marginal buffer utility is higher
- inventory sale increases cash
- sale affects market supply
- no inventory goes negative
```

Pipeline guidance:

```text
- If this week introduces new sell/ask commands or player-controlled toggles, classify them early as either discrete actions or latest-desired-value controls and reuse the established command path rather than inventing a third interaction model.
```

**Deliverable:**
The first real utility-derived asks exist.

---

## Week 28 — Regional trade / market latency spike OR utility market clearing v1

This week has two possible paths. I’d choose based on pain.

### Option A: Regional trade / market latency spike

Choose this if the economy feels too magically national/global.

Build:

```text
- simple regional market assignment
- inter-market shipment delay
- market reporting delay
- price smoothing between connected markets
- trade explanation: why goods did/did not arrive
```

This lines up with the MMO temporal model: the world has one clock, but information/order/economic effects propagate through latency rather than instantly. 

Deliverable:

```text
A shortage in one region does not instantly rebalance everywhere.
```

### Option B: Utility market clearing v1

Choose this if national market behavior still feels too artificial.

Build:

```text
- aggregate POP utility-derived desired purchases
- aggregate production inventory asks
- simple clearing approximation
- unmet utility-demand metrics
- market price pressure based partly on utility bids/asks
```

Deliverable:

```text
Prices are increasingly driven by what POPs would actually choose, not just basket quotas.
```

My recommendation: **Option B if Month 7 utility is stable; Option A if MMO/trade feel is the bigger missing piece.**

Pipeline guidance:

```text
- Any new market-facing UI produced here should still respect the rule that durable market truth must be reconstructable after refresh or reconnect.
- Use transient feed items or pulses for "market changed" style feedback, but keep inspectable market state fetchable and authoritative.
```

---

# Month 7 exit criteria

By the end of Month 7:

```text
- Utility purchasing controls at least one meaningful live economy path.
- POP purchase behavior is explainable.
- Production inventory sales exist or are close.
- Market demand is less fake than Month 4/5.
- Medium scenario still runs.
- 2-player playtest still works.
- Tick performance remains acceptable.
```

Write:

```text
month7_review.md
utility_purchasing_report.md
utility_balance_notes.md
market_behavior_known_issues.md
month8_targets.md
```

---

# What Month 7 still defers

Hold the line:

```text
- stocks
- dividend escrow
- scrip dividends
- credit/debt
- civic trust
- newspapers
- disease
- climate
- law rollout depth
- LLM diplomacy
- sophisticated logistics
- full world market
```

Stocks/scrip are probably **Month 9**, after utility purchasing and inventory asks are real. Newspapers/civic trust/disease/climate are later still. The shiny beasts must remain in the paddock.

---

# The compact roadmap

```text
Month 5 / Week 17:
Country dashboard + POP/economy inspection.

Month 5 / Week 18:
Budget controls + construction loop.

Month 5 / Week 19:
Event feed + economy feedback.

Month 5 / Week 20:
Army movement + battle v1 + war/peace v1 + 30-minute playtest.

Month 6 / Week 21:
Explanation tools + playtest repair.

Month 6 / Week 22:
Medium scenario + bigger data pass.

Month 6 / Week 23:
Two-player country-control playtest + reconnect hardening.

Month 6 / Week 24:
Utility shadow v0 + POP-heavy soak.

Month 7 / Week 25:
Utility-driven private purchasing v1 for narrow scope.

Month 7 / Week 26:
Expand utility purchasing + explanations.

Month 7 / Week 27:
Production inventory asks/sales v1.

Month 7 / Week 28:
Either regional trade/latency spike or utility market clearing v1.
```

My strongest take: **Month 5 is about playability, Month 6 is about legibility and robustness, Month 7 is about carefully letting the smarter economy touch live gameplay.**
