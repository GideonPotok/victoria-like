using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VictoriaLike.Server.Api.Dtos;

public sealed class AdminSummaryDto
{
    [JsonPropertyName("tick")]
    public long Tick { get; set; }

    [JsonPropertyName("world_date")]
    public string WorldDate { get; set; } = string.Empty;

    [JsonPropertyName("is_paused")]
    public bool IsPaused { get; set; }

    [JsonPropertyName("last_tick_duration_ms")]
    public long LastTickDurationMs { get; set; }

    [JsonPropertyName("average_tick_duration_ms")]
    public double AverageTickDurationMs { get; set; }

    [JsonPropertyName("connected_clients")]
    public int ConnectedClients { get; set; }

    [JsonPropertyName("active_sessions")]
    public int ActiveSessions { get; set; }

    [JsonPropertyName("active_subscriptions")]
    public int ActiveSubscriptions { get; set; }

    [JsonPropertyName("pending_commands")]
    public int PendingCommands { get; set; }

    [JsonPropertyName("last_tick_db_writes")]
    public long LastTickDbWrites { get; set; }

    [JsonPropertyName("total_db_writes")]
    public long TotalDbWrites { get; set; }

    [JsonPropertyName("command_budgets")]
    public List<AdminCommandBudgetDto> CommandBudgets { get; set; } = new();

    [JsonPropertyName("server_health")]
    public string ServerHealth { get; set; } = string.Empty;

    [JsonPropertyName("health_checks")]
    public List<AdminHealthCheckDto> HealthChecks { get; set; } = new();

    [JsonPropertyName("connections")]
    public List<AdminConnectionDto> Connections { get; set; } = new();

    [JsonPropertyName("recent_commands")]
    public List<CommandHistoryDto> RecentCommands { get; set; } = new();

    [JsonPropertyName("latest_snapshot")]
    public AdminSnapshotDto? LatestSnapshot { get; set; }

    [JsonPropertyName("recent_snapshots")]
    public List<AdminSnapshotDto> RecentSnapshots { get; set; } = new();

    [JsonPropertyName("tick_profile")]
    public Dictionary<string, long> TickProfile { get; set; } = new();

    [JsonPropertyName("invariant_violations")]
    public List<AdminInvariantViolationDto> InvariantViolations { get; set; } = new();
}

public sealed class AdminInvariantViolationDto
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class CreateSnapshotRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class AdminMarketInspectorDto
{
    [JsonPropertyName("tick")]
    public long Tick { get; set; }

    [JsonPropertyName("goods")]
    public List<AdminMarketGoodDto> Goods { get; set; } = new();

    [JsonPropertyName("top_shortages")]
    public List<AdminMarketGoodDto> TopShortages { get; set; } = new();

    [JsonPropertyName("average_needs_fulfillment")]
    public decimal AverageNeedsFulfillment { get; set; }

    [JsonPropertyName("price_history_ticks")]
    public int PriceHistoryTicks { get; set; }
}

public sealed class AdminMarketGoodDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("previous_price")]
    public decimal PreviousPrice { get; set; }

    [JsonPropertyName("base_price")]
    public decimal BasePrice { get; set; }

    [JsonPropertyName("target_pressure")]
    public decimal TargetPressure { get; set; }

    [JsonPropertyName("supply")]
    public decimal Supply { get; set; }

    [JsonPropertyName("demand")]
    public decimal Demand { get; set; }

    [JsonPropertyName("unmet_demand")]
    public decimal UnmetDemand { get; set; }

    [JsonPropertyName("clamp_applied")]
    public bool ClampApplied { get; set; }

    [JsonPropertyName("largest_producer")]
    public string? LargestProducer { get; set; }

    [JsonPropertyName("largest_consumer")]
    public string? LargestConsumer { get; set; }

    [JsonPropertyName("fulfillment_rate")]
    public decimal FulfillmentRate { get; set; }

    [JsonPropertyName("price_delta")]
    public decimal PriceDelta { get; set; }

    [JsonPropertyName("price_history")]
    public List<decimal> PriceHistory { get; set; } = new();
}

