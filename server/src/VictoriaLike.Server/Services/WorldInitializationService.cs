using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Scenarios;
using VictoriaLike.Server.Auth;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public interface IWorldInitializationService
{
    Task InitializeWorldAsync(CancellationToken cancellationToken = default);
}

public class WorldInitializationService : IWorldInitializationService
{
    private readonly IConfiguration _configuration;
    private readonly IWorldStateDatabase _worldDatabase;
    private readonly IWorldStateRepository _worldStateRepository;
    private readonly IScenarioLoader _scenarioLoader;
    private readonly IWorldSnapshotService _snapshotService;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<WorldInitializationService> _logger;

    public WorldInitializationService(
        IConfiguration configuration,
        IWorldStateDatabase worldDatabase,
        IWorldStateRepository worldStateRepository,
        IScenarioLoader scenarioLoader,
        IWorldSnapshotService snapshotService,
        IPasswordHasher passwordHasher,
        ISessionRepository sessionRepository,
        ILogger<WorldInitializationService> logger)
    {
        _configuration = configuration;
        _worldDatabase = worldDatabase;
        _worldStateRepository = worldStateRepository;
        _scenarioLoader = scenarioLoader;
        _snapshotService = snapshotService;
        _passwordHasher = passwordHasher;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task InitializeWorldAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Initializing world state...");

        // Check if world already exists in database
        var existing = await _worldDatabase.LoadWorldAsync(cancellationToken);
        if (existing != null)
        {
            var validationErrors = ValidateWorldSnapshot(existing);
            if (validationErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Existing world state is invalid and cannot be started: {string.Join("; ", validationErrors)}");
            }

            if (existing.Armies.Count == 0)
            {
                existing.Armies.AddRange(BuildDefaultArmies(existing));
                if (existing.Armies.Count > 0)
                {
                    await _worldDatabase.SaveArmiesAsync(existing.Armies, cancellationToken);
                    _logger.LogInformation("Created {ArmyCount} default army stack(s) for existing world", existing.Armies.Count);
                }
            }

            _logger.LogInformation(
                "World already exists in database, skipping initialization: countries={CountryCount} provinces={ProvinceCount} markets={MarketCount} building_queue={BuildingQueueCount}",
                existing.Countries.Count,
                existing.Provinces.Count,
                existing.Markets.Count,
                existing.BuildingQueue.Count);
            return;
        }

        var restoreLatestSnapshot = _configuration.GetValue<bool>("World:Snapshots:RestoreLatestOnStartup", true);
        if (restoreLatestSnapshot)
        {
            var snapshot = await _snapshotService.LoadLatestAsync(cancellationToken);
            if (snapshot != null)
            {
                var players = snapshot.ToPlayers();

                // Snapshots don't store password hashes. Priority:
                // 1. Existing hash already in DB (survive multiple restarts)
                // 2. Plain-text password from scenario file (first boot after clear)
                var snapshotScenarioPath = _configuration.GetValue<string>("World:ScenarioPath")
                    ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content", "scenarios", "tiny-2country.json");
                LoadedScenario? scenarioForPasswords = null;
                try { scenarioForPasswords = await _scenarioLoader.LoadAsync(snapshotScenarioPath, cancellationToken); }
                catch { /* scenario unavailable — passwords will remain empty */ }

                foreach (var p in players)
                {
                    var creds = await _sessionRepository.GetCredentialsAsync(p.Username, cancellationToken);
                    if (creds.HasValue && !string.IsNullOrEmpty(creds.Value.passwordHash))
                    {
                        p.PasswordHash = creds.Value.passwordHash;
                    }
                    else if (scenarioForPasswords != null)
                    {
                        var sp = scenarioForPasswords.Players.Find(x => x.Username == p.Username);
                        if (!string.IsNullOrWhiteSpace(sp?.PasswordHash))
                            p.PasswordHash = _passwordHasher.Hash(sp.PasswordHash);
                    }
                }

                var snapshotArmies = snapshot.ToArmies();
                if (snapshotArmies.Count == 0)
                {
                    snapshotArmies = BuildDefaultArmies(new WorldStateSnapshot
                    {
                        Countries = snapshot.ToCountries().ToList(),
                        Provinces = snapshot.ToProvinces().ToList()
                    });
                }

                await _worldDatabase.SeedWorldAsync(
                    new WorldSeedData(
                        snapshot.ToCountries(),
                        snapshot.ToMarkets(),
                        snapshot.ToProvinces(),
                        players,
                        snapshot.ToFactories(),
                        snapshotArmies,
                        snapshot.ToWars()),
                    cancellationToken);
                await _worldDatabase.SaveBuildingQueueAsync(snapshot.ToBuildingQueue(), cancellationToken);
                await _worldDatabase.SaveGoodProfitHistoryAsync(snapshot.ToGoodProfitHistory(), cancellationToken);
                await _worldDatabase.SaveBattleReportsAsync(snapshot.ToBattleReports(), cancellationToken);

                await _worldStateRepository.SaveAsync(
                    new WorldState
                    {
                        TickNumber = snapshot.TickNumber,
                        WorldTimestamp = snapshot.WorldTimestamp,
                        LastSavedAt = snapshot.CapturedAtUtc
                    },
                    cancellationToken);

                _logger.LogInformation(
                    "World restored from snapshot: tick {Tick} date {WorldDate} building_queue={BuildingQueueCount}",
                    snapshot.TickNumber,
                    snapshot.WorldTimestamp.ToString("yyyy-MM-dd"),
                    snapshot.BuildingQueue.Count);
                return;
            }
        }

        // Load scenario and seed
        var scenarioPath = _configuration.GetValue<string>("World:ScenarioPath")
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "content", "scenarios", "tiny-2country.json");

