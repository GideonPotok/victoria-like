namespace VictoriaLike.Core.Core.Economy;

public sealed class GoodProfitHistoryEntry
{
    public required string Month { get; init; }
    public required string GoodId { get; init; }
    public decimal AverageProducerProfit { get; set; }
    public int ProducerCount { get; set; }
}
