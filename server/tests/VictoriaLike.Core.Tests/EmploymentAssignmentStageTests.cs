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

public sealed class EmploymentAssignmentStageTests
{
    [Fact]
    public void EmploymentAssignment_AssignsFarmersToFarmRgoAndTracksUnemployment()
    {
        var world = CreateWorld();
        world.Pops["farmers"].Size = 5_000;

        new EmploymentAssignmentStage().Execute(CreateContext(world));

        Assert.Equal(4_000, world.Pops["farmers"].EmployedCount);
        Assert.Equal(1_000, world.Pops["farmers"].UnemployedCount);
        Assert.Equal(0, world.Pops["laborers"].EmployedCount);
        Assert.Equal(4_000, world.Pops["laborers"].UnemployedCount);
    }

    [Fact]
    public void EmploymentAssignment_AssignsCraftsmenAndClerksToFactoryCapacity()
    {
        var world = CreateWorld();
        world.Provinces["northshire"].PopulationIds.AddRange(["craftsmen", "clerks"]);
        world.Pops["craftsmen"] = CreatePop("craftsmen", "craftsmen", size: 1_500);
        world.Pops["clerks"] = CreatePop("clerks", "clerks", size: 400);
        world.Factories["steel-1"] = new FactoryState
        {
            Id = "steel-1",
            CountryId = "albion",
            ProvinceId = "northshire",
            Type = "steel_mill",
            Level = 1,
            OutputGood = "steel",
            OutputPerTick = 10m
        };

        new EmploymentAssignmentStage().Execute(CreateContext(world));

        Assert.Equal(1_000, world.Factories["steel-1"].EmployedCraftsmen);
        Assert.Equal(250, world.Factories["steel-1"].EmployedClerks);
        Assert.Equal(1_000, world.Pops["craftsmen"].EmployedCount);
        Assert.Equal(500, world.Pops["craftsmen"].UnemployedCount);
        Assert.Equal(250, world.Pops["clerks"].EmployedCount);
        Assert.Equal(150, world.Pops["clerks"].UnemployedCount);
    }

    [Fact]
    public void EmploymentAssignment_LeavesArtisansAndStateWorkersFullyEmployed()
    {
        var world = CreateWorld();
        world.Provinces["northshire"].PopulationIds.AddRange(["artisans", "soldiers", "clergy", "bureaucrats"]);
        world.Pops["artisans"] = CreatePop("artisans", "artisans", size: 800);
        world.Pops["soldiers"] = CreatePop("soldiers", "soldiers", size: 600);
        world.Pops["clergy"] = CreatePop("clergy", "clergy", size: 120);
        world.Pops["bureaucrats"] = CreatePop("bureaucrats", "bureaucrats", size: 90);

        new EmploymentAssignmentStage().Execute(CreateContext(world));

        Assert.Equal(800, world.Pops["artisans"].EmployedCount);
        Assert.Equal(600, world.Pops["soldiers"].EmployedCount);
        Assert.Equal(120, world.Pops["clergy"].EmployedCount);
        Assert.Equal(90, world.Pops["bureaucrats"].EmployedCount);
        Assert.All(
            new[] { "artisans", "soldiers", "clergy", "bureaucrats" },
            popId => Assert.Equal(0, world.Pops[popId].UnemployedCount));
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
                ["farmers"] = CreatePop("farmers", "farmers", size: 4_000),
                ["laborers"] = CreatePop("laborers", "laborers", size: 4_000)
            },
            Goods = new Dictionary<string, GoodDefinition>
            {
                ["grain"] = new("grain", "Grain", 1m, "food"),
                ["steel"] = new("steel", "Steel", 10m, "industrial")
            },
            Market = new MarketState
            {
                Prices =
                {
                    ["grain"] = 1m,
                    ["steel"] = 10m
                }
            }
        };

    private static PopState CreatePop(string id, string popClass, int size) =>
        new()
        {
            Id = id,
            ProvinceId = "northshire",
            PopClass = popClass,
            Size = size,
            Needs = new PopNeedProfile()
        };
}
