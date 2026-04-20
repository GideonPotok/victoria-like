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

public sealed class MonthlyPopUpdateStageTests
{
    [Fact]
    public void Execute_DoesNothingOutsideFirstDayOfMonth()
    {
        var world = CreateWorld(new DateOnly(1836, 1, 2));
        var pop = world.Pops["pop-1"];
        var cashBefore = pop.CashReserve;
        var literacyBefore = pop.Literacy;
        var militancyBefore = pop.Militancy;
        var consciousnessBefore = pop.Consciousness;

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        Assert.Equal(cashBefore, pop.CashReserve);
        Assert.Equal(literacyBefore, pop.Literacy);
        Assert.Equal(militancyBefore, pop.Militancy);
        Assert.Equal(consciousnessBefore, pop.Consciousness);
        Assert.DoesNotContain(world.EventLog, entry => entry.StartsWith("monthly-pop-update:", StringComparison.Ordinal));
    }

    [Fact]
    public void Execute_OnFirstDayOfMonthUpdatesPopStateDeterministically()
    {
        var world = CreateWorld(new DateOnly(1836, 2, 1));
        var pop = world.Pops["pop-1"];

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        Assert.Equal(10m, pop.CashReserve);
        Assert.True(pop.Literacy > 0.30m);
        Assert.True(pop.Militancy < 0.20m);
        Assert.True(pop.Consciousness > 0.20m);
        Assert.Contains("monthly-pop-update:1836-02-01:1", world.EventLog);
    }

    [Fact]
    public void Execute_ClampsPopStateToInvariantRanges()
    {
        var world = CreateWorld(new DateOnly(1836, 2, 1));
        var pop = world.Pops["pop-1"];
        pop.CashReserve = 0.1m;
        pop.Literacy = 1m;
        pop.Militancy = 9.99m;
        pop.Consciousness = 9.99m;
        pop.NeedsFulfillment = 0m;
        world.Market.Prices["grain"] = 1_000m;
        world.Market.Prices["fish"] = 1_000m;

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        Assert.Equal(0.1m, pop.CashReserve);
        Assert.InRange(pop.Literacy, 0m, 1m);
        Assert.InRange(pop.Militancy, 0m, 10m);
        Assert.InRange(pop.Consciousness, 0m, 10m);
        Assert.Contains("demotion-risk:pop-1", world.EventLog);
    }

    [Fact]
    public void Execute_UsesEducationNeedsAndUnemploymentForPoliticalDrift()
    {
        var world = CreateWorld(new DateOnly(1836, 2, 1));
        var country = world.Countries["albion"];
        var pop = world.Pops["pop-1"];
        country.EducationSpending = 1m;
        pop.NeedsFulfillment = 0.4m;
        pop.EmployedCount = 500;
        pop.UnemployedCount = 500;
        var literacyBefore = pop.Literacy;
        var militancyBefore = pop.Militancy;
        var consciousnessBefore = pop.Consciousness;

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        Assert.True(pop.Literacy > literacyBefore);
        Assert.True(pop.Militancy > militancyBefore);
        Assert.True(pop.Consciousness > consciousnessBefore);
    }

    [Fact]
    public void Execute_PromotionCandidateTransfersSmallPopulationToTargetClass()
    {
        var world = CreateWorld(new DateOnly(1836, 2, 1));
        var pop = world.Pops["pop-1"];
        pop.Size = 1_000;
        pop.EmployedCount = 1_000;
        pop.UnemployedCount = 0;
        pop.CashReserve = 30m;
        pop.Literacy = 0.5m;
        pop.NeedsFulfillment = 1m;
        var totalBefore = world.Pops.Values.Sum(p => p.Size);

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        Assert.Equal(999, pop.Size);
        var craftsmen = Assert.Single(world.Pops.Values.Where(p => p.PopClass == "craftsmen"));
        Assert.Equal(1, craftsmen.Size);
        Assert.Equal(totalBefore, world.Pops.Values.Sum(p => p.Size));
        Assert.Contains("promotion:pop-1:craftsmen:1", world.EventLog);
    }

    [Fact]
    public void Execute_DemotionRiskTransfersSmallPopulationToTargetClass()
    {
        var world = CreateWorld(new DateOnly(1836, 2, 1));
        world.Pops["pop-1"] = new PopState
        {
            Id = "pop-1",
            ProvinceId = "northshire",
            PopClass = "craftsmen",
            Size = 1_000,
            CashReserve = 0m,
            Literacy = 0.35m,
            Militancy = 0.2m,
            Consciousness = 0.2m,
            NeedsFulfillment = 0.3m,
            EmployedCount = 800,
            UnemployedCount = 200,
            Needs = new PopNeedProfile()
        };
        var pop = world.Pops["pop-1"];
        var totalBefore = world.Pops.Values.Sum(p => p.Size);

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        Assert.Equal(999, pop.Size);
        var laborers = Assert.Single(world.Pops.Values.Where(p => p.PopClass == "laborers"));
        Assert.Equal(1, laborers.Size);
        Assert.Equal(totalBefore, world.Pops.Values.Sum(p => p.Size));
        Assert.Contains("demotion:pop-1:laborers:1", world.EventLog);
    }

    [Fact]
    public void Execute_RecalculatesCountryReformPressureFromPopConditions()
    {
        var world = CreateWorld(new DateOnly(1836, 2, 1));
        var pop = world.Pops["pop-1"];
        pop.NeedsFulfillment = 0.25m;
        pop.Militancy = 5m;
        pop.Consciousness = 4m;
        pop.EmployedCount = 500;
        pop.UnemployedCount = 500;

        new MonthlyPopUpdateStage().Execute(CreateContext(world));

        var pressure = world.Metrics.ReformPressureByCountry["albion"];
        Assert.InRange(pressure, 0m, 100m);
        Assert.True(pressure > 40m);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(1),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static WorldState CreateWorld(DateOnly date)
    {
        const string countryId = "albion";
        const string provinceId = "northshire";
        const string popId = "pop-1";

        return new WorldState
        {
            Seed = 1,
            Date = new GameDate(date),
            Countries = new Dictionary<string, CountryState>
            {
                [countryId] = new()
                {
                    Id = countryId,
                    DisplayName = "Albion",
                    ProvinceIds = [provinceId],
                    Treasury = 5000m,
                    TaxRate = 0.20m,
                    TariffRate = 0m,
                    EducationSpending = 0.5m,
                    IsPlayable = true
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                [provinceId] = new()
                {
                    Id = provinceId,
                    DisplayName = "Northshire",
                    OwnerId = countryId,
                    PopulationIds = [popId]
                }
            },
            Pops = new Dictionary<string, PopState>
            {
                [popId] = new()
                {
                    Id = popId,
                    ProvinceId = provinceId,
                    PopClass = "farmers",
                    Size = 1_000,
                    CashReserve = 10m,
                    Literacy = 0.30m,
                    Militancy = 0.20m,
                    Consciousness = 0.20m,
                    NeedsFulfillment = 1m,
                    Needs = new PopNeedProfile
                    {
                        Life = new Dictionary<string, decimal>
                        {
                            ["grain"] = 0.5m,
                            ["fish"] = 0.2m
                        }
                    }
                }
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 1m, "food"),
                ["fish"] = new("fish", "Fish", 1.2m, "food")
            },
            Market = new MarketState
            {
                Prices =
                {
                    ["grain"] = 1m,
                    ["fish"] = 1.2m
                }
            }
        };
    }
}
