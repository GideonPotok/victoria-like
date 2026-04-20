namespace VictoriaLike.Core.Domain;

/// Minimal domain entities.

public class Country
{
    public CountryId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tag { get; set; } = string.Empty; // Short code, e.g., "ENG", "FRA"
    public int TaxRate { get; set; } // 0-100, percentage
    public decimal Treasury { get; set; }
    public decimal TariffRate { get; set; }
    /// Strata-specific tax rates. -1 means "fall back to flat TaxRate".
    public decimal PoorTaxRate { get; set; } = -1m;
    public decimal MiddleTaxRate { get; set; } = -1m;
    public decimal RichTaxRate { get; set; } = -1m;
    /// Spending intensities, normalized 0..1.
    public decimal EducationSpending { get; set; } = 0.5m;
    public decimal MilitarySpending { get; set; } = 0.5m;
    public decimal AdministrationSpending { get; set; } = 0.5m;

    public Country() { }

    public Country(CountryId id, string name, string tag, int taxRate = 10)
    {
        Id = id;
        Name = name;
        Tag = tag;
        TaxRate = Math.Clamp(taxRate, 0, 100);
    }
}

public class Province
{
    public ProvinceId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CountryId OwnerId { get; set; }
    public MarketId MarketId { get; set; }
    public int Population { get; set; }
    public string RgoType { get; set; } = "grain_farm";
    public Dictionary<string, decimal> OutputsPerTick { get; set; } = new();
    public decimal NeedsFulfillment { get; set; } = 1.0m;
    public List<PopGroup> PopGroups { get; set; } = new();

    public Province() { }

    public Province(ProvinceId id, string name, CountryId ownerId, MarketId marketId, int population = 1000)
    {
        Id = id;
        Name = name;
        OwnerId = ownerId;
        MarketId = marketId;
        Population = Math.Max(100, population);
    }
}

public class PopGroup
{
    public static readonly IReadOnlySet<string> ValidStrata = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "poor",
        "middle",
        "rich"
    };

    public Guid Id { get; set; }
    public ProvinceId ProvinceId { get; set; }
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
    public decimal LuxuryNeedsFulfillment { get; set; } = 0m;
    public int EmployedCount { get; set; }
    public int UnemployedCount { get; set; }
    public string? ArtisanProducedGood { get; set; }
    public int ArtisanDaysUntilReconsider { get; set; }
    public DateTime? ArtisanLastReconsideredAt { get; set; }
    public decimal ArtisanProfitLastTick { get; set; }

    public PopGroup() { }

    public PopGroup(
        Guid id,
        ProvinceId provinceId,
        int size,
        string popType,
        string strata,
        string culture,
        string religion,
        decimal literacy = 0.1m)
    {
        Id = id;
        ProvinceId = provinceId;
        Size = Math.Max(0, size);
        PopType = NormalizePopType(popType);
        Strata = NormalizeStrata(strata);
        Culture = culture;
        Religion = religion;
        Literacy = Math.Clamp(literacy, 0m, 1m);
        EmployedCount = Size;
        UnemployedCount = 0;
    }

    public static string InferStrata(string popType)
    {
        return NormalizePopType(popType) switch
        {
            "clerks" or "clergy" or "bureaucrats" or "artisans" => "middle",
            "aristocrats" or "capitalists" => "rich",
            _ => "poor"
        };
    }

    public static string NormalizePopType(string popType) =>
        string.IsNullOrWhiteSpace(popType)
            ? "farmers"
            : popType.Trim().ToLowerInvariant();

    public static string NormalizeStrata(string strata)
    {
        var normalized = string.IsNullOrWhiteSpace(strata)
            ? "poor"
            : strata.Trim().ToLowerInvariant();

        return ValidStrata.Contains(normalized) ? normalized : "poor";
    }
}

public class Factory
{
    public Guid Id { get; set; }
    public CountryId CountryId { get; set; }
    public ProvinceId? ProvinceId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int Level { get; set; } = 1;
    public int EmployedCraftsmen { get; set; }
    public int EmployedClerks { get; set; }
    public Dictionary<string, decimal> InputGoods { get; set; } = new();
    public string OutputGood { get; set; } = string.Empty;
    public decimal OutputPerTick { get; set; }
    public decimal CashReserve { get; set; }
    public decimal ProfitLastTick { get; set; }
}

public class Market
{
    public MarketId Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, decimal> GoodPrices { get; set; } = new();
    public Dictionary<string, decimal> GoodSupply { get; set; } = new();
    public Dictionary<string, decimal> GoodDemand { get; set; } = new();

    public Market() { }

    public Market(MarketId id, string name)
    {
        Id = id;
        Name = name;
    }
}

public class GoodProfitHistory
{
    public string Month { get; set; } = string.Empty;
    public string GoodId { get; set; } = string.Empty;
    public decimal AverageProducerProfit { get; set; }
    public int ProducerCount { get; set; }
}

public class ArmyStack
{
    public Guid Id { get; set; }
    public CountryId CountryId { get; set; }
    public ProvinceId LocationProvinceId { get; set; }
    public ProvinceId? DestinationProvinceId { get; set; }
    public int MovementTicksRemaining { get; set; }
    public int SoldierCount { get; set; }
    public decimal Morale { get; set; } = 1m;
}

public class War
{
    public Guid Id { get; set; }
    public CountryId AttackerCountryId { get; set; }
    public CountryId DefenderCountryId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BattleReport
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

public class PlayerAccount
{
    public ActorId Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public CountryId ControlledCountry { get; set; }
    public DateTime CreatedAt { get; set; }
    /// Only populated during seeding from scenario; never loaded from DB into memory.
    public string? PasswordHash { get; set; }

    public PlayerAccount() { }

    public PlayerAccount(ActorId id, string username, CountryId controlledCountry)
    {
        Id = id;
        Username = username;
        ControlledCountry = controlledCountry;
        CreatedAt = DateTime.UtcNow;
    }
}

public class BuildingQueueItem
{
    public Guid Id { get; set; }
    public Guid ProvinceId { get; set; }
    public Guid CountryId { get; set; }
    public string BuildingType { get; set; } = string.Empty;
    public int TicksRemaining { get; set; }
    public DateTime QueuedAt { get; set; }
}

public class CommandEnvelope
{
    public CommandId Id { get; set; }
    public ActorId ActorId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
    public DateTime IssuedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public long SubmittedTick { get; set; }
    public long? ExpectedWorldTick { get; set; }
    public string? IdempotencyKey { get; set; }
    /// Country the actor controlled at submit time — set by the API layer before enqueuing.
    public Guid? CountryId { get; set; }
    /// Primary target IDs extracted from the payload (e.g. province being built on) — set by the API layer.
    public List<string> TargetIds { get; set; } = [];

    public CommandEnvelope() { }

    public CommandEnvelope(ActorId actorId, string commandType, Dictionary<string, object>? payload = null)
    {
        Id = CommandId.New();
        ActorId = actorId;
        CommandType = commandType;
        Payload = payload ?? new();
        IssuedAt = DateTime.UtcNow;
        ReceivedAt = IssuedAt;
    }
}
