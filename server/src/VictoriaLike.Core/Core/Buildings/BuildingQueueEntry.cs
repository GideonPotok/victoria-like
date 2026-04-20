namespace VictoriaLike.Core.Core.Buildings;

public sealed class BuildingQueueEntry
{
    public required string Id { get; init; }
    public required string ProvinceId { get; init; }
    public required string CountryId { get; init; }
    public required string BuildingType { get; init; }
    public int TicksRemaining { get; set; }
    public DateTime QueuedAt { get; init; }
}
