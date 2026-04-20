using System.Collections.Generic;
using System.Linq;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class BuildingConstructionStage : ISimulationStage
{
    public string Name => "building_construction";

    public void Execute(SimulationContext context)
    {
        foreach (var entry in context.World.BuildingQueue)
            entry.TicksRemaining--;

        var completed = context.World.BuildingQueue
            .Where(e => e.TicksRemaining <= 0)
            .ToList();

        foreach (var entry in completed)
        {
            context.World.BuildingQueue.Remove(entry);

            if (!BuildingTemplates.All.TryGetValue(entry.BuildingType, out var template)) continue;
            if (!context.World.Provinces.TryGetValue(entry.ProvinceId, out var province)) continue;

            if (template.InfrastructureDelta != 0m)
                province.Infrastructure = Math.Max(0m, province.Infrastructure + template.InfrastructureDelta);

            if (template.Factory is not null)
            {
                var factoryId = $"{entry.ProvinceId}:{template.Type}:{entry.Id}";
                context.World.Factories[factoryId] = new FactoryState
                {
                    Id = factoryId,
                    CountryId = entry.CountryId,
                    ProvinceId = entry.ProvinceId,
                    Type = template.Factory.Type,
                    InputGoods = new Dictionary<string, decimal>(template.Factory.InputGoods),
                    OutputGood = template.Factory.OutputGood,
                    OutputPerTick = template.Factory.OutputPerTick
                };
            }
            else
            {
                foreach (var (good, output) in template.OutputPerTick)
                {
                    province.OutputsPerTick.TryGetValue(good, out var current);
                    province.OutputsPerTick[good] = current + output;
                }
            }

            context.World.Metrics.CompletedBuildingProvinceIds.Add(entry.ProvinceId);
            context.World.EventLog.Add(
                $"Building '{entry.BuildingType}' completed in {province.DisplayName}");
        }
    }
}
