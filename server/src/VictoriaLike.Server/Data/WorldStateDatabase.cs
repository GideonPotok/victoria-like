using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Server.Data;

public sealed record WorldSeedData(
    IEnumerable<Country> Countries,
    IEnumerable<Market> Markets,
    IEnumerable<Province> Provinces,
    IEnumerable<PlayerAccount> Players,
    IEnumerable<Factory> Factories,
    IEnumerable<ArmyStack> Armies,
    IEnumerable<War> Wars)
{
    public static WorldSeedData Empty { get; } = new(
        [], [], [], [], [], [], []);
}

public sealed record TickWriteBatch(
    IReadOnlyList<Country> Countries,
    Guid? MarketId,
    IReadOnlyDictionary<string, decimal>? MarketPrices,
    IReadOnlyDictionary<string, decimal>? MarketSupply,
    IReadOnlyDictionary<string, decimal>? MarketDemand,
    IReadOnlyDictionary<string, decimal> ProvinceNeedsFulfillment,
    IReadOnlyList<PopGroupSimulationUpdate> PopGroups,
    IReadOnlyList<Factory> Factories,
    IReadOnlyList<GoodProfitHistory> GoodProfitHistory,
    IReadOnlyList<ArmyStack> Armies,
    IReadOnlyList<War> Wars,
    IReadOnlyList<BattleReport> BattleReports,
    IReadOnlyList<BuildingQueueItem> BuildingQueue,
    IReadOnlyDictionary<string, Dictionary<string, decimal>>? ProvinceOutputs);

public interface IWorldStateDatabase
{
    Task SeedWorldAsync(WorldSeedData seed, CancellationToken cancellationToken = default);

    /// <summary>
    /// Persist all per-tick simulation results in a single transaction.
    /// </summary>
    Task SaveTickResultsAsync(TickWriteBatch batch, CancellationToken cancellationToken = default);

    Task<WorldStateSnapshot?> LoadWorldAsync(CancellationToken cancellationToken = default);
    Task UpsertCountriesAsync(IEnumerable<Country> countries, CancellationToken cancellationToken = default);
    Task UpdateMarketAsync(
        Guid marketId,
        Dictionary<string, decimal> prices,
        Dictionary<string, decimal> supply,
        Dictionary<string, decimal> demand,
        CancellationToken cancellationToken = default);
    Task UpdateProvinceNeedsFulfillmentAsync(
        Dictionary<string, decimal> needsByProvinceId,
        CancellationToken cancellationToken = default);
    Task UpdatePopGroupsAsync(
        IReadOnlyList<PopGroupSimulationUpdate> popGroups,
        CancellationToken cancellationToken = default);
    Task UpdateProvinceOutputsAsync(
        Dictionary<string, Dictionary<string, decimal>> outputsByProvinceId,
        CancellationToken cancellationToken = default);
    Task<List<BuildingQueueItem>> LoadBuildingQueueAsync(CancellationToken cancellationToken = default);
    Task SaveBuildingQueueAsync(List<BuildingQueueItem> items, CancellationToken cancellationToken = default);
    Task SaveFactoriesAsync(List<Factory> factories, CancellationToken cancellationToken = default);
    Task SaveGoodProfitHistoryAsync(List<GoodProfitHistory> history, CancellationToken cancellationToken = default);
    Task SaveArmiesAsync(List<ArmyStack> armies, CancellationToken cancellationToken = default);
    Task SaveWarsAsync(List<War> wars, CancellationToken cancellationToken = default);
    Task SaveBattleReportsAsync(List<BattleReport> battleReports, CancellationToken cancellationToken = default);
    Task ClearWorldAsync(CancellationToken cancellationToken = default);
    Task<PlayerAccount?> GetPlayerAccountAsync(Guid actorId, CancellationToken cancellationToken = default);
}

public sealed class WorldStateSnapshot
{
    public List<Country> Countries { get; init; } = new();
    public List<Market> Markets { get; init; } = new();
    public List<Province> Provinces { get; init; } = new();
    public List<PlayerAccount> Players { get; init; } = new();
    public List<BuildingQueueItem> BuildingQueue { get; init; } = new();
    public List<Factory> Factories { get; init; } = new();
    public List<GoodProfitHistory> GoodProfitHistory { get; init; } = new();
    public List<ArmyStack> Armies { get; init; } = new();
    public List<War> Wars { get; init; } = new();
    public List<BattleReport> BattleReports { get; init; } = new();
}

public sealed record PopGroupSimulationUpdate(
    Guid Id,
    int Size,
    decimal Cash,
    decimal Literacy,
    decimal Militancy,
    decimal Consciousness,
    decimal LifeNeedsFulfillment,
    decimal EverydayNeedsFulfillment,
    decimal LuxuryNeedsFulfillment,
    int EmployedCount,
    int UnemployedCount,
    string? ArtisanProducedGood,
    int ArtisanDaysUntilReconsider,
    DateTime? ArtisanLastReconsideredAt,
    decimal ArtisanProfitLastTick);

