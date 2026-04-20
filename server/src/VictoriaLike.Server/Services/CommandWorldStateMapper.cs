using System;
using System.Collections.Generic;
using System.Linq;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Data;
using SimulationWorldState = VictoriaLike.Core.Core.World.WorldState;
using SimulationGameDate = VictoriaLike.Core.Core.World.GameDate;
using SimulationProvinceState = VictoriaLike.Core.Core.World.ProvinceState;
using SimulationMetrics = VictoriaLike.Core.Core.World.SimulationMetrics;
using PopState = VictoriaLike.Core.Core.Pops.PopState;

namespace VictoriaLike.Server.Services;

public static class CommandWorldStateMapper
{
    // Life-need consumption per 1000 population per tick — matches economy_mvp.md spec
    private static readonly Dictionary<string, decimal> LifeNeeds = new()
    {
        ["grain"] = 0.5m,
        ["fish"] = 0.2m
    };

    public static SimulationWorldState ToSimulationWorld(
        WorldStateSnapshot snapshot,
        DateTime worldTimestamp,
        IReadOnlyList<GoodDefinition> goods)
    {
        var provinceIdsByCountry = snapshot.Provinces
            .GroupBy(province => province.OwnerId)
            .ToDictionary(
                group => group.Key.Value.ToString(),
                group => group.Select(province => province.Id.Value.ToString()).ToList());

        var countries = snapshot.Countries.ToDictionary(
            country => country.Id.Value.ToString(),
            country => new CountryState
            {
                Id = country.Id.Value.ToString(),
                DisplayName = country.Name,
                ProvinceIds = provinceIdsByCountry.TryGetValue(country.Id.Value.ToString(), out var ids)
                    ? ids
                    : new List<string>(),
                Treasury = country.Treasury,
                TaxRate = country.TaxRate,
                PoorTaxRate = country.PoorTaxRate,
                MiddleTaxRate = country.MiddleTaxRate,
                RichTaxRate = country.RichTaxRate,
                TariffRate = country.TariffRate,
                EducationSpending = country.EducationSpending,
                MilitarySpending = country.MilitarySpending,
                AdministrationSpending = country.AdministrationSpending,
                IsPlayable = true
            });

        var provinces = new Dictionary<string, SimulationProvinceState>();
        var pops = new Dictionary<string, PopState>();

        foreach (var province in snapshot.Provinces)
        {
            var provinceKey = province.Id.Value.ToString();
            var provincePopIds = province.PopGroups.Count > 0
                ? province.PopGroups.Select(pop => pop.Id.ToString()).ToList()
                : new List<string> { $"pop-{provinceKey}" };

            provinces[provinceKey] = new SimulationProvinceState
            {
                Id = provinceKey,
                DisplayName = province.Name,
                OwnerId = province.OwnerId.Value.ToString(),
                RgoType = province.RgoType,
                OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
                Infrastructure = 0m,
                PopulationIds = provincePopIds
            };

            if (province.PopGroups.Count == 0)
            {
                var popId = $"pop-{provinceKey}";
                pops[popId] = new PopState
                {
                    Id = popId,
                    ProvinceId = provinceKey,
                    PopClass = "farmers",
                    Size = province.Population,
                    Needs = new PopNeedProfile
                    {
                        Life = new Dictionary<string, decimal>(LifeNeeds),
                        Everyday = new Dictionary<string, decimal>(),
                        Luxury = new Dictionary<string, decimal>()
                    }
                };
            }
            else
            {
                foreach (var pop in province.PopGroups)
                {
                    var popId = pop.Id.ToString();
                    pops[popId] = new PopState
                    {
                        Id = popId,
                        ProvinceId = provinceKey,
                        PopClass = pop.PopType,
                        Size = pop.Size,
                        CashReserve = pop.Cash,
                        Militancy = pop.Militancy,
                        Consciousness = pop.Consciousness,
                        Literacy = pop.Literacy,
                        NeedsFulfillment = pop.LifeNeedsFulfillment,
                        LifeNeedsFulfillment = pop.LifeNeedsFulfillment,
                        EverydayNeedsFulfillment = pop.EverydayNeedsFulfillment,
                        LuxuryNeedsFulfillment = pop.LuxuryNeedsFulfillment,
                        EmployedCount = pop.EmployedCount,
                        UnemployedCount = pop.UnemployedCount,
                        ArtisanProducedGood = pop.ArtisanProducedGood,
                        ArtisanDaysUntilReconsider = Math.Max(0, pop.ArtisanDaysUntilReconsider),
                        ArtisanLastReconsideredAt = pop.ArtisanLastReconsideredAt.HasValue
                            ? DateOnly.FromDateTime(pop.ArtisanLastReconsideredAt.Value)
                            : null,
                        ArtisanProfitLastTick = pop.ArtisanProfitLastTick,
                        Needs = new PopNeedProfile
                        {
                            Life = new Dictionary<string, decimal>(LifeNeeds),
                            Everyday = new Dictionary<string, decimal>(),
                            Luxury = new Dictionary<string, decimal>()
                        }
                    };
                }
            }
        }

        var prices = snapshot.Markets
            .SelectMany(market => market.GoodPrices)
            .GroupBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Value, StringComparer.OrdinalIgnoreCase);

