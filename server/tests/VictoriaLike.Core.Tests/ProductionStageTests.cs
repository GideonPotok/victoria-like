using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class ProductionStageTests
{
    [Fact]
    public void ProvinceProduction_UsesEmployedRgoWorkers()
    {
        var world = CreateWorld();
        var province = world.Provinces["northshire"];

        new ProvinceProductionStage().Execute(CreateContext(world));

        Assert.Equal(10m, province.Stockpile["grain"]);
        Assert.Equal(10m, world.Market.ProductionLastTick["grain"]);
    }

    [Fact]
    public void FactoryProduction_ConsumesInputsAndProducesOutput()
    {
        var world = CreateWorld();
        var country = world.Countries["albion"];
        country.Stockpile["coal"] = 20m;
        country.Stockpile["iron"] = 20m;
        world.Factories["steel-1"] = new FactoryState
        {
            Id = "steel-1",
            CountryId = "albion",
            Type = "steel_mill",
            Level = 1,
            EmployedCraftsmen = 1000,
            InputGoods = new Dictionary<string, decimal>
            {
                ["coal"] = 0.5m,
                ["iron"] = 0.5m
            },
            OutputGood = "steel",
            OutputPerTick = 10m
        };

        new FactoryProductionStage().Execute(CreateContext(world));

        Assert.Equal(15m, country.Stockpile["coal"]);
        Assert.Equal(15m, country.Stockpile["iron"]);
        Assert.Equal(10m, country.Stockpile["steel"]);
        Assert.Equal(10m, world.Market.ProductionLastTick["steel"]);
        Assert.True(world.Factories["steel-1"].ProfitLastTick > 0m);
    }

    [Fact]
    public void FactoryProduction_InputShortageLimitsOutput()
    {
        var world = CreateWorld();
        var country = world.Countries["albion"];
        country.Stockpile["coal"] = 2m;
        country.Stockpile["iron"] = 20m;
        world.Factories["steel-1"] = new FactoryState
        {
            Id = "steel-1",
            CountryId = "albion",
            Type = "steel_mill",
            Level = 1,
            EmployedCraftsmen = 1000,
            InputGoods = new Dictionary<string, decimal>
            {
                ["coal"] = 0.5m,
                ["iron"] = 0.5m
            },
            OutputGood = "steel",
            OutputPerTick = 10m
        };

        new FactoryProductionStage().Execute(CreateContext(world));

        Assert.Equal(0m, country.Stockpile["coal"]);
        Assert.Equal(18m, country.Stockpile["iron"]);
        Assert.Equal(4m, country.Stockpile["steel"]);
        Assert.Equal(4m, world.Market.ProductionLastTick["steel"]);
    }

    [Fact]
    public void ArtisanProduction_ConsumesInputsProducesGoodAndRecordsProfit()
    {
        var world = CreateWorld();
        var country = world.Countries["albion"];
        country.Stockpile["iron"] = 10m;
        country.Stockpile["coal"] = 10m;
        world.Provinces["northshire"].PopulationIds.Add("artisans");
        world.Pops["artisans"] = new PopState
        {
            Id = "artisans",
            ProvinceId = "northshire",
            PopClass = "artisans",
            Size = 1_000,
            EmployedCount = 1_000,
            CashReserve = 2m,
            ArtisanProducedGood = "tools",
            ArtisanDaysUntilReconsider = 12,
            Needs = new PopNeedProfile()
        };

        new ArtisanProductionStage().Execute(CreateContext(world));

        Assert.Equal(9.865m, country.Stockpile["iron"]);
        Assert.Equal(9.91m, country.Stockpile["coal"]);
        Assert.Equal(0.45m, country.Stockpile["tools"]);
        Assert.Equal(0.45m, world.Market.ProductionLastTick["tools"]);
        Assert.True(world.Pops["artisans"].CashReserve > 2m);
        Assert.True(world.Pops["artisans"].ArtisanProfitLastTick > 0m);

        var history = Assert.Single(world.GoodProfitHistory);
        Assert.Equal("1836-01", history.Month);
        Assert.Equal("tools", history.GoodId);
        Assert.Equal(1, history.ProducerCount);
    }

    [Fact]
    public void ArtisanProduction_ReconsidersTowardMoreProfitableGoodWithInertia()
    {
        var world = CreateWorld();
        var country = world.Countries["albion"];
        country.Stockpile["fabric"] = 10m;
        world.Market.Prices["clothes"] = 8m;
        world.Market.Prices["fabric"] = 1m;
        world.Provinces["northshire"].PopulationIds.Add("artisans");
        world.Pops["artisans"] = new PopState
        {
            Id = "artisans",
            ProvinceId = "northshire",
            PopClass = "artisans",
            Size = 1_000,
            EmployedCount = 1_000,
            CashReserve = 2m,
            ArtisanProducedGood = "tools",
            ArtisanDaysUntilReconsider = 0,
            Needs = new PopNeedProfile()
        };
        world.GoodProfitHistory.Add(new GoodProfitHistoryEntry
        {
            Month = "1836-01",
            GoodId = "clothes",
            AverageProducerProfit = 5m,
            ProducerCount = 2
        });
        world.GoodProfitHistory.Add(new GoodProfitHistoryEntry
        {
            Month = "1836-01",
            GoodId = "tools",
            AverageProducerProfit = -1m,
            ProducerCount = 2
        });

        new ArtisanProductionStage().Execute(CreateContext(world));

        Assert.Equal("clothes", world.Pops["artisans"].ArtisanProducedGood);
        Assert.True(world.Pops["artisans"].ArtisanDaysUntilReconsider > 0);
        Assert.True(country.Stockpile["clothes"] > 0m);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(1),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static WorldState CreateWorld() =>
        new()
        {
            Seed = 1,
            Date = new GameDate(new DateOnly(1836, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                ["albion"] = new()
                {
                    Id = "albion",
                    DisplayName = "Albion",
                    ProvinceIds = ["northshire"],
                    Treasury = 100m,
                    TaxRate = 0.2m,
                    TariffRate = 0m,
                    IsPlayable = true
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                ["northshire"] = new()
                {
                    Id = "northshire",
                    DisplayName = "Northshire",
                    OwnerId = "albion",
                    RgoType = "grain_farm",
                    PopulationIds = ["farmers", "laborers"],
                    OutputsPerTick = { ["grain"] = 10m }
                }
            },
            Pops = new Dictionary<string, PopState>
            {
                ["farmers"] = new()
                {
                    Id = "farmers",
                    ProvinceId = "northshire",
                    PopClass = "farmers",
                    Size = 4_000,
                    EmployedCount = 4_000,
                    Needs = new PopNeedProfile()
                },
                ["laborers"] = new()
                {
                    Id = "laborers",
                    ProvinceId = "northshire",
                    PopClass = "laborers",
                    Size = 4_000,
                    EmployedCount = 4_000,
                    Needs = new PopNeedProfile()
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 1m, "food"),
                ["coal"] = new("coal", "Coal", 2m, "raw"),
                ["iron"] = new("iron", "Iron", 3m, "raw"),
                ["steel"] = new("steel", "Steel", 10m, "industrial"),
                ["tools"] = new("tools", "Tools", 4.5m, "industrial"),
                ["clothes"] = new("clothes", "Clothes", 3.2m, "consumer"),
                ["fabric"] = new("fabric", "Fabric", 2.7m, "industrial")
            },
            Market = new MarketState
            {
                Prices =
                {
                    ["grain"] = 1m,
                    ["coal"] = 2m,
                    ["iron"] = 3m,
                    ["steel"] = 10m,
                    ["tools"] = 4.5m,
                    ["clothes"] = 3.2m,
                    ["fabric"] = 2.7m
                }
            }
        };
}
