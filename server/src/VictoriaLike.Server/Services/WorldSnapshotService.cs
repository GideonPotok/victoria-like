using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public interface IWorldSnapshotService
{
    WorldSnapshotMetadata? LatestSnapshot { get; }
    Task<WorldSnapshotMetadata> SaveAsync(WorldState worldState, WorldStateSnapshot snapshot, string? savepointName = null, CancellationToken cancellationToken = default);
    Task<WorldSnapshotDocument?> LoadLatestAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<WorldSnapshotMetadata> ListSnapshots(int limit = 10);
}

public sealed class WorldSnapshotMetadata
{
    public string FileName { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string? SavepointName { get; set; }
    public long TickNumber { get; set; }
    public DateTime WorldTimestamp { get; set; }
    public DateTime CapturedAtUtc { get; set; }
}

public sealed class WorldSnapshotDocument
{
    public DateTime CapturedAtUtc { get; set; }
    public long TickNumber { get; set; }
    public DateTime WorldTimestamp { get; set; }
    public string? SavepointName { get; set; }
    public List<CountrySnapshotDto> Countries { get; set; } = new();
    public List<MarketSnapshotDto> Markets { get; set; } = new();
    public List<ProvinceSnapshotDto> Provinces { get; set; } = new();
    public List<PlayerSnapshotDto> Players { get; set; } = new();
    public List<BuildingQueueSnapshotDto> BuildingQueue { get; set; } = new();
    public List<FactorySnapshotDto> Factories { get; set; } = new();
    public List<GoodProfitHistorySnapshotDto> GoodProfitHistory { get; set; } = new();
    public List<ArmyStackSnapshotDto> Armies { get; set; } = new();
    public List<WarSnapshotDto> Wars { get; set; } = new();
    public List<BattleReportSnapshotDto> BattleReports { get; set; } = new();

    public IEnumerable<Country> ToCountries() =>
        Countries.Select(country => new Country(new CountryId(country.Id), country.Name, country.Tag, country.TaxRate)
        {
            Treasury = country.Treasury,
            TariffRate = country.TariffRate,
            PoorTaxRate = country.PoorTaxRate,
            MiddleTaxRate = country.MiddleTaxRate,
            RichTaxRate = country.RichTaxRate,
            EducationSpending = country.EducationSpending,
            MilitarySpending = country.MilitarySpending,
            AdministrationSpending = country.AdministrationSpending
        });

    public IEnumerable<Market> ToMarkets() =>
        Markets.Select(market => new Market(new MarketId(market.Id), market.Name)
        {
            GoodPrices = new Dictionary<string, decimal>(market.GoodPrices, StringComparer.OrdinalIgnoreCase),
            GoodSupply = new Dictionary<string, decimal>(market.GoodSupply, StringComparer.OrdinalIgnoreCase),
            GoodDemand = new Dictionary<string, decimal>(market.GoodDemand, StringComparer.OrdinalIgnoreCase)
        });

    public IEnumerable<Province> ToProvinces() =>
        Provinces.Select(province => new Province(
            new ProvinceId(province.Id),
            province.Name,
            new CountryId(province.OwnerId),
            new MarketId(province.MarketId),
            province.Population)
        {
            RgoType = province.RgoType,
            OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
            NeedsFulfillment = province.NeedsFulfillment,
            PopGroups = province.PopGroups
                .Select(pop => new PopGroup(
                    pop.Id,
                    new ProvinceId(province.Id),
                    pop.Size,
                    pop.PopType,
                    pop.Strata,
                    pop.Culture,
                    pop.Religion,
                    pop.Literacy)
                {
                    Militancy = pop.Militancy,
                    Consciousness = pop.Consciousness,
                    Cash = pop.Cash,
                    LifeNeedsFulfillment = pop.LifeNeedsFulfillment,
                    EverydayNeedsFulfillment = pop.EverydayNeedsFulfillment,
                    LuxuryNeedsFulfillment = pop.LuxuryNeedsFulfillment,
                    EmployedCount = pop.EmployedCount,
                    UnemployedCount = pop.UnemployedCount,
                    ArtisanProducedGood = pop.ArtisanProducedGood,
                    ArtisanDaysUntilReconsider = pop.ArtisanDaysUntilReconsider,
                    ArtisanLastReconsideredAt = pop.ArtisanLastReconsideredAt,
                    ArtisanProfitLastTick = pop.ArtisanProfitLastTick
                })
                .ToList()
        });

