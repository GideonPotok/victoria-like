using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class ProvinceProductionStage : ISimulationStage
{
    public string Name => "province-production";

    public void Execute(SimulationContext context)
    {
        context.World.Market.ProductionLastTick.Clear();

        foreach (var province in context.World.Provinces.Values)
        {
            var employedRgoWorkers = province.PopulationIds
                .Select(popId => context.World.Pops[popId])
                .Where(pop => IsRgoWorkerForProvince(province.RgoType, pop.PopClass))
                .Sum(pop => pop.EmployedCount);

            var laborModifier = Math.Min(1.35m, employedRgoWorkers / 4000m);
            var infrastructureModifier = 1m + (province.Infrastructure * 0.25m);

            foreach (var output in province.OutputsPerTick)
            {
                var quantity = output.Value * laborModifier * infrastructureModifier;
                if (quantity <= 0m)
                {
                    continue;
                }

                province.Stockpile[output.Key] = province.Stockpile.GetValueOrDefault(output.Key) + quantity;
                context.World.Market.ProductionLastTick[output.Key] =
                    context.World.Market.ProductionLastTick.GetValueOrDefault(output.Key) + quantity;
            }
        }
    }

    private static bool IsRgoWorkerForProvince(string rgoType, string popClass)
    {
        var normalizedPop = popClass.Trim().ToLowerInvariant();
        if (rgoType.Contains("farm", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPop == "farmers";
        }

        return normalizedPop == "laborers";
    }
}
