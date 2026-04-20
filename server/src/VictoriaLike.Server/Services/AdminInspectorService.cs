using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Auth;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public interface IAdminInspectorService
{
    Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task<AdminSnapshotDto?> CreateSnapshotAsync(string? savepointName = null, CancellationToken cancellationToken = default);
    Task<AdminMarketInspectorDto> GetMarketInspectorAsync(CancellationToken cancellationToken = default);
    Task<AdminProvinceInspectorDto?> GetProvinceInspectorAsync(string provinceId, CancellationToken cancellationToken = default);
    Task<AdminCountryInspectorDto?> GetCountryInspectorAsync(string countryId, CancellationToken cancellationToken = default);
    AdminTickProfileDto GetTickProfile();
}

public sealed class AdminInspectorService : IAdminInspectorService
{
    private readonly IWorldClockService _clockService;
    private readonly ICommandQueueService _commandQueue;
    private readonly ICommandRepository _commandRepository;
    private readonly IWorldWebSocketHub _webSocketHub;
    private readonly IWorldSnapshotService _snapshotService;
    private readonly IWorldStateDatabase _worldStateDatabase;
    private readonly IMarketHistoryService _marketHistory;
    private readonly IGoodsService _goodsService;
    private readonly ICommandBudgetService _commandBudgetService;
    private readonly ISessionRepository _sessionRepository;
    private readonly HealthCheckService _healthCheckService;

    public AdminInspectorService(
        IWorldClockService clockService,
        ICommandQueueService commandQueue,
        ICommandRepository commandRepository,
        IWorldWebSocketHub webSocketHub,
        IWorldSnapshotService snapshotService,
        IWorldStateDatabase worldStateDatabase,
        IMarketHistoryService marketHistory,
        IGoodsService goodsService,
        ICommandBudgetService commandBudgetService,
        ISessionRepository sessionRepository,
        HealthCheckService healthCheckService)
    {
        _clockService = clockService;
        _commandQueue = commandQueue;
        _commandRepository = commandRepository;
        _webSocketHub = webSocketHub;
        _snapshotService = snapshotService;
        _worldStateDatabase = worldStateDatabase;
        _marketHistory = marketHistory;
        _goodsService = goodsService;
        _commandBudgetService = commandBudgetService;
        _sessionRepository = sessionRepository;
        _healthCheckService = healthCheckService;
    }

    public async Task<AdminSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var metrics = _clockService.CurrentMetrics;
        var healthReport = await _healthCheckService.CheckHealthAsync(cancellationToken);
        var commandHistory = await _commandRepository.GetCommandHistoryAsync(20, cancellationToken);
        var recentSnapshots = _snapshotService.ListSnapshots(5);
        var connections = _webSocketHub.GetConnections();
        var activeSessions = await _sessionRepository.CountActiveSessionsAsync(cancellationToken);

