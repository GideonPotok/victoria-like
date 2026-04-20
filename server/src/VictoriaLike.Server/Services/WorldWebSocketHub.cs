using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace VictoriaLike.Server.Services;

public interface IWorldWebSocketHub
{
    int ConnectedClientCount { get; }
    Task BroadcastWorldUpdateAsync(long tick, DateTime worldDate, IReadOnlyDictionary<string, decimal>? marketPrices = null);
    Task BroadcastMarketUpdateAsync(long tick, IReadOnlyDictionary<string, decimal> prices, IReadOnlyDictionary<string, decimal> supply, IReadOnlyDictionary<string, decimal> demand);
    Task SendCountryUpdateAsync(string actorId, long tick, string countryId, int taxRate, decimal treasury);
    Task SendCommandResultAsync(string actorId, string commandId, string commandType, string status, string? message, string? rejectionReason, long? retryAfterTicks);
    Task RegisterAsync(WebSocket socket, string? actorId, CancellationToken cancellationToken, byte[]? initialMessage = null);
    IReadOnlyList<WorldWebSocketConnectionInfo> GetConnections();
}

public sealed record WorldWebSocketConnectionInfo(string? ActorId, DateTime ConnectedAtUtc, IReadOnlySet<string> Subscriptions);

public class WorldWebSocketHub : IWorldWebSocketHub
{
    private sealed class Connection
    {
        public string? ActorId { get; init; }
        public SemaphoreSlim SendLock { get; } = new(1, 1);
        public DateTime ConnectedAtUtc { get; init; }
        public HashSet<string> Subscriptions { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private readonly ILogger<WorldWebSocketHub> _logger;
    private readonly ConcurrentDictionary<WebSocket, Connection> _connections = new();

    public WorldWebSocketHub(ILogger<WorldWebSocketHub> logger)
    {
        _logger = logger;
    }

    public int ConnectedClientCount => _connections.Count(kv => kv.Key.State == WebSocketState.Open);

    public async Task BroadcastWorldUpdateAsync(long tick, DateTime worldDate, IReadOnlyDictionary<string, decimal>? marketPrices = null)
    {
        if (_connections.IsEmpty)
            return;

        var message = JsonSerializer.Serialize(new
        {
            type = "world_update",
            tick,
            world_date = worldDate.ToString("yyyy-MM-dd"),
            market_prices = marketPrices
        });
        await SendToAllAsync(message);
    }

    public async Task BroadcastMarketUpdateAsync(long tick, IReadOnlyDictionary<string, decimal> prices, IReadOnlyDictionary<string, decimal> supply, IReadOnlyDictionary<string, decimal> demand)
    {
        if (_connections.IsEmpty)
            return;

        var message = JsonSerializer.Serialize(new
        {
            type = "market_update",
            tick,
            prices,
            supply,
            demand
        });
        var bytes = Encoding.UTF8.GetBytes(message);
        var targets = _connections
            .Where(kv => kv.Key.State == WebSocketState.Open && kv.Value.Subscriptions.Contains("market"))
            .ToList();

        if (targets.Count == 0)
            return;

        await Task.WhenAll(targets.Select(kv => SendFrameAsync(kv.Key, kv.Value.SendLock, bytes)));
    }

    public async Task SendCountryUpdateAsync(string actorId, long tick, string countryId, int taxRate, decimal treasury)
    {
        var message = JsonSerializer.Serialize(new
        {
            type = "country_update",
            tick,
            country_id = countryId,
            tax_rate = taxRate,
            treasury
        });
        var bytes = Encoding.UTF8.GetBytes(message);
        var targets = _connections
            .Where(kv => kv.Value.ActorId == actorId && kv.Key.State == WebSocketState.Open)
            .ToList();

        if (targets.Count == 0)
            return;

        await Task.WhenAll(targets.Select(kv => SendFrameAsync(kv.Key, kv.Value.SendLock, bytes)));
    }

    public async Task SendCommandResultAsync(string actorId, string commandId, string commandType, string status, string? message, string? rejectionReason, long? retryAfterTicks)
    {
        var payload = JsonSerializer.Serialize(new
        {
            type = "command_result",
            actor_id = actorId,
            command_id = commandId,
            command_type = commandType,
            status,
            message,
            reason = message,
            rejection_reason = rejectionReason,
            retry_after_ticks = retryAfterTicks
        });

        var bytes = Encoding.UTF8.GetBytes(payload);
        var targets = _connections
            .Where(kv => kv.Value.ActorId == actorId && kv.Key.State == WebSocketState.Open)
            .ToList();

        if (targets.Count == 0)
            return;

        await Task.WhenAll(targets.Select(kv => SendFrameAsync(kv.Key, kv.Value.SendLock, bytes)));
    }

    public async Task RegisterAsync(WebSocket socket, string? actorId, CancellationToken cancellationToken, byte[]? initialMessage = null)
    {
        var connection = new Connection { ActorId = actorId, ConnectedAtUtc = DateTime.UtcNow };
        connection.Subscriptions.Add("world_summary");
        if (actorId != null)
            connection.Subscriptions.Add("market");

        _connections.TryAdd(socket, connection);
        _logger.LogInformation("WebSocket connected: actor={Actor}, subscriptions={Subs}, total={Count}",
            actorId ?? "anonymous",
            string.Join(",", connection.Subscriptions),
            _connections.Count);

        if (initialMessage != null)
        {
            try { await SendFrameAsync(socket, connection.SendLock, initialMessage); }
            catch (Exception ex) { _logger.LogDebug("Failed to send initial message: {Error}", ex.Message); }
        }

        try
        {
            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var msgBuffer = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                        return;
                    }
                    if (result.MessageType == WebSocketMessageType.Text)
                        msgBuffer.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (msgBuffer.Length > 0)
                    await ProcessClientMessageAsync(socket, connection, msgBuffer.ToArray(), CancellationToken.None);
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogDebug("WebSocket closed prematurely for actor {Actor}", actorId);
        }
        finally
        {
            _connections.TryRemove(socket, out _);
            _logger.LogInformation("WebSocket disconnected: actor={Actor}, remaining={Count}",
                actorId ?? "anonymous", _connections.Count);
        }
    }

