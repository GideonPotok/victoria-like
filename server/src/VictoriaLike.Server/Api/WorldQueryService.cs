using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;
using VictoriaLike.Server.Api.Dtos;

namespace VictoriaLike.Server.Api;

public interface IWorldQueryService
{
    Task<WorldSummaryDto?> GetWorldSummaryAsync(CancellationToken cancellationToken = default);
    Task<List<CountryDto>> ListCountriesAsync(CancellationToken cancellationToken = default);
    Task<List<ProvinceDto>> ListProvincesAsync(CancellationToken cancellationToken = default);
    Task<List<ProvinceDto>> ListProvincesAsync(string? ownerId, string? sort, string? order, CancellationToken cancellationToken = default);
    Task<ProvinceDetailDto?> GetProvinceDetailAsync(string provinceId, CancellationToken cancellationToken = default);
    Task<MarketSummaryDto?> GetMarketSummaryAsync(CancellationToken cancellationToken = default);
    Task<List<BuildingQueueItemDto>> GetBuildingQueueAsync(CancellationToken cancellationToken = default);
    Task<CountryInspectionDto?> GetCountryInspectionAsync(string countryId, CancellationToken cancellationToken = default);
    Task<ProvinceInspectionDto?> GetProvinceInspectionAsync(string provinceId, CancellationToken cancellationToken = default);
    Task<BudgetAdjustmentPreviewDto?> GetBudgetAdjustmentPreviewAsync(string countryId, string kind, string target, decimal proposedValue, CancellationToken cancellationToken = default);
    Task<List<ConstructionOptionPreviewDto>> GetConstructionOptionsAsync(string provinceId, CancellationToken cancellationToken = default);
    Task<List<WorldEventDto>> GetEventFeedAsync(string? countryId, int limit, CancellationToken cancellationToken = default);
    Task<List<ArmyStackDto>> ListArmiesAsync(string? countryId, CancellationToken cancellationToken = default);
    Task<List<WarDto>> ListWarsAsync(CancellationToken cancellationToken = default);
}

public class WorldQueryService : IWorldQueryService
{
    private readonly IWorldStateDatabase _worldDatabase;
    private readonly IWorldClockService _clockService;
    private readonly IGoodsService _goodsService;

    public WorldQueryService(IWorldStateDatabase worldDatabase, IWorldClockService clockService, IGoodsService goodsService)
    {
        _worldDatabase = worldDatabase;
        _clockService = clockService;
        _goodsService = goodsService;
    }

    public async Task<WorldSummaryDto?> GetWorldSummaryAsync(CancellationToken cancellationToken = default)
    {
        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var metrics = _clockService.CurrentMetrics;

        return new WorldSummaryDto
        {
            Tick = metrics.TickCount,
            WorldDate = metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            CountryCount = world.Countries.Count,
            ProvinceCount = world.Provinces.Count,
            MarketCount = world.Markets.Count
        };
    }

    public async Task<List<CountryDto>> ListCountriesAsync(CancellationToken cancellationToken = default)
    {
        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return new();

        var playerMap = world.Players.ToDictionary(player => player.ControlledCountry, player => player);

        return world.Countries.Select(c =>
        {
            playerMap.TryGetValue(c.Id, out var player);

            return new CountryDto
            {
                Id = c.Id.Value.ToString(),
                Name = c.Name,
                Tag = c.Tag,
                TaxRate = c.TaxRate,
                Treasury = c.Treasury,
                ProvinceCount = world.Provinces.Count(p => p.OwnerId.Value == c.Id.Value),
                ControllerActorId = player?.Id.Value.ToString(),
                ControllerUsername = player?.Username
            };
        }).ToList();
    }

    public Task<List<ProvinceDto>> ListProvincesAsync(CancellationToken cancellationToken = default) =>
        ListProvincesAsync(null, null, null, cancellationToken);

    public async Task<List<ProvinceDto>> ListProvincesAsync(string? ownerId, string? sort, string? order, CancellationToken cancellationToken = default)
    {
        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return new();

        var countryMap = world.Countries.ToDictionary(c => c.Id);

        IEnumerable<Province> source = world.Provinces;
        if (!string.IsNullOrWhiteSpace(ownerId) && Guid.TryParse(ownerId, out var ownerGuid))
            source = source.Where(p => p.OwnerId.Value == ownerGuid);

        var dtos = source.Select(p => new ProvinceDto
        {
            Id = p.Id.Value.ToString(),
            Name = p.Name,
            OwnerId = p.OwnerId.Value.ToString(),
            OwnerName = countryMap.TryGetValue(p.OwnerId, out var country) ? country.Name : "Unknown",
            MarketId = p.MarketId.Value.ToString(),
            Population = p.Population,
            RgoType = p.RgoType
        });

        var descending = string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase);
        Func<ProvinceDto, IComparable> keySelector = (sort ?? "name").ToLowerInvariant() switch
        {
            "population" => p => p.Population,
            "owner" => p => p.OwnerName,
            "rgo" => p => p.RgoType,
            _ => p => p.Name
        };
        dtos = descending ? dtos.OrderByDescending(keySelector) : dtos.OrderBy(keySelector);

