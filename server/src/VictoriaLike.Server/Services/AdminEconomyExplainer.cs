using System;
using System.Collections.Generic;
using System.Linq;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public static class AdminEconomyExplainer
{
    private static readonly IReadOnlyDictionary<string, decimal> LifeNeedsPerThousand = new Dictionary<string, decimal>
    {
        ["grain"] = 0.5m,
        ["fish"] = 0.2m
    };

    public static AdminMarketGoodDto ExplainGood(
        string goodId,
        string displayName,
        GoodDefinition? definition,
        decimal price,
        decimal previousPrice,
        decimal supply,
        decimal demand,
        WorldStateSnapshot? world,
        IReadOnlyList<MarketTickSnapshot> history)
    {
        var basePrice = definition?.BasePrice ?? price;
        var pressure = Math.Max(1m, demand) / Math.Max(1m, supply);
        var rawPressurePrice = basePrice * pressure;
        var minPrice = 0.5m;
        var maxPrice = basePrice * 5m;

        return new AdminMarketGoodDto
        {
            Id = goodId,
            Name = displayName,
            Price = price,
            PreviousPrice = previousPrice,
            BasePrice = basePrice,
            TargetPressure = pressure,
            Supply = supply,
            Demand = demand,
            UnmetDemand = Math.Max(0m, demand - supply),
            ClampApplied = rawPressurePrice < minPrice || rawPressurePrice > maxPrice || price <= minPrice || price >= maxPrice,
            LargestProducer = GetLargestProducer(world, goodId),
            LargestConsumer = GetLargestConsumer(world, goodId),
            FulfillmentRate = demand > 0 ? Math.Min(1m, supply / demand) : 1m,
            PriceDelta = price - previousPrice,
            PriceHistory = history.Select(h => h.Prices.GetValueOrDefault(goodId)).ToList()
        };
    }

    public static Dictionary<string, decimal> EstimateProvinceDemand(Province province)
    {
        var populationFactor = province.Population / 1000m;
        return LifeNeedsPerThousand.ToDictionary(
            need => need.Key,
            need => need.Value * populationFactor,
            StringComparer.OrdinalIgnoreCase);
    }

    private static string? GetLargestProducer(WorldStateSnapshot? world, string goodId)
    {
        return world?.Provinces
            .Select(province => new
            {
                province.Name,
                Output = province.OutputsPerTick.GetValueOrDefault(goodId)
            })
            .Where(row => row.Output > 0)
            .OrderByDescending(row => row.Output)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .Select(row => row.Name)
            .FirstOrDefault();
    }

    private static string? GetLargestConsumer(WorldStateSnapshot? world, string goodId)
    {
        return world?.Provinces
            .Select(province => new
            {
                province.Name,
                Demand = EstimateProvinceDemand(province).GetValueOrDefault(goodId)
            })
            .Where(row => row.Demand > 0)
            .OrderByDescending(row => row.Demand)
            .ThenBy(row => row.Name, StringComparer.Ordinal)
            .Select(row => row.Name)
            .FirstOrDefault();
    }
}
