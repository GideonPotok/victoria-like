using VictoriaLike.Core.Simulation.Queries;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class LogSummaryStage : ISimulationStage
{
    private readonly WorldSummaryQuery _summaryQuery = new();

    public string Name => "log-summary";

    public void Execute(SimulationContext context)
    {
        context.Log.Info(_summaryQuery.Build(context.World));
    }
}
