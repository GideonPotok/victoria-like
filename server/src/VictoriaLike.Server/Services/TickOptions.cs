namespace VictoriaLike.Server.Services;

public sealed class TickOptions
{
    public int TickIntervalMs { get; set; } = 1000;
    public int SaveIntervalTicks { get; set; } = 100;
    public int SnapshotIntervalTicks { get; set; } = 25;
}
