# Day 79 Unity Inspection Pass

Day 79 deliverable: Unity surfaces the Month 4 substrate without database spelunking.

## Implemented

Server-side admin inspector DTOs are extended so the Month 4 mechanics are addressable from a Unity client over plain HTTP:

- `AdminProvinceInspectorDto` now includes a `factories` list (id, type, level, output good, output per tick, employed craftsmen/clerks, cash reserve, profit last tick).
- `AdminCountryInspectorDto` now includes:
  - `pop_type_breakdown` — POP groups aggregated by type with size, employed, unemployed, and weighted literacy/militancy/consciousness/life-needs averages
  - country-level weighted averages: `average_literacy`, `average_militancy`, `average_consciousness`
  - `unemployment_share`

Unity client:

- `WorldApiClient` exposes `GetCountryInspectorAsync` and `GetProvinceInspectorAsync` against `/api/admin/...`.
- `WorldUIManager` shows a national POP summary (population, literacy, militancy, consciousness, unemployment, per-type breakdown) and a budget summary (treasury, tax rate, province count). A refresh button re-pulls the inspector on demand.
- `ProvinceDetailUI` shows the RGO type, workforce, needs fulfillment, the POP-group breakdown, and the factory list for the selected province.

## Inspection Coverage

After Day 79 the Unity client can answer the demo-script questions without admin-only tools:

- Inspect country population and POPs by type.
- Inspect province POP breakdown (size, type, strata, militancy, literacy, employment).
- Inspect RGO type and per-tick output.
- Inspect factories sitting in a province (type, output good, employment, profit).
- Inspect tax rate and treasury.
- Watch market prices live via the existing market WebSocket subscription.

## Known Gaps Surfaced

The inspection pass surfaces gaps that belong in `pop_known_issues.md` rather than Month 4 feature work:

- Per-strata budget categories (`EducationSpending`, `MilitarySpending`, `AdministrationSpending`) live on `CountryState` in the simulation but are not persisted in the DB-loaded `WorldStateSnapshot`, so admin endpoints cannot expose live values.
- Country reform pressure lives on the in-memory simulation `WorldState.Metrics.ReformPressureByCountry` and is not surfaced through the admin clock service or persisted snapshot.
- `Dictionary<string, T>` JSON fields (input goods, market goods) are not deserialized by Unity's `JsonUtility`. The Unity client currently shows scalar/list fields only.

These are visibility gaps, not simulation correctness issues, and Month 5 UI work will need to address the persistence and serialization split.

## Validation

```bash
dotnet build server/src/VictoriaLike.Server/VictoriaLike.Server.csproj --no-restore
dotnet test server/tests/VictoriaLike.Core.Tests/VictoriaLike.Core.Tests.csproj --no-restore
```

Result: build succeeds, 59/59 tests pass.

Manual verification path (server + Unity):

1. Run the server.
2. Open Unity scene, log in.
3. Confirm the country panel shows national population, POP-type breakdown, literacy/militancy/consciousness/unemployment, and treasury/tax.
4. Open a province and confirm RGO type, POPs, and factories are listed.

## Day 79 Result

Month 4 mechanics are inspectable from Unity for a single playable country. The remaining inspection gaps (budget categories, reform pressure, dictionary fields) are deferred to Month 5 UI/persistence work and recorded in the Month 4 closeout docs.
