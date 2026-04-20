using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Economy;
using VictoriaLike.Core.Core.Military;
using VictoriaLike.Core.Core.Pops;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

/// Week 23 hardening — verifies that under a multi-player scenario each actor can
/// only mutate the country, province, or army it controls, and that concurrent
/// commands from independent actors do not interfere with one another.
public sealed class MultiplayerCommandIsolationTests
{
    // ----------------------- per-handler wrong-country rejection -----------------------

    [Fact]
    public void ChangeTaxRate_RejectsActorWhoControlsDifferentCountry()
    {
        var fixture = TwoPlayerFixture.Create();

        var command = NewCommand(fixture.PlayerA, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryB}\""),
            ("newTaxRate", "30"));
        var result = new ChangeTaxRateCommandHandler().Handle(command, fixture.World, fixture.PlayerA);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.NotCountryOwner, result.RejectionReason);
        Assert.Equal(10m, fixture.World.Countries[fixture.CountryB.ToString()].TaxRate);
    }

    [Fact]
    public void ChangeStrataTax_RejectsActorWhoControlsDifferentCountry()
    {
        var fixture = TwoPlayerFixture.Create();

        var command = NewCommand(fixture.PlayerA, "ChangeStrataTax",
            ("countryId", $"\"{fixture.CountryB}\""),
            ("strata", "\"poor\""),
            ("rate", "0.42"));
        var result = new ChangeStrataTaxCommandHandler().Handle(command, fixture.World, fixture.PlayerA);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.NotCountryOwner, result.RejectionReason);
        Assert.Equal(-1m, fixture.World.Countries[fixture.CountryB.ToString()].PoorTaxRate);
    }

    [Fact]
    public void QueueBuilding_RejectsActorWhoDoesNotOwnTheProvince()
    {
        var fixture = TwoPlayerFixture.Create();

        var command = NewCommand(fixture.PlayerA, "QueueBuilding",
            ("provinceId", $"\"{fixture.ProvinceB}\""),
            ("buildingType", "\"railroad\""));
        var result = new QueueBuildingCommandHandler().Handle(command, fixture.World, fixture.PlayerA);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.ProvinceNotOwned, result.RejectionReason);
        Assert.Empty(fixture.World.BuildingQueue);
        Assert.Equal(10_000m, fixture.World.Countries[fixture.CountryB.ToString()].Treasury);
    }

    [Fact]
    public void MoveArmy_RejectsActorWhoDoesNotOwnTheArmy()
    {
        var fixture = TwoPlayerFixture.Create();

        var command = NewCommand(fixture.PlayerA, "MoveArmy",
            ("armyId", $"\"{fixture.ArmyB}\""),
            ("destinationProvinceId", $"\"{fixture.ProvinceB}\""));
        var result = new MoveArmyCommandHandler().Handle(command, fixture.World, fixture.PlayerA);

        Assert.False(result.IsSuccess);
        Assert.Equal(CommandRejectionReason.NotCountryOwner, result.RejectionReason);
        Assert.False(fixture.World.Armies[fixture.ArmyB.ToString()].IsMoving);
    }

    // ----------------------- two-player concurrent isolation -----------------------

    [Fact]
    public async Task ProcessCommandsAsync_BothPlayersIndependentCommandsApply()
    {
        var fixture = TwoPlayerFixture.Create();
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { (ICommandHandler)new ChangeTaxRateCommandHandler() }, recorder);

        var aTaxes = NewCommand(fixture.PlayerA, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryA}\""),
            ("newTaxRate", "22"));
        aTaxes.SubmittedTick = 4;
        aTaxes.ReceivedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc);

        var bTaxes = NewCommand(fixture.PlayerB, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryB}\""),
            ("newTaxRate", "28"));
        bTaxes.SubmittedTick = 4;
        bTaxes.ReceivedAt = new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc);

        await stage.ProcessCommandsAsync(
            new List<CommandEnvelope> { bTaxes, aTaxes },
            CreateContext(fixture.World),
            currentTick: 5);

        Assert.Equal(22m, fixture.World.Countries[fixture.CountryA.ToString()].TaxRate);
        Assert.Equal(28m, fixture.World.Countries[fixture.CountryB.ToString()].TaxRate);
        Assert.All(recorder.Outcomes, o => Assert.Equal("applied", o.Status));
    }

    [Fact]
    public async Task ProcessCommandsAsync_AdversarialCommandRejected_OwnCommandStillAppliesInSameBatch()
    {
        var fixture = TwoPlayerFixture.Create();
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { (ICommandHandler)new ChangeTaxRateCommandHandler() }, recorder);

        // Player A tries to mutate Player B's country.
        var aSabotage = NewCommand(fixture.PlayerA, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryB}\""),
            ("newTaxRate", "99"));
        aSabotage.SubmittedTick = 4;
        aSabotage.ReceivedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc);

        // Player B's legitimate command in the same batch.
        var bOwn = NewCommand(fixture.PlayerB, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryB}\""),
            ("newTaxRate", "27"));
        bOwn.SubmittedTick = 4;
        bOwn.ReceivedAt = new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc);

        await stage.ProcessCommandsAsync(
            new List<CommandEnvelope> { aSabotage, bOwn },
            CreateContext(fixture.World),
            currentTick: 5);

        Assert.Equal(27m, fixture.World.Countries[fixture.CountryB.ToString()].TaxRate);
        Assert.Equal(10m, fixture.World.Countries[fixture.CountryA.ToString()].TaxRate);

        var sabotageOutcome = recorder.Outcomes.Single(o => o.CommandId.Equals(aSabotage.Id));
        var ownOutcome = recorder.Outcomes.Single(o => o.CommandId.Equals(bOwn.Id));
        Assert.Equal("rejected", sabotageOutcome.Status);
        Assert.Equal(CommandRejectionReason.NotCountryOwner, sabotageOutcome.RejectionReason);
        Assert.Equal("applied", ownOutcome.Status);
    }

    [Fact]
    public async Task ProcessCommandsAsync_DuplicateCommandIdAcrossActors_StillDeduped()
    {
        var fixture = TwoPlayerFixture.Create();
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { (ICommandHandler)new ChangeTaxRateCommandHandler() }, recorder);

        var sharedCommandId = CommandId.New();
        var first = NewCommand(fixture.PlayerA, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryA}\""),
            ("newTaxRate", "21"));
        first.Id = sharedCommandId;
        first.SubmittedTick = 4;
        first.ReceivedAt = new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc);

        var collision = NewCommand(fixture.PlayerB, "ChangeTaxRate",
            ("countryId", $"\"{fixture.CountryB}\""),
            ("newTaxRate", "31"));
        collision.Id = sharedCommandId;
        collision.SubmittedTick = 4;
        collision.ReceivedAt = new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc);

        await stage.ProcessCommandsAsync(
            new List<CommandEnvelope> { first, collision },
            CreateContext(fixture.World),
            currentTick: 5);

        Assert.Equal(21m, fixture.World.Countries[fixture.CountryA.ToString()].TaxRate);
        Assert.Equal(10m, fixture.World.Countries[fixture.CountryB.ToString()].TaxRate);
        Assert.Equal("applied", recorder.Outcomes[0].Status);
        Assert.Equal("rejected", recorder.Outcomes[1].Status);
        Assert.Equal(CommandRejectionReason.DuplicateCommand, recorder.Outcomes[1].RejectionReason);
    }

    // ----------------------- helpers -----------------------

    private static CommandEnvelope NewCommand(ActorId actor, string commandType, params (string Key, string Json)[] values)
    {
        var payload = new Dictionary<string, object>();
        foreach (var (key, json) in values)
            payload[key] = JsonSerializer.Deserialize<JsonElement>(json);
        return new CommandEnvelope(actor, commandType, payload);
    }

    private static SimulationContext CreateContext(WorldState world) =>
        new()
        {
            World = world,
            Random = new SeededRandom(0),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };

    private sealed class TwoPlayerFixture
    {
        public required WorldState World { get; init; }
        public required ActorId PlayerA { get; init; }
        public required ActorId PlayerB { get; init; }
        public required Guid CountryA { get; init; }
        public required Guid CountryB { get; init; }
        public required Guid ProvinceA { get; init; }
        public required Guid ProvinceB { get; init; }
        public required Guid ArmyA { get; init; }
        public required Guid ArmyB { get; init; }

        public static TwoPlayerFixture Create()
        {
            var playerA = ActorId.New();
            var playerB = ActorId.New();
            var countryA = Guid.NewGuid();
            var countryB = Guid.NewGuid();
            var provinceA = Guid.NewGuid();
            var provinceB = Guid.NewGuid();
            var armyA = Guid.NewGuid();
            var armyB = Guid.NewGuid();

            var world = new WorldState
            {
                Seed = 0,
                Date = new GameDate(new DateOnly(1836, 1, 1)),
                Countries = new Dictionary<string, CountryState>
                {
                    [countryA.ToString()] = NewCountryState(countryA, "Albion", new[] { provinceA }),
                    [countryB.ToString()] = NewCountryState(countryB, "Bretoria", new[] { provinceB })
                },
                PlayerAccounts = new Dictionary<string, PlayerAccount>
                {
                    [playerA.ToString()] = new(playerA, "albion-player", new CountryId(countryA)),
                    [playerB.ToString()] = new(playerB, "bretoria-player", new CountryId(countryB))
                },
                Provinces = new Dictionary<string, ProvinceState>
                {
                    [provinceA.ToString()] = NewProvince(provinceA, "Albion-Capital", countryA),
                    [provinceB.ToString()] = NewProvince(provinceB, "Bretoria-Capital", countryB)
                },
                Armies = new Dictionary<string, ArmyStackState>
                {
                    [armyA.ToString()] = new()
                    {
                        Id = armyA.ToString(),
                        CountryId = countryA.ToString(),
                        LocationProvinceId = provinceA.ToString(),
                        SoldierCount = 1_000
                    },
                    [armyB.ToString()] = new()
                    {
                        Id = armyB.ToString(),
                        CountryId = countryB.ToString(),
                        LocationProvinceId = provinceB.ToString(),
                        SoldierCount = 1_000
                    }
                },
                Pops = new Dictionary<string, PopState>(),
                Goods = new Dictionary<string, GoodDefinition>(),
                Market = new MarketState(),
                Metrics = new SimulationMetrics(),
                BuildingQueue = new List<BuildingQueueEntry>(),
                EventLog = new List<string>()
            };

            return new TwoPlayerFixture
            {
                World = world,
                PlayerA = playerA,
                PlayerB = playerB,
                CountryA = countryA,
                CountryB = countryB,
                ProvinceA = provinceA,
                ProvinceB = provinceB,
                ArmyA = armyA,
                ArmyB = armyB
            };
        }

        private static CountryState NewCountryState(Guid id, string name, IEnumerable<Guid> provinceIds) =>
            new()
            {
                Id = id.ToString(),
                DisplayName = name,
                ProvinceIds = provinceIds.Select(p => p.ToString()).ToList(),
                Treasury = 10_000m,
                TaxRate = 10m,
                TariffRate = 0m,
                EducationSpending = 0.5m,
                MilitarySpending = 0.5m,
                AdministrationSpending = 0.5m,
                IsPlayable = true
            };

        private static ProvinceState NewProvince(Guid id, string name, Guid ownerCountry) =>
            new()
            {
                Id = id.ToString(),
                DisplayName = name,
                OwnerId = ownerCountry.ToString(),
                Infrastructure = 1m
            };
    }

    private sealed class CapturingOutcomeRecorder : ICommandOutcomeRecorder
    {
        public List<CapturedOutcome> Outcomes { get; } = new();

        public Task RecordOutcomeAsync(
            CommandId commandId,
            ActorId actorId,
            string commandType,
            string outcomeStatus,
            string? reason,
            long appliedTick,
            CommandRejectionReason? rejectionReasonCode = null)
        {
            Outcomes.Add(new CapturedOutcome(commandId, actorId, commandType, outcomeStatus, reason, appliedTick, rejectionReasonCode));
            return Task.CompletedTask;
        }
    }

    private sealed record CapturedOutcome(
        CommandId CommandId,
        ActorId ActorId,
        string CommandType,
        string Status,
        string? Reason,
        long AppliedTick,
        CommandRejectionReason? RejectionReason);
}
