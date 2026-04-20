namespace VictoriaLike.Core.Application.Profiling;

public sealed class TickProfile
{
    public Dictionary<string, TimeSpan> StageDurations { get; } = [];
}
