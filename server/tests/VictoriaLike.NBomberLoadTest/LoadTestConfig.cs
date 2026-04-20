using System.Text.Json;

namespace VictoriaLike.NBomberLoadTest;

public sealed record LoadTestConfig(
    string BaseUrl,
    int TotalUsers,
    int AuthenticatedUsers,
    int DurationSeconds,
    int WarmupSeconds,
    bool ReconnectEnabled,
    bool CommandsEnabled,
    bool DuplicateRetriesEnabled,
    bool StaleTokenEnabled,
    int CommandIntervalSeconds,
    int StartupStaggerMs,
    string ScenarioProfile,
    string CommandMix,
    string CredentialsFile,
    IReadOnlyList<TestCredential> Credentials)
{
    private static readonly TestCredential[] DefaultCredentials =
    [
        new("albion-player", "alb123"),
        new("bretoria-player", "bre123")
    ];

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public TimeSpan Warmup => TimeSpan.FromSeconds(WarmupSeconds);
    public TimeSpan CommandInterval => TimeSpan.FromSeconds(CommandIntervalSeconds);
    public bool IsTwoPlayerSoak => ScenarioProfile.Equals("two-player-soak", StringComparison.OrdinalIgnoreCase);
    public bool IsPeacefulCommandMix => CommandMix.Equals("peaceful", StringComparison.OrdinalIgnoreCase);

    public bool IncludeSubscriberScenario =>
        ScenarioProfile.Equals("smoke", StringComparison.OrdinalIgnoreCase)
        || ScenarioProfile.Equals("baseline", StringComparison.OrdinalIgnoreCase)
        || ScenarioProfile.Equals("stress", StringComparison.OrdinalIgnoreCase)
        || ScenarioProfile.Equals("soak", StringComparison.OrdinalIgnoreCase)
        || ScenarioProfile.Equals("subscribers", StringComparison.OrdinalIgnoreCase)
        || ScenarioProfile.Equals("all", StringComparison.OrdinalIgnoreCase);

    public bool IncludeCommandScenario =>
        CommandsEnabled
        && (ScenarioProfile.Equals("commands", StringComparison.OrdinalIgnoreCase)
            || ScenarioProfile.Equals("all", StringComparison.OrdinalIgnoreCase)
            || ScenarioProfile.Equals("baseline", StringComparison.OrdinalIgnoreCase)
            || ScenarioProfile.Equals("stress", StringComparison.OrdinalIgnoreCase)
            || ScenarioProfile.Equals("soak", StringComparison.OrdinalIgnoreCase)
            || ScenarioProfile.Equals("two-player-soak", StringComparison.OrdinalIgnoreCase));

    public static LoadTestConfig Parse(string[] args)
    {
        var options = ParseOptions(args);
        var env = Environment.GetEnvironmentVariables();
        var profile = GetString(options, env, "profile", "VICTORIA_NBOMBER_PROFILE", "baseline");
        var isTwoPlayerSoak = profile.Equals("two-player-soak", StringComparison.OrdinalIgnoreCase);

        var totalUsers = GetInt(options, env, "total-users", "VICTORIA_NBOMBER_TOTAL_USERS", isTwoPlayerSoak ? 2 : 20);
        var authUsers = Math.Clamp(
            GetInt(options, env, "auth-users", "VICTORIA_NBOMBER_AUTH_USERS", isTwoPlayerSoak ? 2 : Math.Min(totalUsers, 20)),
            0,
            totalUsers);
        var credentialsFile = GetString(options, env, "credentials-file", "VICTORIA_NBOMBER_CREDENTIALS_FILE", "");

        return new LoadTestConfig(
            BaseUrl: GetString(options, env, "url", "VICTORIA_NBOMBER_BASE_URL", "http://localhost:5001"),
            TotalUsers: totalUsers,
            AuthenticatedUsers: authUsers,
            DurationSeconds: GetInt(options, env, "duration", "VICTORIA_NBOMBER_DURATION_SECONDS", isTwoPlayerSoak ? 1800 : 120),
            WarmupSeconds: GetInt(options, env, "warmup", "VICTORIA_NBOMBER_WARMUP_SECONDS", 10),
            ReconnectEnabled: GetBool(options, env, "reconnect", "VICTORIA_NBOMBER_RECONNECT", true),
            CommandsEnabled: GetBool(options, env, "commands", "VICTORIA_NBOMBER_COMMANDS", true),
            DuplicateRetriesEnabled: GetBool(options, env, "duplicates", "VICTORIA_NBOMBER_DUPLICATES", true),
            StaleTokenEnabled: GetBool(options, env, "stale-token", "VICTORIA_NBOMBER_STALE_TOKEN", true),
            CommandIntervalSeconds: GetInt(options, env, "command-interval", "VICTORIA_NBOMBER_COMMAND_INTERVAL_SECONDS", 20),
            StartupStaggerMs: GetInt(options, env, "startup-stagger-ms", "VICTORIA_NBOMBER_STARTUP_STAGGER_MS", 150),
            ScenarioProfile: profile,
            CommandMix: GetString(options, env, "command-mix", "VICTORIA_NBOMBER_COMMAND_MIX", isTwoPlayerSoak ? "peaceful" : "full"),
            CredentialsFile: credentialsFile,
            Credentials: LoadCredentials(credentialsFile));
    }

    public TestCredential? GetCredential(int instanceNumber)
    {
        if (instanceNumber >= AuthenticatedUsers || Credentials.Count == 0)
            return null;

        return Credentials[instanceNumber % Credentials.Count];
    }

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var arg in args.Where(arg => arg.StartsWith("--", StringComparison.Ordinal)))
        {
            var trimmed = arg[2..];
            var parts = trimmed.Split('=', 2, StringSplitOptions.TrimEntries);
            result[parts[0]] = parts.Length == 2 ? parts[1] : "true";
        }

        return result;
    }

    private static IReadOnlyList<TestCredential> LoadCredentials(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return DefaultCredentials;

        if (!File.Exists(path))
            throw new FileNotFoundException("Credentials file was not found.", path);

        if (path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var credentials = JsonSerializer.Deserialize<List<TestCredential>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return credentials is { Count: > 0 } ? credentials : DefaultCredentials;
        }

        var rows = File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split(',', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2)
            .Select(parts => new TestCredential(parts[0], parts[1]))
            .ToList();

        return rows.Count > 0 ? rows : DefaultCredentials;
    }

    private static string GetString(
        Dictionary<string, string> options,
        System.Collections.IDictionary env,
        string optionKey,
        string envKey,
        string fallback)
    {
        if (options.TryGetValue(optionKey, out var optionValue) && !string.IsNullOrWhiteSpace(optionValue))
            return optionValue;

        var envValue = env[envKey]?.ToString();
        return string.IsNullOrWhiteSpace(envValue) ? fallback : envValue;
    }

    private static int GetInt(
        Dictionary<string, string> options,
        System.Collections.IDictionary env,
        string optionKey,
        string envKey,
        int fallback)
    {
        var value = GetString(options, env, optionKey, envKey, "");
        return int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool GetBool(
        Dictionary<string, string> options,
        System.Collections.IDictionary env,
        string optionKey,
        string envKey,
        bool fallback)
    {
        var value = GetString(options, env, optionKey, envKey, "");
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("1", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record TestCredential(string Username, string Password);
