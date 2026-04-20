using System;
using System.Collections.Generic;

namespace VictoriaLike.Server.Services;

public sealed class MarketTickSnapshot
{
    public long Tick { get; set; }
    public Dictionary<string, decimal> Prices { get; set; } = new();
    public Dictionary<string, decimal> Supply { get; set; } = new();
    public Dictionary<string, decimal> Demand { get; set; } = new();
}

public interface IMarketHistoryService
{
    void RecordTick(long tick, Dictionary<string, decimal> prices, Dictionary<string, decimal> supply, Dictionary<string, decimal> demand);
    IReadOnlyList<MarketTickSnapshot> GetHistory(int count = 20);
    MarketTickSnapshot? Latest { get; }
}

public sealed class MarketHistoryService : IMarketHistoryService
{
    private const int MaxHistory = 20;
    private readonly List<MarketTickSnapshot> _history = new();
    private readonly object _lock = new();

    public MarketTickSnapshot? Latest
    {
        get
        {
            lock (_lock)
                return _history.Count > 0 ? _history[^1] : null;
        }
    }

    public void RecordTick(long tick, Dictionary<string, decimal> prices, Dictionary<string, decimal> supply, Dictionary<string, decimal> demand)
    {
        var snapshot = new MarketTickSnapshot
        {
            Tick = tick,
            Prices = new Dictionary<string, decimal>(prices),
            Supply = new Dictionary<string, decimal>(supply),
            Demand = new Dictionary<string, decimal>(demand)
        };

        lock (_lock)
        {
            _history.Add(snapshot);
            if (_history.Count > MaxHistory)
                _history.RemoveAt(0);
        }
    }

    public IReadOnlyList<MarketTickSnapshot> GetHistory(int count = 20)
    {
        lock (_lock)
        {
            var start = Math.Max(0, _history.Count - count);
            return _history.GetRange(start, _history.Count - start);
        }
    }
}