    public IEnumerable<PlayerAccount> ToPlayers() =>
        Players.Select(player => new PlayerAccount(
            new ActorId(player.ActorId),
            player.Username,
            new CountryId(player.ControlledCountryId))
        {
            CreatedAt = player.CreatedAt
        });

    public List<BuildingQueueItem> ToBuildingQueue() =>
        BuildingQueue.Select(item => new BuildingQueueItem
        {
            Id = item.Id,
            ProvinceId = item.ProvinceId,
            CountryId = item.CountryId,
            BuildingType = item.BuildingType,
            TicksRemaining = item.TicksRemaining,
            QueuedAt = item.QueuedAt
        }).ToList();

    public List<Factory> ToFactories() =>
        Factories.Select(factory => new Factory
        {
            Id = factory.Id,
            CountryId = new CountryId(factory.CountryId),
            ProvinceId = factory.ProvinceId.HasValue ? new ProvinceId(factory.ProvinceId.Value) : null,
            Type = factory.Type,
            Level = factory.Level,
            EmployedCraftsmen = factory.EmployedCraftsmen,
            EmployedClerks = factory.EmployedClerks,
            InputGoods = new Dictionary<string, decimal>(factory.InputGoods, StringComparer.OrdinalIgnoreCase),
            OutputGood = factory.OutputGood,
            OutputPerTick = factory.OutputPerTick,
            CashReserve = factory.CashReserve,
            ProfitLastTick = factory.ProfitLastTick
        }).ToList();

    public List<GoodProfitHistory> ToGoodProfitHistory() =>
        GoodProfitHistory.Select(entry => new GoodProfitHistory
        {
            Month = entry.Month,
            GoodId = entry.GoodId,
            AverageProducerProfit = entry.AverageProducerProfit,
            ProducerCount = entry.ProducerCount
        }).ToList();

    public List<ArmyStack> ToArmies() =>
        Armies.Select(army => new ArmyStack
        {
            Id = army.Id,
            CountryId = new CountryId(army.CountryId),
            LocationProvinceId = new ProvinceId(army.LocationProvinceId),
            DestinationProvinceId = army.DestinationProvinceId.HasValue
                ? new ProvinceId(army.DestinationProvinceId.Value)
                : null,
            MovementTicksRemaining = army.MovementTicksRemaining,
            SoldierCount = army.SoldierCount,
            Morale = army.Morale
        }).ToList();

    public List<War> ToWars() =>
        Wars.Select(war => new War
        {
            Id = war.Id,
            AttackerCountryId = new CountryId(war.AttackerCountryId),
            DefenderCountryId = new CountryId(war.DefenderCountryId),
            StartedAt = war.StartedAt,
            EndedAt = war.EndedAt,
            IsActive = war.IsActive
        }).ToList();

    public List<BattleReport> ToBattleReports() =>
        BattleReports.Select(battle => new BattleReport
        {
            Id = battle.Id,
            WarId = battle.WarId,
            ProvinceId = battle.ProvinceId,
            WinnerArmyId = battle.WinnerArmyId,
            LoserArmyId = battle.LoserArmyId,
            WinnerCountryId = battle.WinnerCountryId,
            LoserCountryId = battle.LoserCountryId,
            OccurredAt = battle.OccurredAt,
            WinnerCasualties = battle.WinnerCasualties,
            LoserCasualties = battle.LoserCasualties,
            WinnerMoraleAfter = battle.WinnerMoraleAfter,
            LoserMoraleAfter = battle.LoserMoraleAfter
        }).ToList();

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var countryIds = Countries.Select(country => country.Id).ToHashSet();
        var marketIds = Markets.Select(market => market.Id).ToHashSet();
        var provinceIds = Provinces.Select(province => province.Id).ToHashSet();

