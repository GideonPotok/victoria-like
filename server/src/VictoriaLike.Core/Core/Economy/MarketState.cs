namespace VictoriaLike.Core.Core.Economy;

public sealed class MarketState
{
    public Dictionary<string, decimal> Prices { get; init; } = [];
    public Dictionary<string, decimal> SupplyLastTick { get; init; } = [];
    public Dictionary<string, decimal> DemandLastTick { get; init; } = [];
    public Dictionary<string, decimal> PricePressureLastTick { get; init; } = [];
    public Dictionary<string, decimal> UnmetDemandLastTick { get; init; } = [];
    public Dictionary<string, decimal> ProductionLastTick { get; init; } = [];
    public Dictionary<string, decimal> ConsumptionLastTick { get; init; } = [];
    public Dictionary<string, decimal> ImportsLastTick { get; init; } = [];
    public Dictionary<string, decimal> ExportsLastTick { get; init; } = [];
    public decimal TradeValueLastTick { get; set; }
}
