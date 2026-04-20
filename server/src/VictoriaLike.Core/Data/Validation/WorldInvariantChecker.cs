using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Core.Data.Validation;

public sealed record WorldInvariantViolation(string Code, string Message);

public sealed record WorldInvariantReport(IReadOnlyList<WorldInvariantViolation> Violations)
{
    public bool IsValid => Violations.Count == 0;
}

public sealed class WorldInvariantChecker
{
    public WorldInvariantReport Check(WorldState world)
    {
        var violations = new List<WorldInvariantViolation>();

        CheckCountries(world, violations);
        CheckProvinces(world, violations);
        CheckPops(world, violations);
        CheckMarket(world, violations);
        CheckFactories(world, violations);
        CheckGoodProfitHistory(world, violations);
        CheckBuildingQueue(world, violations);
        CheckMilitary(world, violations);
        CheckPlayerAccounts(world, violations);

        return new WorldInvariantReport(violations);
    }

    public void ThrowIfInvalid(WorldState world)
    {
        var report = Check(world);
        if (!report.IsValid)
            throw new InvalidOperationException(string.Join("; ", report.Violations.Select(v => $"{v.Code}: {v.Message}")));
    }

    private static void CheckCountries(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var (countryId, country) in world.Countries)
        {
            if (!IsFinite(country.Treasury))
                violations.Add(new("country_treasury_not_finite", $"Country {countryId} treasury is not finite"));

            if (!IsFinite(country.TaxRate) || country.TaxRate < 0m || country.TaxRate > 100m)
                violations.Add(new("country_tax_out_of_bounds", $"Country {countryId} tax rate {country.TaxRate} is outside 0-100"));

            foreach (var provinceId in country.ProvinceIds)
            {
                if (!world.Provinces.ContainsKey(provinceId))
                    violations.Add(new("country_missing_province", $"Country {countryId} references missing province {provinceId}"));
            }

            CheckNonNegativeDictionary(country.Stockpile, $"country {countryId} stockpile", "country_stockpile_negative", violations);
        }
    }

    private static void CheckProvinces(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var (provinceId, province) in world.Provinces)
        {
            if (!world.Countries.ContainsKey(province.OwnerId))
                violations.Add(new("province_missing_owner", $"Province {provinceId} references missing owner country {province.OwnerId}"));

            if (string.IsNullOrWhiteSpace(province.RgoType))
                violations.Add(new("province_missing_rgo", $"Province {provinceId} has no RGO type"));

            foreach (var popId in province.PopulationIds)
            {
                if (!world.Pops.TryGetValue(popId, out var pop))
                {
                    violations.Add(new("province_missing_pop", $"Province {provinceId} references missing pop {popId}"));
                    continue;
                }

                if (!string.Equals(pop.ProvinceId, provinceId, StringComparison.Ordinal))
                    violations.Add(new("pop_province_mismatch", $"Pop {popId} belongs to {pop.ProvinceId} but is listed in {provinceId}"));
            }

            CheckNonNegativeDictionary(province.Stockpile, $"province {provinceId} stockpile", "province_stockpile_negative", violations);
            CheckNonNegativeDictionary(province.OutputsPerTick, $"province {provinceId} outputs", "province_outputs_negative", violations);
            foreach (var goodId in province.OutputsPerTick.Keys)
            {
                if (string.IsNullOrWhiteSpace(goodId) || !world.Goods.ContainsKey(goodId))
                    violations.Add(new("province_unknown_output_good", $"Province {provinceId} outputs unknown good {goodId}"));
            }
        }
    }

    private static void CheckPops(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var (popId, pop) in world.Pops)
        {
            if (!world.Provinces.ContainsKey(pop.ProvinceId))
                violations.Add(new("pop_missing_province", $"Pop {popId} references missing province {pop.ProvinceId}"));

            if (pop.Size < 0)
                violations.Add(new("pop_size_negative", $"Pop {popId} size is negative"));

            if (pop.EmployedCount < 0 || pop.UnemployedCount < 0)
                violations.Add(new("pop_employment_negative", $"Pop {popId} has negative employment counts"));

            if (pop.EmployedCount + pop.UnemployedCount > pop.Size)
                violations.Add(new("pop_employment_exceeds_size", $"Pop {popId} employment exceeds size"));

            if (!IsFinite(pop.CashReserve) || pop.CashReserve < 0m)
                violations.Add(new("pop_cash_negative", $"Pop {popId} cash reserve {pop.CashReserve} is negative"));

            if (!IsFinite(pop.Literacy) || pop.Literacy < 0m || pop.Literacy > 1m)
                violations.Add(new("pop_literacy_out_of_bounds", $"Pop {popId} literacy {pop.Literacy} is outside 0-1"));

            if (!IsFinite(pop.Militancy) || pop.Militancy < 0m || pop.Militancy > 10m)
                violations.Add(new("pop_militancy_out_of_bounds", $"Pop {popId} militancy {pop.Militancy} is outside 0-10"));

            if (!IsFinite(pop.Consciousness) || pop.Consciousness < 0m || pop.Consciousness > 10m)
                violations.Add(new("pop_consciousness_out_of_bounds", $"Pop {popId} consciousness {pop.Consciousness} is outside 0-10"));

            if (!IsFinite(pop.NeedsFulfillment) || pop.NeedsFulfillment < 0m || pop.NeedsFulfillment > 1m)
                violations.Add(new("pop_needs_out_of_bounds", $"Pop {popId} needs fulfillment {pop.NeedsFulfillment} is outside 0-1"));

            if (!IsFinite(pop.LifeNeedsFulfillment) || pop.LifeNeedsFulfillment < 0m || pop.LifeNeedsFulfillment > 1m)
                violations.Add(new("pop_life_needs_out_of_bounds", $"Pop {popId} life needs fulfillment {pop.LifeNeedsFulfillment} is outside 0-1"));

            if (!IsFinite(pop.EverydayNeedsFulfillment) || pop.EverydayNeedsFulfillment < 0m || pop.EverydayNeedsFulfillment > 1m)
                violations.Add(new("pop_everyday_needs_out_of_bounds", $"Pop {popId} everyday needs fulfillment {pop.EverydayNeedsFulfillment} is outside 0-1"));

            if (!IsFinite(pop.LuxuryNeedsFulfillment) || pop.LuxuryNeedsFulfillment < 0m || pop.LuxuryNeedsFulfillment > 1m)
                violations.Add(new("pop_luxury_needs_out_of_bounds", $"Pop {popId} luxury needs fulfillment {pop.LuxuryNeedsFulfillment} is outside 0-1"));

            if (pop.ArtisanDaysUntilReconsider < 0)
                violations.Add(new("pop_artisan_reconsider_negative", $"Pop {popId} artisan reconsider delay is negative"));

            if (!IsFinite(pop.ArtisanProfitLastTick))
                violations.Add(new("pop_artisan_profit_invalid", $"Pop {popId} artisan profit {pop.ArtisanProfitLastTick} is invalid"));
        }
    }

    private static void CheckMarket(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var good in world.Goods.Values)
        {
            if (!world.Market.Prices.TryGetValue(good.Id, out var price))
                continue;

            var max = good.BasePrice * 5m;
            if (!IsFinite(price) || price < 0.5m || price > max)
                violations.Add(new("market_price_out_of_bounds", $"Good {good.Id} price {price} is outside 0.5-{max}"));
        }

        CheckNonNegativeDictionary(world.Market.SupplyLastTick, "market supply", "market_supply_negative", violations);
        CheckNonNegativeDictionary(world.Market.DemandLastTick, "market demand", "market_demand_negative", violations);
        CheckNonNegativeDictionary(world.Market.ProductionLastTick, "market production", "market_production_negative", violations);
        CheckNonNegativeDictionary(world.Market.ConsumptionLastTick, "market consumption", "market_consumption_negative", violations);
    }

    private static void CheckFactories(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var (factoryId, factory) in world.Factories)
        {
            if (!world.Countries.ContainsKey(factory.CountryId))
                violations.Add(new("factory_missing_country", $"Factory {factoryId} references missing country {factory.CountryId}"));

            if (!string.IsNullOrWhiteSpace(factory.ProvinceId) && !world.Provinces.ContainsKey(factory.ProvinceId))
                violations.Add(new("factory_missing_province", $"Factory {factoryId} references missing province {factory.ProvinceId}"));

            if (factory.Level < 1)
                violations.Add(new("factory_level_invalid", $"Factory {factoryId} level {factory.Level} is less than 1"));

            if (factory.EmployedCraftsmen < 0 || factory.EmployedClerks < 0)
                violations.Add(new("factory_employment_negative", $"Factory {factoryId} has negative employment"));

            if (!IsFinite(factory.OutputPerTick) || factory.OutputPerTick < 0m)
                violations.Add(new("factory_output_invalid", $"Factory {factoryId} output per tick {factory.OutputPerTick} is invalid"));

            if (string.IsNullOrWhiteSpace(factory.OutputGood) || !world.Goods.ContainsKey(factory.OutputGood))
                violations.Add(new("factory_unknown_output_good", $"Factory {factoryId} outputs unknown good {factory.OutputGood}"));

            if (!IsFinite(factory.CashReserve) || factory.CashReserve < 0m)
                violations.Add(new("factory_cash_invalid", $"Factory {factoryId} cash reserve {factory.CashReserve} is invalid"));

            CheckNonNegativeDictionary(factory.InputGoods, $"factory {factoryId} inputs", "factory_inputs_negative", violations);
            foreach (var goodId in factory.InputGoods.Keys)
            {
                if (string.IsNullOrWhiteSpace(goodId) || !world.Goods.ContainsKey(goodId))
                    violations.Add(new("factory_unknown_input_good", $"Factory {factoryId} consumes unknown good {goodId}"));
            }
        }
    }

    private static void CheckGoodProfitHistory(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var entry in world.GoodProfitHistory)
        {
            if (string.IsNullOrWhiteSpace(entry.Month))
                violations.Add(new("profit_history_missing_month", "Good profit history entry has no month"));

            if (string.IsNullOrWhiteSpace(entry.GoodId))
                violations.Add(new("profit_history_missing_good", "Good profit history entry has no good id"));

            if (!IsFinite(entry.AverageProducerProfit))
                violations.Add(new("profit_history_profit_invalid", $"Good {entry.GoodId} history profit {entry.AverageProducerProfit} is invalid"));

            if (entry.ProducerCount < 0)
                violations.Add(new("profit_history_producer_count_negative", $"Good {entry.GoodId} producer count is negative"));
        }
    }

    private static void CheckBuildingQueue(WorldState world, List<WorldInvariantViolation> violations)
    {
        var activeProvinceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in world.BuildingQueue)
        {
            if (!world.Provinces.ContainsKey(entry.ProvinceId))
                violations.Add(new("building_queue_missing_province", $"Building queue item {entry.Id} references missing province {entry.ProvinceId}"));

            if (!world.Countries.ContainsKey(entry.CountryId))
                violations.Add(new("building_queue_missing_country", $"Building queue item {entry.Id} references missing country {entry.CountryId}"));

            if (entry.TicksRemaining < 0 || entry.TicksRemaining > MaxReasonableBuildTicks(entry))
                violations.Add(new("building_queue_ticks_out_of_bounds", $"Building queue item {entry.Id} has {entry.TicksRemaining} ticks remaining"));

            if (string.IsNullOrWhiteSpace(entry.BuildingType))
                violations.Add(new("building_queue_missing_type", $"Building queue item {entry.Id} has no building type"));

            if (!activeProvinceIds.Add(entry.ProvinceId))
                violations.Add(new("building_queue_duplicate_province", $"Province {entry.ProvinceId} has multiple active construction queue items"));
        }
    }

    private static void CheckPlayerAccounts(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var (actorId, account) in world.PlayerAccounts)
        {
            if (!world.Countries.ContainsKey(account.ControlledCountry.Value.ToString()))
                violations.Add(new("account_missing_country", $"Actor {actorId} controls missing country {account.ControlledCountry}"));
        }
    }

    private static void CheckMilitary(WorldState world, List<WorldInvariantViolation> violations)
    {
        foreach (var (armyId, army) in world.Armies)
        {
            if (!world.Countries.ContainsKey(army.CountryId))
                violations.Add(new("army_missing_country", $"Army {armyId} references missing country {army.CountryId}"));

            if (!world.Provinces.ContainsKey(army.LocationProvinceId))
                violations.Add(new("army_missing_location", $"Army {armyId} references missing province {army.LocationProvinceId}"));

            if (!string.IsNullOrWhiteSpace(army.DestinationProvinceId) &&
                !world.Provinces.ContainsKey(army.DestinationProvinceId))
                violations.Add(new("army_missing_destination", $"Army {armyId} references missing destination {army.DestinationProvinceId}"));

            if (army.SoldierCount < 0)
                violations.Add(new("army_soldiers_negative", $"Army {armyId} has negative soldiers"));

            if (army.MovementTicksRemaining < 0)
                violations.Add(new("army_movement_negative", $"Army {armyId} has negative movement ticks"));

            if (!IsFinite(army.Morale) || army.Morale < 0m || army.Morale > 1m)
                violations.Add(new("army_morale_out_of_bounds", $"Army {armyId} morale {army.Morale} is outside 0-1"));
        }

        var activeWarPairs = new HashSet<(string First, string Second)>();
        foreach (var (warId, war) in world.Wars)
        {
            if (!world.Countries.ContainsKey(war.AttackerCountryId))
                violations.Add(new("war_missing_attacker", $"War {warId} references missing attacker {war.AttackerCountryId}"));

            if (!world.Countries.ContainsKey(war.DefenderCountryId))
                violations.Add(new("war_missing_defender", $"War {warId} references missing defender {war.DefenderCountryId}"));

            if (string.Equals(war.AttackerCountryId, war.DefenderCountryId, StringComparison.Ordinal))
                violations.Add(new("war_self_conflict", $"War {warId} has the same attacker and defender"));

            if (!war.IsActive)
                continue;

            var pair = string.CompareOrdinal(war.AttackerCountryId, war.DefenderCountryId) <= 0
                ? (war.AttackerCountryId, war.DefenderCountryId)
                : (war.DefenderCountryId, war.AttackerCountryId);

            if (!activeWarPairs.Add(pair))
                violations.Add(new("war_duplicate_active_pair", $"Multiple active wars exist between {pair.Item1} and {pair.Item2}"));
        }

        foreach (var (battleId, battle) in world.BattleReports)
        {
            if (!world.Wars.ContainsKey(battle.WarId))
                violations.Add(new("battle_missing_war", $"Battle {battleId} references missing war {battle.WarId}"));

            if (!world.Provinces.ContainsKey(battle.ProvinceId))
                violations.Add(new("battle_missing_province", $"Battle {battleId} references missing province {battle.ProvinceId}"));

            if (!world.Countries.ContainsKey(battle.WinnerCountryId))
                violations.Add(new("battle_missing_winner_country", $"Battle {battleId} references missing winner country {battle.WinnerCountryId}"));

            if (!world.Countries.ContainsKey(battle.LoserCountryId))
                violations.Add(new("battle_missing_loser_country", $"Battle {battleId} references missing loser country {battle.LoserCountryId}"));

            if (battle.WinnerCasualties < 0 || battle.LoserCasualties < 0)
                violations.Add(new("battle_casualties_negative", $"Battle {battleId} has negative casualties"));
        }
    }

    private static void CheckNonNegativeDictionary(
        IReadOnlyDictionary<string, decimal> values,
        string label,
        string code,
        List<WorldInvariantViolation> violations)
    {
        foreach (var (key, value) in values)
        {
            if (!IsFinite(value) || value < 0m)
                violations.Add(new(code, $"{label} value {key} is {value}"));
        }
    }

    private static bool IsFinite(decimal value) =>
        value != decimal.MinValue && value != decimal.MaxValue;

    private static int MaxReasonableBuildTicks(BuildingQueueEntry entry) =>
        string.IsNullOrWhiteSpace(entry.BuildingType) ? 10_000 : 10_000;
}