        if (TickNumber < 0)
            errors.Add("Snapshot tick number cannot be negative");

        if (Countries.Count == 0)
            errors.Add("Snapshot contains no countries");

        if (Markets.Count == 0)
            errors.Add("Snapshot contains no markets");

        foreach (var province in Provinces)
        {
            if (!countryIds.Contains(province.OwnerId))
                errors.Add($"Province {province.Id} references missing owner country {province.OwnerId}");
            if (!marketIds.Contains(province.MarketId))
                errors.Add($"Province {province.Id} references missing market {province.MarketId}");

            var popTotal = 0;
            foreach (var pop in province.PopGroups)
            {
                if (pop.Size < 0)
                    errors.Add($"POP {pop.Id} has negative size");
                if (!VictoriaLike.Core.Domain.PopGroup.ValidStrata.Contains(pop.Strata))
                    errors.Add($"POP {pop.Id} has invalid strata {pop.Strata}");
                if (pop.EmployedCount < 0 || pop.UnemployedCount < 0)
                    errors.Add($"POP {pop.Id} has negative employment counts");
                if (pop.EmployedCount + pop.UnemployedCount > pop.Size)
                    errors.Add($"POP {pop.Id} employment exceeds size");
                popTotal += Math.Max(0, pop.Size);
            }

            if (province.PopGroups.Count > 0 && popTotal != province.Population)
                errors.Add($"Province {province.Id} POP sizes sum to {popTotal}, expected population {province.Population}");
        }

        foreach (var player in Players)
        {
            if (!countryIds.Contains(player.ControlledCountryId))
                errors.Add($"Player {player.ActorId} controls missing country {player.ControlledCountryId}");
        }

        foreach (var item in BuildingQueue)
        {
            if (!provinceIds.Contains(item.ProvinceId))
                errors.Add($"Building queue item {item.Id} references missing province {item.ProvinceId}");
            if (!countryIds.Contains(item.CountryId))
                errors.Add($"Building queue item {item.Id} references missing country {item.CountryId}");
            if (item.TicksRemaining < 0)
                errors.Add($"Building queue item {item.Id} has negative ticks remaining");
            if (string.IsNullOrWhiteSpace(item.BuildingType))
                errors.Add($"Building queue item {item.Id} has no building type");
        }

        foreach (var factory in Factories)
        {
            if (!countryIds.Contains(factory.CountryId))
                errors.Add($"Factory {factory.Id} references missing country {factory.CountryId}");
            if (factory.ProvinceId.HasValue && !provinceIds.Contains(factory.ProvinceId.Value))
                errors.Add($"Factory {factory.Id} references missing province {factory.ProvinceId}");
            if (factory.Level < 1)
                errors.Add($"Factory {factory.Id} level must be >= 1");
            if (factory.EmployedCraftsmen < 0 || factory.EmployedClerks < 0)
                errors.Add($"Factory {factory.Id} has negative employment");
            if (factory.InputGoods.Values.Any(value => value < 0m))
                errors.Add($"Factory {factory.Id} has negative input quantity");
            if (string.IsNullOrWhiteSpace(factory.OutputGood))
                errors.Add($"Factory {factory.Id} has no output good");
        }

        foreach (var entry in GoodProfitHistory)
        {
            if (string.IsNullOrWhiteSpace(entry.Month))
                errors.Add("Good profit history entry has no month");
            if (string.IsNullOrWhiteSpace(entry.GoodId))
                errors.Add("Good profit history entry has no good id");
            if (entry.ProducerCount < 0)
                errors.Add($"Good profit history entry {entry.GoodId} has negative producer count");
        }