public sealed class AdminProvinceInspectorDto
{
    [JsonPropertyName("province_id")]
    public string ProvinceId { get; set; } = string.Empty;

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

    [JsonPropertyName("workforce")]
    public int Workforce { get; set; }

    [JsonPropertyName("rgo_type")]
    public string RgoType { get; set; } = string.Empty;

    [JsonPropertyName("outputs_per_tick")]
    public Dictionary<string, decimal> OutputsPerTick { get; set; } = new();

    [JsonPropertyName("local_demand")]
    public Dictionary<string, decimal> LocalDemand { get; set; } = new();

    [JsonPropertyName("needs_fulfillment")]
    public decimal NeedsFulfillment { get; set; }

    [JsonPropertyName("pop_groups")]
    public List<AdminProvincePopGroupDto> PopGroups { get; set; } = new();

    [JsonPropertyName("construction")]
    public List<AdminConstructionQueueItemDto> Construction { get; set; } = new();

    [JsonPropertyName("factories")]
    public List<AdminFactoryDto> Factories { get; set; } = new();
}

public sealed class AdminFactoryDto
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

    [JsonPropertyName("input_goods")]
    public Dictionary<string, decimal> InputGoods { get; set; } = new();

    [JsonPropertyName("cash_reserve")]
    public decimal CashReserve { get; set; }

    [JsonPropertyName("profit_last_tick")]
    public decimal ProfitLastTick { get; set; }
}

public sealed class AdminProvincePopGroupDto
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

public sealed class AdminCountryInspectorDto
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

    [JsonPropertyName("controlled_account_id")]
    public string? ControlledAccountId { get; set; }

    [JsonPropertyName("controlled_username")]
    public string? ControlledUsername { get; set; }

    [JsonPropertyName("province_count")]
    public int ProvinceCount { get; set; }

    [JsonPropertyName("population")]
    public int Population { get; set; }

    [JsonPropertyName("active_commands")]
    public List<AdminCommandAuditRecordDto> ActiveCommands { get; set; } = new();

    [JsonPropertyName("market_summary")]
    public List<AdminMarketGoodDto> MarketSummary { get; set; } = new();

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

    [JsonPropertyName("average_literacy")]
    public decimal AverageLiteracy { get; set; }

    [JsonPropertyName("average_militancy")]
    public decimal AverageMilitancy { get; set; }

    [JsonPropertyName("average_consciousness")]
    public decimal AverageConsciousness { get; set; }

    [JsonPropertyName("unemployment_share")]
    public decimal UnemploymentShare { get; set; }

    [JsonPropertyName("reform_pressure")]
    public decimal ReformPressure { get; set; }

    [JsonPropertyName("pop_type_breakdown")]
    public List<AdminCountryPopTypeDto> PopTypeBreakdown { get; set; } = new();

    [JsonPropertyName("pop_groups")]
    public List<AdminCountryPopGroupDto> PopGroups { get; set; } = new();
}

public sealed class AdminCountryPopGroupDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("province_id")]
    public string ProvinceId { get; set; } = string.Empty;

    [JsonPropertyName("province_name")]
    public string ProvinceName { get; set; } = string.Empty;

    [JsonPropertyName("pop_type")]
    public string PopType { get; set; } = string.Empty;

    [JsonPropertyName("strata")]
    public string Strata { get; set; } = string.Empty;

    [JsonPropertyName("culture")]
    public string Culture { get; set; } = string.Empty;

    [JsonPropertyName("religion")]
    public string Religion { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public int Size { get; set; }

    [JsonPropertyName("employed_count")]
    public int EmployedCount { get; set; }

    [JsonPropertyName("unemployed_count")]
    public int UnemployedCount { get; set; }

    [JsonPropertyName("literacy")]
    public decimal Literacy { get; set; }

    [JsonPropertyName("militancy")]
    public decimal Militancy { get; set; }

    [JsonPropertyName("life_needs_fulfillment")]
    public decimal LifeNeedsFulfillment { get; set; }
}