        var buildingQueue = snapshot.BuildingQueue.Select(item => new BuildingQueueEntry
        {
            Id = item.Id.ToString(),
            ProvinceId = item.ProvinceId.ToString(),
            CountryId = item.CountryId.ToString(),
            BuildingType = item.BuildingType,
            TicksRemaining = item.TicksRemaining,
            QueuedAt = item.QueuedAt
        }).ToList();

        return new SimulationWorldState
        {
            Seed = 42,
            Date = new SimulationGameDate(DateOnly.FromDateTime(worldTimestamp)),
            Countries = countries,
            Provinces = provinces,
            Pops = pops,
            Factories = snapshot.Factories.ToDictionary(
                factory => factory.Id.ToString(),
                factory => new FactoryState
                {
                    Id = factory.Id.ToString(),
                    CountryId = factory.CountryId.Value.ToString(),
                    ProvinceId = factory.ProvinceId?.Value.ToString(),
                    Type = factory.Type,
                    Level = Math.Max(1, factory.Level),
                    EmployedCraftsmen = Math.Max(0, factory.EmployedCraftsmen),
                    EmployedClerks = Math.Max(0, factory.EmployedClerks),
                    InputGoods = new Dictionary<string, decimal>(factory.InputGoods),
                    OutputGood = factory.OutputGood,
                    OutputPerTick = Math.Max(0m, factory.OutputPerTick),
                    CashReserve = Math.Max(0m, factory.CashReserve),
                    ProfitLastTick = factory.ProfitLastTick
                }),
            Armies = snapshot.Armies.ToDictionary(
                army => army.Id.ToString(),
                army => new ArmyStackState
                {
                    Id = army.Id.ToString(),
                    CountryId = army.CountryId.Value.ToString(),
                    LocationProvinceId = army.LocationProvinceId.Value.ToString(),
                    DestinationProvinceId = army.DestinationProvinceId?.Value.ToString(),
                    MovementTicksRemaining = Math.Max(0, army.MovementTicksRemaining),
                    SoldierCount = Math.Max(0, army.SoldierCount),
                    Morale = Math.Clamp(army.Morale, 0m, 1m)
                }),
            Wars = snapshot.Wars.ToDictionary(
                war => war.Id.ToString(),
                war => new WarState
                {
                    Id = war.Id.ToString(),
                    AttackerCountryId = war.AttackerCountryId.Value.ToString(),
                    DefenderCountryId = war.DefenderCountryId.Value.ToString(),
                    StartedOn = DateOnly.FromDateTime(war.StartedAt),
                    EndedOn = war.EndedAt.HasValue ? DateOnly.FromDateTime(war.EndedAt.Value) : null,
                    IsActive = war.IsActive
                }),
            BattleReports = snapshot.BattleReports.ToDictionary(
                battle => battle.Id,
                battle => new BattleReportState
                {
                    Id = battle.Id,
                    WarId = battle.WarId.ToString(),
                    ProvinceId = battle.ProvinceId.ToString(),
                    WinnerArmyId = battle.WinnerArmyId.ToString(),
                    LoserArmyId = battle.LoserArmyId.ToString(),
                    WinnerCountryId = battle.WinnerCountryId.ToString(),
                    LoserCountryId = battle.LoserCountryId.ToString(),
                    OccurredOn = DateOnly.FromDateTime(battle.OccurredAt),
                    WinnerCasualties = battle.WinnerCasualties,
                    LoserCasualties = battle.LoserCasualties,
                    WinnerMoraleAfter = battle.WinnerMoraleAfter,
                    LoserMoraleAfter = battle.LoserMoraleAfter
                }),
            GoodProfitHistory = snapshot.GoodProfitHistory.Select(entry => new GoodProfitHistoryEntry
            {
                Month = entry.Month,
                GoodId = entry.GoodId,
                AverageProducerProfit = entry.AverageProducerProfit,
                ProducerCount = Math.Max(0, entry.ProducerCount)
            }).ToList(),
            PlayerAccounts = snapshot.Players.ToDictionary(player => player.Id.Value.ToString(), player => player),
            Goods = goods.ToDictionary(good => good.Id, good => good),
            Market = new MarketState { Prices = prices },
            BuildingQueue = buildingQueue,
            Metrics = new SimulationMetrics(),
            EventLog = new List<string>()
        };
    }

    public static List<Country> ToPersistedCountries(WorldStateSnapshot snapshot, SimulationWorldState world)
    {
        return snapshot.Countries.Select(country =>
        {
            if (world.Countries.TryGetValue(country.Id.Value.ToString(), out var state))
            {
                country.TaxRate = Math.Clamp((int)Math.Round(state.TaxRate, MidpointRounding.AwayFromZero), 0, 100);
                country.Treasury = state.Treasury;
                country.TariffRate = state.TariffRate;
                country.PoorTaxRate = state.PoorTaxRate;
                country.MiddleTaxRate = state.MiddleTaxRate;
                country.RichTaxRate = state.RichTaxRate;
                country.EducationSpending = state.EducationSpending;
                country.MilitarySpending = state.MilitarySpending;
                country.AdministrationSpending = state.AdministrationSpending;
            }
            return country;
        }).ToList();
    }

    public static List<BuildingQueueItem> ToPersistedBuildingQueue(SimulationWorldState world)
    {
        return world.BuildingQueue.Select(entry => new BuildingQueueItem
        {
            Id = Guid.TryParse(entry.Id, out var id) ? id : Guid.NewGuid(),
            ProvinceId = Guid.Parse(entry.ProvinceId),
            CountryId = Guid.Parse(entry.CountryId),
            BuildingType = entry.BuildingType,
            TicksRemaining = entry.TicksRemaining,
            QueuedAt = entry.QueuedAt
        }).ToList();
    }

    public static Dictionary<string, Dictionary<string, decimal>> ToProvinceOutputs(
        SimulationWorldState world,
        IReadOnlyList<string> provinceIds)
    {
        return provinceIds
            .Where(id => world.Provinces.ContainsKey(id))
            .ToDictionary(id => id, id => new Dictionary<string, decimal>(world.Provinces[id].OutputsPerTick));
    }

    public static Dictionary<string, decimal> ToProvinceNeedsFulfillment(SimulationWorldState world)
    {
        var result = new Dictionary<string, decimal>();
        foreach (var (provinceKey, province) in world.Provinces)
        {
            var popFulfillments = province.PopulationIds
                .Where(popId => world.Pops.ContainsKey(popId))
                .Select(popId => world.Pops[popId].NeedsFulfillment)
                .ToList();

            result[provinceKey] = popFulfillments.Count > 0
                ? popFulfillments.Average()
                : 1m;
        }
        return result;
    }

    public static List<PopGroupSimulationUpdate> ToPopGroupUpdates(SimulationWorldState world)
    {
        return world.Pops.Values
            .Where(pop => Guid.TryParse(pop.Id, out _))
            .Select(pop => new PopGroupSimulationUpdate(
                Guid.Parse(pop.Id),
                pop.Size,
                pop.CashReserve,
                pop.Literacy,
                pop.Militancy,
                pop.Consciousness,
                pop.LifeNeedsFulfillment,
                pop.EverydayNeedsFulfillment,
                pop.LuxuryNeedsFulfillment,
                pop.EmployedCount,
                pop.UnemployedCount,
                pop.ArtisanProducedGood,
                pop.ArtisanDaysUntilReconsider,
                pop.ArtisanLastReconsideredAt.HasValue
                    ? pop.ArtisanLastReconsideredAt.Value.ToDateTime(TimeOnly.MinValue)
                    : null,
                pop.ArtisanProfitLastTick))
            .ToList();
    }

    public static List<Factory> ToPersistedFactories(SimulationWorldState world)
    {
        return world.Factories.Values
            .Where(factory => Guid.TryParse(factory.Id, out _) && Guid.TryParse(factory.CountryId, out _))
            .Select(factory => new Factory
            {
                Id = Guid.Parse(factory.Id),
                CountryId = new CountryId(Guid.Parse(factory.CountryId)),
                ProvinceId = Guid.TryParse(factory.ProvinceId, out var provinceId)
                    ? new ProvinceId(provinceId)
                    : null,
                Type = factory.Type,
                Level = Math.Max(1, factory.Level),
                EmployedCraftsmen = Math.Max(0, factory.EmployedCraftsmen),
                EmployedClerks = Math.Max(0, factory.EmployedClerks),
                InputGoods = new Dictionary<string, decimal>(factory.InputGoods),
                OutputGood = factory.OutputGood,
                OutputPerTick = Math.Max(0m, factory.OutputPerTick),
                CashReserve = Math.Max(0m, factory.CashReserve),
                ProfitLastTick = factory.ProfitLastTick
            })
            .ToList();
    }

    public static List<GoodProfitHistory> ToPersistedGoodProfitHistory(SimulationWorldState world)
    {
        return world.GoodProfitHistory
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Month) && !string.IsNullOrWhiteSpace(entry.GoodId))
            .Select(entry => new GoodProfitHistory
            {
                Month = entry.Month,
                GoodId = entry.GoodId,
                AverageProducerProfit = entry.AverageProducerProfit,
                ProducerCount = Math.Max(0, entry.ProducerCount)
            })
            .ToList();
    }

    public static List<ArmyStack> ToPersistedArmies(SimulationWorldState world)
    {
        return world.Armies.Values
            .Where(army =>
                Guid.TryParse(army.Id, out _) &&
                Guid.TryParse(army.CountryId, out _) &&
                Guid.TryParse(army.LocationProvinceId, out _))
            .Select(army => new ArmyStack
            {
                Id = Guid.Parse(army.Id),
                CountryId = new CountryId(Guid.Parse(army.CountryId)),
                LocationProvinceId = new ProvinceId(Guid.Parse(army.LocationProvinceId)),
                DestinationProvinceId = Guid.TryParse(army.DestinationProvinceId, out var destinationId)
                    ? new ProvinceId(destinationId)
                    : null,
                MovementTicksRemaining = Math.Max(0, army.MovementTicksRemaining),
                SoldierCount = Math.Max(0, army.SoldierCount),
                Morale = Math.Clamp(army.Morale, 0m, 1m)
            })
            .ToList();
    }

    public static List<War> ToPersistedWars(SimulationWorldState world)
    {
        return world.Wars.Values
            .Where(war =>
                Guid.TryParse(war.Id, out _) &&
                Guid.TryParse(war.AttackerCountryId, out _) &&
                Guid.TryParse(war.DefenderCountryId, out _))
            .Select(war => new War
            {
                Id = Guid.Parse(war.Id),
                AttackerCountryId = new CountryId(Guid.Parse(war.AttackerCountryId)),
                DefenderCountryId = new CountryId(Guid.Parse(war.DefenderCountryId)),
                StartedAt = war.StartedOn.ToDateTime(TimeOnly.MinValue),
                EndedAt = war.EndedOn?.ToDateTime(TimeOnly.MinValue),
                IsActive = war.IsActive
            })
            .ToList();
    }

    public static List<BattleReport> ToPersistedBattleReports(SimulationWorldState world)
    {
        return world.BattleReports.Values
            .Where(battle =>
                !string.IsNullOrWhiteSpace(battle.Id) &&
                Guid.TryParse(battle.WarId, out _) &&
                Guid.TryParse(battle.ProvinceId, out _) &&
                Guid.TryParse(battle.WinnerArmyId, out _) &&
                Guid.TryParse(battle.LoserArmyId, out _) &&
                Guid.TryParse(battle.WinnerCountryId, out _) &&
                Guid.TryParse(battle.LoserCountryId, out _))
            .Select(battle => new BattleReport
            {
                Id = battle.Id,
                WarId = Guid.Parse(battle.WarId),
                ProvinceId = Guid.Parse(battle.ProvinceId),
                WinnerArmyId = Guid.Parse(battle.WinnerArmyId),
                LoserArmyId = Guid.Parse(battle.LoserArmyId),
                WinnerCountryId = Guid.Parse(battle.WinnerCountryId),
                LoserCountryId = Guid.Parse(battle.LoserCountryId),
                OccurredAt = battle.OccurredOn.ToDateTime(TimeOnly.MinValue),
                WinnerCasualties = Math.Max(0, battle.WinnerCasualties),
                LoserCasualties = Math.Max(0, battle.LoserCasualties),
                WinnerMoraleAfter = Math.Clamp(battle.WinnerMoraleAfter, 0m, 1m),
                LoserMoraleAfter = Math.Clamp(battle.LoserMoraleAfter, 0m, 1m)
            })
            .ToList();
    }
}