        foreach (var army in Armies)
        {
            if (!countryIds.Contains(army.CountryId))
                errors.Add($"Army {army.Id} references missing country {army.CountryId}");
            if (!provinceIds.Contains(army.LocationProvinceId))
                errors.Add($"Army {army.Id} references missing location province {army.LocationProvinceId}");
            if (army.DestinationProvinceId.HasValue && !provinceIds.Contains(army.DestinationProvinceId.Value))
                errors.Add($"Army {army.Id} references missing destination province {army.DestinationProvinceId}");
            if (army.MovementTicksRemaining < 0)
                errors.Add($"Army {army.Id} has negative movement ticks");
            if (army.SoldierCount < 0)
                errors.Add($"Army {army.Id} has negative soldiers");
            if (army.Morale < 0m || army.Morale > 1m)
                errors.Add($"Army {army.Id} has invalid morale {army.Morale}");
        }

        var activeWarPairs = new HashSet<(Guid First, Guid Second)>();
        foreach (var war in Wars)
        {
            if (!countryIds.Contains(war.AttackerCountryId))
                errors.Add($"War {war.Id} references missing attacker country {war.AttackerCountryId}");
            if (!countryIds.Contains(war.DefenderCountryId))
                errors.Add($"War {war.Id} references missing defender country {war.DefenderCountryId}");
            if (war.AttackerCountryId == war.DefenderCountryId)
                errors.Add($"War {war.Id} has the same attacker and defender");
            if (!war.IsActive)
                continue;

            var pair = war.AttackerCountryId.CompareTo(war.DefenderCountryId) <= 0
                ? (war.AttackerCountryId, war.DefenderCountryId)
                : (war.DefenderCountryId, war.AttackerCountryId);
            if (!activeWarPairs.Add(pair))
                errors.Add($"Multiple active wars exist between {pair.Item1} and {pair.Item2}");
        }

        var warIds = Wars.Select(war => war.Id).ToHashSet();
        foreach (var battle in BattleReports)
        {
            if (!warIds.Contains(battle.WarId))
                errors.Add($"Battle report {battle.Id} references missing war {battle.WarId}");
            if (!provinceIds.Contains(battle.ProvinceId))
                errors.Add($"Battle report {battle.Id} references missing province {battle.ProvinceId}");
            if (!countryIds.Contains(battle.WinnerCountryId))
                errors.Add($"Battle report {battle.Id} references missing winner country {battle.WinnerCountryId}");
            if (!countryIds.Contains(battle.LoserCountryId))
                errors.Add($"Battle report {battle.Id} references missing loser country {battle.LoserCountryId}");
            if (battle.WinnerCasualties < 0 || battle.LoserCasualties < 0)
                errors.Add($"Battle report {battle.Id} has negative casualties");
        }

        return errors;
    }
}

public sealed class CountrySnapshotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty;
    public int TaxRate { get; set; }
    public decimal Treasury { get; set; }
    public decimal TariffRate { get; set; }
    public decimal PoorTaxRate { get; set; } = -1m;
    public decimal MiddleTaxRate { get; set; } = -1m;
    public decimal RichTaxRate { get; set; } = -1m;
    public decimal EducationSpending { get; set; } = 0.5m;
    public decimal MilitarySpending { get; set; } = 0.5m;
    public decimal AdministrationSpending { get; set; } = 0.5m;
}

