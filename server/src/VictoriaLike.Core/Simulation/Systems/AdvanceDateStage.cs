using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class AdvanceDateStage : ISimulationStage
{
    public string Name => "advance-date";

    public void Execute(SimulationContext context)
    {
        context.World.Date = context.World.Date.AdvanceWeeks();
    }
}
