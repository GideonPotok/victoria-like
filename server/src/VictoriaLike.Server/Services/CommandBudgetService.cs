using System;
using System.Collections.Generic;
using System.Linq;

namespace VictoriaLike.Server.Services;

public interface ICommandBudgetService
{
    CommandBudgetDecision Evaluate(Guid actorId, Guid countryId, string commandType, long currentTick, DateTime receivedAtUtc);
    void RecordAccepted(Guid actorId, Guid countryId, string commandType, long currentTick, DateTime receivedAtUtc);
    IReadOnlyList<CommandBudgetSnapshot> GetSnapshots(long currentTick, DateTime nowUtc);
}

public sealed record CommandBudgetDecision(
    bool Accepted,
    bool SoftLimited,
    string Status,
    string? Reason,
    int RemainingInWindow,
    int SoftLimit,
    int HardLimit,
    TimeSpan Window,
    long? RetryAfterTicks,
    TimeSpan? RetryAfter);

public sealed record CommandBudgetSnapshot(
    string ActorId,
    string? CountryId,
    int UsedInWindow,
    int RemainingInWindow,
    int SoftLimit,
    int HardLimit,
    double WindowSeconds,
    IReadOnlyDictionary<string, long> CooldownsRemainingTicks);

public sealed class CommandBudgetService : ICommandBudgetService
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(10);
    private static readonly IReadOnlyDictionary<string, long> StrategicCooldownTicks =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["ChangeTaxRate"] = 3,
            ["QueueBuilding"] = 1,
            ["ChangeStrataTax"] = 3,
            ["ChangeSpending"] = 3,
            ["MoveArmy"] = 1,
            ["DeclareWar"] = 10,
            ["MakePeace"] = 5
        };

    private const int SoftLimit = 10;
    private const int HardLimit = 20;

    private readonly object _lockObject = new();
    private readonly Dictionary<Guid, Queue<DateTime>> _actorWindows = new();
    private readonly Dictionary<(Guid CountryId, string CommandType), long> _countryCooldowns = new();
    private readonly Dictionary<Guid, Guid> _lastCountryByActor = new();

    public CommandBudgetDecision Evaluate(Guid actorId, Guid countryId, string commandType, long currentTick, DateTime receivedAtUtc)
    {
        lock (_lockObject)
        {
            var window = GetPrunedWindow(actorId, receivedAtUtc);
            var used = window.Count;
            var remaining = Math.Max(0, HardLimit - used);

            if (used >= HardLimit)
            {
                var retryAfter = Window - (receivedAtUtc - window.Peek());
                if (retryAfter < TimeSpan.Zero) retryAfter = TimeSpan.Zero;

                return new CommandBudgetDecision(
                    false,
                    false,
                    "rejected",
                    $"Hard rate limit exceeded: {used}/{HardLimit} commands in {Window.TotalSeconds:0}s",
                    remaining,
                    SoftLimit,
                    HardLimit,
                    Window,
                    null,
                    retryAfter);
            }

            if (StrategicCooldownTicks.TryGetValue(commandType, out var cooldownTicks) &&
                _countryCooldowns.TryGetValue((countryId, commandType), out var availableTick) &&
                currentTick < availableTick)
            {
                var retryTicks = availableTick - currentTick;
                return new CommandBudgetDecision(
                    false,
                    false,
                    "rejected",
                    $"Country command cooldown active for {commandType}; retry after {retryTicks} tick(s)",
                    remaining,
                    SoftLimit,
                    HardLimit,
                    Window,
                    retryTicks,
                    null);
            }

            var softLimited = used >= SoftLimit;
            return new CommandBudgetDecision(
                true,
                softLimited,
                softLimited ? "queued_soft_limited" : "queued",
                softLimited ? $"Soft rate limit reached: {used}/{SoftLimit} commands in {Window.TotalSeconds:0}s" : null,
                remaining,
                SoftLimit,
                HardLimit,
                Window,
                null,
                null);
        }
    }

    public void RecordAccepted(Guid actorId, Guid countryId, string commandType, long currentTick, DateTime receivedAtUtc)
    {
        lock (_lockObject)
        {
            var window = GetPrunedWindow(actorId, receivedAtUtc);
            window.Enqueue(receivedAtUtc);
            _lastCountryByActor[actorId] = countryId;

            if (StrategicCooldownTicks.TryGetValue(commandType, out var cooldownTicks))
                _countryCooldowns[(countryId, commandType)] = currentTick + cooldownTicks;
        }
    }

    public IReadOnlyList<CommandBudgetSnapshot> GetSnapshots(long currentTick, DateTime nowUtc)
    {
        lock (_lockObject)
        {
            foreach (var actorId in _actorWindows.Keys.ToList())
                GetPrunedWindow(actorId, nowUtc);

            return _actorWindows
                .Where(kv => kv.Value.Count > 0 || _lastCountryByActor.ContainsKey(kv.Key))
                .OrderByDescending(kv => kv.Value.Count)
                .Take(20)
                .Select(kv =>
                {
                    _lastCountryByActor.TryGetValue(kv.Key, out var countryId);
                    var cooldowns = _countryCooldowns
                        .Where(cd => cd.Key.CountryId == countryId && cd.Value > currentTick)
                        .ToDictionary(cd => cd.Key.CommandType, cd => cd.Value - currentTick, StringComparer.Ordinal);

                    return new CommandBudgetSnapshot(
                        kv.Key.ToString(),
                        countryId == Guid.Empty ? null : countryId.ToString(),
                        kv.Value.Count,
                        Math.Max(0, HardLimit - kv.Value.Count),
                        SoftLimit,
                        HardLimit,
                        Window.TotalSeconds,
                        cooldowns);
                })
                .ToList();
        }
    }

    private Queue<DateTime> GetPrunedWindow(Guid actorId, DateTime nowUtc)
    {
        if (!_actorWindows.TryGetValue(actorId, out var window))
        {
            window = new Queue<DateTime>();
            _actorWindows[actorId] = window;
        }

        while (window.Count > 0 && nowUtc - window.Peek() >= Window)
            window.Dequeue();

        return window;
    }
}
