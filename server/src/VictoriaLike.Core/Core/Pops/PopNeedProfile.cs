namespace VictoriaLike.Core.Core.Pops;

public sealed class PopNeedProfile
{
    public Dictionary<string, decimal> Life { get; init; } = [];
    public Dictionary<string, decimal> Everyday { get; init; } = [];
    public Dictionary<string, decimal> Luxury { get; init; } = [];
}
