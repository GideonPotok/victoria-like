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

public sealed class MarketPricingStageTests
{
    [Fact]
    public void Execute_RaisesPricesGraduallyWhenDemandExceedsSupply()
    {
        var world = CreateWorld(stockpile: 1m, needPerThousand: 4m, startingPrice: 2m);

        new MarketPricingStage().Execute(CreateContext(world));

        Assert.Equal(8m, world.Market.DemandLastTick["grain"]);
        Assert.Equal(1m, world.Market.SupplyLastTick["grain"]);
        Assert.Equal(8m, world.Market.PricePressureLastTick["grain"]);
        Assert.Equal(7m, world.Market.UnmetDemandLastTick["grain"]);
        Assert.Equal(2.3m, world.Market.Prices["grain"]);
    }

    [Fact]
    public void Execute_LowersPricesGraduallyWhenSupplyExceedsDemand()
    {
        var world = CreateWorld(stockpile: 100m, needPerThousand: 0.5m, startingPrice: 4m);

        new MarketPricingStage().Execute(CreateContext(world));

        Assert.Equal(1m, world.Market.DemandLastTick["grain"]);
        Assert.Equal(100m, world.Market.SupplyLastTick["grain"]);
        Assert.Equal(0m, world.Market.UnmetDemandLastTick["grain"]);
        Assert.Equal(3.4m, world.Market.Prices["grain"]);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(73),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static WorldState CreateWorld(decimal stockpile, decimal needPerThousand, decimal startingPrice) =>
        new()
        {
            Seed = 73,
            Date = new GameDate(new DateOnly(1836, 1, 8)),
            Countries = new Dictionary<string, CountryState>
            {
                ["albion"] = new()
                {
                    Id = "albion",
                    DisplayName = "Albion",
                    ProvinceIds = ["northshire"],
                    Treasury = 1_000m,
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
                    PopulationIds = ["farmers"],
                    Stockpile = { ["grain"] = stockpile }
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
                    CashReserve = 5m,
                    Literacy = 0.2m,
                    Militancy = 0m,
                    Consciousness = 0m,
                    Needs = new PopNeedProfile
                    {
                        Life = { ["grain"] = needPerThousand }
                    }
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 2m, "food")
            },
            Market = new MarketState
            {
                Prices = { ["grain"] = startingPrice }
            }
        };
}
