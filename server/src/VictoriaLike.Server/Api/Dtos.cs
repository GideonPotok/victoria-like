using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VictoriaLike.Server.Api;

// Minimal DTOs for API responses - no business logic, just data transfer

public class WorldSummaryDto
{
    [JsonPropertyName("tick")]
    public long Tick { get; set; }

    [JsonPropertyName("world_date")]
    public string WorldDate { get; set; } = string.Empty;

    [JsonPropertyName("country_count")]
    public int CountryCount { get; set; }

    [JsonPropertyName("province_count")]
    public int ProvinceCount { get; set; }

    [JsonPropertyName("market_count")]
    public int MarketCount { get; set; }
}

public class CountryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("tax_rate")]
    public int TaxRate { get; set; }

    [JsonPropertyName("treasury")]
    public decimal Treasury { get; set; }

    [JsonPropertyName("province_count")]
    public int ProvinceCount { get; set; }

    [JsonPropertyName("controller_actor_id")]
    public string? ControllerActorId { get; set; }

    [JsonPropertyName("controller_username")]
    public string? ControllerUsername { get; set; }
}

public class MarketDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("goods")]
    public Dictionary<string, decimal> Goods { get; set; } = new();
}

public class ProvinceDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("owner_name")]
    public string OwnerName { get; set; } = string.Empty;

    [JsonPropertyName("market_id")]
    public string MarketId { get; set; } = string.Empty;

    [JsonPropertyName("population")]
    public int Population { get; set; }

    [JsonPropertyName("rgo_type")]
    public string RgoType { get; set; } = string.Empty;
}

public class ProvinceDetailDto : ProvinceDto
{
    [JsonPropertyName("market_name")]
    public string MarketName { get; set; } = string.Empty;

    [JsonPropertyName("market_goods")]
    public Dictionary<string, decimal> MarketGoods { get; set; } = new();

    [JsonPropertyName("outputs_per_tick")]
    public Dictionary<string, decimal> OutputsPerTick { get; set; } = new();

    [JsonPropertyName("needs_fulfillment")]
    public decimal NeedsFulfillment { get; set; } = 1.0m;

    [JsonPropertyName("pop_groups")]
    public List<ProvincePopGroupDto> PopGroups { get; set; } = new();
}

public class ProvincePopGroupDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("population_share")]
    public decimal PopulationShare { get; set; }

    [JsonPropertyName("pop_type")]
    public string PopType { get; set; } = string.Empty;

    [JsonPropertyName("strata")]
    public string Strata { get; set; } = string.Empty;

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;

    [JsonPropertyName("religion")]
    public string Religion { get; set; } = string.Empty;

    [JsonPropertyName("literacy")]
    public decimal Literacy { get; set; }

    [JsonPropertyName("militancy")]
    public decimal Militancy { get; set; }

    [JsonPropertyName("consciousness")]
    public decimal Consciousness { get; set; }

    [JsonPropertyName("cash")]
    public decimal Cash { get; set; }

    [JsonPropertyName("life_needs_fulfillment")]
    public decimal LifeNeedsFulfillment { get; set; }

    [JsonPropertyName("everyday_needs_fulfillment")]
    public decimal EverydayNeedsFulfillment { get; set; }

    [JsonPropertyName("luxury_needs_fulfillment")]
    public decimal LuxuryNeedsFulfillment { get; set; }

    [JsonPropertyName("employed_count")]
    public int EmployedCount { get; set; }

    [JsonPropertyName("unemployed_count")]
    public int UnemployedCount { get; set; }
}

public class BuildingQueueItemDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("province_id")]
    public string ProvinceId { get; set; } = string.Empty;

    [JsonPropertyName("province_name")]
    public string ProvinceName { get; set; } = string.Empty;

    [JsonPropertyName("country_id")]
    public string CountryId { get; set; } = string.Empty;

    [JsonPropertyName("building_type")]
    public string BuildingType { get; set; } = string.Empty;

    [JsonPropertyName("ticks_remaining")]
    public int TicksRemaining { get; set; }

    [JsonPropertyName("queued_at")]
    public DateTime QueuedAt { get; set; }
}

public class ConstructionOptionPreviewDto
{
    [JsonPropertyName("building_type")]
    public string BuildingType { get; set; } = string.Empty;

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("rejection_reason")]
    public string? RejectionReason { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("cost")]
    public decimal Cost { get; set; }

    [JsonPropertyName("build_ticks")]
    public int BuildTicks { get; set; }

    [JsonPropertyName("treasury_after_command")]
    public decimal? TreasuryAfterCommand { get; set; }

    [JsonPropertyName("output_per_tick")]
    public Dictionary<string, decimal> OutputPerTick { get; set; } = new();
}