public sealed class MarketSnapshotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, decimal> GoodPrices { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> GoodSupply { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, decimal> GoodDemand { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ProvinceSnapshotDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public Guid MarketId { get; set; }
    public int Population { get; set; }
    public string RgoType { get; set; } = "grain_farm";
    public Dictionary<string, decimal> OutputsPerTick { get; set; } = new();
    public decimal NeedsFulfillment { get; set; } = 1.0m;
    public List<PopGroupSnapshotDto> PopGroups { get; set; } = new();
}

public sealed class PopGroupSnapshotDto
{
    public Guid Id { get; set; }
    public int Size { get; set; }
    public string PopType { get; set; } = string.Empty;
    public string Strata { get; set; } = "poor";
    public string Culture { get; set; } = string.Empty;
    public string Religion { get; set; } = string.Empty;
    public decimal Literacy { get; set; }
    public decimal Militancy { get; set; }
    public decimal Consciousness { get; set; }
    public decimal Cash { get; set; }
    public decimal LifeNeedsFulfillment { get; set; } = 1.0m;
    public decimal EverydayNeedsFulfillment { get; set; } = 1.0m;
    public decimal LuxuryNeedsFulfillment { get; set; }
    public int EmployedCount { get; set; }
    public int UnemployedCount { get; set; }
    public string? ArtisanProducedGood { get; set; }
    public int ArtisanDaysUntilReconsider { get; set; }
    public DateTime? ArtisanLastReconsideredAt { get; set; }
    public decimal ArtisanProfitLastTick { get; set; }
}

public sealed class PlayerSnapshotDto
{
    public Guid ActorId { get; set; }
    public string Username { get; set; } = string.Empty;
    public Guid ControlledCountryId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class BuildingQueueSnapshotDto
{
    public Guid Id { get; set; }
    public Guid ProvinceId { get; set; }
    public Guid CountryId { get; set; }
    public string BuildingType { get; set; } = string.Empty;
    public int TicksRemaining { get; set; }
    public DateTime QueuedAt { get; set; }
}

public sealed class FactorySnapshotDto
{
    public Guid Id { get; set; }
    public Guid CountryId { get; set; }
    public Guid? ProvinceId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int EmployedCraftsmen { get; set; }
    public int EmployedClerks { get; set; }
    public Dictionary<string, decimal> InputGoods { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public string OutputGood { get; set; } = string.Empty;
    public decimal OutputPerTick { get; set; }
    public decimal CashReserve { get; set; }
    public decimal ProfitLastTick { get; set; }
}

public sealed class GoodProfitHistorySnapshotDto
{
    public string Month { get; set; } = string.Empty;
    public string GoodId { get; set; } = string.Empty;
    public decimal AverageProducerProfit { get; set; }
    public int ProducerCount { get; set; }
}

public sealed class ArmyStackSnapshotDto
{
    public Guid Id { get; set; }
    public Guid CountryId { get; set; }
    public Guid LocationProvinceId { get; set; }
    public Guid? DestinationProvinceId { get; set; }
    public int MovementTicksRemaining { get; set; }
    public int SoldierCount { get; set; }
    public decimal Morale { get; set; } = 1m;
}

public sealed class WarSnapshotDto
{
    public Guid Id { get; set; }
    public Guid AttackerCountryId { get; set; }
    public Guid DefenderCountryId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class BattleReportSnapshotDto
{
    public string Id { get; set; } = string.Empty;
    public Guid WarId { get; set; }
    public Guid ProvinceId { get; set; }
    public Guid WinnerArmyId { get; set; }
    public Guid LoserArmyId { get; set; }
    public Guid WinnerCountryId { get; set; }
    public Guid LoserCountryId { get; set; }
    public DateTime OccurredAt { get; set; }
    public int WinnerCasualties { get; set; }
    public int LoserCasualties { get; set; }
    public decimal WinnerMoraleAfter { get; set; }
    public decimal LoserMoraleAfter { get; set; }
}

public sealed class WorldSnapshotService : IWorldSnapshotService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<WorldSnapshotService> _logger;
    private readonly string _snapshotDirectory;
    private readonly int _retainCount;
    private readonly object _metadataLock = new();
    private WorldSnapshotMetadata? _latestSnapshot;

