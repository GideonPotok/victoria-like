using System.Text.Json;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Data.Definitions;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Data.Loaders;

public sealed class JsonContentLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public IReadOnlyList<GoodDefinition> LoadGoods(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<GoodDefinition>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to parse goods file: {path}");
    }

    public WorldState LoadScenario(string scenarioPath, IReadOnlyList<GoodDefinition> goods)
    {
        var json = File.ReadAllText(scenarioPath);
        var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to parse scenario file: {scenarioPath}");

        return new WorldState
        {
            Seed = scenario.Seed,
            Date = new GameDate(DateOnly.Parse(scenario.StartDate)),
            Countries = scenario.Countries.ToDictionary(
                country => country.Id,
                country => new CountryState
                {
                    Id = country.Id,
                    DisplayName = country.DisplayName,
                    ProvinceIds = country.ProvinceIds,
                    Treasury = country.Treasury,
                    TaxRate = country.TaxRate,
                    PoorTaxRate = country.PoorTaxRate ?? country.TaxRate,
                    MiddleTaxRate = country.MiddleTaxRate ?? country.TaxRate,
                    RichTaxRate = country.RichTaxRate ?? country.TaxRate,
                    TariffRate = country.TariffRate,
                    EducationSpending = country.EducationSpending,
                    MilitarySpending = country.MilitarySpending,
                    AdministrationSpending = country.AdministrationSpending,
                    IsPlayable = country.IsPlayable,
                    Stockpile = new Dictionary<string, decimal>(country.Stockpile),
                }),
            PlayerAccounts = new Dictionary<string, PlayerAccount>(),
            Provinces = scenario.Provinces.ToDictionary(
                province => province.Id,
                province => new ProvinceState
                {
                    Id = province.Id,
                    DisplayName = province.DisplayName,
                    OwnerId = province.OwnerId,
                    RgoType = province.RgoType,
                    PopulationIds = province.PopulationIds,
                    OutputsPerTick = new Dictionary<string, decimal>(province.OutputsPerTick),
                    Stockpile = new Dictionary<string, decimal>(province.Stockpile),
                    Infrastructure = province.Infrastructure,
                }),
            Pops = scenario.Pops.ToDictionary(
                pop => pop.Id,
                pop => new PopState
                {
                    Id = pop.Id,
                    ProvinceId = pop.ProvinceId,
                    PopClass = pop.PopClass,
                    Size = pop.Size,
                    CashReserve = pop.CashReserve,
                    Militancy = pop.Militancy,
                    Consciousness = pop.Consciousness,
                    Literacy = pop.Literacy,
                    EmployedCount = pop.EmployedCount ?? pop.Size,
                    UnemployedCount = pop.UnemployedCount ?? Math.Max(0, pop.Size - (pop.EmployedCount ?? pop.Size)),
                    ArtisanProducedGood = pop.ArtisanProducedGood,
                    ArtisanDaysUntilReconsider = Math.Max(0, pop.ArtisanDaysUntilReconsider),
                    Needs = PopNeedProfileCatalog.ApplyScenarioOverrides(
                        pop.PopClass,
                        pop.LifeNeeds,
                        pop.EverydayNeeds,
                        pop.LuxuryNeeds),
                }),
            Factories = scenario.Factories.ToDictionary(
                factory => factory.Id,
                factory => new FactoryState
                {
                    Id = factory.Id,
                    CountryId = factory.CountryId,
                    ProvinceId = factory.ProvinceId,
                    Type = factory.Type,
                    Level = Math.Max(1, factory.Level),
                    EmployedCraftsmen = Math.Max(0, factory.EmployedCraftsmen),
                    EmployedClerks = Math.Max(0, factory.EmployedClerks),
                    InputGoods = new Dictionary<string, decimal>(factory.InputGoods),
                    OutputGood = factory.OutputGood,
                    OutputPerTick = Math.Max(0m, factory.OutputPerTick),
                    CashReserve = Math.Max(0m, factory.CashReserve)
                }),
            Armies = BuildArmies(scenario),
            Wars = scenario.Wars.ToDictionary(
                war => war.Id,
                war => new WarState
                {
                    Id = war.Id,
                    AttackerCountryId = war.AttackerCountryId,
                    DefenderCountryId = war.DefenderCountryId,
                    StartedOn = DateOnly.TryParse(war.StartedOn, out var started) ? started : DateOnly.Parse(scenario.StartDate),
                    EndedOn = DateOnly.TryParse(war.EndedOn, out var ended) ? ended : null,
                    IsActive = war.IsActive,
                }),
            Goods = goods.ToDictionary(good => good.Id, good => good),
            Market = new MarketState
            {
                Prices = goods.ToDictionary(good => good.Id, good => good.BasePrice),
            },
            Metrics = new SimulationMetrics(),
            EventLog = ["Scenario loaded."],
        };
    }

    private static Dictionary<string, ArmyStackState> BuildArmies(ScenarioDefinition scenario)
    {
        if (scenario.Armies.Count > 0)
        {
            return scenario.Armies.ToDictionary(
                army => army.Id,
                army => new ArmyStackState
                {
                    Id = army.Id,
                    CountryId = army.CountryId,
                    LocationProvinceId = army.LocationProvinceId,
                    DestinationProvinceId = string.IsNullOrWhiteSpace(army.DestinationProvinceId) ? null : army.DestinationProvinceId,
                    MovementTicksRemaining = Math.Max(0, army.MovementTicksRemaining),
                    SoldierCount = Math.Max(0, army.SoldierCount),
                    Morale = Math.Clamp(army.Morale, 0m, 1m),
                });
        }

        return scenario.Countries
            .Select(country => new
            {
                Country = country,
                FirstProvinceId = scenario.Provinces
                    .Where(province => string.Equals(province.OwnerId, country.Id, StringComparison.Ordinal))
                    .OrderBy(province => province.Id, StringComparer.Ordinal)
                    .Select(province => province.Id)
                    .FirstOrDefault()
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.FirstProvinceId))
            .ToDictionary(
                entry => $"{entry.Country.Id}-army-1",
                entry => new ArmyStackState
                {
                    Id = $"{entry.Country.Id}-army-1",
                    CountryId = entry.Country.Id,
                    LocationProvinceId = entry.FirstProvinceId!,
                    SoldierCount = 1_000,
                    Morale = 1m
                });
    }
}
