using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class PopNeedsStage : ISimulationStage
{
    public string Name => "pop-needs";

    public void Execute(SimulationContext context)
    {
        context.World.Market.ConsumptionLastTick.Clear();
        var fulfilledTotal = 0m;
        var unmetCount = 0;

        foreach (var pop in context.World.Pops.Values)
        {
            var province = context.World.Provinces[pop.ProvinceId];
            var country = context.World.Countries[province.OwnerId];

            PayWeeklyIncomeAndTaxes(country, pop);

            var life = BuyNeeds(context, province, pop, pop.Needs.Life);
            var everyday = BuyNeeds(context, province, pop, pop.Needs.Everyday);
            var luxury = BuyNeeds(context, province, pop, pop.Needs.Luxury);

            pop.LifeNeedsFulfillment = life.fulfillment;
            pop.EverydayNeedsFulfillment = everyday.fulfillment;
            pop.LuxuryNeedsFulfillment = luxury.fulfillment;
            var required = life.required + everyday.required + luxury.required;
            pop.NeedsFulfillment = required <= 0m
                ? 1m
                : (life.satisfied + everyday.satisfied + luxury.satisfied) / required;
            fulfilledTotal += pop.NeedsFulfillment;

            if (pop.NeedsFulfillment < 0.85m)
            {
                pop.Militancy = Math.Min(10m, pop.Militancy + 0.01m);
                unmetCount++;
            }
            else
            {
                pop.Militancy = Math.Max(0m, pop.Militancy - 0.005m);
            }
        }

        context.World.Metrics.AverageNeedsFulfilled =
            context.World.Pops.Count == 0 ? 1m : fulfilledTotal / context.World.Pops.Count;
        context.World.Metrics.UnmetPopCount = unmetCount;
    }

    private static void PayWeeklyIncomeAndTaxes(CountryState country, PopState pop)
    {
        var grossIncome = WeeklyIncome(pop);
        var taxRate = NormalizeRate(TaxRateForPop(country, pop));
        var tax = grossIncome * taxRate;

        pop.CashReserve = ScalarMath.Clamp(pop.CashReserve + grossIncome - tax, 0m, 100_000m);
        country.Treasury += tax;
    }

    private static decimal WeeklyIncome(PopState pop)
    {
        var baseWage = pop.PopClass switch
        {
            "aristocrats" => 9.0m,
            "capitalists" => 8.0m,
            "bureaucrats" => 5.0m,
            "clerks" => 5.5m,
            "clergy" => 4.5m,
            "artisans" => 4.0m,
            "craftsmen" => 3.2m,
            "soldiers" => 2.4m,
            "laborers" => 2.2m,
            "farmers" => 2.0m,
            _ => 2.0m
        };

        var employedShare = pop.Size <= 0 ? 0m : (decimal)pop.EmployedCount / pop.Size;
        var serviceEmployment = IsServiceOrPropertyPop(pop.PopClass) ? 1m : employedShare;
        var unemploymentFloor = pop.UnemployedCount > 0 ? 0.15m : 0m;
        var laborModifier = Math.Max(unemploymentFloor, serviceEmployment);
        var literacyBonus = 1m + (pop.Literacy * 0.25m);
        var monthlyIncome = (pop.Size / 1000m) * baseWage * laborModifier * literacyBonus;
        return monthlyIncome / 4m;
    }

    private static bool IsServiceOrPropertyPop(string popClass) =>
        popClass is "soldiers" or "clergy" or "bureaucrats" or "aristocrats" or "capitalists";

    private static decimal TaxRateForPop(CountryState country, PopState pop)
    {
        var fallback = country.TaxRate;
        return StrataFor(pop.PopClass) switch
        {
            "middle" => country.MiddleTaxRate < 0m ? fallback : country.MiddleTaxRate,
            "rich" => country.RichTaxRate < 0m ? fallback : country.RichTaxRate,
            _ => country.PoorTaxRate < 0m ? fallback : country.PoorTaxRate
        };
    }

    private static string StrataFor(string popClass) =>
        popClass.Trim().ToLowerInvariant() switch
        {
            "clerks" or "clergy" or "bureaucrats" or "artisans" => "middle",
            "aristocrats" or "capitalists" => "rich",
            _ => "poor"
        };

    private static decimal NormalizeRate(decimal rate) =>
        rate <= 1m
            ? ScalarMath.Clamp(rate, 0m, 1m)
            : ScalarMath.Clamp(rate / 100m, 0m, 1m);

    private static (decimal required, decimal satisfied, decimal fulfillment) BuyNeeds(
        SimulationContext context,
        VictoriaLike.Core.Core.World.ProvinceState province,
        PopState pop,
        IReadOnlyDictionary<string, decimal> needs)
    {
        var required = needs.Sum(entry => entry.Value * (pop.Size / 1000m));
        var satisfied = 0m;

        foreach (var need in needs)
        {
            var amountNeeded = need.Value * (pop.Size / 1000m);
            var available = province.Stockpile.GetValueOrDefault(need.Key);
            var price = Math.Max(0.01m, context.World.Market.Prices.GetValueOrDefault(need.Key, 1m));
            var affordable = pop.CashReserve / price;
            var purchased = Math.Min(Math.Min(available, amountNeeded), affordable);
            var cost = purchased * price;

            province.Stockpile[need.Key] = available - purchased;
            pop.CashReserve = ScalarMath.Clamp(pop.CashReserve - cost, 0m, 100_000m);
            context.World.Market.ConsumptionLastTick[need.Key] =
                context.World.Market.ConsumptionLastTick.GetValueOrDefault(need.Key) + purchased;
            satisfied += purchased;
        }

        return (required, satisfied, required <= 0m ? 1m : satisfied / required);
    }
}
