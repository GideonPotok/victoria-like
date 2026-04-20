namespace VictoriaLike.Core.Core.Buildings;

public sealed class BuildingTemplate
{
    public required string Type { get; init; }
    public decimal Cost { get; init; }
    public int BuildTicks { get; init; }
    public decimal InfrastructureDelta { get; init; }
    public Dictionary<string, decimal> OutputPerTick { get; init; } = new();
    public FactoryBuildTemplate? Factory { get; init; }
}

public sealed class FactoryBuildTemplate
{
    public required string Type { get; init; }
    public Dictionary<string, decimal> InputGoods { get; init; } = new();
    public required string OutputGood { get; init; }
    public decimal OutputPerTick { get; init; }
}

public static class BuildingTemplates
{
    public static readonly IReadOnlyDictionary<string, BuildingTemplate> All =
        new Dictionary<string, BuildingTemplate>(StringComparer.OrdinalIgnoreCase)
        {
            ["railroad"] = new()
            {
                Type = "railroad",
                Cost = 500m,
                BuildTicks = 45,
                InfrastructureDelta = 0.20m
            },
            ["tools_factory"] = new()
            {
                Type = "tools_factory",
                Cost = 1_200m,
                BuildTicks = 90,
                OutputPerTick = new() { ["tools"] = 1.0m },
                Factory = new FactoryBuildTemplate
                {
                    Type = "tools_factory",
                    InputGoods = new() { ["iron"] = 0.5m, ["coal"] = 0.2m },
                    OutputGood = "tools",
                    OutputPerTick = 1.0m
                }
            },
            ["cement_factory"] = new()
            {
                Type = "cement_factory",
                Cost = 1_000m,
                BuildTicks = 75,
                OutputPerTick = new() { ["cement"] = 1.0m },
                Factory = new FactoryBuildTemplate
                {
                    Type = "cement_factory",
                    InputGoods = new() { ["coal"] = 0.4m },
                    OutputGood = "cement",
                    OutputPerTick = 1.0m
                }
            },
        };
}
