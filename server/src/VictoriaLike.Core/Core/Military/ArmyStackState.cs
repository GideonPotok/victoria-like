namespace VictoriaLike.Core.Core.Military;

public sealed class ArmyStackState
{
    public required string Id { get; init; }
    public required string CountryId { get; set; }
    public required string LocationProvinceId { get; set; }
    public string? DestinationProvinceId { get; set; }
    public int MovementTicksRemaining { get; set; }
    public int SoldierCount { get; set; }
    public decimal Morale { get; set; } = 1m;

    public bool IsMoving => !string.IsNullOrWhiteSpace(DestinationProvinceId) && MovementTicksRemaining > 0;
    public bool CanFight => SoldierCount > 0 && Morale > 0m && !IsMoving;
}
