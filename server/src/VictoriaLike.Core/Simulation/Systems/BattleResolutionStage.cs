using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public sealed class BattleResolutionStage : ISimulationStage
{
    public string Name => "battle-resolution";

    public void Execute(SimulationContext context)
    {
        foreach (var provinceGroup in context.World.Armies.Values
            .Where(army => army.CanFight)
            .GroupBy(army => army.LocationProvinceId, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            ResolveProvinceBattles(context, provinceGroup.Key, provinceGroup.ToList());
        }
    }

    private static void ResolveProvinceBattles(SimulationContext context, string provinceId, List<ArmyStackState> armies)
    {
        while (true)
        {
            var pair = FindOpposingPair(context, armies);
            if (pair == null)
                return;

            var (first, second) = pair.Value;
            ResolveBattle(context, provinceId, first, second);
        }
    }

    private static (ArmyStackState First, ArmyStackState Second)? FindOpposingPair(
        SimulationContext context,
        List<ArmyStackState> armies)
    {
        var ready = armies
            .Where(army => army.CanFight)
            .OrderByDescending(EffectiveStrength(context, null))
            .ThenBy(army => army.Id, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < ready.Count; i++)
        {
            for (var j = i + 1; j < ready.Count; j++)
            {
                if (context.World.Wars.Values.Any(war =>
                        war.IsActive &&
                        war.IsBetween(ready[i].CountryId, ready[j].CountryId)))
                {
                    return (ready[i], ready[j]);
                }
            }
        }

        return null;
    }

    private static void ResolveBattle(
        SimulationContext context,
        string provinceId,
        ArmyStackState first,
        ArmyStackState second)
    {
        var firstStrength = EffectiveStrength(context, first.CountryId)(first);
        var secondStrength = EffectiveStrength(context, second.CountryId)(second);
        var winner = firstStrength >= secondStrength ? first : second;
        var loser = ReferenceEquals(winner, first) ? second : first;

        var winnerLoss = Math.Min(winner.SoldierCount, Math.Max(1, (int)Math.Ceiling(loser.SoldierCount * 0.10m)));
        var loserLoss = Math.Min(loser.SoldierCount, Math.Max(1, (int)Math.Ceiling(winner.SoldierCount * 0.25m)));

        winner.SoldierCount -= winnerLoss;
        loser.SoldierCount -= loserLoss;
        winner.Morale = Math.Clamp(winner.Morale - 0.12m, 0m, 1m);
        loser.Morale = Math.Clamp(loser.Morale - 0.45m, 0m, 1m);

        if (loser.SoldierCount > 0)
        {
            loser.LocationProvinceId = FindRetreatProvince(context, loser.CountryId, provinceId) ?? loser.LocationProvinceId;
            loser.DestinationProvinceId = null;
            loser.MovementTicksRemaining = 0;
            loser.Morale = Math.Max(loser.Morale, 0.15m);
        }

        var war = context.World.Wars.Values
            .Where(war => war.IsActive && war.IsBetween(winner.CountryId, loser.CountryId))
            .OrderBy(war => war.Id, StringComparer.Ordinal)
            .FirstOrDefault();

        var battleId = $"battle-{context.World.Date.Value:yyyyMMdd}-{provinceId}-{winner.Id}-{loser.Id}";
        if (war != null)
        {
            context.World.BattleReports[battleId] = new BattleReportState
            {
                Id = battleId,
                WarId = war.Id,
                ProvinceId = provinceId,
                WinnerArmyId = winner.Id,
                LoserArmyId = loser.Id,
                WinnerCountryId = winner.CountryId,
                LoserCountryId = loser.CountryId,
                OccurredOn = context.World.Date.Value,
                WinnerCasualties = winnerLoss,
                LoserCasualties = loserLoss,
                WinnerMoraleAfter = winner.Morale,
                LoserMoraleAfter = loser.Morale
            };
        }

        context.World.EventLog.Add(
            $"battle-resolved:{battleId}:{provinceId}:winner={winner.Id}:loser={loser.Id}:winnerLoss={winnerLoss}:loserLoss={loserLoss}");
    }

    private static Func<ArmyStackState, decimal> EffectiveStrength(SimulationContext context, string? preferredCountryId) =>
        army =>
        {
            var spending = 0.5m;
            if (context.World.Countries.TryGetValue(army.CountryId, out CountryState? country))
                spending = Math.Clamp(country.MilitarySpending, 0m, 1m);

            var readiness = 0.75m + (spending * 0.5m);
            return army.SoldierCount * Math.Clamp(army.Morale, 0m, 1m) * readiness;
        };

    private static string? FindRetreatProvince(SimulationContext context, string countryId, string battleProvinceId) =>
        context.World.Provinces.Values
            .Where(province =>
                string.Equals(province.OwnerId, countryId, StringComparison.Ordinal) &&
                !string.Equals(province.Id, battleProvinceId, StringComparison.Ordinal))
            .OrderBy(province => province.Id, StringComparer.Ordinal)
            .Select(province => province.Id)
            .FirstOrDefault();
}
