using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class BudgetStage : ISimulationStage
{
    public string Name => "budget";

    public void Execute(SimulationContext context)
    {
        context.World.Metrics.TreasuryDeltaByCountry.Clear();

        foreach (var country in context.World.Countries.Values)
        {
            var pops = country.ProvinceIds
                .SelectMany(provinceId => context.World.Provinces[provinceId].PopulationIds)
                .Select(popId => context.World.Pops[popId])
                .ToList();
            var population = pops.Sum(pop => pop.Size);

            var tariffIncome = country.Stockpile.Values.Sum() * country.TariffRate * 0.01m;
            var tradeTax = context.World.Market.TradeValueLastTick * country.TariffRate * 0.015m;
            var spendingCost = WeeklySpendingCost(country, population);
            var drift = context.Random.NextDecimal(-1.5m, 1.5m);
            var change = tariffIncome + tradeTax - spendingCost + drift;

            country.Treasury += change;
            context.World.Metrics.TreasuryDeltaByCountry[country.Id] = change;
            ApplySpendingEffects(country, pops);
        }
    }

    private static decimal WeeklySpendingCost(CountryState country, int population)
    {
        var populationScale = population / 1000m;
        var education = NormalizeRate(country.EducationSpending);
        var military = NormalizeRate(country.MilitarySpending);
        var administration = NormalizeRate(country.AdministrationSpending);
        return populationScale * ((education * 0.10m) + (military * 0.12m) + (administration * 0.08m));
    }

    private static void ApplySpendingEffects(CountryState country, IReadOnlyList<PopState> pops)
    {
        var education = NormalizeRate(country.EducationSpending);
        var military = NormalizeRate(country.MilitarySpending);
        var administration = NormalizeRate(country.AdministrationSpending);

        foreach (var pop in pops)
        {
            if (education > 0m && pop.PopClass is "clergy" or "clerks")
            {
                pop.CashReserve = ScalarMath.Clamp(pop.CashReserve + education * (pop.Size / 1000m) * 0.08m, 0m, 100_000m);
                pop.Literacy = ScalarMath.Clamp(pop.Literacy + education * 0.0005m, 0m, 1m);
            }

            if (military > 0m && pop.PopClass == "soldiers")
            {
                pop.CashReserve = ScalarMath.Clamp(pop.CashReserve + military * (pop.Size / 1000m) * 0.10m, 0m, 100_000m);
                pop.Militancy = ScalarMath.Clamp(pop.Militancy - military * 0.002m, 0m, 10m);
            }

            if (administration > 0m && pop.PopClass == "bureaucrats")
            {
                pop.CashReserve = ScalarMath.Clamp(pop.CashReserve + administration * (pop.Size / 1000m) * 0.08m, 0m, 100_000m);
                pop.Consciousness = ScalarMath.Clamp(pop.Consciousness + administration * 0.0005m, 0m, 10m);
            }
        }
    }

    private static decimal NormalizeRate(decimal rate) =>
        rate <= 1m
            ? ScalarMath.Clamp(rate, 0m, 1m)
            : ScalarMath.Clamp(rate / 100m, 0m, 1m);
}
