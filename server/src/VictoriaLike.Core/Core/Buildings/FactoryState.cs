namespace VictoriaLike.Core.Core.Buildings;

public sealed class FactoryState
{
    public required string Id { get; init; }
    public required string CountryId { get; set; }
    public string? ProvinceId { get; set; }
    public required string Type { get; init; }
    public int Level { get; set; } = 1;
    public int EmployedCraftsmen { get; set; }
    public int EmployedClerks { get; set; }
    public Dictionary<string, decimal> InputGoods { get; init; } = [];
    public required string OutputGood { get; init; }
    public decimal OutputPerTick { get; set; }
    public decimal CashReserve { get; set; }
    public decimal ProfitLastTick { get; set; }
}