    public WorldSnapshotService(IConfiguration configuration, ILogger<WorldSnapshotService> logger)
    {
        _logger = logger;
        _snapshotDirectory = configuration.GetValue<string>("World:Snapshots:Directory")
            ?? Path.Combine(AppContext.BaseDirectory, "snapshots");
        _retainCount = Math.Max(1, configuration.GetValue<int>("World:Snapshots:RetainCount", 10));

        EnsureSnapshotDirectory();
        RefreshLatestMetadata();
    }

    public WorldSnapshotMetadata? LatestSnapshot
    {
        get
        {
            lock (_metadataLock)
            {
                return _latestSnapshot;
            }
        }
    }

    public async Task<WorldSnapshotMetadata> SaveAsync(
        WorldState worldState,
        WorldStateSnapshot snapshot,
        string? savepointName = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSnapshotDirectory();

        var document = new WorldSnapshotDocument
        {
            CapturedAtUtc = DateTime.UtcNow,
            TickNumber = worldState.TickNumber,
            WorldTimestamp = worldState.WorldTimestamp,
            SavepointName = NormalizeSavepointName(savepointName),
            Countries = snapshot.Countries
                .Select(country => new CountrySnapshotDto
                {
                    Id = country.Id.Value,
                    Name = country.Name,
                    Tag = country.Tag,
                    TaxRate = country.TaxRate,
                    Treasury = country.Treasury,
                    TariffRate = country.TariffRate,
                    PoorTaxRate = country.PoorTaxRate,
                    MiddleTaxRate = country.MiddleTaxRate,
                    RichTaxRate = country.RichTaxRate,
                    EducationSpending = country.EducationSpending,
                    MilitarySpending = country.MilitarySpending,
                    AdministrationSpending = country.AdministrationSpending
                })
                .ToList(),
            Markets = snapshot.Markets
                .Select(market => new MarketSnapshotDto
                {
                    Id = market.Id.Value,
                    Name = market.Name,
                    GoodPrices = new Dictionary<string, decimal>(market.GoodPrices, StringComparer.OrdinalIgnoreCase),
                    GoodSupply = new Dictionary<string, decimal>(market.GoodSupply, StringComparer.OrdinalIgnoreCase),
                    GoodDemand = new Dictionary<string, decimal>(market.GoodDemand, StringComparer.OrdinalIgnoreCase)
                })
                .ToList(),
            Provinces = snapshot.Provinces
                .Select(province => new ProvinceSnapshotDto
                {
                    Id = province.Id.Value,
                    Name = province.Name,
                    OwnerId = province.OwnerId.Value,
                    MarketId = province.MarketId.Value,
                    Population = province.Population,
                    RgoType = province.RgoType,
                    OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
                    NeedsFulfillment = province.NeedsFulfillment,
                    PopGroups = province.PopGroups
                        .Select(pop => new PopGroupSnapshotDto
                        {
                            Id = pop.Id,
                            Size = pop.Size,
                            PopType = pop.PopType,
                            Strata = pop.Strata,
                            Culture = pop.Culture,
                            Religion = pop.Religion,
                            Literacy = pop.Literacy,
                            Militancy = pop.Militancy,
                            Consciousness = pop.Consciousness,
                            Cash = pop.Cash,
                            LifeNeedsFulfillment = pop.LifeNeedsFulfillment,
                            EverydayNeedsFulfillment = pop.EverydayNeedsFulfillment,
                            LuxuryNeedsFulfillment = pop.LuxuryNeedsFulfillment,
                            EmployedCount = pop.EmployedCount,
                            UnemployedCount = pop.UnemployedCount,
                            ArtisanProducedGood = pop.ArtisanProducedGood,
                            ArtisanDaysUntilReconsider = pop.ArtisanDaysUntilReconsider,
                            ArtisanLastReconsideredAt = pop.ArtisanLastReconsideredAt,
                            ArtisanProfitLastTick = pop.ArtisanProfitLastTick
                        })
                        .ToList()
                })
                .ToList(),
            Players = snapshot.Players
                .Select(player => new PlayerSnapshotDto
                {
                    ActorId = player.Id.Value,
                    Username = player.Username,
                    ControlledCountryId = player.ControlledCountry.Value,
                    CreatedAt = player.CreatedAt
                })
                .ToList(),
            BuildingQueue = snapshot.BuildingQueue
                .Select(item => new BuildingQueueSnapshotDto
                {
                    Id = item.Id,
                    ProvinceId = item.ProvinceId,
                    CountryId = item.CountryId,
                    BuildingType = item.BuildingType,
                    TicksRemaining = item.TicksRemaining,
                    QueuedAt = item.QueuedAt
                })
                .ToList(),
            Factories = snapshot.Factories
                .Select(factory => new FactorySnapshotDto
                {
                    Id = factory.Id,
                    CountryId = factory.CountryId.Value,
                    ProvinceId = factory.ProvinceId?.Value,
                    Type = factory.Type,
                    Level = factory.Level,
                    EmployedCraftsmen = factory.EmployedCraftsmen,
                    EmployedClerks = factory.EmployedClerks,
                    InputGoods = new Dictionary<string, decimal>(factory.InputGoods, StringComparer.OrdinalIgnoreCase),
                    OutputGood = factory.OutputGood,
                    OutputPerTick = factory.OutputPerTick,
                    CashReserve = factory.CashReserve,
                    ProfitLastTick = factory.ProfitLastTick
                })
                .ToList(),
            GoodProfitHistory = snapshot.GoodProfitHistory
                .Select(entry => new GoodProfitHistorySnapshotDto
                {
                    Month = entry.Month,
                    GoodId = entry.GoodId,
                    AverageProducerProfit = entry.AverageProducerProfit,
                    ProducerCount = entry.ProducerCount
                })
                .ToList(),
            Armies = snapshot.Armies
                .Select(army => new ArmyStackSnapshotDto
                {
                    Id = army.Id,
                    CountryId = army.CountryId.Value,
                    LocationProvinceId = army.LocationProvinceId.Value,
                    DestinationProvinceId = army.DestinationProvinceId?.Value,
                    MovementTicksRemaining = army.MovementTicksRemaining,
                    SoldierCount = army.SoldierCount,
                    Morale = army.Morale
                })
                .ToList(),
            Wars = snapshot.Wars
                .Select(war => new WarSnapshotDto
                {
                    Id = war.Id,
                    AttackerCountryId = war.AttackerCountryId.Value,
                    DefenderCountryId = war.DefenderCountryId.Value,
                    StartedAt = war.StartedAt,
                    EndedAt = war.EndedAt,
                    IsActive = war.IsActive
                })
                .ToList(),
            BattleReports = snapshot.BattleReports
                .Select(battle => new BattleReportSnapshotDto
                {
                    Id = battle.Id,
                    WarId = battle.WarId,
                    ProvinceId = battle.ProvinceId,
                    WinnerArmyId = battle.WinnerArmyId,
                    LoserArmyId = battle.LoserArmyId,
                    WinnerCountryId = battle.WinnerCountryId,
                    LoserCountryId = battle.LoserCountryId,
                    OccurredAt = battle.OccurredAt,
                    WinnerCasualties = battle.WinnerCasualties,
                    LoserCasualties = battle.LoserCasualties,
                    WinnerMoraleAfter = battle.WinnerMoraleAfter,
                    LoserMoraleAfter = battle.LoserMoraleAfter
                })
                .ToList()
        };

        var namePart = document.SavepointName == null ? string.Empty : $"-{document.SavepointName}";
        var fileName = $"world-snapshot-t{worldState.TickNumber:D12}{namePart}-{document.CapturedAtUtc:yyyyMMddHHmmss}.json";
        var fullPath = Path.Combine(_snapshotDirectory, fileName);
        var tempPath = $"{fullPath}.tmp";

        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, document, JsonOptions, cancellationToken);
        }

