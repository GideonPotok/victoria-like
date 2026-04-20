using System;
using System.Collections.Generic;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class BuildingConstructionStageTests
{
    [Fact]
    public void RailroadCompletionImprovesInfrastructureWithoutChangingRgoOutput()
    {
        var world = CreateWorld();
        world.Provinces["province-a"] = new ProvinceState
        {
            Id = "province-a",
            DisplayName = "Albion 1",
            OwnerId = "country-a",
            Infrastructure = 1.0m,
            OutputsPerTick = { ["grain"] = 5m }
        };
        world.BuildingQueue.Add(new BuildingQueueEntry
        {
            Id = "build-1",
            ProvinceId = "province-a",
            CountryId = "country-a",
            BuildingType = "railroad",
            TicksRemaining = 1,
            QueuedAt = DateTime.UtcNow
        });

        new BuildingConstructionStage().Execute(CreateContext(world));

        Assert.Empty(world.BuildingQueue);
        Assert.Equal(1.20m, world.Provinces["province-a"].Infrastructure);
        Assert.Equal(5m, world.Provinces["province-a"].OutputsPerTick["grain"]);
        Assert.Empty(world.Factories);
    }

    [Fact]
    public void FactoryCompletionCreatesFactoryInsteadOfPrimaryGoodsOutput()
    {
        var world = CreateWorld();
        world.Provinces["province-a"] = new ProvinceState
        {
            Id = "province-a",
            DisplayName = "Albion 1",
            OwnerId = "country-a",
            Infrastructure = 1.0m,
            OutputsPerTick = { ["iron"] = 4m }
        };
        world.BuildingQueue.Add(new BuildingQueueEntry
        {
            Id = "build-1",
            ProvinceId = "province-a",
            CountryId = "country-a",
            BuildingType = "tools_factory",
            TicksRemaining = 1,
            QueuedAt = DateTime.UtcNow
        });

        new BuildingConstructionStage().Execute(CreateContext(world));

        var factory = Assert.Single(world.Factories.Values);
        Assert.Equal("tools_factory", factory.Type);
        Assert.Equal("province-a", factory.ProvinceId);
        Assert.Equal("country-a", factory.CountryId);
        Assert.Equal("tools", factory.OutputGood);
        Assert.Equal(0.5m, factory.InputGoods["iron"]);
        Assert.False(world.Provinces["province-a"].OutputsPerTick.ContainsKey("tools"));
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(0),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static WorldState CreateWorld() =>
        new()
        {
            Seed = 0,
            Date = new GameDate(new DateOnly(1800, 1, 1)),
            Countries = new Dictionary<string, VictoriaLike.Core.Core.Countries.CountryState>(),
            Provinces = new Dictionary<string, ProvinceState>(),
            PlayerAccounts = new Dictionary<string, PlayerAccount>(),
            Pops = new Dictionary<string, VictoriaLike.Core.Core.Pops.PopState>(),
            Factories = new Dictionary<string, FactoryState>(),
            Goods = new Dictionary<string, GoodDefinition>(),
            Market = new MarketState(),
            Metrics = new SimulationMetrics(),
            BuildingQueue = new List<BuildingQueueEntry>(),
            EventLog = new List<string>()
        };
}
