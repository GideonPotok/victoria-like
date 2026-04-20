using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Buildings;
using VictoriaLike.Core.Core.Countries;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Core.World;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using Xunit;

namespace VictoriaLike.Core.Tests;

public sealed class CommandProcessingStageTests
{
    [Fact]
    public async Task ProcessCommandsAsync_AppliesCommandsInDeterministicOrder()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var context = CreateContext(countryId, actorId);
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { new ChangeTaxRateCommandHandler() }, recorder);
        var earlier = CreateTaxCommand(actorId, countryId, 20, submittedTick: 4, receivedAt: new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc));
        var later = CreateTaxCommand(actorId, countryId, 30, submittedTick: 4, receivedAt: new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));

        await stage.ProcessCommandsAsync(new List<CommandEnvelope> { later, earlier }, context, currentTick: 5);

        Assert.Equal(30m, context.World.Countries[countryId.ToString()].TaxRate);
        Assert.Equal(new[] { earlier.Id, later.Id }, recorder.Outcomes.Select(o => o.CommandId).ToArray());
        Assert.All(recorder.Outcomes, outcome => Assert.Equal("applied", outcome.Status));
    }

    [Fact]
    public async Task ProcessCommandsAsync_RejectsDuplicateCommandIdInSameBatch()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var context = CreateContext(countryId, actorId);
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { new ChangeTaxRateCommandHandler() }, recorder);
        var commandId = CommandId.New();
        var first = CreateTaxCommand(actorId, countryId, 20, submittedTick: 4, receivedAt: new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc));
        var duplicate = CreateTaxCommand(actorId, countryId, 30, submittedTick: 4, receivedAt: new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));
        first.Id = commandId;
        duplicate.Id = commandId;

        await stage.ProcessCommandsAsync(new List<CommandEnvelope> { first, duplicate }, context, currentTick: 5);

        Assert.Equal(20m, context.World.Countries[countryId.ToString()].TaxRate);
        Assert.Equal("applied", recorder.Outcomes[0].Status);
        Assert.Equal("rejected", recorder.Outcomes[1].Status);
        Assert.Equal(CommandRejectionReason.DuplicateCommand, recorder.Outcomes[1].RejectionReason);
    }

    [Fact]
    public async Task ProcessCommandsAsync_RejectsStaleClientState()
    {
        var countryId = Guid.NewGuid();
        var actorId = ActorId.New();
        var context = CreateContext(countryId, actorId);
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { new ChangeTaxRateCommandHandler() }, recorder);
        var command = CreateTaxCommand(actorId, countryId, 20, submittedTick: 4, receivedAt: DateTime.UtcNow);
        command.ExpectedWorldTick = 8;

        await stage.ProcessCommandsAsync(new List<CommandEnvelope> { command }, context, currentTick: 10);

        Assert.Equal(10m, context.World.Countries[countryId.ToString()].TaxRate);
        Assert.Single(recorder.Outcomes);
        Assert.Equal("rejected", recorder.Outcomes[0].Status);
        Assert.Equal(CommandRejectionReason.StaleClientState, recorder.Outcomes[0].RejectionReason);
    }

    [Fact]
    public async Task ProcessCommandsAsync_RejectsSecondConstructionInSameProvince()
    {
        var countryId = Guid.NewGuid();
        var provinceId = Guid.NewGuid();
        var actorId = ActorId.New();
        var context = CreateContext(countryId, actorId, provinceId);
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { new QueueBuildingCommandHandler() }, recorder);
        var first = CreateBuildCommand(actorId, provinceId, submittedTick: 4, receivedAt: new DateTime(2026, 1, 1, 0, 0, 1, DateTimeKind.Utc));
        var second = CreateBuildCommand(actorId, provinceId, submittedTick: 4, receivedAt: new DateTime(2026, 1, 1, 0, 0, 2, DateTimeKind.Utc));

        await stage.ProcessCommandsAsync(new List<CommandEnvelope> { second, first }, context, currentTick: 5);

        Assert.Single(context.World.BuildingQueue);
        Assert.Equal(first.Id, recorder.Outcomes[0].CommandId);
        Assert.Equal("applied", recorder.Outcomes[0].Status);
        Assert.Equal(second.Id, recorder.Outcomes[1].CommandId);
        Assert.Equal("rejected", recorder.Outcomes[1].Status);
        Assert.Equal(CommandRejectionReason.ActiveConstructionConflict, recorder.Outcomes[1].RejectionReason);
    }

    [Fact]
    public async Task ProcessCommandsAsync_RecordsFailedOutcome_WhenHandlerThrows()
    {
        var actorId = ActorId.New();
        var command = new CommandEnvelope(actorId, "Explode");
        var recorder = new CapturingOutcomeRecorder();
        var stage = new CommandProcessingStage(new[] { new ThrowingCommandHandler() }, recorder);
        var context = CreateEmptyContext();

        await stage.ProcessCommandsAsync(new List<CommandEnvelope> { command }, context, currentTick: 7);

        Assert.Single(recorder.Outcomes);
        var outcome = recorder.Outcomes[0];
        Assert.Equal("failed", outcome.Status);
        Assert.Equal(7, outcome.AppliedTick);
        Assert.Contains("Command pipeline error", outcome.Reason);
    }

    private static CommandEnvelope CreateTaxCommand(
        ActorId actorId,
        Guid countryId,
        int taxRate,
        long submittedTick,
        DateTime receivedAt)
    {
        return new CommandEnvelope(
            actorId,
            "ChangeTaxRate",
            new Dictionary<string, object>
            {
                ["countryId"] = JsonSerializer.Deserialize<JsonElement>($"\"{countryId}\""),
                ["newTaxRate"] = JsonSerializer.Deserialize<JsonElement>(taxRate.ToString())
            })
        {
            SubmittedTick = submittedTick,
            ReceivedAt = receivedAt
        };
    }

    private static CommandEnvelope CreateBuildCommand(
        ActorId actorId,
        Guid provinceId,
        long submittedTick,
        DateTime receivedAt)
    {
        return new CommandEnvelope(
            actorId,
            "QueueBuilding",
            new Dictionary<string, object>
            {
                ["provinceId"] = JsonSerializer.Deserialize<JsonElement>($"\"{provinceId}\""),
                ["buildingType"] = JsonSerializer.Deserialize<JsonElement>("\"railroad\"")
            })
        {
            SubmittedTick = submittedTick,
            ReceivedAt = receivedAt
        };
    }

    private static SimulationContext CreateContext(Guid countryId, ActorId actorId, Guid? provinceId = null)
    {
        var context = CreateEmptyContext();
        var countryKey = countryId.ToString();
        context.World.Countries[countryKey] = new CountryState
        {
            Id = countryKey,
            DisplayName = "Albion",
            ProvinceIds = provinceId.HasValue ? new List<string> { provinceId.Value.ToString() } : new List<string>(),
            Treasury = 1_000m,
            TaxRate = 10m,
            TariffRate = 0m,
            IsPlayable = true
        };
        context.World.PlayerAccounts[actorId.ToString()] = new PlayerAccount(actorId, "albion-player", new CountryId(countryId));

        if (provinceId.HasValue)
        {
            context.World.Provinces[provinceId.Value.ToString()] = new ProvinceState
            {
                Id = provinceId.Value.ToString(),
                DisplayName = "Albionshire",
                OwnerId = countryKey,
                Infrastructure = 1m
            };
        }

        return context;
    }

    private static SimulationContext CreateEmptyContext()
    {
        return new SimulationContext
        {
            World = new WorldState
            {
                Seed = 0,
                Date = new GameDate(DateOnly.FromDateTime(DateTime.UtcNow)),
                Countries = new Dictionary<string, CountryState>(),
                PlayerAccounts = new Dictionary<string, PlayerAccount>(),
                Provinces = new Dictionary<string, ProvinceState>(),
                Pops = new Dictionary<string, VictoriaLike.Core.Core.Pops.PopState>(),
                Goods = new Dictionary<string, VictoriaLike.Core.Core.Economy.GoodDefinition>(),
                Market = new VictoriaLike.Core.Core.Economy.MarketState(),
                Metrics = new SimulationMetrics(),
                BuildingQueue = new List<BuildingQueueEntry>(),
                EventLog = new List<string>()
            },
            Random = new SeededRandom(0),
            Log = new SimulationLog(),
            Profile = new TickProfile()
        };
    }

    private sealed class ThrowingCommandHandler : ICommandHandler
    {
        public string CommandType => "Explode";

        public CommandResult Handle(CommandEnvelope envelope, WorldState world, ActorId actor)
        {
            throw new System.InvalidOperationException("boom");
        }
    }

    private sealed class CapturingOutcomeRecorder : ICommandOutcomeRecorder
    {
        public List<(CommandId CommandId, ActorId ActorId, string CommandType, string Status, string? Reason, long AppliedTick, CommandRejectionReason? RejectionReason)> Outcomes { get; } = new();

        public Task RecordOutcomeAsync(
            CommandId commandId,
            ActorId actorId,
            string commandType,
            string outcomeStatus,
            string? reason,
            long appliedTick,
            CommandRejectionReason? rejectionReasonCode = null)
        {
            Outcomes.Add((commandId, actorId, commandType, outcomeStatus, reason, appliedTick, rejectionReasonCode));
            return Task.CompletedTask;
        }
    }
}
