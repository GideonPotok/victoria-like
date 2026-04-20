using System.Text.Json;
using System.Text.Json.Serialization;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Core.Scenarios;

public class ScenarioDefinition
{
    [JsonPropertyName("scenario")]
    public ScenarioContent? Content { get; set; }
}

public class ScenarioContent
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public string StartDate { get; set; } = "1800-01-01";

    [JsonPropertyName("countries")]
    public List<CountryDef> Countries { get; set; } = new();

    [JsonPropertyName("markets")]
    public List<MarketDef> Markets { get; set; } = new();

    [JsonPropertyName("provinces")]
    public List<ProvinceDef> Provinces { get; set; } = new();

    [JsonPropertyName("factories")]
    public List<FactoryDef> Factories { get; set; } = new();

    [JsonPropertyName("armies")]
    public List<ArmyDef> Armies { get; set; } = new();

    [JsonPropertyName("wars")]
    public List<WarDef> Wars { get; set; } = new();

    [JsonPropertyName("players")]
    public List<PlayerDef> Players { get; set; } = new();
}

public class CountryDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("taxRate")]
    public int TaxRate { get; set; } = 10;

    [JsonPropertyName("poorTaxRate")]
    public decimal? PoorTaxRate { get; set; }

    [JsonPropertyName("middleTaxRate")]
    public decimal? MiddleTaxRate { get; set; }

    [JsonPropertyName("richTaxRate")]
    public decimal? RichTaxRate { get; set; }

    [JsonPropertyName("educationSpending")]
    public decimal EducationSpending { get; set; } = 0.5m;

    [JsonPropertyName("militarySpending")]
    public decimal MilitarySpending { get; set; } = 0.5m;

    [JsonPropertyName("administrationSpending")]
    public decimal AdministrationSpending { get; set; } = 0.5m;
}

public class MarketDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("goods")]
    public Dictionary<string, decimal> Goods { get; set; } = new();
}

public class ProvinceDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner")]
    public string Owner { get; set; } = string.Empty;

    [JsonPropertyName("market")]
    public string Market { get; set; } = string.Empty;

    [JsonPropertyName("population")]
    public int Population { get; set; } = 1000;

    [JsonPropertyName("rgoType")]
    public string RgoType { get; set; } = "grain_farm";

    [JsonPropertyName("outputsPerTick")]
    public Dictionary<string, decimal> OutputsPerTick { get; set; } = new();

    [JsonPropertyName("pops")]
    public List<PopGroupDef> Pops { get; set; } = new();
}

public class PopGroupDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("popType")]
    public string PopType { get; set; } = "farmers";

    [JsonPropertyName("strata")]
    public string? Strata { get; set; }

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = "primary";

    [JsonPropertyName("religion")]
    public string Religion { get; set; } = "secular";

    [JsonPropertyName("literacy")]
    public decimal Literacy { get; set; } = 0.1m;

    [JsonPropertyName("militancy")]
    public decimal Militancy { get; set; } = 0m;

    [JsonPropertyName("consciousness")]
    public decimal Consciousness { get; set; } = 0m;

    [JsonPropertyName("cash")]
    public decimal Cash { get; set; } = 0m;

    [JsonPropertyName("lifeNeedsFulfillment")]
    public decimal LifeNeedsFulfillment { get; set; } = 1.0m;

    [JsonPropertyName("everydayNeedsFulfillment")]
    public decimal EverydayNeedsFulfillment { get; set; } = 1.0m;

    [JsonPropertyName("luxuryNeedsFulfillment")]
    public decimal LuxuryNeedsFulfillment { get; set; } = 0m;

    [JsonPropertyName("employedCount")]
    public int? EmployedCount { get; set; }

    [JsonPropertyName("unemployedCount")]
    public int? UnemployedCount { get; set; }

    [JsonPropertyName("artisanProducedGood")]
    public string? ArtisanProducedGood { get; set; }

    [JsonPropertyName("artisanDaysUntilReconsider")]
    public int ArtisanDaysUntilReconsider { get; set; }
}