        return dtos.ToList();
    }

    public async Task<ProvinceDetailDto?> GetProvinceDetailAsync(string provinceId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(provinceId, out var id))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var province = world.Provinces.FirstOrDefault(p => p.Id.Value == id);
        if (province == null)
            return null;

        var owner = world.Countries.FirstOrDefault(c => c.Id.Value == province.OwnerId.Value);
        var market = world.Markets.FirstOrDefault(m => m.Id.Value == province.MarketId.Value);

        return new ProvinceDetailDto
        {
            Id = province.Id.Value.ToString(),
            Name = province.Name,
            OwnerId = province.OwnerId.Value.ToString(),
            OwnerName = owner?.Name ?? "Unknown",
            MarketId = province.MarketId.Value.ToString(),
            MarketName = market?.Name ?? "Unknown",
            Population = province.Population,
            RgoType = province.RgoType,
            MarketGoods = market?.GoodPrices ?? new(),
            OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
            NeedsFulfillment = province.NeedsFulfillment,
            PopGroups = ProvincePopGroupMapper.ToProvincePopGroups(province)
        };
    }

    public async Task<MarketSummaryDto?> GetMarketSummaryAsync(CancellationToken cancellationToken = default)
    {
        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null || world.Markets.Count == 0)
            return null;

        var metrics = _clockService.CurrentMetrics;
        var market = world.Markets[0];
        var goodsById = _goodsService.All.ToDictionary(g => g.Id);

        var averageNeeds = world.Provinces.Count > 0
            ? world.Provinces.Average(p => (double)p.NeedsFulfillment)
            : 1.0;

        var goods = market.GoodPrices.Select(kvp =>
        {
            goodsById.TryGetValue(kvp.Key, out var def);
            var supply = market.GoodSupply.GetValueOrDefault(kvp.Key);
            var demand = market.GoodDemand.GetValueOrDefault(kvp.Key);
            return new MarketGoodDto
            {
                Id = kvp.Key,
                Name = def?.DisplayName ?? kvp.Key,
                Category = def?.Category ?? "unknown",
                BasePrice = def?.BasePrice ?? kvp.Value,
                Price = kvp.Value,
                Supply = supply,
                Demand = demand,
                FulfillmentRate = demand > 0 ? Math.Min(1m, supply / demand) : 1m
            };
        }).OrderBy(g => g.Id).ToList();

        return new MarketSummaryDto
        {
            Goods = goods,
            AverageNeedsFulfillment = (decimal)averageNeeds,
            Tick = metrics.TickCount
        };
    }

    public async Task<CountryInspectionDto?> GetCountryInspectionAsync(string countryId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(countryId, out var id))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var country = world.Countries.FirstOrDefault(c => c.Id.Value == id);
        if (country == null)
            return null;

        var provinces = world.Provinces.Where(p => p.OwnerId.Value == id).ToList();
        var pops = provinces.SelectMany(p => p.PopGroups).ToList();
        var warnings = ComputeMarketWarnings(world);
        var totalEmployed = pops.Sum(p => (long)p.EmployedCount);
        var totalUnemployed = pops.Sum(p => (long)p.UnemployedCount);

        var breakdown = pops
            .GroupBy(p => p.PopType, StringComparer.Ordinal)
            .Select(group => new CountryPopTypeDto
            {
                PopType = group.Key,
                Strata = group.First().Strata,
                Size = (int)Math.Min(int.MaxValue, group.Sum(p => (long)p.Size)),
                Employed = (int)Math.Min(int.MaxValue, group.Sum(p => (long)p.EmployedCount)),
                Unemployed = (int)Math.Min(int.MaxValue, group.Sum(p => (long)p.UnemployedCount)),
                AverageLiteracy = WeightedAverage(group, p => p.Literacy),
                AverageMilitancy = WeightedAverage(group, p => p.Militancy),
                AverageConsciousness = WeightedAverage(group, p => p.Consciousness),
                AverageLifeNeeds = WeightedAverage(group, p => p.LifeNeedsFulfillment)
            })
            .OrderByDescending(b => b.Size)
            .ToList();

        return new CountryInspectionDto
        {
            CountryId = country.Id.Value.ToString(),
            Name = country.Name,
            Tag = country.Tag,
            Treasury = country.Treasury,
            TaxRate = country.TaxRate,
            PoorTaxRate = country.PoorTaxRate,
            MiddleTaxRate = country.MiddleTaxRate,
            RichTaxRate = country.RichTaxRate,
            EducationSpending = country.EducationSpending,
            MilitarySpending = country.MilitarySpending,
            AdministrationSpending = country.AdministrationSpending,
            ProvinceCount = provinces.Count,
            Population = provinces.Sum(p => p.Population),
            AverageLiteracy = WeightedAverage(pops, p => p.Literacy),
            AverageMilitancy = WeightedAverage(pops, p => p.Militancy),
            AverageConsciousness = WeightedAverage(pops, p => p.Consciousness),
            UnemploymentShare = (totalEmployed + totalUnemployed) > 0
                ? Math.Round((decimal)totalUnemployed / (totalEmployed + totalUnemployed), 4)
                : 0m,
            PopTypeBreakdown = breakdown,
            MarketWarnings = warnings,
            ReformPressure = WeightedAverage(pops, p => p.Militancy),
            PopGroups = provinces.SelectMany(p => ProvincePopGroupMapper.ToProvincePopGroups(p)).ToList()
        };
    }

    private static List<MarketWarningDto> ComputeMarketWarnings(WorldStateSnapshot world)
    {
        if (world.Markets.Count == 0)
            return new List<MarketWarningDto>();

        var market = world.Markets[0];
        return market.GoodPrices
            .Select(kv =>
            {
                var supply = market.GoodSupply.GetValueOrDefault(kv.Key);
                var demand = market.GoodDemand.GetValueOrDefault(kv.Key);
                var fulfillment = demand > 0 ? Math.Min(1m, supply / demand) : 1m;
                return (good: kv.Key, price: kv.Value, supply, demand, fulfillment);
            })
            .Where(g => g.demand > 0 && g.fulfillment < 0.85m)
            .OrderBy(g => g.fulfillment)
            .Take(5)
            .Select(g => new MarketWarningDto
            {
                GoodId = g.good,
                Severity = g.fulfillment < 0.5m ? "critical" : g.fulfillment < 0.7m ? "high" : "warn",
                Price = g.price,
                Supply = g.supply,
                Demand = g.demand,
                FulfillmentRate = Math.Round(g.fulfillment, 4),
                Message = $"{g.good} demand {g.demand:F1} vs supply {g.supply:F1} (fulfilled {g.fulfillment:P0})"
            })
            .ToList();
    }

    public async Task<ProvinceInspectionDto?> GetProvinceInspectionAsync(string provinceId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(provinceId, out var id))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var province = world.Provinces.FirstOrDefault(p => p.Id.Value == id);
        if (province == null)
            return null;

        var owner = world.Countries.FirstOrDefault(c => c.Id.Value == province.OwnerId.Value);
        var factories = world.Factories
            .Where(f => f.ProvinceId.HasValue && f.ProvinceId.Value.Value == id)
            .OrderBy(f => f.Type, StringComparer.Ordinal)
            .Select(f => new ProvinceFactoryDto
            {
                Id = f.Id.ToString(),
                Type = f.Type,
                Level = Math.Max(1, f.Level),
                OutputGood = f.OutputGood,
                OutputPerTick = f.OutputPerTick,
                EmployedCraftsmen = f.EmployedCraftsmen,
                EmployedClerks = f.EmployedClerks,
                ProfitLastTick = f.ProfitLastTick
            })
            .ToList();

        return new ProvinceInspectionDto
        {
            ProvinceId = province.Id.Value.ToString(),
            Name = province.Name,
            OwnerId = province.OwnerId.Value.ToString(),
            OwnerName = owner?.Name ?? "Unknown",
            RgoType = province.RgoType,
            Population = province.Population,
            Workforce = province.Population,
            NeedsFulfillment = province.NeedsFulfillment,
            OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
            PopGroups = ProvincePopGroupMapper.ToProvincePopGroups(province),
            Factories = factories
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

    public async Task<List<BuildingQueueItemDto>> GetBuildingQueueAsync(CancellationToken cancellationToken = default)
    {
        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return new();

        var queue = await _worldDatabase.LoadBuildingQueueAsync(cancellationToken);
        var provinceMap = world.Provinces.ToDictionary(p => p.Id.Value);

        return queue.Select(item =>
        {
            provinceMap.TryGetValue(item.ProvinceId, out var province);
            return new BuildingQueueItemDto
            {
                Id = item.Id.ToString(),
                ProvinceId = item.ProvinceId.ToString(),
                ProvinceName = province?.Name ?? item.ProvinceId.ToString(),
                CountryId = item.CountryId.ToString(),
                BuildingType = item.BuildingType,
                TicksRemaining = item.TicksRemaining,
                QueuedAt = item.QueuedAt
            };
        }).ToList();
    }

    public async Task<List<WorldEventDto>> GetEventFeedAsync(
        string? countryId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        Guid? countryGuid = null;
        if (!string.IsNullOrWhiteSpace(countryId))
        {
            if (!Guid.TryParse(countryId, out var parsed))
                return [];

            countryGuid = parsed;
        }

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return [];

        var queue = await _worldDatabase.LoadBuildingQueueAsync(cancellationToken);
        var metrics = _clockService.CurrentMetrics;
        var goodsById = _goodsService.All.ToDictionary(g => g.Id, StringComparer.OrdinalIgnoreCase);
        var countriesById = world.Countries.ToDictionary(c => c.Id.Value);
        var provincesById = world.Provinces.ToDictionary(p => p.Id.Value);
        var events = new List<WorldEventDto>();

        AddBudgetAndPopEvents(events, world, countriesById, countryGuid, metrics);
        AddProvinceEvents(events, world, countriesById, countryGuid, metrics);
        AddMarketEvents(events, world, goodsById, metrics);
        AddConstructionEvents(events, queue, countriesById, provincesById, countryGuid, metrics);
        AddMilitaryEvents(events, world, countriesById, provincesById, countryGuid, metrics);

        return events
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .Select(g => g.First())
            .OrderBy(e => SeverityRank(e.Severity))
            .ThenBy(e => e.Category, StringComparer.Ordinal)
            .ThenBy(e => e.Title, StringComparer.Ordinal)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }

    public async Task<List<ArmyStackDto>> ListArmiesAsync(string? countryId, CancellationToken cancellationToken = default)
    {
        Guid? countryGuid = null;
        if (!string.IsNullOrWhiteSpace(countryId))
        {
            if (!Guid.TryParse(countryId, out var parsed))
                return [];
            countryGuid = parsed;
        }

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return [];

        var countriesById = world.Countries.ToDictionary(country => country.Id.Value);
        var provincesById = world.Provinces.ToDictionary(province => province.Id.Value);

        return world.Armies
            .Where(army => countryGuid == null || army.CountryId.Value == countryGuid.Value)
            .OrderBy(army => countriesById.TryGetValue(army.CountryId.Value, out var country) ? country.Name : "")
            .ThenBy(army => army.Id)
            .Select(army =>
            {
                countriesById.TryGetValue(army.CountryId.Value, out var country);
                provincesById.TryGetValue(army.LocationProvinceId.Value, out var location);
                Province? destination = null;
                if (army.DestinationProvinceId.HasValue)
                    provincesById.TryGetValue(army.DestinationProvinceId.Value.Value, out destination);

                return new ArmyStackDto
                {
                    Id = army.Id.ToString(),
                    CountryId = army.CountryId.Value.ToString(),
                    CountryName = country?.Name ?? "Unknown",
                    LocationProvinceId = army.LocationProvinceId.Value.ToString(),
                    LocationProvinceName = location?.Name ?? army.LocationProvinceId.Value.ToString(),
                    DestinationProvinceId = army.DestinationProvinceId?.Value.ToString(),
                    DestinationProvinceName = destination?.Name,
                    MovementTicksRemaining = army.MovementTicksRemaining,
                    SoldierCount = army.SoldierCount,
                    Morale = army.Morale,
                    IsMoving = army.DestinationProvinceId.HasValue && army.MovementTicksRemaining > 0
                };
            })
            .ToList();
    }

    public async Task<List<WarDto>> ListWarsAsync(CancellationToken cancellationToken = default)
    {
        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return [];

        var countriesById = world.Countries.ToDictionary(country => country.Id.Value);
        return world.Wars
            .OrderByDescending(war => war.IsActive)
            .ThenByDescending(war => war.StartedAt)
            .Select(war =>
            {
                countriesById.TryGetValue(war.AttackerCountryId.Value, out var attacker);
                countriesById.TryGetValue(war.DefenderCountryId.Value, out var defender);
                return new WarDto
                {
                    Id = war.Id.ToString(),
                    AttackerCountryId = war.AttackerCountryId.Value.ToString(),
                    AttackerCountryName = attacker?.Name ?? "Unknown",
                    DefenderCountryId = war.DefenderCountryId.Value.ToString(),
                    DefenderCountryName = defender?.Name ?? "Unknown",
                    StartedAt = war.StartedAt,
                    EndedAt = war.EndedAt,
                    IsActive = war.IsActive
                };
            })
            .ToList();
    }

    public async Task<BudgetAdjustmentPreviewDto?> GetBudgetAdjustmentPreviewAsync(
        string countryId,
        string kind,
        string target,
        decimal proposedValue,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(countryId, out var id))
            return null;

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return null;

        var country = world.Countries.FirstOrDefault(c => c.Id.Value == id);
        if (country == null)
            return null;

        var provinces = world.Provinces.Where(p => p.OwnerId.Value == id).ToList();
        var population = provinces.Sum(GetEffectivePopulation);
        kind = kind.Trim().ToLowerInvariant();
        target = target.Trim().ToLowerInvariant();
        proposedValue = NormalizeUnitValue(proposedValue);

        var dto = new BudgetAdjustmentPreviewDto
        {
            CountryId = countryId,
            Kind = kind,
            Target = target,
            ProposedValue = proposedValue
        };

        if (kind == "spending")
        {
            dto.CurrentValue = target switch
            {
                "military" => NormalizeUnitValue(country.MilitarySpending),
                "administration" => NormalizeUnitValue(country.AdministrationSpending),
                _ => NormalizeUnitValue(country.EducationSpending)
            };

            dto.EstimatedWeeklySpendingCostCurrent = EstimateWeeklySpendingCost(
                population,
                NormalizeUnitValue(country.EducationSpending),
                NormalizeUnitValue(country.MilitarySpending),
                NormalizeUnitValue(country.AdministrationSpending));

            var education = target == "education" ? proposedValue : NormalizeUnitValue(country.EducationSpending);
            var military = target == "military" ? proposedValue : NormalizeUnitValue(country.MilitarySpending);
            var administration = target == "administration" ? proposedValue : NormalizeUnitValue(country.AdministrationSpending);

            dto.EstimatedWeeklySpendingCostProposed = EstimateWeeklySpendingCost(population, education, military, administration);
            dto.EstimatedWeeklySpendingCostDelta = dto.EstimatedWeeklySpendingCostProposed - dto.EstimatedWeeklySpendingCostCurrent;
            dto.Summary = $"{Capitalize(target)} spending {dto.CurrentValue:P0} -> {dto.ProposedValue:P0}; estimated weekly state spend {FormatSignedCurrency(dto.EstimatedWeeklySpendingCostDelta.Value)}";
            dto.Effects = target switch
            {
                "education" =>
                [
                    proposedValue >= dto.CurrentValue ? "Higher education spending should improve literacy growth and support clergy/clerks income." : "Lower education spending should reduce state cost but slow literacy support.",
                    "Observed literacy and POP conditions still come from authoritative simulation ticks."
                ],
                "military" =>
                [
                    proposedValue >= dto.CurrentValue ? "Higher military spending should support soldier income and lower militancy pressure among soldiers." : "Lower military spending should reduce state cost but weaken soldier support.",
                    "Observed POP outcomes still come from authoritative simulation ticks."
                ],
                _ =>
                [
                    proposedValue >= dto.CurrentValue ? "Higher administration spending should support bureaucrat income and increase administrative support." : "Lower administration spending should reduce state cost but weaken administrative support.",
                    "Observed POP outcomes still come from authoritative simulation ticks."
                ]
            };

            return dto;
        }

        if (kind == "tax")
        {
            dto.CurrentValue = target switch
            {
                "flat" => Math.Clamp(country.TaxRate / 100m, 0m, 1m),
                "middle" => NormalizeTaxOverride(country.MiddleTaxRate, country.TaxRate),
                "rich" => NormalizeTaxOverride(country.RichTaxRate, country.TaxRate),
                _ => NormalizeTaxOverride(country.PoorTaxRate, country.TaxRate)
            };

            dto.Summary = $"{Capitalize(target)} tax {dto.CurrentValue:P0} -> {dto.ProposedValue:P0}";
            dto.Effects =
            [
                proposedValue >= dto.CurrentValue
                    ? $"Higher {target} tax should increase extraction pressure on that strata and may worsen needs fulfillment over time."
                    : $"Lower {target} tax should reduce extraction pressure on that strata and may ease needs fulfillment pressure over time.",
                "Treasury effects are indirect and remain authoritative server outcomes rather than a guaranteed local estimate."
            ];

            return dto;
        }

        return null;
    }

    public async Task<List<ConstructionOptionPreviewDto>> GetConstructionOptionsAsync(string provinceId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(provinceId, out var id))
            return [];

        var world = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (world == null)
            return [];

        var province = world.Provinces.FirstOrDefault(p => p.Id.Value == id);
        if (province == null)
            return [];

        var country = world.Countries.FirstOrDefault(c => c.Id.Value == province.OwnerId.Value);
        if (country == null)
            return [];

        var hasActiveConstruction = world.BuildingQueue.Any(entry => entry.ProvinceId == id);

        return BuildingTemplates.All.Values
            .OrderBy(template => template.Type, StringComparer.Ordinal)
            .Select(template =>
            {
                var available = true;
                string? rejectionReason = null;
                string message;

                if (hasActiveConstruction)
                {
                    available = false;
                    rejectionReason = CommandRejectionReason.ActiveConstructionConflict.ToString();
                    message = "This province already has active construction queued.";
                }
                else if (country.Treasury < template.Cost)
                {
                    available = false;
                    rejectionReason = CommandRejectionReason.InsufficientTreasury.ToString();
                    message = $"Need {template.Cost:F0}, have {country.Treasury:F2}.";
                }
                else
                {
                    message = BuildConstructionOptionMessage(template);
                }

                return new ConstructionOptionPreviewDto
                {
                    BuildingType = template.Type,
                    Available = available,
                    RejectionReason = rejectionReason,
                    Message = message,
                    Cost = template.Cost,
                    BuildTicks = template.BuildTicks,
                    TreasuryAfterCommand = available ? country.Treasury - template.Cost : null,
                    OutputPerTick = new Dictionary<string, decimal>(template.OutputPerTick)
                };
            })
            .ToList();
    }

    private static string BuildConstructionOptionMessage(BuildingTemplate template)
    {
        var effect = template.Factory is not null
            ? $"opens a {template.Factory.Type.Replace('_', ' ')}"
            : template.InfrastructureDelta != 0m
                ? $"infrastructure +{template.InfrastructureDelta:P0}"
                : "province output modifier";

        return $"Available now. Cost {template.Cost:F0}; completes in {template.BuildTicks} tick(s); {effect}.";
    }

    private static decimal NormalizeUnitValue(decimal value) =>
        value > 1m
            ? Math.Clamp(value / 100m, 0m, 1m)
            : Math.Clamp(value, 0m, 1m);

    private static int GetEffectivePopulation(Province province)
    {
        if (province.PopGroups.Count == 0)
            return province.Population;

        var popTotal = province.PopGroups.Sum(g => g.Size);
        return popTotal > 0 ? popTotal : province.Population;
    }

    private static decimal NormalizeTaxOverride(decimal overrideRate, int fallbackTaxRate)
    {
        if (overrideRate < 0m)
            return Math.Clamp(fallbackTaxRate / 100m, 0m, 1m);

        return NormalizeUnitValue(overrideRate);
    }

    private static decimal EstimateWeeklySpendingCost(int population, decimal education, decimal military, decimal administration)
    {
        var populationScale = population / 1000m;
        return populationScale * ((education * 0.10m) + (military * 0.12m) + (administration * 0.08m));
    }

    private static void AddBudgetAndPopEvents(
        List<WorldEventDto> events,
        WorldStateSnapshot world,
        IReadOnlyDictionary<Guid, Country> countriesById,
        Guid? countryFilter,
        TickMetrics metrics)
    {
        foreach (var country in world.Countries.Where(c => countryFilter == null || c.Id.Value == countryFilter.Value))
        {
            var provinces = world.Provinces.Where(p => p.OwnerId.Value == country.Id.Value).ToList();
            var pops = provinces.SelectMany(p => p.PopGroups).ToList();

            if (country.Treasury < 0m)
            {
                events.Add(CreateEvent(
                    "budget",
                    "critical",
                    $"budget:treasury-negative:{country.Id.Value}",
                    "Treasury deficit",
                    $"{country.Name} has a negative treasury of £{country.Treasury:F2}.",
                    "budget",
                    metrics,
                    country));
            }
            else if (country.Treasury < 1_000m)
            {
                events.Add(CreateEvent(
                    "budget",
                    "warn",
                    $"budget:treasury-low:{country.Id.Value}",
                    "Treasury running low",
                    $"{country.Name} has £{country.Treasury:F2} remaining.",
                    "budget",
                    metrics,
                    country));
            }

            AddTaxPressureEvent(events, country, "poor", NormalizeTaxOverride(country.PoorTaxRate, country.TaxRate), metrics);
            AddTaxPressureEvent(events, country, "middle", NormalizeTaxOverride(country.MiddleTaxRate, country.TaxRate), metrics);
            AddTaxPressureEvent(events, country, "rich", NormalizeTaxOverride(country.RichTaxRate, country.TaxRate), metrics);

            if (NormalizeUnitValue(country.EducationSpending) < 0.25m)
            {
                events.Add(CreateEvent(
                    "budget",
                    "info",
                    $"budget:education-low:{country.Id.Value}",
                    "Education spending is low",
                    $"{country.Name} is spending {NormalizeUnitValue(country.EducationSpending):P0} on education.",
                    "budget",
                    metrics,
                    country));
            }

            if (pops.Count == 0)
                continue;

            var totalWorkforce = pops.Sum(p => (long)p.EmployedCount + p.UnemployedCount);
            var totalUnemployed = pops.Sum(p => (long)p.UnemployedCount);
            var unemployment = totalWorkforce > 0 ? (decimal)totalUnemployed / totalWorkforce : 0m;
            if (unemployment >= 0.15m)
            {
                events.Add(CreateEvent(
                    "population",
                    unemployment >= 0.30m ? "critical" : "warn",
                    $"pop:unemployment:{country.Id.Value}",
                    "Unemployment is rising",
                    $"{country.Name} unemployment is {unemployment:P1}.",
                    "population",
                    metrics,
                    country));
            }

            var averageLifeNeeds = WeightedAverage(pops, p => p.LifeNeedsFulfillment);
            if (averageLifeNeeds < 0.85m)
            {
                events.Add(CreateEvent(
                    "population",
                    averageLifeNeeds < 0.50m ? "critical" : "warn",
                    $"pop:life-needs:{country.Id.Value}",
                    "Life needs are under pressure",
                    $"{country.Name} average life needs fulfillment is {averageLifeNeeds:P0}.",
                    "population",
                    metrics,
                    country));
            }

            var averageMilitancy = WeightedAverage(pops, p => p.Militancy);
            if (averageMilitancy >= 4m)
            {
                events.Add(CreateEvent(
                    "population",
                    averageMilitancy >= 7m ? "critical" : "warn",
                    $"pop:militancy:{country.Id.Value}",
                    "Militancy warning",
                    $"{country.Name} average militancy is {averageMilitancy:F2}.",
                    "population",
                    metrics,
                    country));
            }
        }

        static void AddTaxPressureEvent(List<WorldEventDto> events, Country country, string strata, decimal rate, TickMetrics metrics)
        {
            if (rate < 0.50m)
                return;

            events.Add(CreateEvent(
                "budget",
                rate >= 0.75m ? "critical" : "warn",
                $"budget:tax-pressure:{country.Id.Value}:{strata}",
                $"{Capitalize(strata)} tax pressure",
                $"{country.Name} {strata} tax is {rate:P0}.",
                "budget",
                metrics,
                country));
        }
    }

    private static void AddProvinceEvents(
        List<WorldEventDto> events,
        WorldStateSnapshot world,
        IReadOnlyDictionary<Guid, Country> countriesById,
        Guid? countryFilter,
        TickMetrics metrics)
    {
        foreach (var province in world.Provinces.Where(p => countryFilter == null || p.OwnerId.Value == countryFilter.Value))
        {
            countriesById.TryGetValue(province.OwnerId.Value, out var country);

            if (province.NeedsFulfillment < 0.85m)
            {
                events.Add(CreateEvent(
                    "province",
                    province.NeedsFulfillment < 0.50m ? "critical" : "warn",
                    $"province:needs:{province.Id.Value}",
                    $"{province.Name} hardship",
                    $"{province.Name} needs fulfillment is {province.NeedsFulfillment:P0}.",
                    "province",
                    metrics,
                    country,
                    province));
            }

            var pops = province.PopGroups;
            var workforce = pops.Sum(p => (long)p.EmployedCount + p.UnemployedCount);
            var unemployed = pops.Sum(p => (long)p.UnemployedCount);
            var unemployment = workforce > 0 ? (decimal)unemployed / workforce : 0m;
            if (unemployment >= 0.20m)
            {
                events.Add(CreateEvent(
                    "province",
                    unemployment >= 0.35m ? "critical" : "warn",
                    $"province:unemployment:{province.Id.Value}",
                    $"{province.Name} unemployment",
                    $"{province.Name} unemployment is {unemployment:P1}.",
                    "province",
                    metrics,
                    country,
                    province));
            }
        }
    }

    private static void AddMarketEvents(
        List<WorldEventDto> events,
        WorldStateSnapshot world,
        IReadOnlyDictionary<string, GoodDefinition> goodsById,
        TickMetrics metrics)
    {
        foreach (var market in world.Markets)
        {
            foreach (var (goodId, price) in market.GoodPrices)
            {
                goodsById.TryGetValue(goodId, out var definition);
                var displayName = definition?.DisplayName ?? goodId;
                var supply = market.GoodSupply.GetValueOrDefault(goodId);
                var demand = market.GoodDemand.GetValueOrDefault(goodId);
                var fulfillment = demand > 0m ? Math.Min(1m, supply / demand) : 1m;

                if (demand > 0m && fulfillment < 0.85m)
                {
                    events.Add(CreateEvent(
                        "market",
                        fulfillment < 0.50m ? "critical" : fulfillment < 0.70m ? "warn" : "info",
                        $"market:shortage:{market.Id.Value}:{goodId}",
                        $"{Capitalize(displayName)} shortage",
                        $"{displayName} demand {demand:F1} vs supply {supply:F1} ({fulfillment:P0} fulfilled).",
                        "market",
                        metrics,
                        marketId: market.Id.Value.ToString(),
                        goodId: goodId));
                }

                if (definition != null && definition.BasePrice > 0m && price >= definition.BasePrice * 1.5m)
                {
                    events.Add(CreateEvent(
                        "market",
                        price >= definition.BasePrice * 2m ? "warn" : "info",
                        $"market:price-spike:{market.Id.Value}:{goodId}",
                        $"{definition.DisplayName} price spike",
                        $"{definition.DisplayName} is £{price:F2}, above base £{definition.BasePrice:F2}.",
                        "market",
                        metrics,
                        marketId: market.Id.Value.ToString(),
                        goodId: goodId));
                }
            }
        }
    }

    private static void AddConstructionEvents(
        List<WorldEventDto> events,
        IReadOnlyList<BuildingQueueItem> queue,
        IReadOnlyDictionary<Guid, Country> countriesById,
        IReadOnlyDictionary<Guid, Province> provincesById,
        Guid? countryFilter,
        TickMetrics metrics)
    {
        foreach (var item in queue.Where(q => countryFilter == null || q.CountryId == countryFilter.Value))
        {
            countriesById.TryGetValue(item.CountryId, out var country);
            provincesById.TryGetValue(item.ProvinceId, out var province);

            events.Add(CreateEvent(
                "construction",
                item.TicksRemaining <= 1 ? "info" : "info",
                $"construction:queued:{item.Id}",
                $"{Capitalize(item.BuildingType)} under construction",
                $"{Capitalize(item.BuildingType)} in {province?.Name ?? item.ProvinceId.ToString()} has {item.TicksRemaining} tick(s) remaining.",
                "province",
                metrics,
                country,
                province));
        }
    }

    private static void AddMilitaryEvents(
        List<WorldEventDto> events,
        WorldStateSnapshot world,
        IReadOnlyDictionary<Guid, Country> countriesById,
        IReadOnlyDictionary<Guid, Province> provincesById,
        Guid? countryFilter,
        TickMetrics metrics)
    {
        foreach (var war in world.Wars.Where(w =>
                     w.IsActive &&
                     (countryFilter == null ||
                      w.AttackerCountryId.Value == countryFilter.Value ||
                      w.DefenderCountryId.Value == countryFilter.Value)))
        {
            countriesById.TryGetValue(war.AttackerCountryId.Value, out var attacker);
            countriesById.TryGetValue(war.DefenderCountryId.Value, out var defender);
            events.Add(CreateEvent(
                "military",
                "warn",
                $"war:active:{war.Id}",
                "War active",
                $"{attacker?.Name ?? war.AttackerCountryId.Value.ToString()} is at war with {defender?.Name ?? war.DefenderCountryId.Value.ToString()}.",
                "diplomacy",
                metrics,
                countryFilter == war.AttackerCountryId.Value ? attacker : defender));
        }

        foreach (var army in world.Armies.Where(a => countryFilter == null || a.CountryId.Value == countryFilter.Value))
        {
            countriesById.TryGetValue(army.CountryId.Value, out var country);
            provincesById.TryGetValue(army.LocationProvinceId.Value, out var location);
            Province? destination = null;
            if (army.DestinationProvinceId.HasValue)
                provincesById.TryGetValue(army.DestinationProvinceId.Value.Value, out destination);

            if (army.DestinationProvinceId.HasValue && army.MovementTicksRemaining > 0)
            {
                events.Add(CreateEvent(
                    "military",
                    "info",
                    $"army:moving:{army.Id}",
                    "Army moving",
                    $"{country?.Name ?? "Army"} stack is moving from {location?.Name ?? army.LocationProvinceId.Value.ToString()} to {destination?.Name ?? army.DestinationProvinceId.Value.Value.ToString()} ({army.MovementTicksRemaining} tick(s)).",
                    "military",
                    metrics,
                    country,
                    location));
            }

            if (army.Morale < 0.3m || army.SoldierCount <= 0)
            {
                events.Add(CreateEvent(
                    "military",
                    army.SoldierCount <= 0 ? "critical" : "warn",
                    $"army:damaged:{army.Id}",
                    "Army damaged",
                    $"{country?.Name ?? "Army"} stack has {army.SoldierCount} soldiers and {army.Morale:P0} morale.",
                    "military",
                    metrics,
                    country,
                    location));
            }
        }
    }

    private static WorldEventDto CreateEvent(
        string category,
        string severity,
        string id,
        string title,
        string message,
        string targetPanel,
        TickMetrics metrics,
        Country? country = null,
        Province? province = null,
        string? marketId = null,
        string? goodId = null) =>
        new()
        {
            Id = id,
            Category = category,
            Severity = severity,
            Tick = metrics.TickCount,
            WorldDate = metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            Title = title,
            Message = message,
            TargetPanel = targetPanel,
            CountryId = country?.Id.Value.ToString(),
            CountryName = country?.Name,
            ProvinceId = province?.Id.Value.ToString(),
            ProvinceName = province?.Name,
            MarketId = marketId,
            GoodId = goodId
        };

    private static int SeverityRank(string severity) =>
        severity switch
        {
            "critical" => 0,
            "warn" => 1,
            "info" => 2,
            _ => 3
        };

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];

    private static string FormatSignedCurrency(decimal value) =>
        value >= 0m ? $"+£{value:F2}" : $"-£{Math.Abs(value):F2}";
}
