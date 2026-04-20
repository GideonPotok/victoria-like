using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Application.Profiling;
using VictoriaLike.Core.Core.Common;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Core.Simulation.TickPipeline;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public sealed record TickRunResult(
    Dictionary<string, decimal> MarketPrices,
    Dictionary<string, decimal> MarketSupply,
    Dictionary<string, decimal> MarketDemand,
    List<(string actorId, string countryId, int taxRate, decimal treasury)> ActorCountries,
    Dictionary<string, long> StageDurationsMs,
    SimulationMetricsSnapshot SimulationMetrics,
    List<WorldInvariantViolation> InvariantViolations,
    int DbWrites)
{
    public bool HasInvariantViolations => InvariantViolations.Count > 0;

    internal static TickRunResult FromViolations(WorldInvariantReport report, Dictionary<string, long> stageDurations) =>
        new([], [], [], [], stageDurations, new SimulationMetricsSnapshot(), report.Violations.ToList(), 0);
}

public sealed class TickExecutor
{
    private readonly IWorldStateDatabase _worldStateDb;
    private readonly ICommandQueueService _commandQueue;
    private readonly CommandProcessingStage _commandProcessingStage;
    private readonly IGoodsService _goodsService;
    private readonly ICommandOutcomeRecorder _outcomeRecorder;
    private readonly SimulationOrchestrator _orchestrator;
    private readonly IMarketHistoryService _marketHistory;
    private readonly WorldInvariantChecker _invariantChecker = new();
    private readonly ILogger<TickExecutor> _logger;

    public TickExecutor(
        IWorldStateDatabase worldStateDb,
        ICommandQueueService commandQueue,
        CommandProcessingStage commandProcessingStage,
        IGoodsService goodsService,
        ICommandOutcomeRecorder outcomeRecorder,
        SimulationOrchestrator orchestrator,
        IMarketHistoryService marketHistory,
        ILogger<TickExecutor> logger)
    {
        _worldStateDb = worldStateDb;
        _commandQueue = commandQueue;
        _commandProcessingStage = commandProcessingStage;
        _goodsService = goodsService;
        _outcomeRecorder = outcomeRecorder;
        _orchestrator = orchestrator;
        _marketHistory = marketHistory;
        _logger = logger;
    }

