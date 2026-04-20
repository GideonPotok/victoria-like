namespace VictoriaLike.Core.Data.Definitions;

public sealed class ScenarioDefinition
{
    public required int Seed { get; init; }
    public required string StartDate { get; init; }
    public required List<CountryDefinition> Countries { get; init; }
    public required List<ProvinceDefinition> Provinces { get; init; }
    public required List<PopDefinition> Pops { get; init; }
    public List<FactoryDefinition> Factories { get; init; } = [];
    public List<ArmyDefinition> Armies { get; init; } = [];
    public List<WarDefinition> Wars { get; init; } = [];
}

public sealed class CountryDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required List<string> ProvinceIds { get; init; }
    public required decimal Treasury { get; init; }
    public required decimal TaxRate { get; init; }
    public decimal? PoorTaxRate { get; init; }
    public decimal? MiddleTaxRate { get; init; }
    public decimal? RichTaxRate { get; init; }
    public required decimal TariffRate { get; init; }
    public decimal EducationSpending { get; init; } = 0.5m;
    public decimal MilitarySpending { get; init; } = 0.5m;
    public decimal AdministrationSpending { get; init; } = 0.5m;
    public required bool IsPlayable { get; init; }
    public Dictionary<string, decimal> Stockpile { get; init; } = [];
}

public sealed class ProvinceDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string OwnerId { get; init; }
    public string RgoType { get; init; } = "grain_farm";
    public required List<string> PopulationIds { get; init; }
    public Dictionary<string, decimal> OutputsPerTick { get; init; } = [];
    public Dictionary<string, decimal> Stockpile { get; init; } = [];
    public decimal Infrastructure { get; init; }
}

public sealed class PopDefinition
{
    public required string Id { get; init; }
    public required string ProvinceId { get; init; }
    public required string PopClass { get; init; }
    public required int Size { get; init; }
    public decimal CashReserve { get; init; }
    public decimal Militancy { get; init; }
    public decimal Consciousness { get; init; }
    public decimal Literacy { get; init; }
    public int? EmployedCount { get; init; }
    public int? UnemployedCount { get; init; }
    public string? ArtisanProducedGood { get; init; }
    public int ArtisanDaysUntilReconsider { get; init; }
    public Dictionary<string, decimal> LifeNeeds { get; init; } = [];
    public Dictionary<string, decimal> EverydayNeeds { get; init; } = [];
    public Dictionary<string, decimal> LuxuryNeeds { get; init; } = [];
}

public sealed class FactoryDefinition
{
    public required string Id { get; init; }
    public required string CountryId { get; init; }
    public string? ProvinceId { get; init; }
    public required string Type { get; init; }
    public int Level { get; init; } = 1;
    public int EmployedCraftsmen { get; init; }
    public int EmployedClerks { get; init; }
    public Dictionary<string, decimal> InputGoods { get; init; } = [];
    public required string OutputGood { get; init; }
    public decimal OutputPerTick { get; init; }
    public decimal CashReserve { get; init; }
}

public sealed class ArmyDefinition
{
    public required string Id { get; init; }
    public required string CountryId { get; init; }
    public required string LocationProvinceId { get; init; }
    public string? DestinationProvinceId { get; init; }
    public int MovementTicksRemaining { get; init; }
    public int SoldierCount { get; init; } = 1_000;
    public decimal Morale { get; init; } = 1m;
}

public sealed class WarDefinition
{
    public required string Id { get; init; }
    public required string AttackerCountryId { get; init; }
    public required string DefenderCountryId { get; init; }
    public string? StartedOn { get; init; }
    public string? EndedOn { get; init; }
    public bool IsActive { get; init; } = true;
}
