using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class PopNeedsStageTests
{
    [Fact]
    public void Execute_PaysIncomeCollectsTaxesAndBuysNeeds()
    {
        var world = CreateWorld();
        var pop = world.Pops["farmers"];
        var country = world.Countries["albion"];
        var cashBefore = pop.CashReserve;
        var treasuryBefore = country.Treasury;

        new PopNeedsStage().Execute(CreateContext(world));

        Assert.True(country.Treasury > treasuryBefore);
        Assert.True(pop.CashReserve < cashBefore);
        Assert.Equal(1m, pop.LifeNeedsFulfillment);
        Assert.Equal(1m, pop.EverydayNeedsFulfillment);
        Assert.Equal(1m, pop.LuxuryNeedsFulfillment);
        Assert.Equal(1m, world.Market.ConsumptionLastTick["grain"]);
        Assert.Equal(0.4m, world.Market.ConsumptionLastTick["tools"]);
        Assert.Equal(0.2m, world.Market.ConsumptionLastTick["luxury_clothes"]);
    }

    [Fact]
    public void Execute_CashShortageLimitsNeedFulfillment()
    {
        var world = CreateWorld();
        var pop = world.Pops["farmers"];
        pop.CashReserve = 0m;
        world.Market.Prices["grain"] = 10m;
        world.Market.Prices["tools"] = 10m;
        world.Market.Prices["luxury_clothes"] = 10m;

        new PopNeedsStage().Execute(CreateContext(world));

        Assert.InRange(pop.NeedsFulfillment, 0m, 1m);
        Assert.True(pop.LifeNeedsFulfillment < 1m);
        Assert.True(pop.EverydayNeedsFulfillment < 1m);
        Assert.True(pop.LuxuryNeedsFulfillment < 1m);
        Assert.True(pop.Militancy > 0m);
    }

    [Fact]
    public void Execute_UsesStrataSpecificTaxRates()
    {
        var world = CreateWorld();
        var country = world.Countries["albion"];
        country.PoorTaxRate = 0m;
        country.MiddleTaxRate = 0.5m;
        country.RichTaxRate = 1m;
        world.Provinces["northshire"].PopulationIds.AddRange(["clerks", "capitalists"]);
        world.Pops["clerks"] = CreatePop("clerks", "clerks", cash: 0m);
        world.Pops["capitalists"] = CreatePop("capitalists", "capitalists", cash: 0m);

        new PopNeedsStage().Execute(CreateContext(world));

        Assert.True(world.Pops["farmers"].CashReserve > world.Pops["clerks"].CashReserve);
        Assert.True(world.Pops["clerks"].CashReserve > 0m);
        Assert.Equal(0m, world.Pops["capitalists"].CashReserve);
        Assert.True(country.Treasury > 1_000m);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(72),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static WorldState CreateWorld() =>
        new()
        {
            Seed = 72,
            Date = new GameDate(new DateOnly(1836, 1, 8)),
            Countries = new Dictionary<string, CountryState>
            {
                ["albion"] = new()
                {
                    Id = "albion",
                    DisplayName = "Albion",
                    ProvinceIds = ["northshire"],
                    Treasury = 1_000m,
                    TaxRate = 0.20m,
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
                    PopulationIds = ["farmers"],
                    Stockpile =
                    {
                        ["grain"] = 10m,
                        ["tools"] = 10m,
                        ["luxury_clothes"] = 10m
                    }
                }
            },
            Pops = new Dictionary<string, PopState>
            {
                ["farmers"] = new()
                {
                    Id = "farmers",
                    ProvinceId = "northshire",
                    PopClass = "farmers",
                    Size = 2_000,
                    CashReserve = 10m,
                    Literacy = 0.2m,
                    Militancy = 0m,
                    Consciousness = 0m,
                    EmployedCount = 2_000,
                    UnemployedCount = 0,
                    Needs = new PopNeedProfile
                    {
                        Life = { ["grain"] = 0.5m },
                        Everyday = { ["tools"] = 0.2m },
                        Luxury = { ["luxury_clothes"] = 0.1m }
                    }
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 1m, "food"),
                ["tools"] = new("tools", "Tools", 4.5m, "industrial"),
                ["luxury_clothes"] = new("luxury_clothes", "Luxury Clothes", 8m, "luxury")
            },
            Market = new MarketState
            {
                Prices =
                {
                    ["grain"] = 1m,
                    ["tools"] = 2m,
                    ["luxury_clothes"] = 3m
                }
            }
        };

    private static PopState CreatePop(string id, string popClass, decimal cash) =>
        new()
        {
            Id = id,
            ProvinceId = "northshire",
            PopClass = popClass,
            Size = 1_000,
            CashReserve = cash,
            Literacy = 0.2m,
            Militancy = 0m,
            Consciousness = 0m,
            EmployedCount = 1_000,
            UnemployedCount = 0,
            Needs = new PopNeedProfile()
        };
}
