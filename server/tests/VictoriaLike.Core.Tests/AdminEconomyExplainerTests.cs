using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class AdminEconomyExplainerTests
{
    [Fact]
    public void ExplainGood_ComputesPressureUnmetDemandClampAndAttribution()
    {
        var england = new CountryId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var market = new MarketId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var london = new Province(
            new ProvinceId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            "London",
            england,
            market,
            population: 10_000)
        {
            OutputsPerTick = new Dictionary<string, decimal> { ["grain"] = 3m }
        };
        var yorkshire = new Province(
            new ProvinceId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
            "Yorkshire",
            england,
            market,
            population: 5_000)
        {
            OutputsPerTick = new Dictionary<string, decimal> { ["grain"] = 7m }
        };
        var world = new WorldStateSnapshot { Provinces = [london, yorkshire] };
        var history = new[]
        {
            new MarketTickSnapshot
            {
                Tick = 9,
                Prices = new Dictionary<string, decimal> { ["grain"] = 3m }
            },
            new MarketTickSnapshot
            {
                Tick = 10,
                Prices = new Dictionary<string, decimal> { ["grain"] = 25m }
            }
        };

        var explanation = AdminEconomyExplainer.ExplainGood(
            "grain",
            "Grain",
            new GoodDefinition("grain", "Grain", 4m, "staple"),
            price: 25m,
            previousPrice: 3m,
            supply: 2m,
            demand: 20m,
            world,
            history);

        Assert.Equal(10m, explanation.TargetPressure);
        Assert.Equal(18m, explanation.UnmetDemand);
        Assert.True(explanation.ClampApplied);
        Assert.Equal("Yorkshire", explanation.LargestProducer);
        Assert.Equal("London", explanation.LargestConsumer);
        Assert.Equal(22m, explanation.PriceDelta);
        Assert.Equal([3m, 25m], explanation.PriceHistory);
    }

    [Fact]
    public void EstimateProvinceDemand_UsesLifeNeedsPerThousandPopulation()
    {
        var province = new Province(
            new ProvinceId(Guid.NewGuid()),
            "Paris",
            new CountryId(Guid.NewGuid()),
            new MarketId(Guid.NewGuid()),
            population: 6_000);

        var demand = AdminEconomyExplainer.EstimateProvinceDemand(province);

        Assert.Equal(3.0m, demand["grain"]);
        Assert.Equal(1.2m, demand["fish"]);
    }
}