public sealed class AdminCountryPopTypeDto
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

public sealed class AdminConstructionQueueItemDto
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

public sealed class AdminTickProfileDto
{
    [JsonPropertyName("tick")]
    public long Tick { get; set; }

    [JsonPropertyName("total_duration_ms")]
    public long TotalDurationMs { get; set; }

    [JsonPropertyName("average_duration_ms")]
    public double AverageDurationMs { get; set; }

    [JsonPropertyName("stages")]
    public Dictionary<string, long> Stages { get; set; } = new();
}

public sealed class AdminHealthCheckDto
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class AdminConnectionDto
{
    [JsonPropertyName("actor_id")]
    public string? ActorId { get; set; }

    [JsonPropertyName("connected_at_utc")]
    public DateTime ConnectedAtUtc { get; set; }

    [JsonPropertyName("subscriptions")]
    public List<string> Subscriptions { get; set; } = new();
}

public sealed class AdminCommandBudgetDto
{
    [JsonPropertyName("actor_id")]
    public string ActorId { get; set; } = string.Empty;

    [JsonPropertyName("country_id")]
    public string? CountryId { get; set; }

    [JsonPropertyName("used_in_window")]
    public int UsedInWindow { get; set; }

    [JsonPropertyName("remaining_in_window")]
    public int RemainingInWindow { get; set; }

    [JsonPropertyName("soft_limit")]
    public int SoftLimit { get; set; }

    [JsonPropertyName("hard_limit")]
    public int HardLimit { get; set; }

    [JsonPropertyName("window_seconds")]
    public double WindowSeconds { get; set; }

    [JsonPropertyName("cooldowns_remaining_ticks")]
    public Dictionary<string, long> CooldownsRemainingTicks { get; set; } = new();
}

public sealed class AdminSnapshotDto
{
    [JsonPropertyName("file_name")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("savepoint_name")]
    public string? SavepointName { get; set; }

    [JsonPropertyName("tick")]
    public long Tick { get; set; }

    [JsonPropertyName("world_date")]
    public string WorldDate { get; set; } = string.Empty;

    [JsonPropertyName("captured_at_utc")]
    public DateTime CapturedAtUtc { get; set; }
}

public sealed class AdminCommandAuditDto
{
    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("filters")]
    public Dictionary<string, string?> Filters { get; set; } = new();

    [JsonPropertyName("records")]
    public List<AdminCommandAuditRecordDto> Records { get; set; } = new();
}

public sealed class AdminCommandAuditRecordDto
{
    [JsonPropertyName("command_id")]
    public string CommandId { get; set; } = string.Empty;

    [JsonPropertyName("actor_id")]
    public string ActorId { get; set; } = string.Empty;

    [JsonPropertyName("country_id")]
    public string? CountryId { get; set; }

    [JsonPropertyName("command_type")]
    public string CommandType { get; set; } = string.Empty;

    [JsonPropertyName("target_ids")]
    public List<string> TargetIds { get; set; } = [];

    [JsonPropertyName("submitted_at")]
    public DateTime SubmittedAt { get; set; }

    [JsonPropertyName("submitted_tick")]
    public long SubmittedTick { get; set; }

    [JsonPropertyName("expected_world_tick")]
    public long? ExpectedWorldTick { get; set; }

    [JsonPropertyName("idempotency_key")]
    public string? IdempotencyKey { get; set; }

    [JsonPropertyName("executed_tick")]
    public long? ExecutedTick { get; set; }

    [JsonPropertyName("executed_at")]
    public DateTime? ExecutedAt { get; set; }

    [JsonPropertyName("outcome")]
    public string Outcome { get; set; } = string.Empty;

    [JsonPropertyName("outcome_reason")]
    public string? OutcomeReason { get; set; }

    [JsonPropertyName("rejection_reason_code")]
    public string? RejectionReasonCode { get; set; }
}
