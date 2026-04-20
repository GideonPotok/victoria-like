using Microsoft.Extensions.Diagnostics.HealthChecks;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;
using Xunit;

namespace VictoriaLike.Core.Tests;

/// Day 85 (Week 17): exercise the new /api/world inspection paths against a
/// hand-rolled fake DB. The Unity client routes here for normal inspection,
/// so regressions in this service break the country dashboard.
public sealed class WorldQueryServiceTests
{
    [Fact]
    public async Task ListProvincesAsync_FiltersByOwnerAndSortsByPopulationDescending()
    {
        var (db, eng, _) = BuildFakeWorld();
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var result = await svc.ListProvincesAsync(eng.Id.Value.ToString(), "population", "desc");

        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Equal("England", p.OwnerName));
        Assert.True(result[0].Population >= result[1].Population);
    }

    [Fact]
    public async Task ListProvincesAsync_SortsByNameAscByDefault()
    {
        var (db, _, _) = BuildFakeWorld();
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var result = await svc.ListProvincesAsync(null, null, null);

        Assert.Equal(3, result.Count);
        var sorted = result.Select(p => p.Name).ToList();
        Assert.Equal(sorted.OrderBy(s => s).ToList(), sorted);
    }

    [Fact]
    public async Task GetCountryInspectionAsync_AggregatesPopsAndExposesBudgetCategories()
    {
        var (db, eng, _) = BuildFakeWorld();
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var inspection = await svc.GetCountryInspectionAsync(eng.Id.Value.ToString());

        Assert.NotNull(inspection);
        Assert.Equal("England", inspection!.Name);
        Assert.Equal(0.7m, inspection.EducationSpending);
        Assert.Equal(0.4m, inspection.MilitarySpending);
        Assert.Equal(2, inspection.ProvinceCount);
        Assert.Equal(15_000, inspection.Population);
        Assert.NotEmpty(inspection.PopTypeBreakdown);
        // POP-type breakdown should be ordered by size desc.
        Assert.Equal(
            inspection.PopTypeBreakdown.OrderByDescending(b => b.Size).Select(b => b.PopType).ToList(),
            inspection.PopTypeBreakdown.Select(b => b.PopType).ToList());
    }

    [Fact]
    public async Task GetCountryInspectionAsync_ProducesMarketWarningsForUnderfulfilledGoods()
    {
        var (db, eng, _) = BuildFakeWorld();
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var inspection = await svc.GetCountryInspectionAsync(eng.Id.Value.ToString());

        Assert.NotNull(inspection);
        Assert.Contains(inspection!.MarketWarnings, w => w.GoodId == "grain");
        var grain = inspection.MarketWarnings.First(w => w.GoodId == "grain");
        Assert.True(grain.FulfillmentRate < 0.85m);
        Assert.False(string.IsNullOrEmpty(grain.Severity));
    }

    [Fact]
    public async Task GetProvinceInspectionAsync_IncludesFactoriesForOwningProvince()
    {
        var (db, _, london) = BuildFakeWorld();
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var inspection = await svc.GetProvinceInspectionAsync(london.Id.Value.ToString());

        Assert.NotNull(inspection);
        Assert.Equal("London", inspection!.Name);
        Assert.Single(inspection.Factories);
        Assert.Equal("steel_mill", inspection.Factories[0].Type);
    }

    [Fact]
    public async Task GetEventFeedAsync_GeneratesMarketAndProvinceWarnings()
    {
        var (db, eng, london) = BuildFakeWorld();
        london.NeedsFulfillment = 0.42m;
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var events = await svc.GetEventFeedAsync(eng.Id.Value.ToString(), 20);

        Assert.Contains(events, e => e.Category == "market" && e.GoodId == "grain");
        Assert.Contains(events, e => e.Category == "province" && e.ProvinceId == london.Id.Value.ToString());
        Assert.All(events, e => Assert.False(string.IsNullOrWhiteSpace(e.Title)));
    }

    [Fact]
    public async Task GetEventFeedAsync_IncludesConstructionForFilteredCountry()
    {
        var (db, eng, london) = BuildFakeWorld();
        db.Queue.Add(new BuildingQueueItem
        {
            Id = Guid.Parse("12345678-1234-1234-1234-123456789abc"),
            CountryId = eng.Id.Value,
            ProvinceId = london.Id.Value,
            BuildingType = "farm",
            TicksRemaining = 3,
            QueuedAt = DateTime.UtcNow
        });
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var events = await svc.GetEventFeedAsync(eng.Id.Value.ToString(), 20);

        var construction = Assert.Single(events, e => e.Category == "construction");
        Assert.Equal(london.Id.Value.ToString(), construction.ProvinceId);
        Assert.Equal("province", construction.TargetPanel);
    }

    [Fact]
    public async Task GetEventFeedAsync_ClampsLimitAndSortsBySeverity()
    {
        var (db, eng, london) = BuildFakeWorld();
        london.NeedsFulfillment = 0.40m;
        var svc = new WorldQueryService(db, new FakeClock(), new EmptyGoods());

        var events = await svc.GetEventFeedAsync(eng.Id.Value.ToString(), 1);

        Assert.Single(events);
        Assert.Equal("critical", events[0].Severity);
    }

    private static (FakeWorldDb db, Country eng, Province london) BuildFakeWorld()
    {
        var eng = new Country(new CountryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")), "England", "ENG", 17)
        {
            EducationSpending = 0.7m,
            MilitarySpending = 0.4m,
            AdministrationSpending = 0.5m
        };
        var fra = new Country(new CountryId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), "France", "FRA", 14);
        var market = new Market(new MarketId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), "World")
        {
            GoodPrices = new Dictionary<string, decimal> { ["grain"] = 5m, ["coal"] = 2m },
            GoodSupply = new Dictionary<string, decimal> { ["grain"] = 10m, ["coal"] = 100m },
            GoodDemand = new Dictionary<string, decimal> { ["grain"] = 30m, ["coal"] = 50m }
        };

        var london = new Province(new ProvinceId(Guid.Parse("11111111-2222-3333-4444-555555555555")),
            "London", eng.Id, market.Id, population: 10_000)
        {
            RgoType = "coal_mine"
        };
        london.PopGroups.Add(new PopGroup(Guid.NewGuid(), london.Id, 6_000, "laborers", "poor", "english", "anglican")
        {
            EmployedCount = 5_000,
            UnemployedCount = 1_000,
            Literacy = 0.3m
        });
        london.PopGroups.Add(new PopGroup(Guid.NewGuid(), london.Id, 4_000, "craftsmen", "poor", "english", "anglican")
        {
            EmployedCount = 4_000
        });

        var york = new Province(new ProvinceId(Guid.Parse("66666666-7777-8888-9999-aaaaaaaaaaaa")),
            "York", eng.Id, market.Id, population: 5_000)
        {
            RgoType = "grain_farm"
        };
        york.PopGroups.Add(new PopGroup(Guid.NewGuid(), york.Id, 5_000, "farmers", "poor", "english", "anglican")
        {
            EmployedCount = 5_000
        });

        var paris = new Province(new ProvinceId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            "Paris", fra.Id, market.Id, population: 8_000);

        var factory = new Factory
        {
            Id = Guid.NewGuid(),
            CountryId = eng.Id,
            ProvinceId = london.Id,
            Type = "steel_mill",
            Level = 2,
            EmployedCraftsmen = 800,
            EmployedClerks = 100,
            OutputGood = "steel",
            OutputPerTick = 12m
        };

        var snapshot = new WorldStateSnapshot
        {
            Countries = [eng, fra],
            Markets = [market],
            Provinces = [london, york, paris],
            Factories = [factory]
        };
        return (new FakeWorldDb(snapshot), eng, london);
    }

    private sealed class FakeWorldDb : IWorldStateDatabase
    {
        private readonly WorldStateSnapshot _snapshot;
        public FakeWorldDb(WorldStateSnapshot snapshot) { _snapshot = snapshot; }
        public List<BuildingQueueItem> Queue { get; } = new();

        public Task<WorldStateSnapshot?> LoadWorldAsync(CancellationToken ct = default) => Task.FromResult<WorldStateSnapshot?>(_snapshot);

        public Task SeedWorldAsync(WorldSeedData _, CancellationToken __ = default) => Task.CompletedTask;
        public Task SaveTickResultsAsync(TickWriteBatch _, CancellationToken __ = default) => Task.CompletedTask;
        public Task UpsertCountriesAsync(IEnumerable<Country> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task UpdateMarketAsync(Guid _, Dictionary<string, decimal> __, Dictionary<string, decimal> ___, Dictionary<string, decimal> ____, CancellationToken _____ = default) => Task.CompletedTask;
        public Task UpdateProvinceNeedsFulfillmentAsync(Dictionary<string, decimal> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task UpdatePopGroupsAsync(IReadOnlyList<PopGroupSimulationUpdate> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task UpdateProvinceOutputsAsync(Dictionary<string, Dictionary<string, decimal>> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task<List<BuildingQueueItem>> LoadBuildingQueueAsync(CancellationToken ct = default) => Task.FromResult(Queue);
        public Task SaveBuildingQueueAsync(List<BuildingQueueItem> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task SaveFactoriesAsync(List<Factory> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task SaveGoodProfitHistoryAsync(List<GoodProfitHistory> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task SaveArmiesAsync(List<ArmyStack> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task SaveWarsAsync(List<War> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task SaveBattleReportsAsync(List<BattleReport> _, CancellationToken __ = default) => Task.CompletedTask;
        public Task ClearWorldAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<PlayerAccount?> GetPlayerAccountAsync(Guid _, CancellationToken __ = default) => Task.FromResult<PlayerAccount?>(null);
    }

    private sealed class FakeClock : IWorldClockService
    {
        public TickMetrics CurrentMetrics { get; } = new() { TickCount = 1, WorldTimestamp = new DateTime(1836, 1, 1) };
        public SimulationMetricsSnapshot LatestSimulationMetrics { get; } = new();
        public bool IsPaused => false;
        public void Pause() { }
        public void Resume() { }
        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;
        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EmptyGoods : IGoodsService
    {
        public IReadOnlyList<GoodDefinition> All { get; } = Array.Empty<GoodDefinition>();
    }
}