    public IReadOnlyList<WorldWebSocketConnectionInfo> GetConnections()
    {
        return _connections
            .Where(kv => kv.Key.State == WebSocketState.Open)
            .Select(kv => new WorldWebSocketConnectionInfo(
                kv.Value.ActorId,
                kv.Value.ConnectedAtUtc,
                kv.Value.Subscriptions))
            .OrderBy(c => c.ConnectedAtUtc)
            .ToList();
    }

    private async Task ProcessClientMessageAsync(WebSocket socket, Connection connection, byte[] messageBytes, CancellationToken cancellationToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(messageBytes);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var typeProp))
                return;

            var type = typeProp.GetString();

            if (type == "subscribe" || type == "unsubscribe")
            {
                var added = new List<string>();
                if (root.TryGetProperty("topics", out var topicsProp) && topicsProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var topic in topicsProp.EnumerateArray())
                    {
                        var t = topic.GetString();
                        if (string.IsNullOrWhiteSpace(t)) continue;

                        if (type == "subscribe")
                            connection.Subscriptions.Add(t);
                        else
                            connection.Subscriptions.Remove(t);
                        added.Add(t);
                    }
                }

                var ack = JsonSerializer.Serialize(new
                {
                    type = type == "subscribe" ? "subscribed" : "unsubscribed",
                    topics = added,
                    active_subscriptions = connection.Subscriptions.ToArray()
                });
                await SendFrameAsync(socket, connection.SendLock, Encoding.UTF8.GetBytes(ack));
            }
        }
        catch (JsonException)
        {
            // ignore malformed client messages
        }
    }

    private async Task SendToAllAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var snapshot = _connections.ToArray();
        var open = snapshot.Where(kv => kv.Key.State == WebSocketState.Open).ToList();
        var dead = snapshot.Where(kv => kv.Key.State != WebSocketState.Open).Select(kv => kv.Key).ToList();

        foreach (var socket in dead)
            _connections.TryRemove(socket, out _);

        if (open.Count == 0)
            return;

        var results = await Task.WhenAll(open.Select(async kv =>
        {
            try
            {
                await SendFrameAsync(kv.Key, kv.Value.SendLock, bytes);
                return (Socket: kv.Key, Failed: false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Send failed for socket, removing: {Error}", ex.Message);
                return (Socket: kv.Key, Failed: true);
            }
        }));

        foreach (var r in results.Where(r => r.Failed))
            _connections.TryRemove(r.Socket, out _);
    }

    private static async Task SendFrameAsync(WebSocket socket, SemaphoreSlim sendLock, byte[] bytes)
    {
        await sendLock.WaitAsync();
        try
        {
            await socket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
        }
        finally
        {
            sendLock.Release();
        }
    }
}
