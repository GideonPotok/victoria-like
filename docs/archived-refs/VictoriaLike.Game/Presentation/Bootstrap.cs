using Godot;
using VictoriaLike.Core.Data.Loaders;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Queries;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Game.Presentation.ViewModels;

namespace VictoriaLike.Game.Presentation;

public partial class Bootstrap : Node
{
    public override void _Ready()
    {
        var contentRoot = ProjectSettings.GlobalizePath("res://content");
        var loader = new JsonContentLoader();
        var goods = loader.LoadGoods(Path.Combine(contentRoot, "goods.json"));
        var world = loader.LoadScenario(Path.Combine(contentRoot, "scenarios", "phase1-albion.json"), goods);
        new WorldValidator().Validate(world);

        var orchestrator = new SimulationOrchestrator(
        [
            new AdvanceDateStage(),
            new ProvinceProductionStage(),
            new NationalDistributionStage(),
            new MarketPricingStage(),
            new PopNeedsStage(),
            new BudgetStage(),
            new LogSummaryStage(),
        ]);

        var initialSummary = new WorldSummaryQuery().Build(world);
        GD.Print(initialSummary);

        for (var index = 0; index < 8; index++)
        {
            orchestrator.RunTick(world);
        }

        var viewModel = WorldSummaryViewModel.FromWorld(world);
        GD.Print(viewModel.ToMultilineString());

        // TODO: Bind the view model into Control-based panels instead of printing summaries.
    }
}
