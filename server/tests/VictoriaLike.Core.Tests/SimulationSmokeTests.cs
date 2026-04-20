using Xunit;
using System.Diagnostics;
using System.Text.Json;
using VictoriaLike.Core.Data.Loaders;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;

namespace VictoriaLike.Core.Tests;

public sealed class SimulationSmokeTests
{
    private static readonly string ContentRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content"));

    [Fact]
    public void ScenarioLoadsAndValidates()
    {
        var loader = new JsonContentLoader();
        var goods = loader.LoadGoods(Path.Combine(ContentRoot, "goods.json"));
        var world = loader.LoadScenario(Path.Combine(ContentRoot, "scenarios", "phase1-albion.json"), goods);

        new WorldValidator().Validate(world);

        Assert.Single(world.Countries.Values.Where(country => country.IsPlayable));
        Assert.NotEmpty(world.Provinces);
        Assert.NotEmpty(world.Pops);
    }

    [Fact]
    public void WeeklyTickMovesTreasuryAndNeeds()
    {
        var loader = new JsonContentLoader();
        var goods = loader.LoadGoods(Path.Combine(ContentRoot, "goods.json"));
        var world = loader.LoadScenario(Path.Combine(ContentRoot, "scenarios", "phase1-albion.json"), goods);

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

        orchestrator.RunTick(world);

        var albion = world.Countries["albion"];
        Assert.NotEqual(5000m, albion.Treasury);
        Assert.True(world.Metrics.AverageNeedsFulfilled >= 0m);
        Assert.True(world.Market.Prices["grain"] > 0m);
    }

    [Fact]
    public void MediumScenarioRunsOneSimulatedYearWithValidInvariants()
    {
        var loader = new JsonContentLoader();
        var goods = loader.LoadGoods(Path.Combine(ContentRoot, "goods.json"));
        var loadSw = Stopwatch.StartNew();
        var world = loader.LoadScenario(Path.Combine(ContentRoot, "scenarios", "medium-8country-core.json"), goods);
        loadSw.Stop();

        new WorldValidator().Validate(world);
        var initialSnapshotBytes = JsonSerializer.SerializeToUtf8Bytes(world).Length;

        Assert.InRange(world.Countries.Count, 6, 10);
        Assert.InRange(world.Provinces.Count, 50, 100);
        Assert.NotEmpty(world.Factories);
        Assert.NotEmpty(world.Armies);
        Assert.Contains(world.Wars.Values, war => war.IsActive);
        Assert.True(loadSw.ElapsedMilliseconds < 1_000, $"Medium core scenario load took {loadSw.ElapsedMilliseconds}ms");
        Assert.True(initialSnapshotBytes < 2_000_000, $"Initial medium scenario snapshot was {initialSnapshotBytes} bytes");

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
            new ArmyMovementStage(),
            new BattleResolutionStage(),
        ]);

        var invariantChecker = new WorldInvariantChecker();
        var simulationSw = Stopwatch.StartNew();
        for (var tick = 0; tick < 48; tick++)
        {
            orchestrator.RunTick(world);
            if ((tick + 1) % 4 == 0)
                invariantChecker.ThrowIfInvalid(world);
        }
        simulationSw.Stop();

        var finalSnapshotBytes = JsonSerializer.SerializeToUtf8Bytes(world).Length;

        Assert.True(world.Date.Value >= new DateOnly(1836, 12, 01), $"Expected about one simulated year, got {world.Date.Value}");
        Assert.True(world.Metrics.AverageNeedsFulfilled >= 0m);
        Assert.True(world.Market.Prices["grain"] > 0m);
        Assert.True(simulationSw.ElapsedMilliseconds < 2_000, $"One-year medium simulation took {simulationSw.ElapsedMilliseconds}ms");
        Assert.True(finalSnapshotBytes < 2_500_000, $"Final medium scenario snapshot was {finalSnapshotBytes} bytes");
    }
}