        _logger.LogInformation("Loading scenario from {Path}", scenarioPath);

        var scenario = await _scenarioLoader.LoadAsync(scenarioPath, cancellationToken);
        _logger.LogInformation("Loaded scenario: {Name}", scenario.Name);

        // Hash plain-text passwords supplied by the scenario before persisting
        foreach (var player in scenario.Players)
        {
            if (!string.IsNullOrWhiteSpace(player.PasswordHash))
                player.PasswordHash = _passwordHasher.Hash(player.PasswordHash);
        }

        await _worldDatabase.SeedWorldAsync(
            new WorldSeedData(
                scenario.Countries,
                scenario.Markets,
                scenario.Provinces,
                scenario.Players,
                scenario.Factories,
                scenario.Armies,
                scenario.Wars),
            cancellationToken);

        _logger.LogInformation("World initialized from scenario: {Name}", scenario.Name);
    }

    private static IReadOnlyList<string> ValidateWorldSnapshot(WorldStateSnapshot snapshot)
    {
        var errors = new List<string>();
        var countryIds = snapshot.Countries.Select(country => country.Id.Value).ToHashSet();
        var marketIds = snapshot.Markets.Select(market => market.Id.Value).ToHashSet();
        var provinceIds = snapshot.Provinces.Select(province => province.Id.Value).ToHashSet();

        if (snapshot.Countries.Count == 0)
            errors.Add("World has no countries");
        if (snapshot.Markets.Count == 0)
            errors.Add("World has no markets");

        foreach (var province in snapshot.Provinces)
        {
            if (!countryIds.Contains(province.OwnerId.Value))
                errors.Add($"Province {province.Id} references missing owner country {province.OwnerId}");
            if (!marketIds.Contains(province.MarketId.Value))
                errors.Add($"Province {province.Id} references missing market {province.MarketId}");
        }

        foreach (var player in snapshot.Players)
        {
            if (!countryIds.Contains(player.ControlledCountry.Value))
                errors.Add($"Player {player.Id} controls missing country {player.ControlledCountry}");
        }

        foreach (var item in snapshot.BuildingQueue)
        {
            if (!provinceIds.Contains(item.ProvinceId))
                errors.Add($"Building queue item {item.Id} references missing province {item.ProvinceId}");
            if (!countryIds.Contains(item.CountryId))
                errors.Add($"Building queue item {item.Id} references missing country {item.CountryId}");
            if (item.TicksRemaining < 0)
                errors.Add($"Building queue item {item.Id} has negative ticks remaining");
            if (string.IsNullOrWhiteSpace(item.BuildingType))
                errors.Add($"Building queue item {item.Id} has no building type");
        }

        foreach (var factory in snapshot.Factories)
        {
            if (!countryIds.Contains(factory.CountryId.Value))
                errors.Add($"Factory {factory.Id} references missing country {factory.CountryId}");
            if (factory.ProvinceId.HasValue && !provinceIds.Contains(factory.ProvinceId.Value.Value))
                errors.Add($"Factory {factory.Id} references missing province {factory.ProvinceId}");
        }

        foreach (var army in snapshot.Armies)
        {
            if (!countryIds.Contains(army.CountryId.Value))
                errors.Add($"Army {army.Id} references missing country {army.CountryId}");
            if (!provinceIds.Contains(army.LocationProvinceId.Value))
                errors.Add($"Army {army.Id} references missing province {army.LocationProvinceId}");
            if (army.DestinationProvinceId.HasValue && !provinceIds.Contains(army.DestinationProvinceId.Value.Value))
                errors.Add($"Army {army.Id} references missing destination {army.DestinationProvinceId}");
        }

        var activeWarPairs = new HashSet<(Guid First, Guid Second)>();
        foreach (var war in snapshot.Wars)
        {
            if (!countryIds.Contains(war.AttackerCountryId.Value))
                errors.Add($"War {war.Id} references missing attacker country {war.AttackerCountryId}");
            if (!countryIds.Contains(war.DefenderCountryId.Value))
                errors.Add($"War {war.Id} references missing defender country {war.DefenderCountryId}");
            if (war.AttackerCountryId.Value == war.DefenderCountryId.Value)
                errors.Add($"War {war.Id} has the same attacker and defender");
            if (!war.IsActive)
                continue;

            var pair = war.AttackerCountryId.Value.CompareTo(war.DefenderCountryId.Value) <= 0
                ? (war.AttackerCountryId.Value, war.DefenderCountryId.Value)
                : (war.DefenderCountryId.Value, war.AttackerCountryId.Value);
            if (!activeWarPairs.Add(pair))
                errors.Add($"Multiple active wars exist between {pair.Item1} and {pair.Item2}");
        }

        return errors;
    }

    private static List<ArmyStack> BuildDefaultArmies(WorldStateSnapshot snapshot)
    {
        return snapshot.Countries
            .Select(country => new
            {
                Country = country,
                FirstProvince = snapshot.Provinces
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
}
