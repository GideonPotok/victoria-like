using System.Diagnostics;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class WorldExplanationServiceTests
{
    [Fact]
    public async Task ExplainGoodAsync_MatchesMarketData()
    {
        var fixture = BuildWorld();
        var service = new WorldExplanationService(fixture.Db, fixture.Goods, fixture.History);

        var explanation = await service.ExplainGoodAsync("grain");

        Assert.NotNull(explanation);
        Assert.Equal("good", explanation.SubjectType);
        Assert.Equal(12m, explanation.Metrics["price"]);
        Assert.Equal(20m, explanation.Metrics["demand"]);
        Assert.Equal(8m, explanation.Metrics["supply"]);
        Assert.Contains(explanation.Factors, factor => factor.Label == "Supply and demand" && factor.Impact == "negative");
    }

    [Fact]
    public async Task ExplainPopNeedsAsync_ReflectsNeedsEmploymentAndTaxData()
    {
        var fixture = BuildWorld();
        var service = new WorldExplanationService(fixture.Db, fixture.Goods, fixture.History);

        var explanation = await service.ExplainPopNeedsAsync(fixture.PopId.ToString());

        Assert.NotNull(explanation);
        Assert.Equal(0.40m, explanation.Metrics["life_needs_fulfillment"]);
        Assert.Equal(0.25m, explanation.Metrics["unemployment_share"]);
        Assert.Equal(0.60m, explanation.Metrics["tax_rate"]);
        Assert.Contains(explanation.Factors, factor => factor.Label == "Life needs" && factor.Impact == "negative");
    }

    [Fact]
    public async Task ExplainCountryBudgetAsync_ReflectsBudgetData()
    {
        var fixture = BuildWorld();
        var service = new WorldExplanationService(fixture.Db, fixture.Goods, fixture.History);

        var explanation = await service.ExplainCountryBudgetAsync(fixture.CountryId.ToString());

        Assert.NotNull(explanation);
        Assert.Equal(500m, explanation.Metrics["treasury"]);
        Assert.Equal(0.60m, explanation.Metrics["poor_tax_rate"]);
        Assert.True(explanation.Metrics["estimated_weekly_spending"] > 0m);
        Assert.Contains(explanation.Factors, factor => factor.Label == "Treasury");
    }

    [Fact]
    public async Task ExplainWarAndBattleAsync_AreNonNullForCoreMilitaryStates()
    {
        var fixture = BuildWorld();
        var service = new WorldExplanationService(fixture.Db, fixture.Goods, fixture.History);

        var war = await service.ExplainWarAsync(fixture.WarId.ToString());
        var battle = await service.ExplainBattleAsync(fixture.BattleId);

        Assert.NotNull(war);
        Assert.NotNull(battle);
        Assert.Equal(1m, war.Metrics["battle_count"]);
        Assert.Equal(30m, battle.Metrics["loser_casualties"]);
    }

    [Fact]
    public async Task ExplanationEndpoints_AreFastAgainstLoadedSnapshot()
    {
        var fixture = BuildWorld();
        var service = new WorldExplanationService(fixture.Db, fixture.Goods, fixture.History);
        var sw = Stopwatch.StartNew();

        _ = await service.ExplainGoodAsync("grain");
        _ = await service.ExplainPopNeedsAsync(fixture.PopId.ToString());
        _ = await service.ExplainProvinceEmploymentAsync(fixture.ProvinceId.ToString());
        _ = await service.ExplainCountryBudgetAsync(fixture.CountryId.ToString());
        _ = await service.ExplainWarAsync(fixture.WarId.ToString());
        _ = await service.ExplainBattleAsync(fixture.BattleId);

        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 100, $"Explanation calls took {sw.ElapsedMilliseconds}ms");
    }

    private static ExplanationFixture BuildWorld()
    {
        var countryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var enemyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var marketId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var provinceId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var enemyProvinceId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var popId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var warId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var winnerArmyId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var loserArmyId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var battleId = $"battle-18360101-{provinceId}-{winnerArmyId}-{loserArmyId}";

        var country = new Country(new CountryId(countryId), "Albion", "ALB", 60)
        {
            Treasury = 500m,
            PoorTaxRate = 0.60m,
            MiddleTaxRate = 0.30m,
            RichTaxRate = 0.20m,
            EducationSpending = 0.7m,
            MilitarySpending = 0.5m,
            AdministrationSpending = 0.4m
        };
        var enemy = new Country(new CountryId(enemyId), "Bretoria", "BRE", 10)
        {
            Treasury = 1_000m
        };
        var market = new Market(new MarketId(marketId), "Market")
        {
            GoodPrices = new Dictionary<string, decimal> { ["grain"] = 12m },
            GoodSupply = new Dictionary<string, decimal> { ["grain"] = 8m },
            GoodDemand = new Dictionary<string, decimal> { ["grain"] = 20m }
        };
        var province = new Province(new ProvinceId(provinceId), "Northshire", country.Id, market.Id, 1_000)
        {
            NeedsFulfillment = 0.4m,
            RgoType = "grain_farm",
            OutputsPerTick = new Dictionary<string, decimal> { ["grain"] = 3m },
            PopGroups =
            [
                new PopGroup(popId, new ProvinceId(provinceId), 1_000, "farmers", "poor", "primary", "secular")
                {
                    Cash = 0.5m,
                    LifeNeedsFulfillment = 0.40m,
                    EverydayNeedsFulfillment = 0.25m,
                    LuxuryNeedsFulfillment = 0m,
                    EmployedCount = 750,
                    UnemployedCount = 250,
                    Militancy = 4m
                }
            ]
        };
        var enemyProvince = new Province(new ProvinceId(enemyProvinceId), "Southport", enemy.Id, market.Id, 1_000);

        var snapshot = new WorldStateSnapshot
        {
            Countries = [country, enemy],
            Markets = [market],
            Provinces = [province, enemyProvince],
            Armies =
            [
                new ArmyStack
                {
                    Id = winnerArmyId,
                    CountryId = country.Id,
                    LocationProvinceId = province.Id,
                    SoldierCount = 900,
                    Morale = 0.8m
                },
                new ArmyStack
                {
                    Id = loserArmyId,
                    CountryId = enemy.Id,
                    LocationProvinceId = province.Id,
                    SoldierCount = 700,
                    Morale = 0.2m
                }
            ],
            Wars =
            [
                new War
                {
                    Id = warId,
                    AttackerCountryId = country.Id,
                    DefenderCountryId = enemy.Id,
                    StartedAt = new DateTime(1836, 1, 1),
                    IsActive = true
                }
            ],
            BattleReports =
            [
                new BattleReport
                {
                    Id = battleId,
                    WarId = warId,
                    ProvinceId = provinceId,
                    WinnerArmyId = winnerArmyId,
                    LoserArmyId = loserArmyId,
                    WinnerCountryId = countryId,
                    LoserCountryId = enemyId,
                    OccurredAt = new DateTime(1836, 1, 1),
                    WinnerCasualties = 10,
                    LoserCasualties = 30,
                    WinnerMoraleAfter = 0.8m,
                    LoserMoraleAfter = 0.2m
                }
            ]
        };

        var history = new FakeMarketHistory();
        history.RecordTick(1,
            new Dictionary<string, decimal> { ["grain"] = 10m },
            new Dictionary<string, decimal> { ["grain"] = 9m },
            new Dictionary<string, decimal> { ["grain"] = 15m });
        history.RecordTick(2,
            new Dictionary<string, decimal> { ["grain"] = 12m },
            new Dictionary<string, decimal> { ["grain"] = 8m },
            new Dictionary<string, decimal> { ["grain"] = 20m });

        return new ExplanationFixture(
            new FakeWorldDb(snapshot),
            new FakeGoods(),
            history,
            countryId,
            provinceId,
            popId,
            warId,
            battleId);
    }

    private sealed record ExplanationFixture(
        FakeWorldDb Db,
        FakeGoods Goods,
        FakeMarketHistory History,
        Guid CountryId,
        Guid ProvinceId,
        Guid PopId,
        Guid WarId,
        string BattleId);

    private sealed class FakeGoods : IGoodsService
    {
        public IReadOnlyList<GoodDefinition> All { get; } =
        [
            new GoodDefinition("grain", "Grain", 4m, "staple")
        ];
    }

    private sealed class FakeMarketHistory : IMarketHistoryService
    {
        private readonly MarketHistoryService _inner = new();
        public MarketTickSnapshot? Latest => _inner.Latest;
        public IReadOnlyList<MarketTickSnapshot> GetHistory(int count = 20) => _inner.GetHistory(count);
        public void RecordTick(long tick, Dictionary<string, decimal> prices, Dictionary<string, decimal> supply, Dictionary<string, decimal> demand) =>
            _inner.RecordTick(tick, prices, supply, demand);
    }

    private sealed class FakeWorldDb : IWorldStateDatabase
    {
        private readonly WorldStateSnapshot _snapshot;

        public FakeWorldDb(WorldStateSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public Task<WorldStateSnapshot?> LoadWorldAsync(CancellationToken cancellationToken = default) => Task.FromResult<WorldStateSnapshot?>(_snapshot);
        public Task SeedWorldAsync(WorldSeedData seed, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveTickResultsAsync(TickWriteBatch batch, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertCountriesAsync(IEnumerable<Country> countries, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateMarketAsync(Guid marketId, Dictionary<string, decimal> prices, Dictionary<string, decimal> supply, Dictionary<string, decimal> demand, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateProvinceNeedsFulfillmentAsync(Dictionary<string, decimal> needsByProvinceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdatePopGroupsAsync(IReadOnlyList<PopGroupSimulationUpdate> popGroups, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpdateProvinceOutputsAsync(Dictionary<string, Dictionary<string, decimal>> outputsByProvinceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<List<BuildingQueueItem>> LoadBuildingQueueAsync(CancellationToken cancellationToken = default) => Task.FromResult(new List<BuildingQueueItem>());
        public Task SaveBuildingQueueAsync(List<BuildingQueueItem> items, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveFactoriesAsync(List<Factory> factories, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveGoodProfitHistoryAsync(List<GoodProfitHistory> history, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveArmiesAsync(List<ArmyStack> armies, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveWarsAsync(List<War> wars, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveBattleReportsAsync(List<BattleReport> battleReports, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClearWorldAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<PlayerAccount?> GetPlayerAccountAsync(Guid actorId, CancellationToken cancellationToken = default) => Task.FromResult<PlayerAccount?>(null);
    }
}
