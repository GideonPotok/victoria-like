using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class MonthlyPopUpdateStage : ISimulationStage
{
    public string Name => "monthly-pop-update";

    public void Execute(SimulationContext context)
    {
        if (context.World.Date.Value.Day != 1)
        {
            return;
        }

        var updated = 0;
        foreach (var pop in context.World.Pops.Values.ToList())
        {
            if (!context.World.Provinces.TryGetValue(pop.ProvinceId, out var province) ||
                !context.World.Countries.TryGetValue(province.OwnerId, out var country))
            {
                continue;
            }

            pop.Literacy = ScalarMath.Clamp(pop.Literacy + LiteracyDrift(country, pop), 0m, 1m);
            pop.Militancy = ScalarMath.Clamp(pop.Militancy + MilitancyDrift(pop), 0m, 10m);
            pop.Consciousness = ScalarMath.Clamp(pop.Consciousness + ConsciousnessDrift(country, pop), 0m, 10m);

            // Day 65 only exposes a deterministic hook; actual class migration waits for employment.
            if (pop.NeedsFulfillment > 0.95m && pop.Literacy > 0.45m && pop.CashReserve > 25m)
            {
                context.World.EventLog.Add($"promotion-candidate:{pop.Id}");
                ApplyMobility(context, province, pop, PromotionTarget(pop.PopClass), "promotion");
            }
            else if (pop.NeedsFulfillment < 0.50m || pop.CashReserve <= 0.5m)
            {
                context.World.EventLog.Add($"demotion-risk:{pop.Id}");
                ApplyMobility(context, province, pop, DemotionTarget(pop.PopClass), "demotion");
            }

            updated++;
        }

        if (updated > 0)
        {
            RecalculateReformPressure(context);
            context.World.EventLog.Add($"monthly-pop-update:{context.World.Date.Value:yyyy-MM-dd}:{updated}");
        }
    }

    private static decimal LiteracyDrift(CountryState country, PopState pop)
    {
        var target = pop.PopClass switch
        {
            "clergy" => 0.80m,
            "clerks" => 0.70m,
            "bureaucrats" => 0.65m,
            "capitalists" => 0.60m,
            "artisans" => 0.50m,
            "craftsmen" => 0.45m,
            _ => 0.35m
        };

        var education = NormalizeRate(country.EducationSpending);
        var educationModifier = 0.5m + education;
        return (target - pop.Literacy) * 0.01m * educationModifier;
    }

    private static decimal MilitancyDrift(PopState pop)
    {
        var unemploymentShare = pop.Size <= 0 ? 0m : (decimal)pop.UnemployedCount / pop.Size;
        var unemploymentPressure = unemploymentShare * 0.08m;

        if (pop.NeedsFulfillment < 0.50m)
        {
            return 0.08m + unemploymentPressure;
        }

        if (pop.NeedsFulfillment < 0.85m)
        {
            return 0.03m + unemploymentPressure;
        }

        return -0.02m + unemploymentPressure;
    }

    private static decimal ConsciousnessDrift(CountryState country, PopState pop)
    {
        var education = NormalizeRate(country.EducationSpending);
        var literacyPressure = pop.Literacy * (0.03m + (education * 0.02m));
        var hardshipPressure = pop.NeedsFulfillment < 0.75m ? 0.02m : 0m;
        var unemploymentShare = pop.Size <= 0 ? 0m : (decimal)pop.UnemployedCount / pop.Size;
        return literacyPressure + hardshipPressure + (unemploymentShare * 0.03m) - 0.01m;
    }

    private static decimal NormalizeRate(decimal rate) =>
        rate <= 1m
            ? ScalarMath.Clamp(rate, 0m, 1m)
            : ScalarMath.Clamp(rate / 100m, 0m, 1m);

    private static string? PromotionTarget(string popClass) =>
        popClass switch
        {
            "farmers" or "laborers" => "craftsmen",
            "craftsmen" => "clerks",
            "clerks" => "capitalists",
            _ => null
        };

    private static string? DemotionTarget(string popClass) =>
        popClass switch
        {
            "capitalists" or "aristocrats" => "clerks",
            "clerks" or "artisans" => "craftsmen",
            "craftsmen" => "laborers",
            _ => null
        };

    private static void ApplyMobility(
        SimulationContext context,
        ProvinceState province,
        PopState source,
        string? targetClass,
        string movementType)
    {
        if (string.IsNullOrWhiteSpace(targetClass) || source.Size < 100)
        {
            return;
        }

        var moved = Math.Max(1, (int)Math.Floor(source.Size * 0.001m));
        moved = Math.Min(moved, source.Size - 1);
        if (moved <= 0)
        {
            return;
        }

        var target = province.PopulationIds
            .Select(id => context.World.Pops[id])
            .FirstOrDefault(pop => pop.PopClass == targetClass);

        if (target is null)
        {
            target = new PopState
            {
                Id = $"{province.Id}-{targetClass}-{context.World.Pops.Count + 1}",
                ProvinceId = province.Id,
                PopClass = targetClass,
                Size = 0,
                CashReserve = Math.Max(0m, source.CashReserve * 0.25m),
                Literacy = source.Literacy,
                Militancy = source.Militancy,
                Consciousness = source.Consciousness,
                NeedsFulfillment = source.NeedsFulfillment,
                LifeNeedsFulfillment = source.LifeNeedsFulfillment,
                EverydayNeedsFulfillment = source.EverydayNeedsFulfillment,
                LuxuryNeedsFulfillment = source.LuxuryNeedsFulfillment,
                Needs = PopNeedProfileCatalog.ForPopClass(targetClass)
            };
            context.World.Pops[target.Id] = target;
            province.PopulationIds.Add(target.Id);
        }

        source.Size -= moved;
        source.EmployedCount = Math.Min(source.EmployedCount, source.Size);
        source.UnemployedCount = Math.Min(source.UnemployedCount, Math.Max(0, source.Size - source.EmployedCount));

        target.Size += moved;
        target.UnemployedCount += moved;
        context.World.EventLog.Add($"{movementType}:{source.Id}:{target.PopClass}:{moved}");
    }

    private static void RecalculateReformPressure(SimulationContext context)
    {
        context.World.Metrics.ReformPressureByCountry.Clear();

        foreach (var country in context.World.Countries.Values)
        {
            var pops = country.ProvinceIds
                .Where(context.World.Provinces.ContainsKey)
                .SelectMany(provinceId => context.World.Provinces[provinceId].PopulationIds)
                .Where(context.World.Pops.ContainsKey)
                .Select(popId => context.World.Pops[popId])
                .ToList();

            var population = pops.Sum(pop => pop.Size);
            if (population <= 0)
            {
                context.World.Metrics.ReformPressureByCountry[country.Id] = 0m;
                continue;
            }

            var weightedPressure = pops.Sum(pop =>
            {
                var unemploymentShare = pop.Size <= 0 ? 0m : (decimal)pop.UnemployedCount / pop.Size;
                var unmetNeeds = 1m - pop.NeedsFulfillment;
                var pressure =
                    (pop.Militancy * 6m) +
                    (pop.Consciousness * 2m) +
                    (unemploymentShare * 20m) +
                    (unmetNeeds * 12m);
                return pressure * pop.Size;
            });

            context.World.Metrics.ReformPressureByCountry[country.Id] =
                ScalarMath.Clamp(weightedPressure / population, 0m, 100m);
        }
    }
}
