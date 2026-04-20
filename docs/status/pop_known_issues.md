# POP / Month 4 Known Issues

_Date: 2026-04-27 | End of Month 4 (Day 80)_

This is the Month 4 closeout list of issues with the POP, market, budget, and political-pressure substrate. Active issues are scoped to feed into `month5_playable_slice_targets.md` planning.

---

## Active Issues

### P1 — Market price clamping is still untuned

**Symptom:** Price movement uses simple weekly smoothing inside a bounded clamp. Under heavy shortage or oversupply, prices may pin to the clamp before the rest of the loop reacts; under mild scarcity, prices drift slowly enough that POPs feel unaffected.

**Impact:** The economy can feel "dead" or "pegged." POP cash, militancy, and reform pressure are downstream of price, so miscalibration cascades.

**Fix needed:** Day 75 playtest collected qualitative evidence; Month 5 should run a longer recorded soak and tune elasticity + clamp bounds in the balance CSVs. Inherits from `economy_known_issues.md` E3.

**Priority:** Medium — works, but feel is off.

---

### P2 — Budget categories do not persist across loads ✅ RESOLVED (Week 17 + Day 79 follow-up)

**Symptom:** `CountryState.EducationSpending`, `MilitarySpending`, `AdministrationSpending`, and the per-strata tax rates (`PoorTaxRate`/`MiddleTaxRate`/`RichTaxRate`) lived only on the in-memory `CountryState`, so a server restart reset each country's spending to its scenario default.

**Resolution:** All four pieces are now in place:

- Migration `016_country_budget.sql` added the six columns to the `countries` table.
- `Country` entity carries the matching fields with safe defaults (taxes `-1`, spending `0.5`).
- `WorldStateDatabase` reads/seeds/upserts every column (`LoadWorldAsync`, `SeedWorldAsync`, `UpsertCountriesAsync`).
- `CommandWorldStateMapper.ToSimulationWorld` projects them into `CountryState`; `ToPersistedCountries` projects the post-tick values back onto the `Country` entity each tick.
- `ChangeStrataTaxCommandHandler` and `ChangeSpendingCommandHandler` write to the simulation copy; `AdminCountryInspectorDto` re-exposes all six fields.
- Regression guard: `BudgetPersistenceRoundTripTests` (3 tests) asserts the Country → CountryState → Country round trip preserves both untouched and mutated values.

---

### P3 — Reform pressure is not surfaced ✅ RESOLVED (Day 79 follow-up)

**Symptom:** Reform pressure was computed monthly in `MonthlyPopUpdateStage.RecalculateReformPressure` and stored on `SimulationMetrics.ReformPressureByCountry`, but the metrics object lived only on the in-memory simulation `WorldState` and was never returned from the clock service or projected into any DTO.

**Resolution:** Took option (a). Added `SimulationMetricsSnapshot` and `IWorldClockService.LatestSimulationMetrics`. `PersistentWorldClockService` snapshots `world.Metrics.ReformPressureByCountry` after each tick passes the post-simulation invariant check. `AdminCountryInspectorDto.reform_pressure` now exposes the per-country score, and the Unity dashboard top bar renders a `REFORM` metric with green/amber/red thresholds (≥10 warn, ≥25 crit).

**Caveat:** Snapshot is in-memory only (lost on restart until the next monthly tick recomputes). If long-term history is needed, do option (b) on top: project onto the `Country` entity at end-of-tick.

---

### P4 — Per-strata tax commands are not exposed

**Symptom:** `CountryState` carries `PoorTaxRate`, `MiddleTaxRate`, `RichTaxRate` (default `-1m`, meaning "fall back to flat `TaxRate`"). The only command is `ChangeTaxRate`, which sets the flat value.

**Impact:** Players cannot tax strata differently, even though the simulation already supports it.

**Fix needed:** Add `ChangeStrataTaxRate` command (or extend `ChangeTaxRate` payload). Surface the three rates in inspection. Persist the per-strata rates the same way as P2.

**Priority:** Medium — Month 5 polish.

---

### P5 — Capitalist factory ownership is stubbed

**Symptom:** Factories run, employ craftsmen/clerks, produce output, and accrue `ProfitLastTick` and `CashReserve`, but no profit is distributed to `capitalists` POPs and capitalists do not decide to build/expand factories.

**Impact:** The rich strata's income is mostly cosmetic. Player-driven factory building is the only growth path.

