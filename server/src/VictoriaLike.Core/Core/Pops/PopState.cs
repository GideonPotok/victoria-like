namespace VictoriaLike.Core.Core.Pops;

public sealed class PopState
{
    public required string Id { get; init; }
    public required string ProvinceId { get; set; }
    public required string PopClass { get; init; }
    public required int Size { get; set; }
    public decimal CashReserve { get; set; }
    public decimal Militancy { get; set; }
    public decimal Consciousness { get; set; }
    public decimal Literacy { get; set; }
    public decimal NeedsFulfillment { get; set; }
    public decimal LifeNeedsFulfillment { get; set; } = 1m;
    public decimal EverydayNeedsFulfillment { get; set; } = 1m;
    public decimal LuxuryNeedsFulfillment { get; set; }
    public int EmployedCount { get; set; }
    public int UnemployedCount { get; set; }
    public string? ArtisanProducedGood { get; set; }
    public int ArtisanDaysUntilReconsider { get; set; }
    public DateOnly? ArtisanLastReconsideredAt { get; set; }
    public decimal ArtisanProfitLastTick { get; set; }
    public required PopNeedProfile Needs { get; init; }
}