        return new AdminSummaryDto
        {
            Tick = metrics.TickCount,
            WorldDate = metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            IsPaused = _clockService.IsPaused,
            LastTickDurationMs = metrics.TickDurationMs,
            AverageTickDurationMs = metrics.AverageTickDurationMs,
            ConnectedClients = _webSocketHub.ConnectedClientCount,
            ActiveSessions = activeSessions,
            ActiveSubscriptions = connections.Sum(connection => connection.Subscriptions.Count),
            PendingCommands = _commandQueue.PendingCount,
            LastTickDbWrites = metrics.LastTickDbWrites,
            TotalDbWrites = metrics.TotalDbWrites,
            CommandBudgets = _commandBudgetService.GetSnapshots(metrics.TickCount, DateTime.UtcNow)
                .Select(snapshot => new AdminCommandBudgetDto
                {
                    ActorId = snapshot.ActorId,
                    CountryId = snapshot.CountryId,
                    UsedInWindow = snapshot.UsedInWindow,
                    RemainingInWindow = snapshot.RemainingInWindow,
                    SoftLimit = snapshot.SoftLimit,
                    HardLimit = snapshot.HardLimit,
                    WindowSeconds = snapshot.WindowSeconds,
                    CooldownsRemainingTicks = new Dictionary<string, long>(snapshot.CooldownsRemainingTicks)
                })
                .ToList(),
            ServerHealth = healthReport.Status.ToString(),
            HealthChecks = healthReport.Entries.Select(entry => new AdminHealthCheckDto
            {
                Name = entry.Key,
                Status = entry.Value.Status.ToString(),
                Description = entry.Value.Description
            }).ToList(),
            Connections = connections.Select(connection => new AdminConnectionDto
            {
                ActorId = connection.ActorId,
                ConnectedAtUtc = connection.ConnectedAtUtc,
                Subscriptions = connection.Subscriptions.ToList()
            }).ToList(),
            RecentCommands = commandHistory.Select(history => new CommandHistoryDto
            {
                CommandId = history.CommandId,
                ActorId = history.ActorId,
                CommandType = history.CommandType,
                IssuedAt = history.IssuedAt,
                ReceivedAt = history.ReceivedAt,
                SubmittedTick = history.SubmittedTick,
                ExpectedWorldTick = history.ExpectedWorldTick,
                IdempotencyKey = history.IdempotencyKey,
                Status = history.Status,
                OutcomeStatus = history.OutcomeStatus,
                OutcomeReason = history.OutcomeReason,
                AppliedTick = history.AppliedTick,
                AppliedAt = history.AppliedAt
            }).ToList(),
            LatestSnapshot = _snapshotService.LatestSnapshot == null
                ? null
                : ToSnapshotDto(_snapshotService.LatestSnapshot),
            RecentSnapshots = recentSnapshots.Select(ToSnapshotDto).ToList(),
            TickProfile = metrics.StageDurationsMs,
            InvariantViolations = metrics.InvariantViolations.Select(violation => new AdminInvariantViolationDto
            {
                Code = violation.Code,
                Message = violation.Message
            }).ToList()
        };
    }

    public async Task<AdminMarketInspectorDto> GetMarketInspectorAsync(CancellationToken cancellationToken = default)
    {
        var metrics = _clockService.CurrentMetrics;
        var history = _marketHistory.GetHistory(20);
        var latest = _marketHistory.Latest;
        var previousTick = history.Count >= 2 ? history[^2] : null;
        var goodsById = _goodsService.All.ToDictionary(g => g.Id);

        var world = await _worldStateDatabase.LoadWorldAsync(cancellationToken);
        var avgNeeds = world?.Provinces.Count > 0
            ? world.Provinces.Average(p => (double)p.NeedsFulfillment)
            : 1.0;

        var goods = new List<AdminMarketGoodDto>();
        if (latest != null)
        {
            foreach (var (goodId, price) in latest.Prices)
            {
                goodsById.TryGetValue(goodId, out var def);
                var supply = latest.Supply.GetValueOrDefault(goodId);
                var demand = latest.Demand.GetValueOrDefault(goodId);
                var prevPrice = previousTick?.Prices.GetValueOrDefault(goodId) ?? price;

                goods.Add(AdminEconomyExplainer.ExplainGood(
                    goodId,
                    def?.DisplayName ?? goodId,
                    def,
                    price,
                    prevPrice,
                    supply,
                    demand,
                    world,
                    history));
            }
        }

        var topShortages = goods
            .Where(g => g.Demand > 0)
            .OrderBy(g => g.FulfillmentRate)
            .Take(5)
            .ToList();

        return new AdminMarketInspectorDto
        {
            Tick = metrics.TickCount,
            Goods = goods.OrderBy(g => g.Id).ToList(),
            TopShortages = topShortages,
            AverageNeedsFulfillment = (decimal)avgNeeds,
            PriceHistoryTicks = history.Count
        };
    }

    public async Task<AdminProvinceInspectorDto?> GetProvinceInspectorAsync(string provinceId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(provinceId, out var parsedProvinceId))
            return null;

        var world = await _worldStateDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var province = world.Provinces.FirstOrDefault(p => p.Id.Value == parsedProvinceId);
        if (province == null)
            return null;

        var owner = world.Countries.FirstOrDefault(c => c.Id.Value == province.OwnerId.Value);
        var queue = await _worldStateDatabase.LoadBuildingQueueAsync(cancellationToken);

        var factories = world.Factories
            .Where(f => f.ProvinceId.HasValue && f.ProvinceId.Value.Value == parsedProvinceId)
            .OrderBy(f => f.Type, StringComparer.Ordinal)
            .Select(ToFactoryDto)
            .ToList();

        return new AdminProvinceInspectorDto
        {
            ProvinceId = province.Id.Value.ToString(),
            Name = province.Name,
            OwnerId = province.OwnerId.Value.ToString(),
            OwnerName = owner?.Name ?? "Unknown",
            MarketId = province.MarketId.Value.ToString(),
            Population = province.Population,
            Workforce = province.Population,
            RgoType = province.RgoType,
            OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
            LocalDemand = AdminEconomyExplainer.EstimateProvinceDemand(province),
            NeedsFulfillment = province.NeedsFulfillment,
            PopGroups = ProvincePopGroupMapper.ToAdminProvincePopGroups(province),
            Construction = queue
                .Where(item => item.ProvinceId == parsedProvinceId)
                .Select(item => ToConstructionDto(item, province.Name))
                .ToList(),
            Factories = factories
        };
    }

    private static AdminFactoryDto ToFactoryDto(Factory factory) => new()
    {
        Id = factory.Id.ToString(),
        Type = factory.Type,
        Level = Math.Max(1, factory.Level),
        OutputGood = factory.OutputGood,
        OutputPerTick = factory.OutputPerTick,
        EmployedCraftsmen = factory.EmployedCraftsmen,
        EmployedClerks = factory.EmployedClerks,
        InputGoods = new Dictionary<string, decimal>(factory.InputGoods),
        CashReserve = factory.CashReserve,
        ProfitLastTick = factory.ProfitLastTick
    };

    public async Task<AdminCountryInspectorDto?> GetCountryInspectorAsync(string countryId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(countryId, out var parsedCountryId))
            return null;

        var world = await _worldStateDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var country = world.Countries.FirstOrDefault(c => c.Id.Value == parsedCountryId);
        if (country == null)
            return null;

        var provinces = world.Provinces.Where(p => p.OwnerId.Value == parsedCountryId).ToList();
        var controller = world.Players.FirstOrDefault(p => p.ControlledCountry.Value == parsedCountryId);
        var activeCommands = await _commandRepository.QueryAuditAsync(new CommandAuditQuery
        {
            CountryId = parsedCountryId.ToString(),
            OutcomeStatus = "accepted",
            Limit = 20
        }, cancellationToken);

        var marketInspector = await GetMarketInspectorAsync(cancellationToken);
        var pops = provinces.SelectMany(p => p.PopGroups).ToList();
        var totalEmployed = pops.Sum(p => (long)p.EmployedCount);
        var totalUnemployed = pops.Sum(p => (long)p.UnemployedCount);

        var popTypeBreakdown = pops
            .GroupBy(p => p.PopType, StringComparer.Ordinal)
            .Select(group =>
            {
                var size = group.Sum(p => (long)p.Size);
                if (size == 0)
                {
                    return new AdminCountryPopTypeDto
                    {
                        PopType = group.Key,
                        Strata = group.First().Strata,
                        Size = 0,
                        Employed = 0,
                        Unemployed = 0
                    };
                }
                return new AdminCountryPopTypeDto
                {
                    PopType = group.Key,
                    Strata = group.First().Strata,
                    Size = (int)Math.Min(int.MaxValue, size),
                    Employed = (int)Math.Min(int.MaxValue, group.Sum(p => (long)p.EmployedCount)),
                    Unemployed = (int)Math.Min(int.MaxValue, group.Sum(p => (long)p.UnemployedCount)),
                    AverageLiteracy = WeightedAverage(group, p => p.Literacy),
                    AverageMilitancy = WeightedAverage(group, p => p.Militancy),
                    AverageConsciousness = WeightedAverage(group, p => p.Consciousness),
                    AverageLifeNeeds = WeightedAverage(group, p => p.LifeNeedsFulfillment)
                };
            })
            .OrderByDescending(b => b.Size)
            .ToList();

        return new AdminCountryInspectorDto
        {
            CountryId = country.Id.Value.ToString(),
            Name = country.Name,
            Tag = country.Tag,
            Treasury = country.Treasury,
            TaxRate = country.TaxRate,
            ControlledAccountId = controller?.Id.Value.ToString(),
            ControlledUsername = controller?.Username,
            ProvinceCount = provinces.Count,
            Population = provinces.Sum(p => p.Population),
            ActiveCommands = activeCommands.Select(ToAuditDto).ToList(),
            MarketSummary = marketInspector.Goods,
            PoorTaxRate = country.PoorTaxRate,
            MiddleTaxRate = country.MiddleTaxRate,
            RichTaxRate = country.RichTaxRate,
            EducationSpending = country.EducationSpending,
            MilitarySpending = country.MilitarySpending,
            AdministrationSpending = country.AdministrationSpending,
            AverageLiteracy = WeightedAverage(pops, p => p.Literacy),
            AverageMilitancy = WeightedAverage(pops, p => p.Militancy),
            AverageConsciousness = WeightedAverage(pops, p => p.Consciousness),
            UnemploymentShare = (totalEmployed + totalUnemployed) > 0
                ? Math.Round((decimal)totalUnemployed / (totalEmployed + totalUnemployed), 4)
                : 0m,
            ReformPressure = _clockService.LatestSimulationMetrics
                .ReformPressureByCountry.TryGetValue(parsedCountryId.ToString(), out var pressure)
                ? Math.Round(pressure, 2)
                : 0m,
            PopTypeBreakdown = popTypeBreakdown,
            PopGroups = provinces
                .SelectMany(province => province.PopGroups
                    .Where(pop => pop != null && pop.Size > 0)
                    .Select(pop => new AdminCountryPopGroupDto
                    {
                        Id = pop.Id.ToString(),
                        ProvinceId = province.Id.Value.ToString(),
                        ProvinceName = province.Name,
                        PopType = pop.PopType,
                        Strata = pop.Strata,
                        Culture = pop.Culture,
                        Religion = pop.Religion,
                        Size = pop.Size,
                        EmployedCount = pop.EmployedCount,
                        UnemployedCount = pop.UnemployedCount,
                        Literacy = pop.Literacy,
                        Militancy = pop.Militancy,
                        LifeNeedsFulfillment = pop.LifeNeedsFulfillment
                    }))
                .OrderByDescending(g => g.Size)
                .ToList()
        };
    }

    private static decimal WeightedAverage(IEnumerable<PopGroup> pops, Func<PopGroup, decimal> selector)
    {
        long totalSize = 0;
        decimal weighted = 0m;
        foreach (var pop in pops)
        {
            if (pop.Size <= 0) continue;
            totalSize += pop.Size;
            weighted += selector(pop) * pop.Size;
        }
        return totalSize > 0 ? Math.Round(weighted / totalSize, 4) : 0m;
    }

    public AdminTickProfileDto GetTickProfile()
    {
        var metrics = _clockService.CurrentMetrics;
        return new AdminTickProfileDto
        {
            Tick = metrics.TickCount,
            TotalDurationMs = metrics.TickDurationMs,
            AverageDurationMs = metrics.AverageTickDurationMs,
            Stages = new Dictionary<string, long>(metrics.StageDurationsMs)
        };
    }

    public async Task<AdminSnapshotDto?> CreateSnapshotAsync(string? savepointName = null, CancellationToken cancellationToken = default)
    {
        var worldSnapshot = await _worldStateDatabase.LoadWorldAsync(cancellationToken);
        if (worldSnapshot == null)
            return null;

        var metrics = _clockService.CurrentMetrics;
        var metadata = await _snapshotService.SaveAsync(
            new WorldState
            {
                TickNumber = metrics.TickCount,
                    WorldTimestamp = metrics.WorldTimestamp,
                    LastSavedAt = System.DateTime.UtcNow
                },
                worldSnapshot,
                savepointName,
                cancellationToken);

        return ToSnapshotDto(metadata);
    }

    private static AdminSnapshotDto ToSnapshotDto(WorldSnapshotMetadata metadata)
    {
        return new AdminSnapshotDto
        {
            FileName = metadata.FileName,
            SavepointName = metadata.SavepointName,
            Tick = metadata.TickNumber,
            WorldDate = metadata.WorldTimestamp.ToString("yyyy-MM-dd"),
            CapturedAtUtc = metadata.CapturedAtUtc
        };
    }

    private static AdminConstructionQueueItemDto ToConstructionDto(BuildingQueueItem item, string provinceName)
    {
        return new AdminConstructionQueueItemDto
        {
            Id = item.Id.ToString(),
            ProvinceId = item.ProvinceId.ToString(),
            ProvinceName = provinceName,
            CountryId = item.CountryId.ToString(),
            BuildingType = item.BuildingType,
            TicksRemaining = item.TicksRemaining,
            QueuedAt = item.QueuedAt
        };
    }

    private static AdminCommandAuditRecordDto ToAuditDto(CommandAuditRecord record)
    {
        return new AdminCommandAuditRecordDto
        {
            CommandId = record.CommandId,
            ActorId = record.ActorId,
            CountryId = record.CountryId,
            CommandType = record.CommandType,
            TargetIds = record.TargetIds,
            SubmittedAt = record.SubmittedAt,
            SubmittedTick = record.SubmittedTick,
            ExpectedWorldTick = record.ExpectedWorldTick,
            IdempotencyKey = record.IdempotencyKey,
            ExecutedTick = record.ExecutedTick,
            ExecutedAt = record.ExecutedAt,
            Outcome = record.OutcomeStatus,
            OutcomeReason = record.OutcomeReason,
            RejectionReasonCode = record.RejectionReasonCode
        };
    }
}
