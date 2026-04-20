using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class ProductionIntegrationTests
{
    [Fact]
    public void TwelveMonthProductionRun_KeepsEconomyCoherent()
    {
        var world = CreateWorld();
        var startingDate = world.Date.Value;
        var startingCash = world.Pops.ToDictionary(pop => pop.Key, pop => pop.Value.CashReserve);
        var startingCoal = world.Countries["albion"].Stockpile["coal"];
        var startingTimber = world.Countries["albion"].Stockpile["timber"];
        var checker = new WorldInvariantChecker();
        var orchestrator = new SimulationOrchestrator(
        [
            new AdvanceDateStage(),
            new EmploymentAssignmentStage(),
            new ProvinceProductionStage(),
            new FactoryProductionStage(),
            new ArtisanProductionStage(),
            new NationalDistributionStage(),
            new MarketPricingStage(),
            new PopNeedsStage(),
            new MonthlyPopUpdateStage(),
            new BudgetStage(),
        ]);

        var rawOutput = 0m;
        var factoryOutput = 0m;
        var artisanOutput = 0m;

        while (world.Date.Value < startingDate.AddMonths(12))
        {
            orchestrator.RunTick(world);

            var report = checker.Check(world);
            Assert.True(report.IsValid, string.Join("; ", report.Violations.Select(v => $"{v.Code}: {v.Message}")));

            rawOutput += world.Market.ProductionLastTick.GetValueOrDefault("grain");
            rawOutput += world.Market.ProductionLastTick.GetValueOrDefault("iron");
            factoryOutput += world.Market.ProductionLastTick.GetValueOrDefault("steel");
            artisanOutput += world.Market.ProductionLastTick.GetValueOrDefault("furniture");

            foreach (var good in world.Goods.Values)
            {
                Assert.InRange(world.Market.Prices[good.Id], 0.5m, good.BasePrice * 5m);
            }
        }

        Assert.True(rawOutput > 0m);
        Assert.True(factoryOutput > 0m);
        Assert.True(artisanOutput > 0m);
        Assert.True(world.Countries["albion"].Stockpile["coal"] < startingCoal);
        Assert.True(world.Countries["albion"].Stockpile["timber"] < startingTimber);
        Assert.Contains(world.Pops.Values, pop => pop.CashReserve != startingCash[pop.Id]);
        Assert.True(world.Pops.Values.Sum(pop => pop.UnemployedCount) > 0);
        Assert.Contains(world.Market.SupplyLastTick, entry => entry.Value > 0m);
        Assert.Contains(world.EventLog, entry => entry.StartsWith("monthly-pop-update:", StringComparison.Ordinal));
    }

    private static WorldState CreateWorld() =>
        new()
        {
            Seed = 70,
            Date = new GameDate(new DateOnly(1836, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                ["albion"] = new()
                {
                    Id = "albion",
                    DisplayName = "Albion",
                    ProvinceIds = ["farmshire", "ironvale"],
                    Treasury = 5_000m,
                    TaxRate = 0.20m,
                    TariffRate = 0m,
                    IsPlayable = true,
                    Stockpile =
                    {
                        ["coal"] = 500m,
                        ["iron"] = 120m,
                        ["timber"] = 120m,
                        ["grain"] = 120m,
                        ["furniture"] = 5m,
                        ["tools"] = 5m,
                    }
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                ["farmshire"] = new()
                {
                    Id = "farmshire",
                    DisplayName = "Farmshire",
                    OwnerId = "albion",
                    RgoType = "grain_farm",
                    PopulationIds = ["farmers", "craftsmen", "clerks", "artisans"],
                    OutputsPerTick = { ["grain"] = 16m },
                    Stockpile = { ["grain"] = 50m },
                    Infrastructure = 0.25m
                },
                ["ironvale"] = new()
                {
                    Id = "ironvale",
                    DisplayName = "Ironvale",
                    OwnerId = "albion",
                    RgoType = "iron_mine",
                    PopulationIds = ["laborers"],
                    OutputsPerTick = { ["iron"] = 12m },
                    Stockpile = { ["iron"] = 30m },
                    Infrastructure = 0.25m
                }
            },
            Pops = new Dictionary<string, PopState>
            {
                ["farmers"] = CreatePop("farmers", "farmshire", "farmers", 5_000, 20m),
                ["laborers"] = CreatePop("laborers", "ironvale", "laborers", 5_000, 18m),
                ["craftsmen"] = CreatePop("craftsmen", "farmshire", "craftsmen", 1_500, 30m),
                ["clerks"] = CreatePop("clerks", "farmshire", "clerks", 400, 40m),
                ["artisans"] = CreatePop("artisans", "farmshire", "artisans", 800, 50m, "furniture"),
            },
            Factories = new Dictionary<string, FactoryState>
            {
                ["steel-mill"] = new()
                {
                    Id = "steel-mill",
                    CountryId = "albion",
                    ProvinceId = "farmshire",
                    Type = "steel_mill",
                    Level = 1,
                    InputGoods = { ["coal"] = 0.4m, ["iron"] = 0.4m },
                    OutputGood = "steel",
                    OutputPerTick = 5m,
                    CashReserve = 25m
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 1.0m, "food"),
                ["iron"] = new("iron", "Iron", 2.4m, "raw"),
                ["coal"] = new("coal", "Coal", 2.1m, "raw"),
                ["timber"] = new("timber", "Timber", 1.8m, "raw"),
                ["steel"] = new("steel", "Steel", 5.0m, "industrial"),
                ["furniture"] = new("furniture", "Furniture", 3.6m, "consumer"),
                ["tools"] = new("tools", "Tools", 4.5m, "industrial"),
            },
            Market = new MarketState
            {
                Prices =
                {
                    ["grain"] = 1.0m,
                    ["iron"] = 2.4m,
                    ["coal"] = 2.1m,
                    ["timber"] = 1.8m,
                    ["steel"] = 5.0m,
                    ["furniture"] = 3.6m,
                    ["tools"] = 4.5m,
                }
            },
            Metrics = new SimulationMetrics()
        };

    private static PopState CreatePop(
        string id,
        string provinceId,
        string popClass,
        int size,
        decimal cash,
        string? artisanGood = null) =>
        new()
        {
            Id = id,
            ProvinceId = provinceId,
            PopClass = popClass,
            Size = size,
            CashReserve = cash,
            Literacy = 0.35m,
            Militancy = 0.1m,
            Consciousness = 0.2m,
            ArtisanProducedGood = artisanGood,
            ArtisanDaysUntilReconsider = 30,
            Needs = new PopNeedProfile
            {
                Life = { ["grain"] = 0.4m },
                Everyday = { ["furniture"] = 0.03m, ["tools"] = 0.02m },
            }
        };
}
