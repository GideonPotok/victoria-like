namespace VictoriaLike.Core.Simulation.TickPipeline;

public interface ISimulationStage
{
    string Name { get; }
    void Execute(SimulationContext context);
}
