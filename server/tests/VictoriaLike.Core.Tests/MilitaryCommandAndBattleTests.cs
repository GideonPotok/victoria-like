using System.Text.Json;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class MilitaryCommandAndBattleTests
{
    [Fact]
    public void MoveArmy_QueuesMovementWithEta()
    {
        var fixture = CreateTwoCountryWorld();
        var command = CreateCommand(
            fixture.ActorA,
            "MoveArmy",
            ("armyId", $"\"{fixture.ArmyA}\""),
            ("destinationProvinceId", $"\"{fixture.ProvinceA2}\""));

        var result = new MoveArmyCommandHandler().Handle(command, fixture.World, fixture.ActorA);

        Assert.True(result.IsSuccess);
        var army = fixture.World.Armies[fixture.ArmyA.ToString()];
        Assert.Equal(fixture.ProvinceA2.ToString(), army.DestinationProvinceId);
        Assert.Equal(2, army.MovementTicksRemaining);

        new ArmyMovementStage().Execute(CreateContext(fixture.World));
        Assert.Equal(1, army.MovementTicksRemaining);
        Assert.Equal(fixture.ProvinceA1.ToString(), army.LocationProvinceId);
    }

    [Fact]
    public void MoveArmy_RejectsNeutralForeignProvince()
    {
        var fixture = CreateTwoCountryWorld();
        var command = CreateCommand(
            fixture.ActorA,
            "MoveArmy",
            ("armyId", $"\"{fixture.ArmyA}\""),
            ("destinationProvinceId", $"\"{fixture.ProvinceB1}\""));

        var result = new MoveArmyCommandHandler().Handle(command, fixture.World, fixture.ActorA);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.InvalidMovementTarget, result.RejectionReason);
        Assert.Null(fixture.World.Armies[fixture.ArmyA.ToString()].DestinationProvinceId);
    }

    [Fact]
    public void DeclareWarAndMakePeace_MaintainSingleWarState()
    {
        var fixture = CreateTwoCountryWorld();
        var declare = CreateCommand(
            fixture.ActorA,
            "DeclareWar",
            ("targetCountryId", $"\"{fixture.CountryB}\""));

        var declareResult = new DeclareWarCommandHandler().Handle(declare, fixture.World, fixture.ActorA);
        var duplicateResult = new DeclareWarCommandHandler().Handle(declare, fixture.World, fixture.ActorA);
        var peace = CreateCommand(
            fixture.ActorA,
            "MakePeace",
            ("targetCountryId", $"\"{fixture.CountryB}\""));
        var peaceResult = new MakePeaceCommandHandler().Handle(peace, fixture.World, fixture.ActorA);

        Assert.True(declareResult.IsSuccess);
        Assert.False(duplicateResult.IsSuccess);
        Assert.Equal(CommandRejectionReason.AlreadyAtWar, duplicateResult.RejectionReason);
        Assert.True(peaceResult.IsSuccess);
        Assert.Single(fixture.World.Wars);
        Assert.False(fixture.World.Wars.Values.Single().IsActive);
        Assert.Empty(new WorldInvariantChecker().Check(fixture.World).Violations);
    }

    [Fact]
    public void BattleResolution_IsDeterministicAndRetreatsLoser()
    {
        var first = CreateTwoCountryWorld(activeWar: true);
        var second = CreateTwoCountryWorld(activeWar: true);
        first.World.Armies[first.ArmyA.ToString()].LocationProvinceId = first.ProvinceB1.ToString();
        second.World.Armies[second.ArmyA.ToString()].LocationProvinceId = second.ProvinceB1.ToString();

        new BattleResolutionStage().Execute(CreateContext(first.World));
        new BattleResolutionStage().Execute(CreateContext(second.World));

        var firstWinner = first.World.Armies[first.ArmyA.ToString()];
        var firstLoser = first.World.Armies[first.ArmyB.ToString()];
        var secondWinner = second.World.Armies[second.ArmyA.ToString()];
        var secondLoser = second.World.Armies[second.ArmyB.ToString()];

        Assert.Equal(firstWinner.SoldierCount, secondWinner.SoldierCount);
        Assert.Equal(firstLoser.SoldierCount, secondLoser.SoldierCount);
        Assert.Equal(first.ProvinceB2.ToString(), firstLoser.LocationProvinceId);
        Assert.Contains(first.World.EventLog, entry => entry.StartsWith("battle-resolved:", StringComparison.Ordinal));
    }

    [Fact]
    public void InvariantChecker_RejectsContradictoryActiveWarState()
    {
        var fixture = CreateTwoCountryWorld(activeWar: true);
        fixture.World.Wars[Guid.NewGuid().ToString()] = new WarState
        {
            Id = Guid.NewGuid().ToString(),
            AttackerCountryId = fixture.CountryB.ToString(),
            DefenderCountryId = fixture.CountryA.ToString(),
            StartedOn = fixture.World.Date.Value,
            IsActive = true
        };

        var report = new WorldInvariantChecker().Check(fixture.World);

        Assert.Contains(report.Violations, violation => violation.Code == "war_duplicate_active_pair");
    }

    private static CommandEnvelope CreateCommand(ActorId actorId, string commandType, params (string Key, string Json)[] values)
    {
        var payload = new Dictionary<string, object>();
        foreach (var (key, json) in values)
            payload[key] = JsonSerializer.Deserialize<JsonElement>(json);

        return new CommandEnvelope(actorId, commandType, payload);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(0),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private static MilitaryFixture CreateTwoCountryWorld(bool activeWar = false)
    {
        var countryA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var countryB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var provinceA1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var provinceA2 = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var provinceB1 = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var provinceB2 = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var armyA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
        var armyB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var actorA = ActorId.New();

        var world = new WorldState
        {
            Seed = 0,
            Date = new GameDate(new DateOnly(1836, 1, 1)),
            Countries = new Dictionary<string, CountryState>
            {
                [countryA.ToString()] = new()
                {
                    Id = countryA.ToString(),
                    DisplayName = "Albion",
                    ProvinceIds = [provinceA1.ToString(), provinceA2.ToString()],
                    Treasury = 1_000m,
                    TaxRate = 10m,
                    MilitarySpending = 0.8m,
                    IsPlayable = true
                },
                [countryB.ToString()] = new()
                {
                    Id = countryB.ToString(),
                    DisplayName = "Bretoria",
                    ProvinceIds = [provinceB1.ToString(), provinceB2.ToString()],
                    Treasury = 1_000m,
                    TaxRate = 10m,
                    MilitarySpending = 0.4m,
                    IsPlayable = true
                }
            },
            Provinces = new Dictionary<string, ProvinceState>
            {
                [provinceA1.ToString()] = new() { Id = provinceA1.ToString(), DisplayName = "A1", OwnerId = countryA.ToString() },
                [provinceA2.ToString()] = new() { Id = provinceA2.ToString(), DisplayName = "A2", OwnerId = countryA.ToString() },
                [provinceB1.ToString()] = new() { Id = provinceB1.ToString(), DisplayName = "B1", OwnerId = countryB.ToString() },
                [provinceB2.ToString()] = new() { Id = provinceB2.ToString(), DisplayName = "B2", OwnerId = countryB.ToString() }
            },
            Armies = new Dictionary<string, ArmyStackState>
            {
                [armyA.ToString()] = new()
                {
                    Id = armyA.ToString(),
                    CountryId = countryA.ToString(),
                    LocationProvinceId = provinceA1.ToString(),
                    SoldierCount = 1_000,
                    Morale = 1m
                },
                [armyB.ToString()] = new()
                {
                    Id = armyB.ToString(),
                    CountryId = countryB.ToString(),
                    LocationProvinceId = provinceB1.ToString(),
                    SoldierCount = 800,
                    Morale = 1m
                }
            },
            PlayerAccounts = new Dictionary<string, PlayerAccount>
            {
                [actorA.ToString()] = new(actorA, "albion-player", new CountryId(countryA))
            },
            Pops = new Dictionary<string, VictoriaLike.Core.Core.Pops.PopState>(),
            Goods = new Dictionary<string, VictoriaLike.Core.Core.Economy.GoodDefinition>(),
            Market = new VictoriaLike.Core.Core.Economy.MarketState(),
            Metrics = new SimulationMetrics(),
            EventLog = []
        };

        if (activeWar)
        {
            var warId = Guid.Parse("99999999-9999-9999-9999-999999999999").ToString();
            world.Wars[warId] = new WarState
            {
                Id = warId,
                AttackerCountryId = countryA.ToString(),
                DefenderCountryId = countryB.ToString(),
                StartedOn = world.Date.Value,
                IsActive = true
            };
        }

        return new MilitaryFixture(world, actorA, countryA, countryB, provinceA1, provinceA2, provinceB1, provinceB2, armyA, armyB);
    }

    private sealed record MilitaryFixture(
        WorldState World,
        ActorId ActorA,
        Guid CountryA,
        Guid CountryB,
        Guid ProvinceA1,
        Guid ProvinceA2,
        Guid ProvinceB1,
        Guid ProvinceB2,
        Guid ArmyA,
        Guid ArmyB);
}
