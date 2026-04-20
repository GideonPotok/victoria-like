using VictoriaLike.LoadTest;

var config = LoadTestConfig.Parse(args);

Console.WriteLine("Victoria Fake Client Harness v2");
Console.WriteLine($"  Server:        {config.BaseUrl}");
Console.WriteLine($"  Clients:       {config.TotalClients}");
Console.WriteLine($"  Auth clients:  {config.AuthenticatedClients}");
Console.WriteLine($"  Duration:      {config.DurationSeconds}s");
Console.WriteLine($"  Reconnect:     {config.TestReconnect}");
Console.WriteLine($"  Commands:      {config.SubmitCommands}");
Console.WriteLine($"  Duplicate:     {config.TestDuplicateRetries}");
Console.WriteLine($"  Stale token:   {config.TestStaleToken}");
Console.WriteLine();

var clients = new List<FakeClient>();
var authAccounts = new[] { ("england-player", "eng123"), ("france-player", "fra123") };

for (var i = 0; i < config.TotalClients; i++)
{
    if (i < config.AuthenticatedClients)
    {
        var (username, password) = authAccounts[i % authAccounts.Length];
        clients.Add(new FakeClient(new FakeClientOptions
        {
            ClientId = $"auth-{i:D2}-{username}",
            BaseUrl = config.BaseUrl,
            Username = username,
            Password = password,
            SubmitCommands = config.SubmitCommands,
            TestReconnect = config.TestReconnect,
            TestDuplicateRetries = config.TestDuplicateRetries,
            TestStaleToken = config.TestStaleToken && i < authAccounts.Length,
            CommandInterval = TimeSpan.FromSeconds(config.CommandIntervalSeconds)
        }));
    }
    else
    {
        clients.Add(new FakeClient(new FakeClientOptions
        {
            ClientId = $"anon-{i:D2}",
            BaseUrl = config.BaseUrl,
            TestReconnect = config.TestReconnect
        }));
    }
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var duration = TimeSpan.FromSeconds(config.DurationSeconds);
var started = DateTime.UtcNow;

Console.WriteLine($"Starting {clients.Count} clients... (Ctrl+C to stop early)");

var tasks = clients.Select(async (client, idx) =>
{
    await Task.Delay(idx * config.StartupStaggerMs, cts.Token).ContinueWith(_ => { }, CancellationToken.None);
    await client.RunAsync(duration, cts.Token);
}).ToList();

var statusTask = Task.Run(async () =>
{
    while (!cts.Token.IsCancellationRequested)
    {
        await Task.Delay(10_000, cts.Token).ContinueWith(_ => { }, CancellationToken.None);
        if (cts.Token.IsCancellationRequested)
            break;

        var elapsed = (DateTime.UtcNow - started).TotalSeconds;
        var totalMsgs = clients.Sum(c => c.Metrics.MessagesReceived);
        var maxTick = clients.Max(c => c.Metrics.LastTickSeen);
        var errors = clients.Sum(c => c.Metrics.Errors);
        var commands = clients.Sum(c => c.Metrics.CommandsSent);
        Console.WriteLine($"  [{elapsed:F0}s] msgs={totalMsgs} commands={commands} last_tick={maxTick} errors={errors}");
    }
}).ContinueWith(_ => { }, CancellationToken.None);

await Task.WhenAll(tasks);
cts.Cancel();
await statusTask;

var report = new LoadTestReport(
    clients.Select(c => c.Metrics).ToList(),
    DateTime.UtcNow - started,
    config.TotalClients);
report.Print();

public sealed record LoadTestConfig(
    string BaseUrl,
    int TotalClients,
    int AuthenticatedClients,
    int DurationSeconds,
    bool TestReconnect,
    bool SubmitCommands,
    bool TestDuplicateRetries,
    bool TestStaleToken,
    int CommandIntervalSeconds,
    int StartupStaggerMs)
{
    public static LoadTestConfig Parse(string[] args)
    {
        var options = ParseOptions(args);

        var totalClients = GetInt(options, "clients", 20);
        return new LoadTestConfig(
            BaseUrl: GetString(options, "url", "http://localhost:5001"),
            TotalClients: totalClients,
            AuthenticatedClients: Math.Clamp(GetInt(options, "auth-clients", Math.Min(totalClients, 20)), 0, totalClients),
            DurationSeconds: GetInt(options, "duration", 120),
            TestReconnect: GetBool(options, "reconnect", true),
            SubmitCommands: GetBool(options, "commands", true),
            TestDuplicateRetries: GetBool(options, "duplicates", true),
            TestStaleToken: GetBool(options, "stale-token", true),
            CommandIntervalSeconds: GetInt(options, "command-interval", 20),
            StartupStaggerMs: GetInt(options, "startup-stagger-ms", 150));
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Backward-compatible positional form:
        // dotnet run -- <url> <clients> <duration> [no-reconnect]
        if (args.Length > 0 && !args[0].StartsWith("--", StringComparison.Ordinal))
            result["url"] = args[0];
        if (args.Length > 1 && !args[1].StartsWith("--", StringComparison.Ordinal))
            result["clients"] = args[1];
        if (args.Length > 2 && !args[2].StartsWith("--", StringComparison.Ordinal))
            result["duration"] = args[2];
        if (args.Length > 3 && args[3] == "no-reconnect")
            result["reconnect"] = "false";

        foreach (var arg in args.Where(arg => arg.StartsWith("--", StringComparison.Ordinal)))
        {
            var trimmed = arg[2..];
            var parts = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
            result[parts[0]] = parts.Length == 2 ? parts[1] : "true";
        }

        return result;
    }

    private static string GetString(Dictionary<string, string> options, string key, string fallback)
    {
        return options.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static int GetInt(Dictionary<string, string> options, string key, int fallback)
    {
        return options.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool GetBool(Dictionary<string, string> options, string key, bool fallback)
    {
        if (!options.TryGetValue(key, out var value))
            return fallback;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}
