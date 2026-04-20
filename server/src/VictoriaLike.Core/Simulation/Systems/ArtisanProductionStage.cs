using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class ArtisanProductionStage : ISimulationStage
{
    private static readonly IReadOnlyDictionary<string, Dictionary<string, decimal>> Recipes =
        new Dictionary<string, Dictionary<string, decimal>>(StringComparer.OrdinalIgnoreCase)
        {
            ["clothes"] = new(StringComparer.OrdinalIgnoreCase) { ["fabric"] = 0.40m },
            ["furniture"] = new(StringComparer.OrdinalIgnoreCase) { ["timber"] = 0.50m },
            ["tools"] = new(StringComparer.OrdinalIgnoreCase) { ["iron"] = 0.30m, ["coal"] = 0.20m },
            ["liquor"] = new(StringComparer.OrdinalIgnoreCase) { ["grain"] = 0.50m },
        };

    public string Name => "artisan-production";

    public void Execute(SimulationContext context)
    {
        var candidates = Recipes.Keys
            .Where(good => HasGood(context, good))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            return;
        }

        var artisanProfits = new List<(string goodId, decimal profit)>();

        foreach (var pop in context.World.Pops.Values.Where(IsArtisan))
        {
            if (!context.World.Provinces.TryGetValue(pop.ProvinceId, out var province) ||
                !context.World.Countries.TryGetValue(province.OwnerId, out var country))
            {
                continue;
            }

            EnsureArtisanGood(context, pop, candidates);

            if (pop.ArtisanDaysUntilReconsider <= 0)
            {
                pop.ArtisanProducedGood = ChooseArtisanGood(context, pop, candidates);
                pop.ArtisanLastReconsideredAt = context.World.Date.Value;
                pop.ArtisanDaysUntilReconsider = NextReconsiderDelay(context);
            }
            else
            {
                pop.ArtisanDaysUntilReconsider--;
            }

            var producedGood = pop.ArtisanProducedGood;
            if (string.IsNullOrWhiteSpace(producedGood) || !Recipes.TryGetValue(producedGood, out var recipe))
            {
                continue;
            }

            var employed = Math.Clamp(pop.EmployedCount, 0, pop.Size);
            if (employed <= 0)
            {
                pop.ArtisanProfitLastTick = 0m;
                continue;
            }

            var maxOutput = employed / 1000m * 0.45m;
            var inputModifier = 1m;
            foreach (var (inputGood, requiredPerOutput) in recipe)
            {
                if (requiredPerOutput <= 0m)
                {
                    continue;
                }

                var available = country.Stockpile.GetValueOrDefault(inputGood);
                inputModifier = Math.Min(inputModifier, available / Math.Max(0.0001m, requiredPerOutput * maxOutput));
            }

            var output = Math.Max(0m, maxOutput * Math.Min(1m, inputModifier));
            var inputCost = 0m;
            foreach (var (inputGood, requiredPerOutput) in recipe)
            {
                var consumed = requiredPerOutput * output;
                country.Stockpile[inputGood] = Math.Max(0m, country.Stockpile.GetValueOrDefault(inputGood) - consumed);
                inputCost += consumed * Price(context, inputGood);
            }

            if (output > 0m)
            {
                country.Stockpile[producedGood] = country.Stockpile.GetValueOrDefault(producedGood) + output;
                context.World.Market.ProductionLastTick[producedGood] =
                    context.World.Market.ProductionLastTick.GetValueOrDefault(producedGood) + output;
            }

            var revenue = output * Price(context, producedGood);
            var shortagePenalty = output <= 0m && recipe.Count > 0 ? employed / 1000m * 0.02m : 0m;
            var profit = revenue - inputCost - shortagePenalty;
            pop.ArtisanProfitLastTick = profit;
            pop.CashReserve = Math.Max(0m, pop.CashReserve + profit);
            artisanProfits.Add((producedGood, profit));
        }

        RecordProfitHistory(context, artisanProfits);
    }

    private static bool IsArtisan(PopState pop) =>
        string.Equals(pop.PopClass, "artisans", StringComparison.OrdinalIgnoreCase);

    private static void EnsureArtisanGood(SimulationContext context, PopState pop, IReadOnlyList<string> candidates)
    {
        if (!string.IsNullOrWhiteSpace(pop.ArtisanProducedGood) &&
            candidates.Contains(pop.ArtisanProducedGood, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        pop.ArtisanProducedGood = ChooseArtisanGood(context, pop, candidates);
        pop.ArtisanDaysUntilReconsider = pop.ArtisanDaysUntilReconsider > 0
            ? pop.ArtisanDaysUntilReconsider
            : NextReconsiderDelay(context);
    }

    private static string ChooseArtisanGood(SimulationContext context, PopState pop, IReadOnlyList<string> candidates)
    {
        var lookbackMonths = NextInt(context, 2, 6);
        var recentMonths = RecentMonthKeys(context.World.Date.Value, lookbackMonths).ToHashSet(StringComparer.Ordinal);
        var currentGood = pop.ArtisanProducedGood;

        var bestGood = candidates[0];
        var bestScore = decimal.MinValue;
        foreach (var candidate in candidates)
        {
            var matching = context.World.GoodProfitHistory
                .Where(entry => string.Equals(entry.GoodId, candidate, StringComparison.OrdinalIgnoreCase) &&
                                recentMonths.Contains(entry.Month) &&
                                entry.ProducerCount > 0)
                .ToList();

            var score = matching.Count == 0
                ? 0m
                : matching.Average(entry => entry.AverageProducerProfit);

            if (string.Equals(candidate, currentGood, StringComparison.OrdinalIgnoreCase))
            {
                score += Math.Abs(score) * 0.20m + 0.01m;
            }

            if (score > bestScore)
            {
                bestGood = candidate;
                bestScore = score;
            }
        }

        return bestGood;
    }

    private static void RecordProfitHistory(SimulationContext context, IReadOnlyList<(string goodId, decimal profit)> profits)
    {
        var month = MonthKey(context.World.Date.Value);
        foreach (var group in profits.GroupBy(item => item.goodId, StringComparer.OrdinalIgnoreCase))
        {
            var existing = context.World.GoodProfitHistory.FirstOrDefault(entry =>
                string.Equals(entry.Month, month, StringComparison.Ordinal) &&
                string.Equals(entry.GoodId, group.Key, StringComparison.OrdinalIgnoreCase));

            if (existing == null)
            {
                context.World.GoodProfitHistory.Add(new GoodProfitHistoryEntry
                {
                    Month = month,
                    GoodId = group.Key,
                    AverageProducerProfit = group.Average(item => item.profit),
                    ProducerCount = group.Count()
                });
            }
            else
            {
                existing.AverageProducerProfit = group.Average(item => item.profit);
                existing.ProducerCount = group.Count();
            }
        }

        var keepMonths = RecentMonthKeys(context.World.Date.Value, 12).ToHashSet(StringComparer.Ordinal);
        context.World.GoodProfitHistory.RemoveAll(entry => !keepMonths.Contains(entry.Month));
    }

    private static bool HasGood(SimulationContext context, string goodId) =>
        context.World.Goods.ContainsKey(goodId) || context.World.Market.Prices.ContainsKey(goodId);

    private static decimal Price(SimulationContext context, string goodId) =>
        context.World.Market.Prices.GetValueOrDefault(
            goodId,
            context.World.Goods.TryGetValue(goodId, out var good) ? good.BasePrice : 1m);

    private static int NextReconsiderDelay(SimulationContext context) =>
        NextInt(context, 21, 63);

    private static int NextInt(SimulationContext context, int minInclusive, int maxInclusive) =>
        (int)Math.Floor(context.Random.NextDecimal(minInclusive, maxInclusive + 0.999m));

    private static IEnumerable<string> RecentMonthKeys(DateOnly date, int count)
    {
        var cursor = new DateOnly(date.Year, date.Month, 1);
        for (var i = 0; i < count; i++)
        {
            yield return MonthKey(cursor);
            cursor = cursor.AddMonths(-1);
        }
    }

    private static string MonthKey(DateOnly date) => $"{date.Year:D4}-{date.Month:D2}";
}
