namespace VictoriaLike.Core.Core.World;

public sealed class SimulationMetrics
{
    public decimal AverageNeedsFulfilled { get; set; }
    public int UnmetPopCount { get; set; }
    public Dictionary<string, decimal> TreasuryDeltaByCountry { get; init; } = [];
    public Dictionary<string, decimal> ReformPressureByCountry { get; init; } = [];
    public List<string> CompletedBuildingProvinceIds { get; init; } = [];
}
