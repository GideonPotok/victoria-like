using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Core.World;

public sealed class WorldState
{
    public required int Seed { get; init; }
    public required GameDate Date { get; set; }
    public Dictionary<string, CountryState> Countries { get; init; } = [];
    public Dictionary<string, ProvinceState> Provinces { get; init; } = [];
    public Dictionary<string, PlayerAccount> PlayerAccounts { get; init; } = [];
    public Dictionary<string, PopState> Pops { get; init; } = [];
    public Dictionary<string, FactoryState> Factories { get; init; } = [];
    public Dictionary<string, ArmyStackState> Armies { get; init; } = [];
    public Dictionary<string, WarState> Wars { get; init; } = [];
    public Dictionary<string, BattleReportState> BattleReports { get; init; } = [];
    public Dictionary<string, GoodDefinition> Goods { get; init; } = [];
    public List<GoodProfitHistoryEntry> GoodProfitHistory { get; init; } = [];
    public MarketState Market { get; init; } = new();
    public SimulationMetrics Metrics { get; init; } = new();
    public List<BuildingQueueEntry> BuildingQueue { get; init; } = [];
    public List<string> EventLog { get; init; } = [];
}