public class MarketSummaryDto
{
    [JsonPropertyName("goods")]
    public List<MarketGoodDto> Goods { get; set; } = new();

    [JsonPropertyName("average_needs_fulfillment")]
    public decimal AverageNeedsFulfillment { get; set; }

    [JsonPropertyName("tick")]
    public long Tick { get; set; }
}

public class MarketGoodDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("base_price")]
    public decimal BasePrice { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("supply")]
    public decimal Supply { get; set; }

    [JsonPropertyName("demand")]
    public decimal Demand { get; set; }

    [JsonPropertyName("fulfillment_rate")]
    public decimal FulfillmentRate { get; set; }
}

public class CountryInspectionDto
{
    [JsonPropertyName("country_id")]
    public string CountryId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("treasury")]
    public decimal Treasury { get; set; }

    [JsonPropertyName("tax_rate")]
    public int TaxRate { get; set; }

    [JsonPropertyName("poor_tax_rate")]
    public decimal PoorTaxRate { get; set; }

    [JsonPropertyName("middle_tax_rate")]
    public decimal MiddleTaxRate { get; set; }

    [JsonPropertyName("rich_tax_rate")]
    public decimal RichTaxRate { get; set; }

    [JsonPropertyName("education_spending")]
    public decimal EducationSpending { get; set; }

    [JsonPropertyName("military_spending")]
    public decimal MilitarySpending { get; set; }

    [JsonPropertyName("administration_spending")]
    public decimal AdministrationSpending { get; set; }

    [JsonPropertyName("province_count")]
    public int ProvinceCount { get; set; }

    [JsonPropertyName("population")]
    public int Population { get; set; }

    [JsonPropertyName("average_literacy")]
    public decimal AverageLiteracy { get; set; }

    [JsonPropertyName("average_militancy")]
    public decimal AverageMilitancy { get; set; }

    [JsonPropertyName("average_consciousness")]
    public decimal AverageConsciousness { get; set; }

    [JsonPropertyName("unemployment_share")]
    public decimal UnemploymentShare { get; set; }

    [JsonPropertyName("pop_type_breakdown")]
    public List<CountryPopTypeDto> PopTypeBreakdown { get; set; } = new();

    [JsonPropertyName("market_warnings")]
    public List<MarketWarningDto> MarketWarnings { get; set; } = new();

    [JsonPropertyName("reform_pressure")]
    public decimal ReformPressure { get; set; }

    [JsonPropertyName("pop_groups")]
    public List<ProvincePopGroupDto> PopGroups { get; set; } = new();
}

public class BudgetAdjustmentPreviewDto
{
    [JsonPropertyName("country_id")]
    public string CountryId { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("current_value")]
    public decimal CurrentValue { get; set; }

    [JsonPropertyName("proposed_value")]
    public decimal ProposedValue { get; set; }

    [JsonPropertyName("estimated_weekly_spending_cost_current")]
    public decimal? EstimatedWeeklySpendingCostCurrent { get; set; }

    [JsonPropertyName("estimated_weekly_spending_cost_proposed")]
    public decimal? EstimatedWeeklySpendingCostProposed { get; set; }

    [JsonPropertyName("estimated_weekly_spending_cost_delta")]
    public decimal? EstimatedWeeklySpendingCostDelta { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("effects")]
    public List<string> Effects { get; set; } = new();
}

public class MarketWarningDto
{
    [JsonPropertyName("good_id")]
    public string GoodId { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("supply")]
    public decimal Supply { get; set; }

    [JsonPropertyName("demand")]
    public decimal Demand { get; set; }

    [JsonPropertyName("fulfillment_rate")]
    public decimal FulfillmentRate { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public class WorldEventDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("tick")]
    public long Tick { get; set; }

    [JsonPropertyName("world_date")]
    public string WorldDate { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("country_id")]
    public string? CountryId { get; set; }

    [JsonPropertyName("country_name")]
    public string? CountryName { get; set; }

    [JsonPropertyName("province_id")]
    public string? ProvinceId { get; set; }

    [JsonPropertyName("province_name")]
    public string? ProvinceName { get; set; }

    [JsonPropertyName("market_id")]
    public string? MarketId { get; set; }

    [JsonPropertyName("good_id")]
    public string? GoodId { get; set; }

    [JsonPropertyName("target_panel")]
    public string TargetPanel { get; set; } = string.Empty;
}