        File.Move(tempPath, fullPath, overwrite: true);

        var metadata = new WorldSnapshotMetadata
        {
            FileName = fileName,
            FullPath = fullPath,
            SavepointName = document.SavepointName,
            TickNumber = document.TickNumber,
            WorldTimestamp = document.WorldTimestamp,
            CapturedAtUtc = document.CapturedAtUtc
        };

        lock (_metadataLock)
        {
            _latestSnapshot = metadata;
        }

        CleanupOldSnapshots();

        _logger.LogInformation(
            "World snapshot saved: file={FileName} tick={Tick} date={WorldDate}",
            fileName,
            worldState.TickNumber,
            worldState.WorldTimestamp.ToString("yyyy-MM-dd"));

        return metadata;
    }

    public async Task<WorldSnapshotDocument?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        var latest = ListSnapshotFiles().FirstOrDefault();
        if (latest == null)
        {
            _logger.LogInformation("No world snapshots found in {SnapshotDirectory}", _snapshotDirectory);
            return null;
        }

        await using var stream = File.OpenRead(latest.FullName);
        var document = await JsonSerializer.DeserializeAsync<WorldSnapshotDocument>(stream, JsonOptions, cancellationToken);
        if (document == null)
            throw new InvalidOperationException($"Failed to deserialize world snapshot: {latest.FullName}");

