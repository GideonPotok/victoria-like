using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class FactoryProductionStage : ISimulationStage
{
    public string Name => "factory-production";

    public void Execute(SimulationContext context)
    {
        foreach (var factory in context.World.Factories.Values)
        {
            if (!context.World.Countries.TryGetValue(factory.CountryId, out var country))
            {
                continue;
            }

            var workforce = factory.EmployedCraftsmen + (factory.EmployedClerks * 2);
            var workforceModifier = Math.Min(1.25m, workforce / Math.Max(1m, factory.Level * 1000m));
            var maxOutput = factory.OutputPerTick * Math.Max(1, factory.Level) * workforceModifier;
            if (maxOutput <= 0m)
            {
                factory.ProfitLastTick = 0m;
                continue;
            }

            var inputModifier = 1m;
            foreach (var (good, requiredPerOutput) in factory.InputGoods)
            {
                if (requiredPerOutput <= 0m)
                {
                    continue;
                }

                var available = country.Stockpile.GetValueOrDefault(good);
                inputModifier = Math.Min(inputModifier, available / (requiredPerOutput * maxOutput));
            }

            var output = Math.Max(0m, maxOutput * Math.Min(1m, inputModifier));
            if (output <= 0m)
            {
                factory.ProfitLastTick = 0m;
                continue;
            }

            var inputCost = 0m;
            foreach (var (good, requiredPerOutput) in factory.InputGoods)
            {
                var consumed = requiredPerOutput * output;
                country.Stockpile[good] = country.Stockpile.GetValueOrDefault(good) - consumed;
                inputCost += consumed * context.World.Market.Prices.GetValueOrDefault(good, 1m);
            }

            country.Stockpile[factory.OutputGood] = country.Stockpile.GetValueOrDefault(factory.OutputGood) + output;
            context.World.Market.ProductionLastTick[factory.OutputGood] =
                context.World.Market.ProductionLastTick.GetValueOrDefault(factory.OutputGood) + output;

            var outputValue = output * context.World.Market.Prices.GetValueOrDefault(factory.OutputGood, 1m);
            factory.ProfitLastTick = outputValue - inputCost;
            factory.CashReserve = Math.Max(0m, factory.CashReserve + factory.ProfitLastTick);
        }
    }
}
