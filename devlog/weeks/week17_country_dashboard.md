# Week 17 — Country Dashboard and Playable Inspection

_Days 81-85, Month 5._

Theme: "Unity becomes the main way to understand your country."

## Result

Week 17 passed.

A logged-in player can now answer the country-state questions from `month5_8_weeks.md` § Week 17 entirely through `/api/world` endpoints — no admin scope, no CLI poking required.

## What Was Built

### Day 81 — Persist budget spending categories

Closes `pop_known_issues.md` P2.

- Migration `016_country_budget.sql` adds `poor_tax_rate`, `middle_tax_rate`, `rich_tax_rate`, `education_spending`, `military_spending`, `administration_spending` to `countries`.
- `Country` domain entity carries the new fields end-to-end.
- `CountryDef` (scenario JSON) accepts optional per-strata tax overrides and explicit spending levels.
- `WorldStateDatabase.LoadWorldAsync`, `SeedWorldAsync`, and `UpsertCountriesAsync` round-trip the new columns.
- `CommandWorldStateMapper.ToSimulationWorld` and `ToPersistedCountries` keep `Country` ↔ `CountryState` in sync, so simulation state survives restarts.
- `WorldSnapshotService.CountrySnapshotDto` carries the fields so file snapshots stay coherent.

### Day 82 — Promote inspection to /api/world

The Day 79 admin inspector data is now available without admin scope.

- New DTOs in `Dtos.cs`: `CountryInspectionDto`, `CountryPopTypeDto`, `ProvinceInspectionDto`, `ProvinceFactoryDto`.
- `IWorldQueryService` gains `GetCountryInspectionAsync` / `GetProvinceInspectionAsync`.
- `WorldController` exposes `GET /api/world/countries/{id}/inspect` and `GET /api/world/provinces/{id}/inspect`.
- Unity client switches its inspector calls from `/api/admin/...` to `/api/world/...`.
- The admin equivalents are kept; admin DTOs remain free to grow without changing the player contract.

### Day 83 — Top shortages and unmet-needs warnings

- `CountryInspectionDto` carries a `market_warnings: List<MarketWarningDto>` populated from market supply/demand.
- `WorldQueryService.ComputeMarketWarnings` flags goods with `fulfillment < 0.85` (severity tiers `warn` / `high` / `critical`), top five only, ordered by worst fulfillment.
- Unity surfaces warnings in the budget panel.

### Day 84 — Province list sort/filter

- `GET /api/world/provinces` now accepts `owner=<countryId>`, `sort=name|population|owner|rgo`, `order=asc|desc`.
- `IWorldQueryService.ListProvincesAsync(string?, string?, string?, ...)` overload performs the filter/sort.
- Unity `ProvinceListUI` adds optional sort/filter buttons (toggle-on-second-click direction; "mine" filter uses `PlayerSession.ControlledCountryId`).

### Day 85 — Tests + this doc

- `WorldQueryServiceTests` (5 new tests, 64/64 passing) covers:
  - filter-by-owner and population-desc sort
  - default name ascending sort
  - country inspection aggregates pop-type breakdown ordered by size
  - country inspection exposes new budget categories from persisted state
  - market warnings produced for under-fulfilled goods
  - province inspection surfaces only the factories assigned to that province

## Validation

```bash
dotnet build server/src/VictoriaLike.Server/VictoriaLike.Server.csproj --no-restore
dotnet test server/tests/VictoriaLike.Core.Tests/VictoriaLike.Core.Tests.csproj --no-restore
```

Result: build succeeds, 64/64 tests pass.

After running migration 016, the new columns are present and writable; restart no longer reverts spending levels to scenario defaults.

## Known Limits / Followups

- **No budget commands yet.** Persistence is in place, but Week 18's job is to expose `ChangeBudgetSpending` / strata tax commands so the player can move the values through Unity.
- **Reform pressure (`P3`) is still not surfaced.** Day 79's gap remains; Week 18 or Week 21 (explanation tools) will need to thread `SimulationMetrics` through `IWorldClockService` or persist the per-country score onto `Country`.
- **Single national market only.** Market warnings are computed from the first market; a multi-market scenario will need a per-country market lookup.
- **Unity sort/filter buttons need scene wiring.** `ProvinceListUI` exposes the new SerializeFields, but the existing Day-35 `VictoriaSceneSetup` doesn't author `ProvinceListUI`. Either wire the buttons by hand in the saved scene or extend the setup script.

## Demo Path

```bash
TOKEN=$(curl -s -X POST http://localhost:5001/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{"username":"england-player","password":"eng123"}' | jq -r .token)

# Country inspection (no admin scope)
COUNTRY=$(curl -s http://localhost:5001/api/world/countries | jq -r '.[0].id')
curl -s http://localhost:5001/api/world/countries/$COUNTRY/inspect | jq '{name, treasury, education_spending, average_militancy, market_warnings}'

# Province inspection
PROVINCE=$(curl -s "http://localhost:5001/api/world/provinces?owner=$COUNTRY&sort=population&order=desc" | jq -r '.[0].id')
curl -s http://localhost:5001/api/world/provinces/$PROVINCE/inspect | jq '{name, rgo_type, factories: (.factories | length), pop_groups: (.pop_groups | length)}'
```

In Unity (after Day 79 setup): the country panel shows POPs/budget/warnings; clicking a province shows RGO/POPs/factories. With the optional sort/filter buttons wired, the province list responds to `sort_by_name` / `sort_by_population` / `filter_mine` / `clear_filter`.