        var validationErrors = document.Validate();
        if (validationErrors.Count > 0)
            throw new InvalidOperationException($"Invalid world snapshot {latest.FullName}: {string.Join("; ", validationErrors)}");

        lock (_metadataLock)
        {
            _latestSnapshot = new WorldSnapshotMetadata
            {
                FileName = latest.Name,
                FullPath = latest.FullName,
                SavepointName = document.SavepointName,
                TickNumber = document.TickNumber,
                WorldTimestamp = document.WorldTimestamp,
                CapturedAtUtc = document.CapturedAtUtc
            };
        }

        return document;
    }

    public IReadOnlyList<WorldSnapshotMetadata> ListSnapshots(int limit = 10)
    {
        return ListSnapshotFiles()
            .Take(Math.Max(1, limit))
            .Select(file =>
            {
                var document = JsonSerializer.Deserialize<WorldSnapshotDocument>(File.ReadAllText(file.FullName), JsonOptions);
                return document == null
                    ? null
                    : new WorldSnapshotMetadata
                    {
                        FileName = file.Name,
                        FullPath = file.FullName,
                        SavepointName = document.SavepointName,
                        TickNumber = document.TickNumber,
                        WorldTimestamp = document.WorldTimestamp,
                        CapturedAtUtc = document.CapturedAtUtc
                    };
            })
            .Where(metadata => metadata != null)
            .Cast<WorldSnapshotMetadata>()
            .ToList();
    }

    private void EnsureSnapshotDirectory()
    {
        Directory.CreateDirectory(_snapshotDirectory);
    }

    private void RefreshLatestMetadata()
    {
        var latest = ListSnapshots(1).FirstOrDefault();
        lock (_metadataLock)
        {
            _latestSnapshot = latest;
        }
    }

    private IEnumerable<FileInfo> ListSnapshotFiles()
    {
        var directory = new DirectoryInfo(_snapshotDirectory);
        if (!directory.Exists)
            return Enumerable.Empty<FileInfo>();

        return directory.GetFiles("world-snapshot-*.json")
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .ThenByDescending(file => file.Name);
    }

    private void CleanupOldSnapshots()
    {
        var staleFiles = ListSnapshotFiles().Skip(_retainCount).ToList();
        foreach (var file in staleFiles)
        {
            try
            {
                file.Delete();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old snapshot {SnapshotFile}", file.FullName);
            }
        }
    }

    private static string? NormalizeSavepointName(string? savepointName)
    {
        if (string.IsNullOrWhiteSpace(savepointName))
            return null;

        var cleaned = new string(savepointName
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());

        cleaned = string.Join('-', cleaned.Split('-', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length == 0 ? null : cleaned[..Math.Min(cleaned.Length, 48)];
    }
}
