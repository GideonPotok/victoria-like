using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class NationalDistributionStage : ISimulationStage
{
    public string Name => "national-distribution";

    public void Execute(SimulationContext context)
    {
        context.World.Market.ImportsLastTick.Clear();
        context.World.Market.ExportsLastTick.Clear();

        foreach (var country in context.World.Countries.Values)
        {
            foreach (var provinceId in country.ProvinceIds)
            {
                var province = context.World.Provinces[provinceId];

                foreach (var stockpileEntry in province.Stockpile.ToList())
                {
                    var reserve = Math.Max(8m, stockpileEntry.Value * 0.35m);
                    var exportable = Math.Max(0m, stockpileEntry.Value - reserve);

                    if (exportable <= 0m)
                    {
                        continue;
                    }

                    province.Stockpile[stockpileEntry.Key] -= exportable;
                    country.Stockpile[stockpileEntry.Key] = country.Stockpile.GetValueOrDefault(stockpileEntry.Key) + exportable;
                    context.World.Market.ExportsLastTick[stockpileEntry.Key] =
                        context.World.Market.ExportsLastTick.GetValueOrDefault(stockpileEntry.Key) + exportable;
                }

                foreach (var popId in province.PopulationIds)
                {
                    var pop = context.World.Pops[popId];
                    var needs = pop.Needs.Life.Concat(pop.Needs.Everyday);
                    foreach (var need in needs)
                    {
                        var required = need.Value * (pop.Size / 1000m);
                        var local = province.Stockpile.GetValueOrDefault(need.Key);

                        if (local >= required * 0.9m)
                        {
                            continue;
                        }

                        var shortfall = required - local;
                        var national = country.Stockpile.GetValueOrDefault(need.Key);
                        var imported = Math.Min(shortfall, national);

                        if (imported <= 0m)
                        {
                            continue;
                        }

                        province.Stockpile[need.Key] = local + imported;
                        country.Stockpile[need.Key] = national - imported;
                        context.World.Market.ImportsLastTick[need.Key] =
                            context.World.Market.ImportsLastTick.GetValueOrDefault(need.Key) + imported;
                    }
                }
            }
        }
    }
}