public class ArmyStackDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("country_id")]
    public string CountryId { get; set; } = string.Empty;

    [JsonPropertyName("country_name")]
    public string CountryName { get; set; } = string.Empty;

    [JsonPropertyName("location_province_id")]
    public string LocationProvinceId { get; set; } = string.Empty;

    [JsonPropertyName("location_province_name")]
    public string LocationProvinceName { get; set; } = string.Empty;

    [JsonPropertyName("destination_province_id")]
    public string? DestinationProvinceId { get; set; }

    [JsonPropertyName("destination_province_name")]
    public string? DestinationProvinceName { get; set; }

    [JsonPropertyName("movement_ticks_remaining")]
    public int MovementTicksRemaining { get; set; }

    [JsonPropertyName("soldier_count")]
    public int SoldierCount { get; set; }

    [JsonPropertyName("morale")]
    public decimal Morale { get; set; }

    [JsonPropertyName("is_moving")]
    public bool IsMoving { get; set; }
}

public class WarDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("attacker_country_id")]
    public string AttackerCountryId { get; set; } = string.Empty;

    [JsonPropertyName("attacker_country_name")]
    public string AttackerCountryName { get; set; } = string.Empty;

    [JsonPropertyName("defender_country_id")]
    public string DefenderCountryId { get; set; } = string.Empty;

    [JsonPropertyName("defender_country_name")]
    public string DefenderCountryName { get; set; } = string.Empty;

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; }

    [JsonPropertyName("ended_at")]
    public DateTime? EndedAt { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
}

public class ExplanationDto
{
    [JsonPropertyName("subject_type")]
    public string SubjectType { get; set; } = string.Empty;

    [JsonPropertyName("subject_id")]
    public string SubjectId { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("generated_at")]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("factors")]
    public List<ExplanationFactorDto> Factors { get; set; } = new();

    [JsonPropertyName("metrics")]
    public Dictionary<string, decimal> Metrics { get; set; } = new();

    [JsonPropertyName("related")]
    public List<ExplanationLinkDto> Related { get; set; } = new();
}

public class ExplanationFactorDto
{
    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("detail")]
    public string Detail { get; set; } = string.Empty;

    [JsonPropertyName("impact")]
    public string Impact { get; set; } = "info";
}

public class ExplanationLinkDto
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;
}

public class CountryPopTypeDto
{
    [JsonPropertyName("pop_type")]
    public string PopType { get; set; } = string.Empty;

    [JsonPropertyName("strata")]
    public string Strata { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("employed")]
    public int Employed { get; set; }

    [JsonPropertyName("unemployed")]
    public int Unemployed { get; set; }

    [JsonPropertyName("average_literacy")]
    public decimal AverageLiteracy { get; set; }

    [JsonPropertyName("average_militancy")]
    public decimal AverageMilitancy { get; set; }

    [JsonPropertyName("average_consciousness")]
    public decimal AverageConsciousness { get; set; }

    [JsonPropertyName("average_life_needs")]
    public decimal AverageLifeNeeds { get; set; }
}

public class ProvinceInspectionDto
{
    [JsonPropertyName("province_id")]
    public string ProvinceId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = string.Empty;

    [JsonPropertyName("owner_name")]
    public string OwnerName { get; set; } = string.Empty;

    [JsonPropertyName("rgo_type")]
    public string RgoType { get; set; } = string.Empty;

    [JsonPropertyName("population")]
    public int Population { get; set; }

    [JsonPropertyName("workforce")]
    public int Workforce { get; set; }

    [JsonPropertyName("needs_fulfillment")]
    public decimal NeedsFulfillment { get; set; }

    [JsonPropertyName("outputs_per_tick")]
    public Dictionary<string, decimal> OutputsPerTick { get; set; } = new();

    [JsonPropertyName("pop_groups")]
    public List<ProvincePopGroupDto> PopGroups { get; set; } = new();

    [JsonPropertyName("factories")]
    public List<ProvinceFactoryDto> Factories { get; set; } = new();
}

public class ProvinceFactoryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("level")]
    public int Level { get; set; }

    [JsonPropertyName("output_good")]
    public string OutputGood { get; set; } = string.Empty;

    [JsonPropertyName("output_per_tick")]
    public decimal OutputPerTick { get; set; }

    [JsonPropertyName("employed_craftsmen")]
    public int EmployedCraftsmen { get; set; }

    [JsonPropertyName("employed_clerks")]
    public int EmployedClerks { get; set; }

    [JsonPropertyName("profit_last_tick")]
    public decimal ProfitLastTick { get; set; }
}
