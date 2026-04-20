using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VictoriaLike.Core.Data.Validation;
using VictoriaLike.Core.Simulation.TickPipeline;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public interface IWorldClockService : IHostedService
{
    TickMetrics CurrentMetrics { get; }
    SimulationMetricsSnapshot LatestSimulationMetrics { get; }
    void Pause();
    void Resume();
    bool IsPaused { get; }
}

public sealed class SimulationMetricsSnapshot
{
    public long Tick { get; init; }
    public decimal AverageNeedsFulfilled { get; init; }
    public int UnmetPopCount { get; init; }
    public Dictionary<string, decimal> ReformPressureByCountry { get; init; } = new();
    public Dictionary<string, decimal> TreasuryDeltaByCountry { get; init; } = new();
}

public class TickMetrics
{
    public long TickCount { get; set; }
    public long TickDurationMs { get; set; }
    public double AverageTickDurationMs { get; set; }
    public DateTime WorldTimestamp { get; set; }
    public double TickRate { get; set; }
    public long LastTickDbWrites { get; set; }
    public long TotalDbWrites { get; set; }
    public Dictionary<string, long> StageDurationsMs { get; set; } = new();
    public List<WorldInvariantViolation> InvariantViolations { get; set; } = new();
}

public class PersistentWorldClockService : IWorldClockService
{
    private const double TickDurationEmaAlpha = 0.1;

    private readonly ILogger<PersistentWorldClockService> _logger;
    private readonly IWorldStateRepository _stateRepository;
    private readonly IWorldStateDatabase _worldStateDb;
    private readonly IWorldWebSocketHub _webSocketHub;
    private readonly IWorldSnapshotService _snapshotService;
    private readonly TickExecutor _executor;
    private readonly CancellationTokenSource _stoppingCts;
    private readonly int _tickIntervalMs;
    private readonly int _saveIntervalTicks;
    private readonly int _snapshotIntervalTicks;

    private Dictionary<string, long> _lastStageDurationsMs = new();
    private List<WorldInvariantViolation> _lastInvariantViolations = new();

    private Task _tickTask = null!;
    private long _tickCount = 0;
    private volatile bool _isPaused = false;
    private readonly Stopwatch _tickStopwatch = new();
    private DateTime _worldTimestamp = new(1800, 1, 1);
    private int _ticksSinceLastSave = 0;
    private int _ticksSinceLastSnapshot = 0;
    private long _lastTickDurationMs = 0;
    private double _averageTickDurationMs = 0;
    private long _lastTickDbWrites = 0;
    private long _totalDbWrites = 0;
    private readonly object _metricsLock = new();
    private SimulationMetricsSnapshot _latestSimulationMetrics = new();

    public TickMetrics CurrentMetrics
    {
        get
        {
            lock (_metricsLock)
            {
                var elapsedSecs = _tickStopwatch.Elapsed.TotalSeconds;
                return new TickMetrics
                {
                    TickCount = _tickCount,
                    TickDurationMs = _lastTickDurationMs,
                    AverageTickDurationMs = _averageTickDurationMs,
                    WorldTimestamp = _worldTimestamp,
                    TickRate = _tickCount > 0 && elapsedSecs > 0 ? _tickCount / elapsedSecs : 0,
                    LastTickDbWrites = _lastTickDbWrites,
                    TotalDbWrites = _totalDbWrites,
                    StageDurationsMs = new Dictionary<string, long>(_lastStageDurationsMs),
                    InvariantViolations = new List<WorldInvariantViolation>(_lastInvariantViolations)
                };
            }
        }
    }

    public bool IsPaused => _isPaused;

    public SimulationMetricsSnapshot LatestSimulationMetrics
    {
        get
        {
            lock (_metricsLock)
                return _latestSimulationMetrics;
        }
    }

