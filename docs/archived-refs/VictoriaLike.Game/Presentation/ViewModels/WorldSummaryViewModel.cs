using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Game.Presentation.ViewModels;

public sealed record WorldSummaryViewModel(
    string Date,
    decimal Treasury,
    decimal AverageNeedsFulfilled,
    int UnmetPops,
    decimal TradeValue)
{
    public static WorldSummaryViewModel FromWorld(WorldState world)
    {
        var playable = world.Countries.Values.First(country => country.IsPlayable);
        return new WorldSummaryViewModel(
            world.Date.ToString(),
            playable.Treasury,
            world.Metrics.AverageNeedsFulfilled,
            world.Metrics.UnmetPopCount,
            world.Market.TradeValueLastTick);
    }

    public string ToMultilineString()
    {
        return string.Join(
            Environment.NewLine,
            $"Date {Date}",
            $"Treasury {Treasury:F2}",
            $"Average needs {AverageNeedsFulfilled:F2}",
            $"Unmet pops {UnmetPops}",
            $"Trade value {TradeValue:F2}");
    }
}