    /// <summary>
    /// Runs one full simulation tick: load → validate → commands → stages → persist → market history.
    /// Returns null if the world is not yet initialized.
    /// Returns a result with HasInvariantViolations=true if an invariant check aborted the tick.
    /// </summary>
    public async Task<TickRunResult?> RunAsync(long tickCount, DateTime worldTimestamp, CancellationToken cancellationToken)
    {
        try
        {
            var stageDurations = new Dictionary<string, long>();

            var loadSw = Stopwatch.StartNew();
            var snapshot = await _worldStateDb.LoadWorldAsync(cancellationToken);
            if (snapshot == null)
            {
                var orphaned = await _commandQueue.DequeueAllAsync();
                foreach (var cmd in orphaned)
                    await _outcomeRecorder.RecordOutcomeAsync(cmd.Id, cmd.ActorId, cmd.CommandType, "failed", "World not initialized", tickCount);
                return null;
            }
            loadSw.Stop();
            stageDurations["load_world"] = loadSw.ElapsedMilliseconds;

            var world = CommandWorldStateMapper.ToSimulationWorld(snapshot, worldTimestamp, _goodsService.All);
            var loadCheck = _invariantChecker.Check(world);
            if (!loadCheck.IsValid)
            {
                LogViolations(loadCheck, "after_load", tickCount);
                return TickRunResult.FromViolations(loadCheck, stageDurations);
            }

            var context = new SimulationContext
            {
                World = world,
                Random = new SeededRandom(world.Seed + world.Date.Value.DayNumber),
                Log = new SimulationLog(),
                Profile = new TickProfile()
            };

            var cmdSw = Stopwatch.StartNew();
            var commands = await _commandQueue.DequeueAllAsync();
            if (commands.Count > 0)
            {
                _logger.LogDebug("Processing {CommandCount} commands at tick {Tick}", commands.Count, tickCount);
                await _commandProcessingStage.ProcessCommandsAsync(commands, context, tickCount);
            }
            cmdSw.Stop();
            stageDurations["commands"] = cmdSw.ElapsedMilliseconds;

            _orchestrator.RunTick(context);

            foreach (var (name, elapsed) in context.Profile.StageDurations)
                stageDurations[name] = (long)elapsed.TotalMilliseconds;

            var tickCheck = _invariantChecker.Check(world);
            if (!tickCheck.IsValid)
            {
                LogViolations(tickCheck, "after_simulation", tickCount);
                return TickRunResult.FromViolations(tickCheck, stageDurations);
            }

            var simulationMetrics = new SimulationMetricsSnapshot
            {
                Tick = tickCount,
                AverageNeedsFulfilled = world.Metrics.AverageNeedsFulfilled,
                UnmetPopCount = world.Metrics.UnmetPopCount,
                ReformPressureByCountry = new Dictionary<string, decimal>(world.Metrics.ReformPressureByCountry),
                TreasuryDeltaByCountry = new Dictionary<string, decimal>(world.Metrics.TreasuryDeltaByCountry)
            };

            foreach (var entry in context.Log.Entries)
                _logger.LogDebug("{SimulationLogEntry}", entry);

            var persistSw = Stopwatch.StartNew();
            var updatedCountries = CommandWorldStateMapper.ToPersistedCountries(snapshot, world);
            var batch = new TickWriteBatch(
                Countries: updatedCountries,
                MarketId: snapshot.Markets.Count > 0 ? snapshot.Markets[0].Id.Value : null,
                MarketPrices: snapshot.Markets.Count > 0 ? new Dictionary<string, decimal>(world.Market.Prices) : null,
                MarketSupply: snapshot.Markets.Count > 0 ? new Dictionary<string, decimal>(world.Market.SupplyLastTick) : null,
                MarketDemand: snapshot.Markets.Count > 0 ? new Dictionary<string, decimal>(world.Market.DemandLastTick) : null,
                ProvinceNeedsFulfillment: CommandWorldStateMapper.ToProvinceNeedsFulfillment(world),
                PopGroups: CommandWorldStateMapper.ToPopGroupUpdates(world),
                Factories: CommandWorldStateMapper.ToPersistedFactories(world),
                GoodProfitHistory: CommandWorldStateMapper.ToPersistedGoodProfitHistory(world),
                Armies: CommandWorldStateMapper.ToPersistedArmies(world),
                Wars: CommandWorldStateMapper.ToPersistedWars(world),
                BattleReports: CommandWorldStateMapper.ToPersistedBattleReports(world),
                BuildingQueue: CommandWorldStateMapper.ToPersistedBuildingQueue(world),
                ProvinceOutputs: world.Metrics.CompletedBuildingProvinceIds.Count > 0
                    ? CommandWorldStateMapper.ToProvinceOutputs(world, world.Metrics.CompletedBuildingProvinceIds)
                    : null);

            await _worldStateDb.SaveTickResultsAsync(batch, cancellationToken);
            persistSw.Stop();
            stageDurations["persist"] = persistSw.ElapsedMilliseconds;

            if (world.Metrics.CompletedBuildingProvinceIds.Count > 0)
                _logger.LogInformation("Buildings completed in {Count} province(s) this tick", world.Metrics.CompletedBuildingProvinceIds.Count);

            var prices = new Dictionary<string, decimal>(world.Market.Prices);
            var supply = new Dictionary<string, decimal>(world.Market.SupplyLastTick);
            var demand = new Dictionary<string, decimal>(world.Market.DemandLastTick);

            var countryById = updatedCountries.ToDictionary(c => c.Id.Value);
            var actorCountries = snapshot.Players
                .Select(p => countryById.TryGetValue(p.ControlledCountry.Value, out var c)
                    ? (actorId: p.Id.Value.ToString(), countryId: c.Id.Value.ToString(), taxRate: c.TaxRate, treasury: c.Treasury)
                    : default)
                .Where(x => x.actorId != null)
                .ToList();

            _marketHistory.RecordTick(tickCount, prices, supply, demand);

            _logger.LogDebug(
                "Tick {Tick} economy: needs={Needs:F2} unmet={Unmet} trade={Trade:F2}",
                tickCount, world.Metrics.AverageNeedsFulfilled, world.Metrics.UnmetPopCount,
                world.Market.TradeValueLastTick);

            return new TickRunResult(
                MarketPrices: prices,
                MarketSupply: supply,
                MarketDemand: demand,
                ActorCountries: actorCountries,
                StageDurationsMs: stageDurations,
                SimulationMetrics: simulationMetrics,
                InvariantViolations: [],
                DbWrites: 1);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("Economic tick {Tick} cancelled during shutdown", tickCount);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running economic tick {Tick}", tickCount);
            return null;
        }
    }

    private void LogViolations(WorldInvariantReport report, string phase, long tickCount) =>
        _logger.LogError(
            "World invariant check failed phase={Phase} tick={Tick} violations={Violations}",
            phase, tickCount,
            string.Join(" | ", report.Violations.Select(v => $"{v.Code}: {v.Message}")));
}