    public PersistentWorldClockService(
        ILogger<PersistentWorldClockService> logger,
        IOptions<TickOptions> tickOptions,
        IWorldStateRepository stateRepository,
        IWorldStateDatabase worldStateDb,
        IWorldWebSocketHub webSocketHub,
        IWorldSnapshotService snapshotService,
        TickExecutor executor)
    {
        _logger = logger;
        _stateRepository = stateRepository;
        _worldStateDb = worldStateDb;
        _webSocketHub = webSocketHub;
        _snapshotService = snapshotService;
        _executor = executor;
        var opts = tickOptions.Value;
        _tickIntervalMs = opts.TickIntervalMs;
        _saveIntervalTicks = opts.SaveIntervalTicks;
        _snapshotIntervalTicks = Math.Max(0, opts.SnapshotIntervalTicks);
        _stoppingCts = new CancellationTokenSource();
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("World clock starting with tick interval {TickInterval}ms, save every {SaveTicks} ticks",
            _tickIntervalMs, _saveIntervalTicks);

        try
        {
            var persistedState = await _stateRepository.LoadLatestAsync(cancellationToken);
            if (persistedState != null)
            {
                _tickCount = persistedState.TickNumber;
                _worldTimestamp = persistedState.WorldTimestamp;
                _logger.LogInformation("Loaded persisted world state: tick {Tick}, timestamp {Date}",
                    _tickCount, _worldTimestamp.ToString("yyyy-MM-dd"));
            }
            else
            {
                _logger.LogInformation("Starting fresh world from {Date}", _worldTimestamp.ToString("yyyy-MM-dd"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading persisted world state");
            throw;
        }

        _tickStopwatch.Start();
        _tickTask = TickLoopAsync(_stoppingCts.Token);

        await Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("World clock stopping (tick count: {TickCount})", _tickCount);

        _stoppingCts.Cancel();

        try
        {
            await _tickTask;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Tick loop cancelled");
        }

        try
        {
            await SaveWorldStateAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving final world state");
        }

        _tickStopwatch.Stop();
        _logger.LogInformation("World clock stopped. Total elapsed: {Elapsed}ms, final tick: {Tick}",
            _tickStopwatch.ElapsedMilliseconds, _tickCount);
    }

    public void Pause()
    {
        if (!_isPaused)
        {
            _isPaused = true;
            _logger.LogInformation("World clock paused at tick {TickCount}", _tickCount);
        }
    }

    public void Resume()
    {
        if (_isPaused)
        {
            _isPaused = false;
            _logger.LogInformation("World clock resumed at tick {TickCount}", _tickCount);
        }
    }

    private async Task TickLoopAsync(CancellationToken cancellationToken)
    {
        var nextTickTime = DateTime.UtcNow.AddMilliseconds(_tickIntervalMs);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                if (now < nextTickTime)
                {
                    var delay = (int)(nextTickTime - now).TotalMilliseconds;
                    await Task.Delay(Math.Max(1, delay), cancellationToken);
                    continue;
                }

                if (!_isPaused)
                    await ExecuteTickAsync(cancellationToken);

                var next = nextTickTime.AddMilliseconds(_tickIntervalMs);
                nextTickTime = next < DateTime.UtcNow ? DateTime.UtcNow.AddMilliseconds(_tickIntervalMs) : next;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during tick execution");
            }
        }
    }

    private async Task ExecuteTickAsync(CancellationToken cancellationToken)
    {
        var tickStartedAt = Stopwatch.GetTimestamp();

        lock (_metricsLock)
        {
            _tickCount++;
            _worldTimestamp = _worldTimestamp.AddDays(1);
            _ticksSinceLastSave++;
            _ticksSinceLastSnapshot++;
        }

        var result = await _executor.RunAsync(_tickCount, _worldTimestamp, cancellationToken);

        var tickDurationMs = (long)Stopwatch.GetElapsedTime(tickStartedAt).TotalMilliseconds;
        lock (_metricsLock)
        {
            _lastTickDurationMs = tickDurationMs;
            _averageTickDurationMs = _averageTickDurationMs == 0
                ? tickDurationMs
                : _averageTickDurationMs * (1 - TickDurationEmaAlpha) + tickDurationMs * TickDurationEmaAlpha;
        }

        var metrics = CurrentMetrics;

        _logger.LogDebug(
            "Tick {TickCount} - World: {Date}, Duration: {DurationMs}ms, Rate: {TickRate} ticks/sec",
            metrics.TickCount,
            metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            metrics.TickDurationMs,
            string.Format("{0:F2}", metrics.TickRate));

        if (_tickCount % 10 == 0)
        {
            var profileSummary = string.Join(", ", metrics.StageDurationsMs.Select(kv => $"{kv.Key}={kv.Value}ms"));
            _logger.LogInformation(
                "Tick {TickCount} checkpoint - Total: {Elapsed}ms, World: {Date} | {Profile}",
                metrics.TickCount,
                metrics.TickDurationMs,
                metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
                profileSummary);
        }

        var dbWrites = result?.DbWrites ?? 0;

        if (_ticksSinceLastSave >= _saveIntervalTicks)
        {
            await SaveWorldStateAsync(cancellationToken);
            _ticksSinceLastSave = 0;
            dbWrites++;
        }

        if (_snapshotIntervalTicks > 0 && _ticksSinceLastSnapshot >= _snapshotIntervalTicks)
        {
            await SaveSnapshotAsync(cancellationToken);
            _ticksSinceLastSnapshot = 0;
        }

        lock (_metricsLock)
        {
            _lastTickDbWrites = dbWrites;
            _totalDbWrites += dbWrites;
        }

        if (result == null || result.HasInvariantViolations)
        {
            if (result != null)
            {
                lock (_metricsLock)
                {
                    _lastStageDurationsMs = result.StageDurationsMs;
                    _lastInvariantViolations = result.InvariantViolations;
                }
            }
            return;
        }

        lock (_metricsLock)
        {
            _lastStageDurationsMs = result.StageDurationsMs;
            _lastInvariantViolations = result.InvariantViolations;
            _latestSimulationMetrics = result.SimulationMetrics;
        }

        var broadcastSw = Stopwatch.StartNew();
        await _webSocketHub.BroadcastWorldUpdateAsync(_tickCount, _worldTimestamp, result.MarketPrices);
        await _webSocketHub.BroadcastMarketUpdateAsync(_tickCount, result.MarketPrices, result.MarketSupply, result.MarketDemand);
        foreach (var (actorId, countryId, taxRate, treasury) in result.ActorCountries)
            await _webSocketHub.SendCountryUpdateAsync(actorId, _tickCount, countryId, taxRate, treasury);
        broadcastSw.Stop();

        lock (_metricsLock)
        {
            _lastStageDurationsMs["broadcast_fanout"] = broadcastSw.ElapsedMilliseconds;
        }
    }

    private async Task SaveWorldStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            WorldState state;
            lock (_metricsLock)
            {
                state = new WorldState
                {
                    TickNumber = _tickCount,
                    WorldTimestamp = _worldTimestamp,
                    LastSavedAt = DateTime.UtcNow
                };
            }

            await _stateRepository.SaveAsync(state, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("World state save cancelled during shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving world state");
        }
    }

    private async Task SaveSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _worldStateDb.LoadWorldAsync(cancellationToken);
            if (snapshot == null)
            {
                _logger.LogWarning("Skipping snapshot save because world state is not initialized");
                return;
            }

            TickMetrics metrics;
            lock (_metricsLock)
            {
                metrics = new TickMetrics
                {
                    TickCount = _tickCount,
                    TickDurationMs = _lastTickDurationMs,
                    AverageTickDurationMs = _averageTickDurationMs,
                    WorldTimestamp = _worldTimestamp,
                    TickRate = _tickStopwatch.Elapsed.TotalSeconds > 0
                        ? _tickCount / _tickStopwatch.Elapsed.TotalSeconds
                        : 0
                };
            }

            await _snapshotService.SaveAsync(
                new WorldState
                {
                    TickNumber = metrics.TickCount,
                    WorldTimestamp = metrics.WorldTimestamp,
                    LastSavedAt = DateTime.UtcNow
                },
                snapshot,
                savepointName: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving world snapshot");
        }
    }
}
