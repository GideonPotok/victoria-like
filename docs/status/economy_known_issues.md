# Economy Known Issues

_Date: 2026-04-25 | End of Month 2 (Day 40)_

---

## Active Issues

### E1 — Synthetic pop counts are not persisted

**Symptom:** Province population is synthesized in `CommandWorldStateMapper` on every tick rather than stored as a first-class domain object. Population cannot grow, shrink, or migrate.

**Impact:** Pop needs demand is static. Treasury income doesn't reflect demographic change. No path to pop-driven unrest, migration, or growth mechanics.

**Fix needed:** Add a `pops` table. Persist workforce counts per province. Let `BudgetStage` read persisted pop counts instead of deriving them.

**Priority:** Medium — doesn't break current simulation, but caps economic depth.

---

### E2 — National market has no stock floor

**Symptom:** Market stock can drop to zero or below if pop consumption exceeds production. `MarketPricingStage` does not enforce a non-negative stock invariant.

**Impact:** Potential negative stock values; price clamping prevents infinite prices but the underlying stock number is incoherent.

**Fix needed:** Clamp stock to `Math.Max(0, stock - consumed)` in `PopNeedsStage` and `NationalDistributionStage`. Add invariant check in `StateInvariantChecker` (Day 49).

**Priority:** High — produces incorrect economic state.

---

### E3 — Price movement formula is not tuned

**Symptom:** Price clamp bounds and supply/demand elasticity coefficient are hardcoded constants derived from no empirical testing. Market prices can either barely move or swing to their clamps immediately.

**Impact:** Economy may feel "dead" (prices never respond) or "chaotic" (prices peg to max/min immediately).

**Fix needed:** Run 30-minute simulation, record price histories, adjust elasticity coefficient and clamp bounds in `balance/` CSVs. Aim for ~5–20 tick price oscillation period for the smallest shocks.

**Priority:** Medium — simulation works but feels miscalibrated.

---

### E4 — Construction options still need full Victoria-like depth

**Symptom:** Direct `farm`/`mine` construction has been removed from the player-facing template list in favor of `railroad`, `tools_factory`, and `cement_factory`, but construction is still a thin first pass.

**Impact:** The player can no longer solve food shortages by spamming farms in every province, which better matches Victoria-style RGOs. The remaining gap is depth: railroads and factories need stronger tuning, UI explanation, and more factory chains.

**Fix needed:** Expand factory templates, add explicit railroad/infrastructure balance, and keep RGO output tied to province resource type, labor, infrastructure, and tech rather than direct construction.

**Priority:** Low — doesn't break existing paths; expands economic depth.

---

### E5 — Factory input sourcing is shallow

**Symptom:** `FactoryProductionStage` consumes inputs from the owning country's stockpile, but there is not yet a robust market-purchase/import step that fills those stockpiles from domestic, sphere, or world supply before production.

**Impact:** Factories are no longer pure province-output sources, but industrial success is still too dependent on preexisting country stockpiles and not enough on Victoria-style market access.

**Fix needed:** Add a deterministic industrial input-buy phase before `FactoryProductionStage`, ordered by country/rank/market access once those systems exist.

**Priority:** High — breaks economic incentives as soon as multi-input buildings are used.

---

### E6 — Treasury can go negative without consequence

**Symptom:** `BudgetStage` deducts building costs and wages from treasury but does not enforce a zero floor or pause construction when bankrupt.

**Impact:** Countries can queue infinite buildings with no treasury penalty. Bankruptcy has no gameplay effect.

**Fix needed:** `QueueBuildingCommandHandler` should reject the command if `treasury < buildCost`. `BudgetStage` should pause construction for countries at zero treasury.

**Priority:** High — undermines resource management as a mechanic.

---

### E7 — Market history is in-memory only

**Symptom:** `MarketHistoryService` keeps a 20-tick circular buffer in RAM. Server restart discards all price history.

**Impact:** Admin market inspector shows no history after restart. Price trend charts are not possible across sessions.

**Fix needed:** Either persist price snapshots to DB every N ticks or fold market history into the world snapshot (Month 3 Day 46).

**Priority:** Low — admin usability issue, not a simulation correctness issue.

---

## Won't Fix (Month 3)

- **Multiple national markets** — one market per world is an explicit design decision until scale demands otherwise.
- **Trade between countries** — out of scope until Month 4+.
- **Pop class differentiation** (farmers vs. craftsmen vs. laborers affecting wages/demand) — deferred until pop system is first-class.
