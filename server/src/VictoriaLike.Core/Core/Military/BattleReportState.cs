namespace VictoriaLike.Core.Core.Military;

public sealed class BattleReportState
{
    public required string Id { get; init; }
    public required string WarId { get; set; }
    public required string ProvinceId { get; set; }
    public required string WinnerArmyId { get; set; }
    public required string LoserArmyId { get; set; }
    public required string WinnerCountryId { get; set; }
    public required string LoserCountryId { get; set; }
    public required DateOnly OccurredOn { get; set; }
    public int WinnerCasualties { get; set; }
    public int LoserCasualties { get; set; }
    public decimal WinnerMoraleAfter { get; set; }
    public decimal LoserMoraleAfter { get; set; }
}