public class FactoryDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("province")]
    public string? Province { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; } = 1;

    [JsonPropertyName("employedCraftsmen")]
    public int EmployedCraftsmen { get; set; }

    [JsonPropertyName("employedClerks")]
    public int EmployedClerks { get; set; }

    [JsonPropertyName("inputGoods")]
    public Dictionary<string, decimal> InputGoods { get; set; } = new();

    [JsonPropertyName("outputGood")]
    public string OutputGood { get; set; } = string.Empty;

    [JsonPropertyName("outputPerTick")]
    public decimal OutputPerTick { get; set; }

    [JsonPropertyName("cashReserve")]
    public decimal CashReserve { get; set; }
}

public class ArmyDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("country")]
    public string Country { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("movementTicksRemaining")]
    public int MovementTicksRemaining { get; set; }

    [JsonPropertyName("soldiers")]
    public int Soldiers { get; set; } = 1_000;

    [JsonPropertyName("morale")]
    public decimal Morale { get; set; } = 1m;
}

public class WarDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("attacker")]
    public string Attacker { get; set; } = string.Empty;

    [JsonPropertyName("defender")]
    public string Defender { get; set; } = string.Empty;

    [JsonPropertyName("startedAt")]
    public string? StartedAt { get; set; }

    [JsonPropertyName("endedAt")]
    public string? EndedAt { get; set; }

    [JsonPropertyName("isActive")]
    public bool IsActive { get; set; } = true;
}

public class PlayerDef
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("controls")]
    public string Controls { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string? Password { get; set; }
}

public class LoadedScenario
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public List<Country> Countries { get; set; } = new();
    public List<Market> Markets { get; set; } = new();
    public List<Province> Provinces { get; set; } = new();
    public List<Factory> Factories { get; set; } = new();
    public List<ArmyStack> Armies { get; set; } = new();
    public List<War> Wars { get; set; } = new();
    public List<PlayerAccount> Players { get; set; } = new();
}

public interface IScenarioLoader
{
    Task<LoadedScenario> LoadAsync(string filePath, CancellationToken cancellationToken = default);
}

public class ScenarioLoader : IScenarioLoader
{
    public async Task<LoadedScenario> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Scenario file not found: {filePath}");

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        var def = JsonSerializer.Deserialize<ScenarioDefinition>(json)
            ?? throw new InvalidOperationException("Failed to deserialize scenario");

        var content = def.Content
            ?? throw new InvalidOperationException("Scenario is missing 'scenario' root object");

        ValidateScenario(content);

