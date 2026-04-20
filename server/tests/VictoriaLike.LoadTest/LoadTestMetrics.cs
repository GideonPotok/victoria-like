using System.Net;

namespace VictoriaLike.LoadTest;

public sealed class ClientMetrics
{
    public string ClientId { get; init; } = string.Empty;
    public bool IsAuthenticated { get; init; }

    private readonly List<double> _tickIntervals = new();
    private readonly Dictionary<string, long> _bytesByMessageType = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _messagesByMessageType = new(StringComparer.OrdinalIgnoreCase);
    private DateTime _lastTickTime = DateTime.MinValue;
    private DateTime _connectTime;

    public int MessagesReceived { get; private set; }
    public int WorldUpdates { get; private set; }
    public int MarketUpdates { get; private set; }
    public int CountryUpdates { get; private set; }
    public int SubscriptionTopicsRequested { get; private set; }
    public int SubscriptionAcks { get; private set; }
    public int LoginSuccesses { get; private set; }
    public int ReconnectAttempts { get; private set; }
    public int ReconnectSuccesses { get; private set; }
    public int CommandsSent { get; private set; }
    public int CommandResponsesAccepted { get; private set; }
    public int CommandResponsesRejected { get; private set; }
    public int CommandResponsesErrored { get; private set; }
    public int CommandResultsApplied { get; private set; }
    public int CommandResultsRejected { get; private set; }
    public int DuplicateRetries { get; private set; }
    public int StaleTokenAttempts { get; private set; }
    public int StaleTokenRejected { get; private set; }
    public int Errors { get; private set; }
    public long LastTickSeen { get; set; }
    public long BytesReceived { get; private set; }
    public IReadOnlyDictionary<string, long> BytesByMessageType => _bytesByMessageType;
    public IReadOnlyDictionary<string, int> MessagesByMessageType => _messagesByMessageType;
    public TimeSpan? TimeToFirstMessage { get; private set; }

    public void RecordConnectAttempt() => _connectTime = DateTime.UtcNow;
    public void RecordLoginSuccess() => LoginSuccesses++;
    public void RecordReconnectAttempt() => ReconnectAttempts++;
    public void RecordReconnectSuccess() => ReconnectSuccesses++;
    public void RecordError() => Errors++;
    public void RecordCommandSent() => CommandsSent++;
    public void RecordDuplicateRetry() => DuplicateRetries++;
    public void RecordMarketUpdate() => MarketUpdates++;
    public void RecordCountryUpdate() => CountryUpdates++;
    public void RecordSubscription(int topicCount) => SubscriptionTopicsRequested += topicCount;
    public void RecordSubscriptionAck() => SubscriptionAcks++;

    public void RecordStaleTokenAttempt(bool rejected)
    {
        StaleTokenAttempts++;
        if (rejected)
            StaleTokenRejected++;
        else
            Errors++;
    }

    public void RecordCommandResponse(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (code >= 200 && code < 300)
            CommandResponsesAccepted++;
        else if (code is 401 or 403 or 409 or 422 or 429)
            CommandResponsesRejected++;
        else
            CommandResponsesErrored++;
    }

    public void RecordCommandResult(string? status)
    {
        if (status == "applied")
            CommandResultsApplied++;
        else if (status == "rejected" || status == "failed")
            CommandResultsRejected++;
    }

    public void RecordMessage(string? messageType, int byteCount)
    {
        MessagesReceived++;
        BytesReceived += byteCount;

        var key = string.IsNullOrWhiteSpace(messageType) ? "unknown" : messageType;
        _bytesByMessageType[key] = _bytesByMessageType.GetValueOrDefault(key) + byteCount;
        _messagesByMessageType[key] = _messagesByMessageType.GetValueOrDefault(key) + 1;
    }

    public void RecordWorldUpdate(long tick)
    {
        if (tick > 0)
        {
            LastTickSeen = Math.Max(LastTickSeen, tick);
            WorldUpdates++;
        }

        if (TimeToFirstMessage == null && _connectTime != DateTime.MinValue)
            TimeToFirstMessage = DateTime.UtcNow - _connectTime;

        var now = DateTime.UtcNow;
        if (_lastTickTime != DateTime.MinValue)
            _tickIntervals.Add((now - _lastTickTime).TotalMilliseconds);
        _lastTickTime = now;
    }

