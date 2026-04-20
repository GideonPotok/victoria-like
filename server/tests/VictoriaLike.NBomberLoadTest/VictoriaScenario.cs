using NBomber.CSharp;
using NBomber.Contracts;
using VictoriaLike.NBomberLoadTest.Metrics;

namespace VictoriaLike.NBomberLoadTest;

public static class VictoriaScenario
{
    public static ScenarioProps CreateWebsocketSubscribers(LoadTestConfig config, VictoriaCounters metrics)
    {
        var scenario = Scenario.Create("websocket_subscribers", async context =>
        {
            await StartupStaggerAsync(config, context.ScenarioInfo.InstanceNumber);
            using var cts = new CancellationTokenSource(config.Duration + TimeSpan.FromSeconds(5));
            var session = new VictoriaClientSession(
                config,
                metrics,
                context.ScenarioInfo.InstanceNumber,
                forceAuthenticated: false);

            if (context.ScenarioInfo.InstanceNumber < config.AuthenticatedUsers)
            {
                await Step.Run("login", context, async () =>
                {
                    await session.LoginAsync(cts.Token);
                    return Response.Ok();
                });

                await Step.Run("load_owned_province", context, async () =>
                {
                    await session.LoadCountryAssignmentAsync(cts.Token);
                    return Response.Ok();
                });

                if (config.StaleTokenEnabled && context.ScenarioInfo.InstanceNumber < config.Credentials.Count)
                {
                    await Step.Run("stale_token_validation", context, () => session.TestStaleTokenAsync(cts.Token));
                }
            }

            return await Step.Run("websocket_receive_loop", context, () =>
                session.RunWebsocketAsync(
                    config.Duration,
                    submitCommands: false,
                    reconnectEnabled: config.ReconnectEnabled,
                    cts.Token));
        })
        .WithInit(context =>
        {
            metrics.Register(context);
            return Task.CompletedTask;
        })
        .WithRestartIterationOnFail(false)
        .WithThresholds(
            Threshold.Create(scenarioStats => scenarioStats.Fail.Request.Percent < 5),
            Threshold.Create("websocket_receive_loop", stepStats => stepStats.Fail.Request.Percent < 1))
        .WithLoadSimulations(Simulation.KeepConstant(config.TotalUsers, config.Duration));

        return config.WarmupSeconds > 0
            ? scenario.WithWarmUpDuration(config.Warmup)
            : scenario.WithoutWarmUp();
    }

    public static ScenarioProps CreateAuthenticatedCommandSafety(LoadTestConfig config, VictoriaCounters metrics)
    {
        var copies = Math.Max(1, config.AuthenticatedUsers);
        var scenario = Scenario.Create("authenticated_command_safety", async context =>
        {
            await StartupStaggerAsync(config, context.ScenarioInfo.InstanceNumber);
            using var cts = new CancellationTokenSource(config.Duration + TimeSpan.FromSeconds(5));
            var session = new VictoriaClientSession(
                config,
                metrics,
                context.ScenarioInfo.InstanceNumber,
                forceAuthenticated: true);

            await Step.Run("login", context, async () =>
            {
                await session.LoginAsync(cts.Token);
                return Response.Ok();
            });

            await Step.Run("load_owned_province", context, async () =>
            {
                await session.LoadCountryAssignmentAsync(cts.Token);
                return Response.Ok();
            });

            if (config.StaleTokenEnabled && context.ScenarioInfo.InstanceNumber < config.Credentials.Count)
            {
                await Step.Run("stale_token_validation", context, () => session.TestStaleTokenAsync(cts.Token));
            }

            return await Step.Run("websocket_commands_loop", context, () =>
                session.RunWebsocketAsync(
                    config.Duration,
                    submitCommands: true,
                    reconnectEnabled: config.ReconnectEnabled,
                    cts.Token));
        })
        .WithInit(context =>
        {
            metrics.Register(context);
            return Task.CompletedTask;
        })
        .WithRestartIterationOnFail(false)
        .WithThresholds(CreateCommandThresholds(config))
        .WithLoadSimulations(Simulation.KeepConstant(copies, config.Duration));

        return config.WarmupSeconds > 0
            ? scenario.WithWarmUpDuration(config.Warmup)
            : scenario.WithoutWarmUp();
    }

    private static Task StartupStaggerAsync(LoadTestConfig config, int instanceNumber)
    {
        if (config.StartupStaggerMs <= 0)
            return Task.CompletedTask;

        return Task.Delay(instanceNumber * config.StartupStaggerMs);
    }

    private static Threshold[] CreateCommandThresholds(LoadTestConfig config)
    {
        if (config.IsTwoPlayerSoak)
        {
            return
            [
                Threshold.Create(scenarioStats => scenarioStats.Fail.Request.Count == 0),
                Threshold.Create("websocket_commands_loop", stepStats => stepStats.Fail.Request.Count == 0),
                Threshold.Create("stale_token_validation", stepStats => stepStats.Fail.Request.Count == 0)
            ];
        }

        return
        [
            Threshold.Create(scenarioStats => scenarioStats.Fail.Request.Percent < 5),
            Threshold.Create("websocket_commands_loop", stepStats => stepStats.Fail.Request.Percent < 1),
            Threshold.Create("stale_token_validation", stepStats => stepStats.Fail.Request.Percent == 0)
        ];
    }
}
