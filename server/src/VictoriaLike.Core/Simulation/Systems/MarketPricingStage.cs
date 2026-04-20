using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class MarketPricingStage : ISimulationStage
{
    public string Name => "market-pricing";

    public void Execute(SimulationContext context)
    {
        context.World.Market.SupplyLastTick.Clear();
        context.World.Market.DemandLastTick.Clear();
        context.World.Market.PricePressureLastTick.Clear();
        context.World.Market.UnmetDemandLastTick.Clear();
        context.World.Market.TradeValueLastTick = 0m;

        foreach (var province in context.World.Provinces.Values)
        {
            foreach (var stockpileEntry in province.Stockpile)
            {
                context.World.Market.SupplyLastTick[stockpileEntry.Key] =
                    context.World.Market.SupplyLastTick.GetValueOrDefault(stockpileEntry.Key) + stockpileEntry.Value;
            }
        }

        foreach (var country in context.World.Countries.Values)
        {
            foreach (var stockpileEntry in country.Stockpile)
            {
                context.World.Market.SupplyLastTick[stockpileEntry.Key] =
                    context.World.Market.SupplyLastTick.GetValueOrDefault(stockpileEntry.Key) + stockpileEntry.Value;
            }
        }

        foreach (var pop in context.World.Pops.Values)
        {
            foreach (var need in pop.Needs.Life.Concat(pop.Needs.Everyday).Concat(pop.Needs.Luxury))
            {
                context.World.Market.DemandLastTick[need.Key] =
                    context.World.Market.DemandLastTick.GetValueOrDefault(need.Key) + (need.Value * (pop.Size / 1000m));
            }
        }

        foreach (var good in context.World.Goods.Values)
        {
            var supply = Math.Max(1m, context.World.Market.SupplyLastTick.GetValueOrDefault(good.Id));
            var demand = Math.Max(1m, context.World.Market.DemandLastTick.GetValueOrDefault(good.Id));
            var pressure = demand / supply;
            var importPressure = 1m + (context.World.Market.ImportsLastTick.GetValueOrDefault(good.Id) * 0.015m);
            var exportRelief = Math.Max(0.8m, 1m - (context.World.Market.ExportsLastTick.GetValueOrDefault(good.Id) * 0.005m));
            var targetPrice = ScalarMath.Clamp(good.BasePrice * pressure * importPressure * exportRelief, 0.5m, good.BasePrice * 5m);
            var previousPrice = context.World.Market.Prices.GetValueOrDefault(good.Id, good.BasePrice);
            var maxStep = Math.Max(0.05m, previousPrice * 0.15m);
            var priceDelta = ScalarMath.Clamp(targetPrice - previousPrice, -maxStep, maxStep);
            var price = ScalarMath.Clamp(previousPrice + priceDelta, 0.5m, good.BasePrice * 5m);
            context.World.Market.Prices[good.Id] = price;
            context.World.Market.PricePressureLastTick[good.Id] = pressure;
            context.World.Market.UnmetDemandLastTick[good.Id] = Math.Max(0m, demand - supply);
            context.World.Market.TradeValueLastTick += context.World.Market.ImportsLastTick.GetValueOrDefault(good.Id) * price;
        }
    }
}
