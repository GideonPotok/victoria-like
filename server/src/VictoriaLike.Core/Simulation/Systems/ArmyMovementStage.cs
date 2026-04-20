using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class ArmyMovementStage : ISimulationStage
{
    public string Name => "army-movement";

    public void Execute(SimulationContext context)
    {
        foreach (var army in context.World.Armies.Values.OrderBy(army => army.Id, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(army.DestinationProvinceId))
                continue;

            if (army.MovementTicksRemaining > 0)
                army.MovementTicksRemaining--;

            if (army.MovementTicksRemaining > 0)
                continue;

            if (!context.World.Provinces.ContainsKey(army.DestinationProvinceId))
            {
                context.World.EventLog.Add($"army-move-cancelled:{army.Id}:missing-destination:{army.DestinationProvinceId}");
                army.DestinationProvinceId = null;
                army.MovementTicksRemaining = 0;
                continue;
            }

            var previous = army.LocationProvinceId;
            army.LocationProvinceId = army.DestinationProvinceId;
            army.DestinationProvinceId = null;
            army.MovementTicksRemaining = 0;
            context.World.EventLog.Add($"army-arrived:{army.Id}:{previous}->{army.LocationProvinceId}");
        }
    }
}
