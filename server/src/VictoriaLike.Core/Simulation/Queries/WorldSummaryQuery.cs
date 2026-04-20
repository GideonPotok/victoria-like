using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Core.Simulation.Queries;

public sealed class WorldSummaryQuery
{
    public string Build(WorldState world)
    {
        var country = world.Countries.Values.FirstOrDefault(entry => entry.IsPlayable);
        var treasury = country?.Treasury ?? 0m;

        return string.Join(
            Environment.NewLine,
            $"Date {world.Date}",
            $"Treasury {treasury:F2}",
            $"Average needs {world.Metrics.AverageNeedsFulfilled:F2}",
            $"Unmet pops {world.Metrics.UnmetPopCount}",
            $"Trade value {world.Market.TradeValueLastTick:F2}");
    }
}