    public (double mean, double stddev, double min, double max) TickIntervalStats()
    {
        if (_tickIntervals.Count == 0) return (0, 0, 0, 0);
        var mean = _tickIntervals.Average();
        var stddev = Math.Sqrt(_tickIntervals.Average(x => Math.Pow(x - mean, 2)));
        return (mean, stddev, _tickIntervals.Min(), _tickIntervals.Max());
    }
}

public sealed class LoadTestReport
{
    private readonly List<ClientMetrics> _clients;
    private readonly TimeSpan _duration;
    private readonly int _targetClients;

    public LoadTestReport(List<ClientMetrics> clients, TimeSpan duration, int targetClients)
    {
        _clients = clients;
        _duration = duration;
        _targetClients = targetClients;
    }

    public void Print()
    {
        Console.WriteLine();
        Console.WriteLine("==================================================");
        Console.WriteLine("  FAKE CLIENT HARNESS V2 REPORT");
        Console.WriteLine("==================================================");
        Console.WriteLine($"  Duration:        {_duration.TotalSeconds:F0}s");
        Console.WriteLine($"  Target clients:  {_targetClients}");
        Console.WriteLine($"  Completed:       {_clients.Count}");
        Console.WriteLine();

        var totalMessages = _clients.Sum(c => c.MessagesReceived);
        var totalBytes = _clients.Sum(c => c.BytesReceived);
        var totalErrors = _clients.Sum(c => c.Errors);
        var totalReconnects = _clients.Sum(c => c.ReconnectAttempts);
        var totalReconnectSuccess = _clients.Sum(c => c.ReconnectSuccesses);
        var totalCommands = _clients.Sum(c => c.CommandsSent);
        var totalCommandAccepted = _clients.Sum(c => c.CommandResponsesAccepted);
        var totalCommandRejected = _clients.Sum(c => c.CommandResponsesRejected);
        var totalCommandErrored = _clients.Sum(c => c.CommandResponsesErrored);
        var staleAttempts = _clients.Sum(c => c.StaleTokenAttempts);
        var staleRejected = _clients.Sum(c => c.StaleTokenRejected);
        var duplicateRetries = _clients.Sum(c => c.DuplicateRetries);

        Console.WriteLine("  -- Messages ------------------------------------");
        Console.WriteLine($"  Total received:  {totalMessages}");
        Console.WriteLine($"  World updates:   {_clients.Sum(c => c.WorldUpdates)}");
        Console.WriteLine($"  Market updates:  {_clients.Sum(c => c.MarketUpdates)}");
        Console.WriteLine($"  Country updates: {_clients.Sum(c => c.CountryUpdates)}");
        Console.WriteLine($"  Per client avg:  {(double)totalMessages / Math.Max(1, _clients.Count):F1}");
        Console.WriteLine($"  Errors:          {totalErrors}");
        Console.WriteLine();

        Console.WriteLine("  -- Bandwidth -----------------------------------");
        Console.WriteLine($"  Total received:  {FormatBytes(totalBytes)}");
        Console.WriteLine($"  Per client/min:  {FormatBytes(BytesPerClientMinute(totalBytes))}");
        Console.WriteLine($"  Msgs/client/min: {MessagesPerClientMinute(totalMessages):F1}");
        foreach (var row in MessageTypeRows())
        {
            Console.WriteLine($"  {row.Type,-18} {row.Messages,6} msg  {FormatBytes(row.Bytes),9}  avg {FormatBytes(row.AverageBytes),8}");
        }
        Console.WriteLine();

        var allIntervals = new List<double>();
        foreach (var client in _clients)
        {
            var (mean, _, _, _) = client.TickIntervalStats();
            if (mean > 0)
                allIntervals.Add(mean);
        }
        if (allIntervals.Count > 0)
        {
            var overallMean = allIntervals.Average();
            Console.WriteLine("  -- Observed Tick Interval ----------------------");
            Console.WriteLine($"  Mean:            {overallMean:F0}ms  (target: 1000ms)");
            Console.WriteLine($"  Drift:           {Math.Abs(overallMean - 1000):F0}ms");
            Console.WriteLine();
        }

        Console.WriteLine("  -- Auth and Subscriptions ----------------------");
        Console.WriteLine($"  Logins:          {_clients.Sum(c => c.LoginSuccesses)}");
        Console.WriteLine($"  Topics requested:{_clients.Sum(c => c.SubscriptionTopicsRequested)}");
        Console.WriteLine($"  Subscription ack:{_clients.Sum(c => c.SubscriptionAcks)}");
        Console.WriteLine();

        Console.WriteLine("  -- Reconnects ----------------------------------");
        Console.WriteLine($"  Attempts:        {totalReconnects}");
        Console.WriteLine($"  Successes:       {totalReconnectSuccess}");
        Console.WriteLine($"  Success rate:    {(totalReconnects > 0 ? (double)totalReconnectSuccess / totalReconnects * 100 : 100):F0}%");
        Console.WriteLine();

        Console.WriteLine("  -- Command Safety ------------------------------");
        Console.WriteLine($"  Commands sent:   {totalCommands}");
        Console.WriteLine($"  HTTP accepted:   {totalCommandAccepted}");
        Console.WriteLine($"  HTTP rejected:   {totalCommandRejected}");
        Console.WriteLine($"  HTTP errored:    {totalCommandErrored}");
        Console.WriteLine($"  Duplicate retry: {duplicateRetries}");
        Console.WriteLine($"  Stale attempts:  {staleAttempts}");
        Console.WriteLine($"  Stale rejected:  {staleRejected}");
        Console.WriteLine();

        Console.WriteLine("  -- Time to First Message -----------------------");
        var ttfm = _clients
            .Where(c => c.TimeToFirstMessage.HasValue)
            .Select(c => c.TimeToFirstMessage!.Value.TotalMilliseconds)
            .ToList();
        if (ttfm.Count > 0)
            Console.WriteLine($"  Avg:             {ttfm.Average():F0}ms  Max: {ttfm.Max():F0}ms");
        else
            Console.WriteLine("  No data");

        Console.WriteLine();
        Console.WriteLine("  -- Per-Client ----------------------------------");
        Console.WriteLine($"  {"ID",-22} {"Auth",-6} {"Msgs",-6} {"KB",-7} {"Cmd",-5} {"Dup",-5} {"Stale",-7} {"Reconnects",-12} {"LastTick",-10} {"TTFM"}");
        foreach (var client in _clients.OrderBy(c => c.ClientId))
        {
            var ttfmMs = client.TimeToFirstMessage.HasValue ? $"{client.TimeToFirstMessage.Value.TotalMilliseconds:F0}ms" : "-";
            var reconnects = client.ReconnectAttempts > 0 ? $"{client.ReconnectSuccesses}/{client.ReconnectAttempts}" : "-";
            var stale = client.StaleTokenAttempts > 0 ? $"{client.StaleTokenRejected}/{client.StaleTokenAttempts}" : "-";
            Console.WriteLine($"  {client.ClientId,-22} {(client.IsAuthenticated ? "yes" : "no"),-6} {client.MessagesReceived,-6} {client.BytesReceived / 1024.0,-7:F1} {client.CommandsSent,-5} {client.DuplicateRetries,-5} {stale,-7} {reconnects,-12} {client.LastTickSeen,-10} {ttfmMs}");
        }

        Console.WriteLine("==================================================");
    }

    private double BytesPerClientMinute(long totalBytes)
    {
        var minutes = Math.Max(_duration.TotalMinutes, 1.0 / 60.0);
        return totalBytes / Math.Max(1, _clients.Count) / minutes;
    }

    private double MessagesPerClientMinute(int totalMessages)
    {
        var minutes = Math.Max(_duration.TotalMinutes, 1.0 / 60.0);
        return totalMessages / Math.Max(1, _clients.Count) / minutes;
    }

    private IEnumerable<(string Type, int Messages, long Bytes, double AverageBytes)> MessageTypeRows()
    {
        return _clients
            .SelectMany(c => c.MessagesByMessageType.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t)
            .Select(type =>
            {
                var messages = _clients.Sum(c => c.MessagesByMessageType.GetValueOrDefault(type));
                var bytes = _clients.Sum(c => c.BytesByMessageType.GetValueOrDefault(type));
                var average = messages > 0 ? (double)bytes / messages : 0;
                return (type, messages, bytes, average);
            });
    }

    private static string FormatBytes(double bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / 1024 / 1024:F2} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024:F1} KB";
        return $"{bytes:F0} B";
    }
}
