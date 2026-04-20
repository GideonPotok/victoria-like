namespace VictoriaLike.Core.Core.World;

public sealed class ProvinceState
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string OwnerId { get; set; }
    public string RgoType { get; set; } = "grain_farm";
    public List<string> PopulationIds { get; init; } = [];
    public Dictionary<string, decimal> OutputsPerTick { get; init; } = [];
    public Dictionary<string, decimal> Stockpile { get; init; } = [];
    public decimal Infrastructure { get; set; }
}