**Fix needed:** Deferred per `vic2_basic_mechanics_mvp.md` ("Deep capitalist AI"). For Month 5, at minimum have factory profits credit capitalist POPs in the owning country pro-rata.

**Priority:** Low — Month 5+ scope.

---

### P6 — Mobility is class-direction-only, not job-driven

**Symptom:** Promotion/demotion candidates are hardcoded class transitions (laborers→craftsmen, clerks→capitalists, etc.) capped at 0.1%/month. There is no check for "are there actually craftsman jobs available in this province?" before promoting.

**Impact:** Promotion can outrun the labor market; new craftsmen may immediately swell the unemployed bucket.

**Fix needed:** Gate promotion on local job openings and minimum cash, per the original Day 65 design ("cash + literacy + job openings -> possible promotion"). Demotion already correlates with poverty/unemployment.

**Priority:** Medium — affects Month 5 unemployment realism.

---

### P7 — Unity client cannot deserialize dictionary JSON

**Symptom:** Unity's `JsonUtility.FromJson` does not support `Dictionary<string, T>` fields. The existing `ProvinceDetailData.market_goods` and the new `AdminFactoryDto.input_goods` / `AdminProvinceInspectorDto.outputs_per_tick` fields cannot be read on the client.

**Impact:** Unity cannot show per-good factory inputs or per-province RGO output breakdowns. Day 79 surfaces names and totals only.

**Fix needed:** Either swap to `Newtonsoft.Json` (`JsonConvert.DeserializeObject`) on the Unity side, or change the server DTOs to use `List<{key, value}>` shapes for goods.

**Priority:** Medium — Month 5 UI quality.

---

### P9 — POP grain is too coarse (one group per type×culture×religion per province)

**Symptom:** A `PopGroup` is created at scenario load as one record per `(province × pop_type × culture × religion × strata)` tuple and is never split. Provinces with one dominant culture/religion typically have only ~6–10 POP rows (one per type), so the Vic2-style "thousands of named POPs per state" granularity is missing.

**Impact:** Province POP lists in the Unity client look like aggregates even though no aggregation is happening. Ideology/issue distributions cannot diverge inside a single (type, culture, religion) bucket. Migration, promotion/demotion, and political pressure are all coarser than Vic2.

**Fix needed:**

- During scenario seeding (and on-demand at runtime), split `PopGroup`s along additional dimensions — minimally `ideology` and `issue_stance`, optionally `job_assignment` (which RGO/factory they work in).
- Keep grouping cheap: cap at N groups per province; merge groups that converge to identical attributes.
- Make sure invariants and persistence handle a variable number of POP groups per province.
- Update `ProvincePopGroupMapper` to retain the new dimensions so the Unity client can slice/filter/group by them.

**Priority:** Medium — unblocks much richer political and population UI; not required for current playable slice.

**Reference:** Discussed during Day 79 Unity inspection pass when a player asked why province POPs looked aggregated. Option-3 client-side filtering of the existing grain is the short-term workaround.

---

### P8 — Province population is the sum of POPs, but seeded population may drift

**Symptom:** `Province.Population` is set at seed time and recomputed/asserted by invariants based on POP totals. Small POP transfers (Day 77 mobility) round individual transfers; over many months the recomputed total can drift slightly from the original seeded value before invariants correct it.

**Impact:** Cosmetic: province totals can disagree with the sum of POP sizes for one tick. Invariants catch and reject inconsistent loads.

**Fix needed:** Recompute `Province.Population` deterministically each tick rather than treating it as a stored field.

**Priority:** Low — invariants catch the worst cases.

---

## Won't Fix (Month 4)

- **World market** — explicitly deferred per `vic2_basic_mechanics_mvp.md`.
- **Real reforms with rollout effects** — deferred until political pressure has somewhere to go.
- **Civic trust, chains of command, disease, climate, newspapers, LLM diplomacy** — deferred per Month 4 scope.
- **Per-province markets** — single national market remains the design choice.

---

## Inherited from earlier closeouts

- `economy_known_issues.md` E1 (synthetic pops) — superseded by Month 4 persistent POPs.
- `economy_known_issues.md` E2 (negative stock floor) — addressed during Week 14.
- `economy_known_issues.md` E5 (factory inputs not consumed) — addressed by `FactoryProductionStage` in Week 14.
- `economy_known_issues.md` E3 (price tuning) — folded into P1 above.
- `economy_known_issues.md` E7 (in-memory market history) — still open; tracked there.
