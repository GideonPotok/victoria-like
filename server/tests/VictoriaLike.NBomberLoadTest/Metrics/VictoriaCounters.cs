using NBomber.Contracts;
using NBomber.Contracts.Metrics;

namespace VictoriaLike.NBomberLoadTest.Metrics;

public sealed class VictoriaCounters
{
    public readonly ICounter WebsocketConnectAttempts = Metric.CreateCounter("ws_connect_attempts", "attempts");
    public readonly ICounter WebsocketConnectFailures = Metric.CreateCounter("ws_connect_failures", "failures");
    public readonly ICounter ReconnectAttempts = Metric.CreateCounter("reconnect_attempts", "attempts");
    public readonly ICounter ReconnectSuccesses = Metric.CreateCounter("reconnect_successes", "successes");
    public readonly ICounter SubscriptionMessagesSent = Metric.CreateCounter("subscription_messages_sent", "messages");
    public readonly ICounter SubscriptionAcks = Metric.CreateCounter("subscription_acks", "acks");
    public readonly ICounter MessagesReceived = Metric.CreateCounter("messages_received", "messages");
    public readonly ICounter BytesReceived = Metric.CreateCounter("bytes_received", "bytes");
    public readonly ICounter WorldUpdates = Metric.CreateCounter("world_updates", "messages");
    public readonly ICounter ReconnectSnapshots = Metric.CreateCounter("reconnect_snapshots", "messages");
    public readonly ICounter MarketUpdates = Metric.CreateCounter("market_updates", "messages");
    public readonly ICounter CountryUpdates = Metric.CreateCounter("country_updates", "messages");
    public readonly ICounter CommandResultsApplied = Metric.CreateCounter("command_results_applied", "results");
    public readonly ICounter CommandResultsRejected = Metric.CreateCounter("command_results_rejected", "results");
    public readonly ICounter CommandResultsFailed = Metric.CreateCounter("command_results_failed", "results");
    public readonly ICounter CommandsSent = Metric.CreateCounter("commands_sent", "commands");
    public readonly ICounter PeacefulCommandsSent = Metric.CreateCounter("peaceful_commands_sent", "commands");
    public readonly ICounter CommandHttpAccepted = Metric.CreateCounter("command_http_accepted", "responses");
    public readonly ICounter CommandHttpRejected = Metric.CreateCounter("command_http_rejected", "responses");
    public readonly ICounter CommandHttpErrored = Metric.CreateCounter("command_http_errored", "responses");
    public readonly ICounter DuplicateRetries = Metric.CreateCounter("duplicate_retries", "requests");
    public readonly ICounter StaleTokenAttempts = Metric.CreateCounter("stale_token_attempts", "attempts");
    public readonly ICounter StaleTokenRejected = Metric.CreateCounter("stale_token_rejected", "responses");
    public readonly ICounter UnexpectedWebsocketErrors = Metric.CreateCounter("unexpected_ws_errors", "errors");
    public readonly IGauge LastTickSeen = Metric.CreateGauge("last_tick_seen", "tick");
    public readonly IGauge MeanTickIntervalMs = Metric.CreateGauge("mean_tick_interval_ms", "ms");
    public readonly IGauge MaxTickIntervalMs = Metric.CreateGauge("max_tick_interval_ms", "ms");
    public readonly IGauge TimeToFirstWorldUpdateMs = Metric.CreateGauge("time_to_first_world_update_ms", "ms");

    public void Register(IScenarioInitContext context)
    {
        context.RegisterMetric(WebsocketConnectAttempts);
        context.RegisterMetric(WebsocketConnectFailures);
        context.RegisterMetric(ReconnectAttempts);
        context.RegisterMetric(ReconnectSuccesses);
        context.RegisterMetric(SubscriptionMessagesSent);
        context.RegisterMetric(SubscriptionAcks);
        context.RegisterMetric(MessagesReceived);
        context.RegisterMetric(BytesReceived);
        context.RegisterMetric(WorldUpdates);
        context.RegisterMetric(ReconnectSnapshots);
        context.RegisterMetric(MarketUpdates);
        context.RegisterMetric(CountryUpdates);
        context.RegisterMetric(CommandResultsApplied);
        context.RegisterMetric(CommandResultsRejected);
        context.RegisterMetric(CommandResultsFailed);
        context.RegisterMetric(CommandsSent);
        context.RegisterMetric(PeacefulCommandsSent);
        context.RegisterMetric(CommandHttpAccepted);
        context.RegisterMetric(CommandHttpRejected);
        context.RegisterMetric(CommandHttpErrored);
        context.RegisterMetric(DuplicateRetries);
        context.RegisterMetric(StaleTokenAttempts);
        context.RegisterMetric(StaleTokenRejected);
        context.RegisterMetric(UnexpectedWebsocketErrors);
        context.RegisterMetric(LastTickSeen);
        context.RegisterMetric(MeanTickIntervalMs);
        context.RegisterMetric(MaxTickIntervalMs);
        context.RegisterMetric(TimeToFirstWorldUpdateMs);
    }
}
