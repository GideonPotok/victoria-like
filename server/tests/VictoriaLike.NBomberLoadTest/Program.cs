using NBomber.CSharp;
using NBomber.Contracts;
using VictoriaLike.NBomberLoadTest;
using VictoriaLike.NBomberLoadTest.Metrics;

var config = LoadTestConfig.Parse(args);
var metrics = new VictoriaCounters();

Console.WriteLine("VictoriaLike NBomber Load Test");
Console.WriteLine($"  Server:        {config.BaseUrl}");
Console.WriteLine($"  Profile:       {config.ScenarioProfile}");
Console.WriteLine($"  Users:         {config.TotalUsers}");
Console.WriteLine($"  Auth users:    {config.AuthenticatedUsers}");
Console.WriteLine($"  Duration:      {config.DurationSeconds}s");
Console.WriteLine($"  Warmup:        {config.WarmupSeconds}s");
Console.WriteLine($"  Reconnect:     {config.ReconnectEnabled}");
Console.WriteLine($"  Commands:      {config.CommandsEnabled}");
Console.WriteLine($"  Command mix:   {config.CommandMix}");
Console.WriteLine($"  Duplicate:     {config.DuplicateRetriesEnabled}");
Console.WriteLine($"  Stale token:   {config.StaleTokenEnabled}");
Console.WriteLine($"  Credentials:   {(string.IsNullOrWhiteSpace(config.CredentialsFile) ? "built-in defaults" : config.CredentialsFile)}");
Console.WriteLine();

var scenarios = new List<ScenarioProps>();
if (config.IncludeSubscriberScenario)
    scenarios.Add(VictoriaScenario.CreateWebsocketSubscribers(config, metrics));
if (config.IncludeCommandScenario)
    scenarios.Add(VictoriaScenario.CreateAuthenticatedCommandSafety(config, metrics));
if (scenarios.Count == 0)
    scenarios.Add(VictoriaScenario.CreateWebsocketSubscribers(config, metrics));

var result = NBomberRunner
    .RegisterScenarios(scenarios.ToArray())
    .WithTestName("victoria_like_nbomber_load_test")
    .WithTestSuite("VictoriaLike")
    .WithReportFileName($"victoria-like-{config.ScenarioProfile}")
    .Run();

var failedThreshold = result.Thresholds.FirstOrDefault(threshold => threshold.IsFailed);
if (failedThreshold != null)
{
    Console.Error.WriteLine($"Threshold failed: {failedThreshold}");
    Environment.ExitCode = 1;
}