public class WorldStateDatabase : IWorldStateDatabase
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly string _connectionString;
    private readonly ILogger<WorldStateDatabase> _logger;

    public WorldStateDatabase(string connectionString, ILogger<WorldStateDatabase> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task SeedWorldAsync(WorldSeedData seed, CancellationToken cancellationToken = default)
    {
        var countries = seed.Countries;
        var markets = seed.Markets;
        var provinces = seed.Provinces;
        var players = seed.Players;
        var factories = seed.Factories;
        var armies = seed.Armies;
        var wars = seed.Wars;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "DELETE FROM sessions; DELETE FROM building_queue; DELETE FROM battle_reports; DELETE FROM army_stacks; DELETE FROM wars; DELETE FROM factories; DELETE FROM good_profit_history; DELETE FROM pop_groups; DELETE FROM provinces; DELETE FROM player_accounts; DELETE FROM market_goods; DELETE FROM markets; DELETE FROM countries;";
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var country in countries)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO countries (id, name, tag, tax_rate, treasury, tariff_rate,
                                           poor_tax_rate, middle_tax_rate, rich_tax_rate,
                                           education_spending, military_spending, administration_spending)
                    VALUES (@id, @name, @tag, @tax_rate, @treasury, @tariff_rate,
                            @poor_tax_rate, @middle_tax_rate, @rich_tax_rate,
                            @education_spending, @military_spending, @administration_spending);";
                cmd.Parameters.AddWithValue("@id", country.Id.Value);
                cmd.Parameters.AddWithValue("@name", country.Name);
                cmd.Parameters.AddWithValue("@tag", country.Tag);
                cmd.Parameters.AddWithValue("@tax_rate", country.TaxRate);
                cmd.Parameters.AddWithValue("@treasury", country.Treasury);
                cmd.Parameters.AddWithValue("@tariff_rate", country.TariffRate);
                cmd.Parameters.AddWithValue("@poor_tax_rate", country.PoorTaxRate);
                cmd.Parameters.AddWithValue("@middle_tax_rate", country.MiddleTaxRate);
                cmd.Parameters.AddWithValue("@rich_tax_rate", country.RichTaxRate);
                cmd.Parameters.AddWithValue("@education_spending", country.EducationSpending);
                cmd.Parameters.AddWithValue("@military_spending", country.MilitarySpending);
                cmd.Parameters.AddWithValue("@administration_spending", country.AdministrationSpending);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var market in markets)
            {
                using (var cmd = connection.CreateCommand())
                {
                    cmd.Transaction = transaction;
                    cmd.CommandText = "INSERT INTO markets (id, name) VALUES (@id, @name);";
                    cmd.Parameters.AddWithValue("@id", market.Id.Value);
                    cmd.Parameters.AddWithValue("@name", market.Name);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (var (goodName, price) in market.GoodPrices)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        INSERT INTO market_goods (market_id, good_name, price, supply, demand)
                        VALUES (@market_id, @good_name, @price, @supply, @demand);";
                    cmd.Parameters.AddWithValue("@market_id", market.Id.Value);
                    cmd.Parameters.AddWithValue("@good_name", goodName);
                    cmd.Parameters.AddWithValue("@price", price);
                    cmd.Parameters.AddWithValue("@supply", market.GoodSupply.GetValueOrDefault(goodName));
                    cmd.Parameters.AddWithValue("@demand", market.GoodDemand.GetValueOrDefault(goodName));
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            foreach (var province in provinces)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO provinces (id, name, owner_id, market_id, population, rgo_type, outputs_per_tick, needs_fulfillment)
                    VALUES (@id, @name, @owner_id, @market_id, @population, @rgo_type, @outputs_per_tick, @needs_fulfillment);";
                cmd.Parameters.AddWithValue("@id", province.Id.Value);
                cmd.Parameters.AddWithValue("@name", province.Name);
                cmd.Parameters.AddWithValue("@owner_id", province.OwnerId.Value);
                cmd.Parameters.AddWithValue("@market_id", province.MarketId.Value);
                cmd.Parameters.AddWithValue("@population", province.Population);
                cmd.Parameters.AddWithValue("@rgo_type", province.RgoType);
                cmd.Parameters.Add(new NpgsqlParameter("@outputs_per_tick", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(province.OutputsPerTick)
                });
                cmd.Parameters.AddWithValue("@needs_fulfillment", province.NeedsFulfillment);
                await cmd.ExecuteNonQueryAsync(cancellationToken);

                foreach (var pop in province.PopGroups)
                {
                    using var popCmd = connection.CreateCommand();
                    popCmd.Transaction = transaction;
                    popCmd.CommandText = @"
                        INSERT INTO pop_groups (
                            id, province_id, size, pop_type, strata, culture, religion,
                            literacy, militancy, consciousness, cash,
                            life_needs_fulfillment, everyday_needs_fulfillment, luxury_needs_fulfillment,
                            employed_count, unemployed_count,
                            artisan_produced_good, artisan_days_until_reconsider, artisan_last_reconsidered_at, artisan_profit_last_tick)
                        VALUES (
                            @id, @province_id, @size, @pop_type, @strata, @culture, @religion,
                            @literacy, @militancy, @consciousness, @cash,
                            @life_needs_fulfillment, @everyday_needs_fulfillment, @luxury_needs_fulfillment,
                            @employed_count, @unemployed_count,
                            @artisan_produced_good, @artisan_days_until_reconsider, @artisan_last_reconsidered_at, @artisan_profit_last_tick);";
                    popCmd.Parameters.AddWithValue("@id", pop.Id);
                    popCmd.Parameters.AddWithValue("@province_id", province.Id.Value);
                    popCmd.Parameters.AddWithValue("@size", pop.Size);
                    popCmd.Parameters.AddWithValue("@pop_type", pop.PopType);
                    popCmd.Parameters.AddWithValue("@strata", pop.Strata);
                    popCmd.Parameters.AddWithValue("@culture", pop.Culture);
                    popCmd.Parameters.AddWithValue("@religion", pop.Religion);
                    popCmd.Parameters.AddWithValue("@literacy", pop.Literacy);
                    popCmd.Parameters.AddWithValue("@militancy", pop.Militancy);
                    popCmd.Parameters.AddWithValue("@consciousness", pop.Consciousness);
                    popCmd.Parameters.AddWithValue("@cash", pop.Cash);
                    popCmd.Parameters.AddWithValue("@life_needs_fulfillment", pop.LifeNeedsFulfillment);
                    popCmd.Parameters.AddWithValue("@everyday_needs_fulfillment", pop.EverydayNeedsFulfillment);
                    popCmd.Parameters.AddWithValue("@luxury_needs_fulfillment", pop.LuxuryNeedsFulfillment);
                    popCmd.Parameters.AddWithValue("@employed_count", pop.EmployedCount);
                    popCmd.Parameters.AddWithValue("@unemployed_count", pop.UnemployedCount);
                    popCmd.Parameters.AddWithValue("@artisan_produced_good", (object?)pop.ArtisanProducedGood ?? DBNull.Value);
                    popCmd.Parameters.AddWithValue("@artisan_days_until_reconsider", Math.Max(0, pop.ArtisanDaysUntilReconsider));
                    popCmd.Parameters.AddWithValue("@artisan_last_reconsidered_at", (object?)pop.ArtisanLastReconsideredAt ?? DBNull.Value);
                    popCmd.Parameters.AddWithValue("@artisan_profit_last_tick", pop.ArtisanProfitLastTick);
                    await popCmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            foreach (var player in players)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO player_accounts (actor_id, username, controlled_country_id, created_at, password_hash)
                    VALUES (@actor_id, @username, @controlled_country_id, @created_at, @password_hash);";
                cmd.Parameters.AddWithValue("@actor_id", player.Id.Value);
                cmd.Parameters.AddWithValue("@username", player.Username);
                cmd.Parameters.AddWithValue("@controlled_country_id", player.ControlledCountry.Value);
                cmd.Parameters.AddWithValue("@created_at", player.CreatedAt);
                cmd.Parameters.AddWithValue("@password_hash", player.PasswordHash ?? string.Empty);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var factory in factories)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO factories (
                        id, country_id, province_id, type, level, employed_craftsmen, employed_clerks,
                        input_goods, output_good, output_per_tick, cash_reserve, profit_last_tick)
                    VALUES (
                        @id, @country_id, @province_id, @type, @level, @employed_craftsmen, @employed_clerks,
                        @input_goods, @output_good, @output_per_tick, @cash_reserve, @profit_last_tick);";
                cmd.Parameters.AddWithValue("@id", factory.Id);
                cmd.Parameters.AddWithValue("@country_id", factory.CountryId.Value);
                cmd.Parameters.AddWithValue("@province_id", (object?)factory.ProvinceId?.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", factory.Type);
                cmd.Parameters.AddWithValue("@level", Math.Max(1, factory.Level));
                cmd.Parameters.AddWithValue("@employed_craftsmen", Math.Max(0, factory.EmployedCraftsmen));
                cmd.Parameters.AddWithValue("@employed_clerks", Math.Max(0, factory.EmployedClerks));
                cmd.Parameters.Add(new NpgsqlParameter("@input_goods", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(factory.InputGoods)
                });
                cmd.Parameters.AddWithValue("@output_good", factory.OutputGood);
                cmd.Parameters.AddWithValue("@output_per_tick", Math.Max(0m, factory.OutputPerTick));
                cmd.Parameters.AddWithValue("@cash_reserve", Math.Max(0m, factory.CashReserve));
                cmd.Parameters.AddWithValue("@profit_last_tick", factory.ProfitLastTick);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var war in wars)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO wars (id, attacker_country_id, defender_country_id, started_at, ended_at, is_active)
                    VALUES (@id, @attacker_country_id, @defender_country_id, @started_at, @ended_at, @is_active);";
                cmd.Parameters.AddWithValue("@id", war.Id);
                cmd.Parameters.AddWithValue("@attacker_country_id", war.AttackerCountryId.Value);
                cmd.Parameters.AddWithValue("@defender_country_id", war.DefenderCountryId.Value);
                cmd.Parameters.AddWithValue("@started_at", war.StartedAt);
                cmd.Parameters.AddWithValue("@ended_at", (object?)war.EndedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is_active", war.IsActive);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var army in armies)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO army_stacks (
                        id, country_id, location_province_id, destination_province_id,
                        movement_ticks_remaining, soldier_count, morale)
                    VALUES (
                        @id, @country_id, @location_province_id, @destination_province_id,
                        @movement_ticks_remaining, @soldier_count, @morale);";
                cmd.Parameters.AddWithValue("@id", army.Id);
                cmd.Parameters.AddWithValue("@country_id", army.CountryId.Value);
                cmd.Parameters.AddWithValue("@location_province_id", army.LocationProvinceId.Value);
                cmd.Parameters.AddWithValue("@destination_province_id", (object?)army.DestinationProvinceId?.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@movement_ticks_remaining", Math.Max(0, army.MovementTicksRemaining));
                cmd.Parameters.AddWithValue("@soldier_count", Math.Max(0, army.SoldierCount));
                cmd.Parameters.AddWithValue("@morale", Math.Clamp(army.Morale, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
            _logger.LogInformation("Seeded world: {CountryCount} countries, {ProvinceCount} provinces, {PopCount} POP groups, {FactoryCount} factories, {ArmyCount} armies, {WarCount} wars, {MarketCount} markets, {PlayerCount} players",
                countries.Count(), provinces.Count(), provinces.Sum(p => p.PopGroups.Count), factories.Count(), armies.Count(), wars.Count(), markets.Count(), players.Count());
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error seeding world");
            throw;
        }
    }

    public async Task<WorldStateSnapshot?> LoadWorldAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var snapshot = new WorldStateSnapshot();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, name, tag, tax_rate, treasury, tariff_rate,
                       poor_tax_rate, middle_tax_rate, rich_tax_rate,
                       education_spending, military_spending, administration_spending
                FROM countries
                ORDER BY tag;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.Countries.Add(new Country(
                    new CountryId(reader.GetGuid(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetInt32(3))
                {
                    Treasury = reader.GetDecimal(4),
                    TariffRate = reader.GetDecimal(5),
                    PoorTaxRate = reader.GetDecimal(6),
                    MiddleTaxRate = reader.GetDecimal(7),
                    RichTaxRate = reader.GetDecimal(8),
                    EducationSpending = reader.GetDecimal(9),
                    MilitarySpending = reader.GetDecimal(10),
                    AdministrationSpending = reader.GetDecimal(11)
                });
            }
        }

        var marketMap = new Dictionary<Guid, Market>();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, name FROM markets ORDER BY name;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                var market = new Market(new MarketId(id), reader.GetString(1));
                marketMap[id] = market;
                snapshot.Markets.Add(market);
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT market_id, good_name, price, supply, demand FROM market_goods ORDER BY market_id, good_name;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var marketId = reader.GetGuid(0);
                if (marketMap.TryGetValue(marketId, out var market))
                {
                    var goodName = reader.GetString(1);
                    market.GoodPrices[goodName] = reader.GetDecimal(2);
                    market.GoodSupply[goodName] = reader.GetDecimal(3);
                    market.GoodDemand[goodName] = reader.GetDecimal(4);
                }
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, name, owner_id, market_id, population, rgo_type, outputs_per_tick, needs_fulfillment
                FROM provinces
                ORDER BY name;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var outputsJson = reader.GetString(6);
                var outputs = JsonSerializer.Deserialize<Dictionary<string, decimal>>(outputsJson, JsonOptions)
                    ?? new Dictionary<string, decimal>();

                snapshot.Provinces.Add(new Province(
                    new ProvinceId(reader.GetGuid(0)),
                    reader.GetString(1),
                    new CountryId(reader.GetGuid(2)),
                    new MarketId(reader.GetGuid(3)),
                    reader.GetInt32(4))
                {
                    RgoType = reader.GetString(5),
                    OutputsPerTick = outputs,
                    NeedsFulfillment = reader.GetDecimal(7)
                });
            }
        }

        var provinceMap = snapshot.Provinces.ToDictionary(p => p.Id.Value);
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, province_id, size, pop_type, strata, culture, religion,
                       literacy, militancy, consciousness, cash,
                       life_needs_fulfillment, everyday_needs_fulfillment, luxury_needs_fulfillment,
                       employed_count, unemployed_count,
                       artisan_produced_good, artisan_days_until_reconsider, artisan_last_reconsidered_at, artisan_profit_last_tick
                FROM pop_groups
                ORDER BY province_id, pop_type, culture;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var provinceId = reader.GetGuid(1);
                if (!provinceMap.TryGetValue(provinceId, out var province))
                    continue;

                province.PopGroups.Add(new PopGroup(
                    reader.GetGuid(0),
                    new ProvinceId(provinceId),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetDecimal(7))
                {
                    Militancy = reader.GetDecimal(8),
                    Consciousness = reader.GetDecimal(9),
                    Cash = reader.GetDecimal(10),
                    LifeNeedsFulfillment = reader.GetDecimal(11),
                    EverydayNeedsFulfillment = reader.GetDecimal(12),
                    LuxuryNeedsFulfillment = reader.GetDecimal(13),
                    EmployedCount = reader.GetInt32(14),
                    UnemployedCount = reader.GetInt32(15),
                    ArtisanProducedGood = reader.IsDBNull(16) ? null : reader.GetString(16),
                    ArtisanDaysUntilReconsider = reader.GetInt32(17),
                    ArtisanLastReconsideredAt = reader.IsDBNull(18) ? null : reader.GetDateTime(18),
                    ArtisanProfitLastTick = reader.GetDecimal(19)
                });
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT actor_id, username, controlled_country_id, created_at
                FROM player_accounts
                ORDER BY username;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.Players.Add(new PlayerAccount(
                    new ActorId(reader.GetGuid(0)),
                    reader.GetString(1),
                    new CountryId(reader.GetGuid(2)))
                {
                    CreatedAt = reader.GetDateTime(3)
                });
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, country_id, province_id, type, level, employed_craftsmen, employed_clerks,
                       input_goods, output_good, output_per_tick, cash_reserve, profit_last_tick
                FROM factories
                ORDER BY type, id;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var inputGoodsJson = reader.GetString(7);
                var inputGoods = JsonSerializer.Deserialize<Dictionary<string, decimal>>(inputGoodsJson, JsonOptions)
                    ?? new Dictionary<string, decimal>();

                snapshot.Factories.Add(new Factory
                {
                    Id = reader.GetGuid(0),
                    CountryId = new CountryId(reader.GetGuid(1)),
                    ProvinceId = reader.IsDBNull(2) ? null : new ProvinceId(reader.GetGuid(2)),
                    Type = reader.GetString(3),
                    Level = reader.GetInt32(4),
                    EmployedCraftsmen = reader.GetInt32(5),
                    EmployedClerks = reader.GetInt32(6),
                    InputGoods = inputGoods,
                    OutputGood = reader.GetString(8),
                    OutputPerTick = reader.GetDecimal(9),
                    CashReserve = reader.GetDecimal(10),
                    ProfitLastTick = reader.GetDecimal(11)
                });
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT month_key, good_id, average_producer_profit, producer_count
                FROM good_profit_history
                ORDER BY month_key, good_id;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.GoodProfitHistory.Add(new GoodProfitHistory
                {
                    Month = reader.GetString(0),
                    GoodId = reader.GetString(1),
                    AverageProducerProfit = reader.GetDecimal(2),
                    ProducerCount = reader.GetInt32(3)
                });
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, attacker_country_id, defender_country_id, started_at, ended_at, is_active
                FROM wars
                ORDER BY started_at, id;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.Wars.Add(new War
                {
                    Id = reader.GetGuid(0),
                    AttackerCountryId = new CountryId(reader.GetGuid(1)),
                    DefenderCountryId = new CountryId(reader.GetGuid(2)),
                    StartedAt = reader.GetDateTime(3),
                    EndedAt = reader.IsDBNull(4) ? null : reader.GetDateTime(4),
                    IsActive = reader.GetBoolean(5)
                });
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, country_id, location_province_id, destination_province_id,
                       movement_ticks_remaining, soldier_count, morale
                FROM army_stacks
                ORDER BY country_id, id;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.Armies.Add(new ArmyStack
                {
                    Id = reader.GetGuid(0),
                    CountryId = new CountryId(reader.GetGuid(1)),
                    LocationProvinceId = new ProvinceId(reader.GetGuid(2)),
                    DestinationProvinceId = reader.IsDBNull(3) ? null : new ProvinceId(reader.GetGuid(3)),
                    MovementTicksRemaining = reader.GetInt32(4),
                    SoldierCount = reader.GetInt32(5),
                    Morale = reader.GetDecimal(6)
                });
            }
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT id, war_id, province_id, winner_army_id, loser_army_id,
                       winner_country_id, loser_country_id, occurred_at,
                       winner_casualties, loser_casualties, winner_morale_after, loser_morale_after
                FROM battle_reports
                ORDER BY occurred_at, id;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.BattleReports.Add(new BattleReport
                {
                    Id = reader.GetString(0),
                    WarId = reader.GetGuid(1),
                    ProvinceId = reader.GetGuid(2),
                    WinnerArmyId = reader.GetGuid(3),
                    LoserArmyId = reader.GetGuid(4),
                    WinnerCountryId = reader.GetGuid(5),
                    LoserCountryId = reader.GetGuid(6),
                    OccurredAt = reader.GetDateTime(7),
                    WinnerCasualties = reader.GetInt32(8),
                    LoserCasualties = reader.GetInt32(9),
                    WinnerMoraleAfter = reader.GetDecimal(10),
                    LoserMoraleAfter = reader.GetDecimal(11)
                });
            }
        }

        if (snapshot.Countries.Count == 0)
        {
            _logger.LogInformation("No world state found in database (fresh start)");
            return null;
        }

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT id, province_id, country_id, building_type, ticks_remaining, queued_at FROM building_queue ORDER BY queued_at;";
            using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                snapshot.BuildingQueue.Add(new BuildingQueueItem
                {
                    Id = reader.GetGuid(0),
                    ProvinceId = reader.GetGuid(1),
                    CountryId = reader.GetGuid(2),
                    BuildingType = reader.GetString(3),
                    TicksRemaining = reader.GetInt32(4),
                    QueuedAt = reader.GetDateTime(5)
                });
            }
        }

        _logger.LogInformation("Loaded world: {CountryCount} countries, {ProvinceCount} provinces, {PopCount} POP groups, {FactoryCount} factories, {MarketCount} markets",
            snapshot.Countries.Count, snapshot.Provinces.Count, snapshot.Provinces.Sum(p => p.PopGroups.Count), snapshot.Factories.Count, snapshot.Markets.Count);

        return snapshot;
    }

    public async Task UpsertCountriesAsync(IEnumerable<Country> countries, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var country in countries)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO countries (id, name, tag, tax_rate, treasury, tariff_rate,
                                           poor_tax_rate, middle_tax_rate, rich_tax_rate,
                                           education_spending, military_spending, administration_spending)
                    VALUES (@id, @name, @tag, @tax_rate, @treasury, @tariff_rate,
                            @poor_tax_rate, @middle_tax_rate, @rich_tax_rate,
                            @education_spending, @military_spending, @administration_spending)
                    ON CONFLICT (id) DO UPDATE
                    SET name = EXCLUDED.name,
                        tag = EXCLUDED.tag,
                        tax_rate = EXCLUDED.tax_rate,
                        treasury = EXCLUDED.treasury,
                        tariff_rate = EXCLUDED.tariff_rate,
                        poor_tax_rate = EXCLUDED.poor_tax_rate,
                        middle_tax_rate = EXCLUDED.middle_tax_rate,
                        rich_tax_rate = EXCLUDED.rich_tax_rate,
                        education_spending = EXCLUDED.education_spending,
                        military_spending = EXCLUDED.military_spending,
                        administration_spending = EXCLUDED.administration_spending;";

                cmd.Parameters.AddWithValue("@id", country.Id.Value);
                cmd.Parameters.AddWithValue("@name", country.Name);
                cmd.Parameters.AddWithValue("@tag", country.Tag);
                cmd.Parameters.AddWithValue("@tax_rate", country.TaxRate);
                cmd.Parameters.AddWithValue("@treasury", country.Treasury);
                cmd.Parameters.AddWithValue("@tariff_rate", country.TariffRate);
                cmd.Parameters.AddWithValue("@poor_tax_rate", country.PoorTaxRate);
                cmd.Parameters.AddWithValue("@middle_tax_rate", country.MiddleTaxRate);
                cmd.Parameters.AddWithValue("@rich_tax_rate", country.RichTaxRate);
                cmd.Parameters.AddWithValue("@education_spending", country.EducationSpending);
                cmd.Parameters.AddWithValue("@military_spending", country.MilitarySpending);
                cmd.Parameters.AddWithValue("@administration_spending", country.AdministrationSpending);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
            _logger.LogDebug("Upserted {CountryCount} countries", countries.Count());
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error upserting countries");
            throw;
        }
    }

    public async Task UpdateMarketAsync(
        Guid marketId,
        Dictionary<string, decimal> prices,
        Dictionary<string, decimal> supply,
        Dictionary<string, decimal> demand,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (goodName, price) in prices)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE market_goods
                    SET price = @price, supply = @supply, demand = @demand
                    WHERE market_id = @market_id AND good_name = @good_name;";
                cmd.Parameters.AddWithValue("@market_id", marketId);
                cmd.Parameters.AddWithValue("@good_name", goodName);
                cmd.Parameters.AddWithValue("@price", price);
                cmd.Parameters.AddWithValue("@supply", supply.GetValueOrDefault(goodName));
                cmd.Parameters.AddWithValue("@demand", demand.GetValueOrDefault(goodName));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error updating market prices");
            throw;
        }
    }

    public async Task UpdateProvinceNeedsFulfillmentAsync(
        Dictionary<string, decimal> needsByProvinceId,
        CancellationToken cancellationToken = default)
    {
        if (needsByProvinceId.Count == 0)
            return;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (provinceId, fulfillment) in needsByProvinceId)
            {
                if (!Guid.TryParse(provinceId, out var id))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE provinces SET needs_fulfillment = @fulfillment WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@fulfillment", Math.Clamp(fulfillment, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error updating province needs fulfillment");
            throw;
        }
    }

    public async Task UpdatePopGroupsAsync(
        IReadOnlyList<PopGroupSimulationUpdate> popGroups,
        CancellationToken cancellationToken = default)
    {
        if (popGroups.Count == 0)
            return;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var pop in popGroups)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE pop_groups
                    SET size = @size,
                        cash = @cash,
                        literacy = @literacy,
                        militancy = @militancy,
                        consciousness = @consciousness,
                        life_needs_fulfillment = @life_needs_fulfillment,
                        everyday_needs_fulfillment = @everyday_needs_fulfillment,
                        luxury_needs_fulfillment = @luxury_needs_fulfillment,
                        employed_count = @employed_count,
                        unemployed_count = @unemployed_count,
                        artisan_produced_good = @artisan_produced_good,
                        artisan_days_until_reconsider = @artisan_days_until_reconsider,
                        artisan_last_reconsidered_at = @artisan_last_reconsidered_at,
                        artisan_profit_last_tick = @artisan_profit_last_tick
                    WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", pop.Id);
                cmd.Parameters.AddWithValue("@size", Math.Max(0, pop.Size));
                cmd.Parameters.AddWithValue("@cash", Math.Max(0m, pop.Cash));
                cmd.Parameters.AddWithValue("@literacy", Math.Clamp(pop.Literacy, 0m, 1m));
                cmd.Parameters.AddWithValue("@militancy", Math.Clamp(pop.Militancy, 0m, 10m));
                cmd.Parameters.AddWithValue("@consciousness", Math.Clamp(pop.Consciousness, 0m, 10m));
                cmd.Parameters.AddWithValue("@life_needs_fulfillment", Math.Clamp(pop.LifeNeedsFulfillment, 0m, 1m));
                cmd.Parameters.AddWithValue("@everyday_needs_fulfillment", Math.Clamp(pop.EverydayNeedsFulfillment, 0m, 1m));
                cmd.Parameters.AddWithValue("@luxury_needs_fulfillment", Math.Clamp(pop.LuxuryNeedsFulfillment, 0m, 1m));
                cmd.Parameters.AddWithValue("@employed_count", Math.Max(0, pop.EmployedCount));
                cmd.Parameters.AddWithValue("@unemployed_count", Math.Max(0, pop.UnemployedCount));
                cmd.Parameters.AddWithValue("@artisan_produced_good", (object?)pop.ArtisanProducedGood ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@artisan_days_until_reconsider", Math.Max(0, pop.ArtisanDaysUntilReconsider));
                cmd.Parameters.AddWithValue("@artisan_last_reconsidered_at", (object?)pop.ArtisanLastReconsideredAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@artisan_profit_last_tick", pop.ArtisanProfitLastTick);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error updating POP groups");
            throw;
        }
    }

    public async Task UpdateProvinceOutputsAsync(
        Dictionary<string, Dictionary<string, decimal>> outputsByProvinceId,
        CancellationToken cancellationToken = default)
    {
        if (outputsByProvinceId.Count == 0)
            return;

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            foreach (var (provinceId, outputs) in outputsByProvinceId)
            {
                if (!Guid.TryParse(provinceId, out var id))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE provinces SET outputs_per_tick = @outputs WHERE id = @id;";
                cmd.Parameters.Add(new NpgsqlParameter("@outputs", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(outputs)
                });
                cmd.Parameters.AddWithValue("@id", id);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error updating province outputs");
            throw;
        }
    }

    public async Task<List<BuildingQueueItem>> LoadBuildingQueueAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var items = new List<BuildingQueueItem>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, province_id, country_id, building_type, ticks_remaining, queued_at FROM building_queue ORDER BY queued_at;";
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new BuildingQueueItem
            {
                Id = reader.GetGuid(0),
                ProvinceId = reader.GetGuid(1),
                CountryId = reader.GetGuid(2),
                BuildingType = reader.GetString(3),
                TicksRemaining = reader.GetInt32(4),
                QueuedAt = reader.GetDateTime(5)
            });
        }

        return items;
    }

    public async Task SaveBuildingQueueAsync(List<BuildingQueueItem> items, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM building_queue;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var item in items)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO building_queue (id, province_id, country_id, building_type, ticks_remaining, queued_at)
                    VALUES (@id, @province_id, @country_id, @building_type, @ticks_remaining, @queued_at);";
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@province_id", item.ProvinceId);
                cmd.Parameters.AddWithValue("@country_id", item.CountryId);
                cmd.Parameters.AddWithValue("@building_type", item.BuildingType);
                cmd.Parameters.AddWithValue("@ticks_remaining", item.TicksRemaining);
                cmd.Parameters.AddWithValue("@queued_at", item.QueuedAt);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error saving building queue");
            throw;
        }
    }

    public async Task SaveFactoriesAsync(List<Factory> factories, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM factories;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var factory in factories)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO factories (
                        id, country_id, province_id, type, level, employed_craftsmen, employed_clerks,
                        input_goods, output_good, output_per_tick, cash_reserve, profit_last_tick)
                    VALUES (
                        @id, @country_id, @province_id, @type, @level, @employed_craftsmen, @employed_clerks,
                        @input_goods, @output_good, @output_per_tick, @cash_reserve, @profit_last_tick);";
                cmd.Parameters.AddWithValue("@id", factory.Id);
                cmd.Parameters.AddWithValue("@country_id", factory.CountryId.Value);
                cmd.Parameters.AddWithValue("@province_id", (object?)factory.ProvinceId?.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", factory.Type);
                cmd.Parameters.AddWithValue("@level", Math.Max(1, factory.Level));
                cmd.Parameters.AddWithValue("@employed_craftsmen", Math.Max(0, factory.EmployedCraftsmen));
                cmd.Parameters.AddWithValue("@employed_clerks", Math.Max(0, factory.EmployedClerks));
                cmd.Parameters.Add(new NpgsqlParameter("@input_goods", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(factory.InputGoods)
                });
                cmd.Parameters.AddWithValue("@output_good", factory.OutputGood);
                cmd.Parameters.AddWithValue("@output_per_tick", Math.Max(0m, factory.OutputPerTick));
                cmd.Parameters.AddWithValue("@cash_reserve", Math.Max(0m, factory.CashReserve));
                cmd.Parameters.AddWithValue("@profit_last_tick", factory.ProfitLastTick);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error saving factories");
            throw;
        }
    }

    public async Task SaveGoodProfitHistoryAsync(List<GoodProfitHistory> history, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM good_profit_history;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var entry in history)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO good_profit_history (month_key, good_id, average_producer_profit, producer_count)
                    VALUES (@month_key, @good_id, @average_producer_profit, @producer_count);";
                cmd.Parameters.AddWithValue("@month_key", entry.Month);
                cmd.Parameters.AddWithValue("@good_id", entry.GoodId);
                cmd.Parameters.AddWithValue("@average_producer_profit", entry.AverageProducerProfit);
                cmd.Parameters.AddWithValue("@producer_count", Math.Max(0, entry.ProducerCount));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error saving good profit history");
            throw;
        }
    }

    public async Task SaveArmiesAsync(List<ArmyStack> armies, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM army_stacks;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var army in armies)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO army_stacks (
                        id, country_id, location_province_id, destination_province_id,
                        movement_ticks_remaining, soldier_count, morale)
                    VALUES (
                        @id, @country_id, @location_province_id, @destination_province_id,
                        @movement_ticks_remaining, @soldier_count, @morale);";
                cmd.Parameters.AddWithValue("@id", army.Id);
                cmd.Parameters.AddWithValue("@country_id", army.CountryId.Value);
                cmd.Parameters.AddWithValue("@location_province_id", army.LocationProvinceId.Value);
                cmd.Parameters.AddWithValue("@destination_province_id", (object?)army.DestinationProvinceId?.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@movement_ticks_remaining", Math.Max(0, army.MovementTicksRemaining));
                cmd.Parameters.AddWithValue("@soldier_count", Math.Max(0, army.SoldierCount));
                cmd.Parameters.AddWithValue("@morale", Math.Clamp(army.Morale, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error saving army stacks");
            throw;
        }
    }

    public async Task SaveWarsAsync(List<War> wars, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM wars;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var war in wars)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO wars (id, attacker_country_id, defender_country_id, started_at, ended_at, is_active)
                    VALUES (@id, @attacker_country_id, @defender_country_id, @started_at, @ended_at, @is_active);";
                cmd.Parameters.AddWithValue("@id", war.Id);
                cmd.Parameters.AddWithValue("@attacker_country_id", war.AttackerCountryId.Value);
                cmd.Parameters.AddWithValue("@defender_country_id", war.DefenderCountryId.Value);
                cmd.Parameters.AddWithValue("@started_at", war.StartedAt);
                cmd.Parameters.AddWithValue("@ended_at", (object?)war.EndedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is_active", war.IsActive);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error saving wars");
            throw;
        }
    }

    public async Task SaveBattleReportsAsync(List<BattleReport> battleReports, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM battle_reports;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var battle in battleReports)
            {
                if (string.IsNullOrWhiteSpace(battle.Id))
                    continue;

                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO battle_reports (
                        id, war_id, province_id, winner_army_id, loser_army_id,
                        winner_country_id, loser_country_id, occurred_at,
                        winner_casualties, loser_casualties, winner_morale_after, loser_morale_after)
                    VALUES (
                        @id, @war_id, @province_id, @winner_army_id, @loser_army_id,
                        @winner_country_id, @loser_country_id, @occurred_at,
                        @winner_casualties, @loser_casualties, @winner_morale_after, @loser_morale_after);";
                cmd.Parameters.AddWithValue("@id", battle.Id);
                cmd.Parameters.AddWithValue("@war_id", battle.WarId);
                cmd.Parameters.AddWithValue("@province_id", battle.ProvinceId);
                cmd.Parameters.AddWithValue("@winner_army_id", battle.WinnerArmyId);
                cmd.Parameters.AddWithValue("@loser_army_id", battle.LoserArmyId);
                cmd.Parameters.AddWithValue("@winner_country_id", battle.WinnerCountryId);
                cmd.Parameters.AddWithValue("@loser_country_id", battle.LoserCountryId);
                cmd.Parameters.AddWithValue("@occurred_at", battle.OccurredAt);
                cmd.Parameters.AddWithValue("@winner_casualties", Math.Max(0, battle.WinnerCasualties));
                cmd.Parameters.AddWithValue("@loser_casualties", Math.Max(0, battle.LoserCasualties));
                cmd.Parameters.AddWithValue("@winner_morale_after", Math.Clamp(battle.WinnerMoraleAfter, 0m, 1m));
                cmd.Parameters.AddWithValue("@loser_morale_after", Math.Clamp(battle.LoserMoraleAfter, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            transaction.Rollback();
            throw;
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Error saving battle reports");
            throw;
        }
    }

    public async Task SaveTickResultsAsync(TickWriteBatch batch, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // Countries (upsert)
            foreach (var country in batch.Countries)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO countries (id, name, tag, tax_rate, treasury, tariff_rate,
                                           poor_tax_rate, middle_tax_rate, rich_tax_rate,
                                           education_spending, military_spending, administration_spending)
                    VALUES (@id, @name, @tag, @tax_rate, @treasury, @tariff_rate,
                            @poor_tax_rate, @middle_tax_rate, @rich_tax_rate,
                            @education_spending, @military_spending, @administration_spending)
                    ON CONFLICT (id) DO UPDATE
                    SET name = EXCLUDED.name,
                        tag = EXCLUDED.tag,
                        tax_rate = EXCLUDED.tax_rate,
                        treasury = EXCLUDED.treasury,
                        tariff_rate = EXCLUDED.tariff_rate,
                        poor_tax_rate = EXCLUDED.poor_tax_rate,
                        middle_tax_rate = EXCLUDED.middle_tax_rate,
                        rich_tax_rate = EXCLUDED.rich_tax_rate,
                        education_spending = EXCLUDED.education_spending,
                        military_spending = EXCLUDED.military_spending,
                        administration_spending = EXCLUDED.administration_spending;";
                cmd.Parameters.AddWithValue("@id", country.Id.Value);
                cmd.Parameters.AddWithValue("@name", country.Name);
                cmd.Parameters.AddWithValue("@tag", country.Tag);
                cmd.Parameters.AddWithValue("@tax_rate", country.TaxRate);
                cmd.Parameters.AddWithValue("@treasury", country.Treasury);
                cmd.Parameters.AddWithValue("@tariff_rate", country.TariffRate);
                cmd.Parameters.AddWithValue("@poor_tax_rate", country.PoorTaxRate);
                cmd.Parameters.AddWithValue("@middle_tax_rate", country.MiddleTaxRate);
                cmd.Parameters.AddWithValue("@rich_tax_rate", country.RichTaxRate);
                cmd.Parameters.AddWithValue("@education_spending", country.EducationSpending);
                cmd.Parameters.AddWithValue("@military_spending", country.MilitarySpending);
                cmd.Parameters.AddWithValue("@administration_spending", country.AdministrationSpending);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Market goods (update)
            if (batch.MarketId.HasValue && batch.MarketPrices != null)
            {
                var supply = batch.MarketSupply ?? new Dictionary<string, decimal>();
                var demand = batch.MarketDemand ?? new Dictionary<string, decimal>();
                foreach (var (goodName, price) in batch.MarketPrices)
                {
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = @"
                        UPDATE market_goods
                        SET price = @price, supply = @supply, demand = @demand
                        WHERE market_id = @market_id AND good_name = @good_name;";
                    cmd.Parameters.AddWithValue("@market_id", batch.MarketId.Value);
                    cmd.Parameters.AddWithValue("@good_name", goodName);
                    cmd.Parameters.AddWithValue("@price", price);
                    cmd.Parameters.AddWithValue("@supply", supply.TryGetValue(goodName, out var s) ? s : 0m);
                    cmd.Parameters.AddWithValue("@demand", demand.TryGetValue(goodName, out var d) ? d : 0m);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            // Province needs fulfillment
            foreach (var (provinceId, fulfillment) in batch.ProvinceNeedsFulfillment)
            {
                if (!Guid.TryParse(provinceId, out var id))
                    continue;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = "UPDATE provinces SET needs_fulfillment = @fulfillment WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.Parameters.AddWithValue("@fulfillment", Math.Clamp(fulfillment, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // POP groups
            foreach (var pop in batch.PopGroups)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    UPDATE pop_groups
                    SET size = @size,
                        cash = @cash,
                        literacy = @literacy,
                        militancy = @militancy,
                        consciousness = @consciousness,
                        life_needs_fulfillment = @life_needs_fulfillment,
                        everyday_needs_fulfillment = @everyday_needs_fulfillment,
                        luxury_needs_fulfillment = @luxury_needs_fulfillment,
                        employed_count = @employed_count,
                        unemployed_count = @unemployed_count,
                        artisan_produced_good = @artisan_produced_good,
                        artisan_days_until_reconsider = @artisan_days_until_reconsider,
                        artisan_last_reconsidered_at = @artisan_last_reconsidered_at,
                        artisan_profit_last_tick = @artisan_profit_last_tick
                    WHERE id = @id;";
                cmd.Parameters.AddWithValue("@id", pop.Id);
                cmd.Parameters.AddWithValue("@size", Math.Max(0, pop.Size));
                cmd.Parameters.AddWithValue("@cash", Math.Max(0m, pop.Cash));
                cmd.Parameters.AddWithValue("@literacy", Math.Clamp(pop.Literacy, 0m, 1m));
                cmd.Parameters.AddWithValue("@militancy", Math.Clamp(pop.Militancy, 0m, 10m));
                cmd.Parameters.AddWithValue("@consciousness", Math.Clamp(pop.Consciousness, 0m, 10m));
                cmd.Parameters.AddWithValue("@life_needs_fulfillment", Math.Clamp(pop.LifeNeedsFulfillment, 0m, 1m));
                cmd.Parameters.AddWithValue("@everyday_needs_fulfillment", Math.Clamp(pop.EverydayNeedsFulfillment, 0m, 1m));
                cmd.Parameters.AddWithValue("@luxury_needs_fulfillment", Math.Clamp(pop.LuxuryNeedsFulfillment, 0m, 1m));
                cmd.Parameters.AddWithValue("@employed_count", Math.Max(0, pop.EmployedCount));
                cmd.Parameters.AddWithValue("@unemployed_count", Math.Max(0, pop.UnemployedCount));
                cmd.Parameters.AddWithValue("@artisan_produced_good", (object?)pop.ArtisanProducedGood ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@artisan_days_until_reconsider", Math.Max(0, pop.ArtisanDaysUntilReconsider));
                cmd.Parameters.AddWithValue("@artisan_last_reconsidered_at", (object?)pop.ArtisanLastReconsideredAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@artisan_profit_last_tick", pop.ArtisanProfitLastTick);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Factories (replace all)
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM factories;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var factory in batch.Factories)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO factories (
                        id, country_id, province_id, type, level, employed_craftsmen, employed_clerks,
                        input_goods, output_good, output_per_tick, cash_reserve, profit_last_tick)
                    VALUES (
                        @id, @country_id, @province_id, @type, @level, @employed_craftsmen, @employed_clerks,
                        @input_goods, @output_good, @output_per_tick, @cash_reserve, @profit_last_tick);";
                cmd.Parameters.AddWithValue("@id", factory.Id);
                cmd.Parameters.AddWithValue("@country_id", factory.CountryId.Value);
                cmd.Parameters.AddWithValue("@province_id", (object?)factory.ProvinceId?.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@type", factory.Type);
                cmd.Parameters.AddWithValue("@level", Math.Max(1, factory.Level));
                cmd.Parameters.AddWithValue("@employed_craftsmen", Math.Max(0, factory.EmployedCraftsmen));
                cmd.Parameters.AddWithValue("@employed_clerks", Math.Max(0, factory.EmployedClerks));
                cmd.Parameters.Add(new NpgsqlParameter("@input_goods", NpgsqlDbType.Jsonb)
                {
                    Value = JsonSerializer.Serialize(factory.InputGoods)
                });
                cmd.Parameters.AddWithValue("@output_good", factory.OutputGood);
                cmd.Parameters.AddWithValue("@output_per_tick", Math.Max(0m, factory.OutputPerTick));
                cmd.Parameters.AddWithValue("@cash_reserve", Math.Max(0m, factory.CashReserve));
                cmd.Parameters.AddWithValue("@profit_last_tick", factory.ProfitLastTick);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Good profit history (replace all)
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM good_profit_history;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var entry in batch.GoodProfitHistory)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO good_profit_history (month_key, good_id, average_producer_profit, producer_count)
                    VALUES (@month_key, @good_id, @average_producer_profit, @producer_count);";
                cmd.Parameters.AddWithValue("@month_key", entry.Month);
                cmd.Parameters.AddWithValue("@good_id", entry.GoodId);
                cmd.Parameters.AddWithValue("@average_producer_profit", entry.AverageProducerProfit);
                cmd.Parameters.AddWithValue("@producer_count", Math.Max(0, entry.ProducerCount));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Armies (replace all)
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM army_stacks;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var army in batch.Armies)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO army_stacks (
                        id, country_id, location_province_id, destination_province_id,
                        movement_ticks_remaining, soldier_count, morale)
                    VALUES (
                        @id, @country_id, @location_province_id, @destination_province_id,
                        @movement_ticks_remaining, @soldier_count, @morale);";
                cmd.Parameters.AddWithValue("@id", army.Id);
                cmd.Parameters.AddWithValue("@country_id", army.CountryId.Value);
                cmd.Parameters.AddWithValue("@location_province_id", army.LocationProvinceId.Value);
                cmd.Parameters.AddWithValue("@destination_province_id", (object?)army.DestinationProvinceId?.Value ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@movement_ticks_remaining", Math.Max(0, army.MovementTicksRemaining));
                cmd.Parameters.AddWithValue("@soldier_count", Math.Max(0, army.SoldierCount));
                cmd.Parameters.AddWithValue("@morale", Math.Clamp(army.Morale, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Wars (replace all)
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM wars;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var war in batch.Wars)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO wars (id, attacker_country_id, defender_country_id, started_at, ended_at, is_active)
                    VALUES (@id, @attacker_country_id, @defender_country_id, @started_at, @ended_at, @is_active);";
                cmd.Parameters.AddWithValue("@id", war.Id);
                cmd.Parameters.AddWithValue("@attacker_country_id", war.AttackerCountryId.Value);
                cmd.Parameters.AddWithValue("@defender_country_id", war.DefenderCountryId.Value);
                cmd.Parameters.AddWithValue("@started_at", war.StartedAt);
                cmd.Parameters.AddWithValue("@ended_at", (object?)war.EndedAt ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@is_active", war.IsActive);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Battle reports (replace all)
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM battle_reports;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var battle in batch.BattleReports)
            {
                if (string.IsNullOrWhiteSpace(battle.Id))
                    continue;
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO battle_reports (
                        id, war_id, province_id, winner_army_id, loser_army_id,
                        winner_country_id, loser_country_id, occurred_at,
                        winner_casualties, loser_casualties, winner_morale_after, loser_morale_after)
                    VALUES (
                        @id, @war_id, @province_id, @winner_army_id, @loser_army_id,
                        @winner_country_id, @loser_country_id, @occurred_at,
                        @winner_casualties, @loser_casualties, @winner_morale_after, @loser_morale_after);";
                cmd.Parameters.AddWithValue("@id", battle.Id);
                cmd.Parameters.AddWithValue("@war_id", battle.WarId);
                cmd.Parameters.AddWithValue("@province_id", battle.ProvinceId);
                cmd.Parameters.AddWithValue("@winner_army_id", battle.WinnerArmyId);
                cmd.Parameters.AddWithValue("@loser_army_id", battle.LoserArmyId);
                cmd.Parameters.AddWithValue("@winner_country_id", battle.WinnerCountryId);
                cmd.Parameters.AddWithValue("@loser_country_id", battle.LoserCountryId);
                cmd.Parameters.AddWithValue("@occurred_at", battle.OccurredAt);
                cmd.Parameters.AddWithValue("@winner_casualties", Math.Max(0, battle.WinnerCasualties));
                cmd.Parameters.AddWithValue("@loser_casualties", Math.Max(0, battle.LoserCasualties));
                cmd.Parameters.AddWithValue("@winner_morale_after", Math.Clamp(battle.WinnerMoraleAfter, 0m, 1m));
                cmd.Parameters.AddWithValue("@loser_morale_after", Math.Clamp(battle.LoserMoraleAfter, 0m, 1m));
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Building queue (replace all)
            using (var del = connection.CreateCommand())
            {
                del.Transaction = transaction;
                del.CommandText = "DELETE FROM building_queue;";
                await del.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (var item in batch.BuildingQueue)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO building_queue (id, province_id, country_id, building_type, ticks_remaining, queued_at)
                    VALUES (@id, @province_id, @country_id, @building_type, @ticks_remaining, @queued_at);";
                cmd.Parameters.AddWithValue("@id", item.Id);
                cmd.Parameters.AddWithValue("@province_id", item.ProvinceId);
                cmd.Parameters.AddWithValue("@country_id", item.CountryId);
                cmd.Parameters.AddWithValue("@building_type", item.BuildingType);
                cmd.Parameters.AddWithValue("@ticks_remaining", item.TicksRemaining);
                cmd.Parameters.AddWithValue("@queued_at", item.QueuedAt);
                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            // Province outputs (optional partial update)
            if (batch.ProvinceOutputs != null)
            {
                foreach (var (provinceId, outputs) in batch.ProvinceOutputs)
                {
                    if (!Guid.TryParse(provinceId, out var id))
                        continue;
                    using var cmd = connection.CreateCommand();
                    cmd.Transaction = transaction;
                    cmd.CommandText = "UPDATE provinces SET outputs_per_tick = @outputs WHERE id = @id;";
                    cmd.Parameters.Add(new NpgsqlParameter("@outputs", NpgsqlDbType.Jsonb)
                    {
                        Value = JsonSerializer.Serialize(outputs)
                    });
                    cmd.Parameters.AddWithValue("@id", id);
                    await cmd.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            _logger.LogError(ex, "Error saving tick results");
            throw;
        }
    }

    public async Task ClearWorldAsync(CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions; DELETE FROM building_queue; DELETE FROM battle_reports; DELETE FROM army_stacks; DELETE FROM wars; DELETE FROM factories; DELETE FROM good_profit_history; DELETE FROM pop_groups; DELETE FROM provinces; DELETE FROM player_accounts; DELETE FROM market_goods; DELETE FROM markets; DELETE FROM countries;";
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Cleared world state");
    }

    public async Task<PlayerAccount?> GetPlayerAccountAsync(Guid actorId, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT actor_id, username, controlled_country_id, created_at FROM player_accounts WHERE actor_id = @actor_id LIMIT 1;";
        cmd.Parameters.AddWithValue("@actor_id", actorId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new PlayerAccount(
            new ActorId(reader.GetGuid(0)),
            reader.GetString(1),
            new CountryId(reader.GetGuid(2)))
        {
            CreatedAt = reader.GetDateTime(3)
        };
    }
}