        return BuildScenario(content);
    }

    private void ValidateScenario(ScenarioContent content)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(content.Name))
            errors.Add("Scenario must have a 'name'");

        if (content.Countries.Count == 0)
            errors.Add("Scenario must have at least 1 country");

        if (content.Provinces.Count == 0)
            errors.Add("Scenario must have at least 1 province");

        // Validate countries
        var countryTags = new HashSet<string>();
        foreach (var country in content.Countries)
        {
            if (string.IsNullOrWhiteSpace(country.Name))
                errors.Add($"Country name cannot be empty");

            if (string.IsNullOrWhiteSpace(country.Tag))
                errors.Add($"Country '{country.Name}' must have a 3-letter 'tag'");
            else if (country.Tag.Length != 3)
                errors.Add($"Country tag must be exactly 3 letters, got '{country.Tag}'");
            else if (!countryTags.Add(country.Tag))
                errors.Add($"Duplicate country tag: '{country.Tag}'");

            if (country.TaxRate < 0 || country.TaxRate > 100)
                errors.Add($"Country '{country.Name}' tax rate must be 0-100, got {country.TaxRate}");
        }

        // Validate markets
        var marketNames = new HashSet<string>();
        foreach (var market in content.Markets)
        {
            if (string.IsNullOrWhiteSpace(market.Name))
                errors.Add($"Market name cannot be empty");
            else if (!marketNames.Add(market.Name))
                errors.Add($"Duplicate market name: '{market.Name}'");

            foreach (var (good, price) in market.Goods)
            {
                if (string.IsNullOrWhiteSpace(good))
                    errors.Add($"Market '{market.Name}' contains an empty good id");
                if (price <= 0m)
                    errors.Add($"Market '{market.Name}' good '{good}' price must be positive");
            }
        }

        var knownGoods = content.Markets
            .SelectMany(market => market.Goods.Keys)
            .Where(good => !string.IsNullOrWhiteSpace(good))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Validate provinces
        var provinceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var province in content.Provinces)
        {
            if (string.IsNullOrWhiteSpace(province.Name))
                errors.Add($"Province name cannot be empty");
            else if (!provinceNames.Add(province.Name))
                errors.Add($"Duplicate province name: '{province.Name}'");

            if (!countryTags.Contains(province.Owner))
                errors.Add($"Province '{province.Name}' references unknown country tag '{province.Owner}'");

            if (!marketNames.Contains(province.Market))
                errors.Add($"Province '{province.Name}' references unknown market '{province.Market}'");

            if (province.Population < 100)
                errors.Add($"Province '{province.Name}' population must be >= 100, got {province.Population}");

            if (string.IsNullOrWhiteSpace(province.RgoType))
                errors.Add($"Province '{province.Name}' must have an rgoType");

            foreach (var (good, quantity) in province.OutputsPerTick)
            {
                if (string.IsNullOrWhiteSpace(good))
                    errors.Add($"Province '{province.Name}' has an empty output good id");
                else if (knownGoods.Count > 0 && !knownGoods.Contains(good))
                    errors.Add($"Province '{province.Name}' outputs unknown good '{good}'");
                if (quantity < 0m)
                    errors.Add($"Province '{province.Name}' output '{good}' cannot be negative");
            }

            var popTotal = 0;
            var popIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pop in province.Pops)
            {
                if (!string.IsNullOrWhiteSpace(pop.Id))
                {
                    if (!Guid.TryParse(pop.Id, out _))
                        errors.Add($"Province '{province.Name}' has POP with invalid id '{pop.Id}'");
                    else if (!popIds.Add(pop.Id))
                        errors.Add($"Province '{province.Name}' has duplicate POP id '{pop.Id}'");
                }

                if (pop.Size <= 0)
                    errors.Add($"Province '{province.Name}' has POP size <= 0");
                if (string.IsNullOrWhiteSpace(pop.PopType))
                    errors.Add($"Province '{province.Name}' has POP with empty popType");
                if (!string.IsNullOrWhiteSpace(pop.Strata) &&
                    !PopGroup.ValidStrata.Contains(pop.Strata.Trim()))
                {
                    errors.Add($"Province '{province.Name}' POP strata must be poor, middle, or rich");
                }
                if (string.IsNullOrWhiteSpace(pop.Culture))
                    errors.Add($"Province '{province.Name}' has POP with empty culture");
                if (string.IsNullOrWhiteSpace(pop.Religion))
                    errors.Add($"Province '{province.Name}' has POP with empty religion");
                if (pop.Literacy < 0 || pop.Literacy > 1)
                    errors.Add($"Province '{province.Name}' POP literacy must be 0-1");
                if (pop.Militancy < 0 || pop.Militancy > 10)
                    errors.Add($"Province '{province.Name}' POP militancy must be 0-10");
                if (pop.Consciousness < 0 || pop.Consciousness > 10)
                    errors.Add($"Province '{province.Name}' POP consciousness must be 0-10");

                popTotal += Math.Max(0, pop.Size);
            }

            if (province.Pops.Count > 0 && popTotal != province.Population)
                errors.Add($"Province '{province.Name}' POP sizes must sum to population {province.Population}, got {popTotal}");
        }

        foreach (var factory in content.Factories)
        {
            if (!countryTags.Contains(factory.Country))
                errors.Add($"Factory '{factory.Type}' references unknown country tag '{factory.Country}'");
            if (!string.IsNullOrWhiteSpace(factory.Province) && !provinceNames.Contains(factory.Province))
                errors.Add($"Factory '{factory.Type}' references unknown province '{factory.Province}'");
            if (!string.IsNullOrWhiteSpace(factory.Id) && !Guid.TryParse(factory.Id, out _))
                errors.Add($"Factory '{factory.Type}' has invalid id '{factory.Id}'");
            if (string.IsNullOrWhiteSpace(factory.Type))
                errors.Add("Factory type cannot be empty");
            if (factory.Level < 1)
                errors.Add($"Factory '{factory.Type}' level must be >= 1");
            if (factory.EmployedCraftsmen < 0 || factory.EmployedClerks < 0)
                errors.Add($"Factory '{factory.Type}' employment cannot be negative");
            if (string.IsNullOrWhiteSpace(factory.OutputGood))
                errors.Add($"Factory '{factory.Type}' outputGood cannot be empty");
            else if (knownGoods.Count > 0 && !knownGoods.Contains(factory.OutputGood))
                errors.Add($"Factory '{factory.Type}' outputs unknown good '{factory.OutputGood}'");
            if (factory.OutputPerTick < 0m)
                errors.Add($"Factory '{factory.Type}' outputPerTick cannot be negative");
            if (factory.InputGoods.Values.Any(value => value < 0m))
                errors.Add($"Factory '{factory.Type}' inputGoods cannot contain negative quantities");
            foreach (var good in factory.InputGoods.Keys)
            {
                if (string.IsNullOrWhiteSpace(good))
                    errors.Add($"Factory '{factory.Type}' has an empty input good id");
                else if (knownGoods.Count > 0 && !knownGoods.Contains(good))
                    errors.Add($"Factory '{factory.Type}' consumes unknown good '{good}'");
            }
        }

        foreach (var army in content.Armies)
        {
            if (!string.IsNullOrWhiteSpace(army.Id) && !Guid.TryParse(army.Id, out _))
                errors.Add($"Army has invalid id '{army.Id}'");
            if (!countryTags.Contains(army.Country))
                errors.Add($"Army references unknown country tag '{army.Country}'");
            if (!provinceNames.Contains(army.Location))
                errors.Add($"Army references unknown location province '{army.Location}'");
            if (!string.IsNullOrWhiteSpace(army.Destination) && !provinceNames.Contains(army.Destination))
                errors.Add($"Army references unknown destination province '{army.Destination}'");
            if (army.MovementTicksRemaining < 0)
                errors.Add("Army movementTicksRemaining cannot be negative");
            if (army.Soldiers < 0)
                errors.Add("Army soldiers cannot be negative");
            if (army.Morale < 0m || army.Morale > 1m)
                errors.Add("Army morale must be 0-1");
        }

        foreach (var war in content.Wars)
        {
            if (!string.IsNullOrWhiteSpace(war.Id) && !Guid.TryParse(war.Id, out _))
                errors.Add($"War has invalid id '{war.Id}'");
            if (!countryTags.Contains(war.Attacker))
                errors.Add($"War references unknown attacker country tag '{war.Attacker}'");
            if (!countryTags.Contains(war.Defender))
                errors.Add($"War references unknown defender country tag '{war.Defender}'");
            if (string.Equals(war.Attacker, war.Defender, StringComparison.OrdinalIgnoreCase))
                errors.Add("War attacker and defender cannot be the same country");
        }

        var playerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var controlledCountries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var player in content.Players)
        {
            if (string.IsNullOrWhiteSpace(player.Username))
                errors.Add("Player username cannot be empty");

            if (string.IsNullOrWhiteSpace(player.Controls))
                errors.Add($"Player '{player.Username}' must declare a 'controls' country tag");
            else if (!countryTags.Contains(player.Controls))
                errors.Add($"Player '{player.Username}' references unknown country tag '{player.Controls}'");
            else if (!controlledCountries.Add(player.Controls))
                errors.Add($"Multiple players cannot control country tag '{player.Controls}'");

            if (!string.IsNullOrWhiteSpace(player.Id))
            {
                if (!Guid.TryParse(player.Id, out _))
                    errors.Add($"Player '{player.Username}' has invalid id '{player.Id}'");
                else if (!playerIds.Add(player.Id))
                    errors.Add($"Duplicate player id '{player.Id}'");
            }
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Scenario validation failed:\n" + string.Join("\n", errors));
    }

    private LoadedScenario BuildScenario(ScenarioContent content)
    {
        var countries = new List<Country>();
        var countryTagMap = new Dictionary<string, CountryId>();

        foreach (var def in content.Countries)
        {
            var id = string.IsNullOrEmpty(def.Id) ? CountryId.New() : CountryId.Parse(def.Id);
            var country = new Country(id, def.Name, def.Tag, def.TaxRate)
            {
                PoorTaxRate = def.PoorTaxRate ?? -1m,
                MiddleTaxRate = def.MiddleTaxRate ?? -1m,
                RichTaxRate = def.RichTaxRate ?? -1m,
                EducationSpending = def.EducationSpending,
                MilitarySpending = def.MilitarySpending,
                AdministrationSpending = def.AdministrationSpending
            };
            countries.Add(country);
            countryTagMap[def.Tag] = id;
        }

        var markets = new List<Market>();
        var marketNameMap = new Dictionary<string, MarketId>();

        foreach (var def in content.Markets)
        {
            var id = string.IsNullOrEmpty(def.Id) ? MarketId.New() : MarketId.Parse(def.Id);
            var market = new Market(id, def.Name);
            market.GoodPrices = new Dictionary<string, decimal>(def.Goods);
            markets.Add(market);
            marketNameMap[def.Name] = id;
        }

        var provinces = new List<Province>();
        var provinceNameMap = new Dictionary<string, ProvinceId>(StringComparer.OrdinalIgnoreCase);
        foreach (var def in content.Provinces)
        {
            var id = string.IsNullOrEmpty(def.Id) ? ProvinceId.New() : ProvinceId.Parse(def.Id);
            var province = new Province(
                id,
                def.Name,
                countryTagMap[def.Owner],
                marketNameMap[def.Market],
                def.Population
            )
            {
                OutputsPerTick = new Dictionary<string, decimal>(def.OutputsPerTick)
            };
            province.RgoType = def.RgoType.Trim().ToLowerInvariant();
            province.PopGroups = BuildPopGroups(def, id);
            provinces.Add(province);
            provinceNameMap[def.Name] = id;
        }

        var factories = content.Factories.Select(def => new Factory
        {
            Id = string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid() : Guid.Parse(def.Id),
            CountryId = countryTagMap[def.Country],
            ProvinceId = string.IsNullOrWhiteSpace(def.Province) ? null : provinceNameMap[def.Province],
            Type = def.Type.Trim().ToLowerInvariant(),
            Level = Math.Max(1, def.Level),
            EmployedCraftsmen = Math.Max(0, def.EmployedCraftsmen),
            EmployedClerks = Math.Max(0, def.EmployedClerks),
            InputGoods = new Dictionary<string, decimal>(def.InputGoods),
            OutputGood = def.OutputGood.Trim().ToLowerInvariant(),
            OutputPerTick = Math.Max(0m, def.OutputPerTick),
            CashReserve = Math.Max(0m, def.CashReserve)
        }).ToList();

        var armies = BuildArmies(content, countries, provinces, countryTagMap, provinceNameMap);
        var startDate = DateTime.TryParse(content.StartDate, out var parsed)
            ? parsed
            : new DateTime(1800, 1, 1);
        var wars = content.Wars.Select(def => new War
        {
            Id = string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid() : Guid.Parse(def.Id),
            AttackerCountryId = countryTagMap[def.Attacker],
            DefenderCountryId = countryTagMap[def.Defender],
            StartedAt = DateTime.TryParse(def.StartedAt, out var started) ? started : startDate,
            EndedAt = DateTime.TryParse(def.EndedAt, out var ended) ? ended : (DateTime?)null,
            IsActive = def.IsActive
        }).ToList();

        var players = new List<PlayerAccount>();
        foreach (var def in content.Players)
        {
            var id = string.IsNullOrEmpty(def.Id) ? ActorId.New() : ActorId.Parse(def.Id);
            var player = new PlayerAccount(id, def.Username, countryTagMap[def.Controls])
            {
                PasswordHash = def.Password  // plain text from scenario; server layer will hash before persisting
            };
            players.Add(player);
        }

        return new LoadedScenario
        {
            Name = content.Name,
            Description = content.Description,
            StartDate = startDate,
            Countries = countries,
            Markets = markets,
            Provinces = provinces,
            Factories = factories,
            Armies = armies,
            Wars = wars,
            Players = players
        };
    }

    private static List<ArmyStack> BuildArmies(
        ScenarioContent content,
        IReadOnlyList<Country> countries,
        IReadOnlyList<Province> provinces,
        IReadOnlyDictionary<string, CountryId> countryTagMap,
        IReadOnlyDictionary<string, ProvinceId> provinceNameMap)
    {
        if (content.Armies.Count > 0)
        {
            return content.Armies.Select(def => new ArmyStack
            {
                Id = string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid() : Guid.Parse(def.Id),
                CountryId = countryTagMap[def.Country],
                LocationProvinceId = provinceNameMap[def.Location],
                DestinationProvinceId = string.IsNullOrWhiteSpace(def.Destination) ? null : provinceNameMap[def.Destination],
                MovementTicksRemaining = Math.Max(0, def.MovementTicksRemaining),
                SoldierCount = Math.Max(0, def.Soldiers),
                Morale = Math.Clamp(def.Morale, 0m, 1m)
            }).ToList();
        }

        return countries
            .Select(country => new
            {
                Country = country,
                FirstProvince = provinces
                    .Where(province => province.OwnerId.Equals(country.Id))
                    .OrderBy(province => province.Name, StringComparer.Ordinal)
                    .FirstOrDefault()
            })
            .Where(entry => entry.FirstProvince != null)
            .Select(entry => new ArmyStack
            {
                Id = Guid.NewGuid(),
                CountryId = entry.Country.Id,
                LocationProvinceId = entry.FirstProvince!.Id,
                SoldierCount = 1_000,
                Morale = 1m
            })
            .ToList();
    }

    private static List<PopGroup> BuildPopGroups(ProvinceDef province, ProvinceId provinceId)
    {
        if (province.Pops.Count == 0)
        {
            return
            [
                new PopGroup(
                    Guid.NewGuid(),
                    provinceId,
                    province.Population,
                    "farmers",
                    "poor",
                    "primary",
                    "secular")
            ];
        }

        return province.Pops.Select(def =>
        {
            var pop = new PopGroup(
                string.IsNullOrWhiteSpace(def.Id) ? Guid.NewGuid() : Guid.Parse(def.Id),
                provinceId,
                def.Size,
                def.PopType,
                string.IsNullOrWhiteSpace(def.Strata) ? PopGroup.InferStrata(def.PopType) : def.Strata,
                def.Culture,
                def.Religion,
                def.Literacy)
            {
                Militancy = Math.Clamp(def.Militancy, 0m, 10m),
                Consciousness = Math.Clamp(def.Consciousness, 0m, 10m),
                Cash = def.Cash,
                LifeNeedsFulfillment = Math.Clamp(def.LifeNeedsFulfillment, 0m, 1m),
                EverydayNeedsFulfillment = Math.Clamp(def.EverydayNeedsFulfillment, 0m, 1m),
                LuxuryNeedsFulfillment = Math.Clamp(def.LuxuryNeedsFulfillment, 0m, 1m)
            };

            pop.EmployedCount = def.EmployedCount ?? pop.Size;
            pop.UnemployedCount = def.UnemployedCount ?? Math.Max(0, pop.Size - pop.EmployedCount);
            pop.ArtisanProducedGood = string.IsNullOrWhiteSpace(def.ArtisanProducedGood)
                ? null
                : def.ArtisanProducedGood.Trim().ToLowerInvariant();
            pop.ArtisanDaysUntilReconsider = Math.Max(0, def.ArtisanDaysUntilReconsider);
            if (pop.EmployedCount + pop.UnemployedCount > pop.Size)
            {
                pop.EmployedCount = Math.Min(pop.EmployedCount, pop.Size);
                pop.UnemployedCount = pop.Size - pop.EmployedCount;
            }

            return pop;
        }).ToList();
    }
}
