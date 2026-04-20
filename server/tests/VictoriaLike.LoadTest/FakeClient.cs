using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VictoriaLike.LoadTest;

public sealed class FakeClientOptions
{
    public required string ClientId { get; init; }
    public required string BaseUrl { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public bool SubmitCommands { get; init; }
    public bool TestReconnect { get; init; }
    public bool TestDuplicateRetries { get; init; }
    public bool TestStaleToken { get; init; }
    public TimeSpan CommandInterval { get; init; } = TimeSpan.FromSeconds(15);
}

public sealed class FakeClient
{
    private static readonly HttpClient Http = new();

    private readonly FakeClientOptions _options;
    private readonly string _wsUrl;
    private string? _token;
    private string? _actorId;
    private string? _controlledCountryId;
    private string? _provinceId;
    private long _lastServerTick;
    private bool _staleTokenTested;

    public ClientMetrics Metrics { get; }

    public FakeClient(FakeClientOptions options)
    {
        _options = options;
        _wsUrl = options.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase);
        Metrics = new ClientMetrics
        {
            ClientId = options.ClientId,
            IsAuthenticated = options.Username != null
        };
    }

    public async Task RunAsync(TimeSpan duration, CancellationToken ct)
    {
        if (_options.Username != null && _options.Password != null)
        {
            try
            {
                await LoginAsync(ct);
                await LoadCountryAssignmentAsync(ct);
                Metrics.RecordLoginSuccess();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[{Metrics.ClientId}] login/setup failed: {ex.Message}");
                Metrics.RecordError();
                return;
            }
        }

        if (_options.TestStaleToken && _token != null)
            await TestStaleTokenAsync(ct);

        var deadline = DateTime.UtcNow + duration;
        var nextCommandAt = DateTime.UtcNow.AddSeconds(5 + Random.Shared.Next(5));
        var reconnectAt = _options.TestReconnect
            ? DateTime.UtcNow.AddSeconds(15 + Random.Shared.Next(15))
            : DateTime.MaxValue;
        var connectCount = 0;
        var reconnectedOnce = false;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            Metrics.RecordConnectAttempt();
            connectCount++;
            if (connectCount > 1)
                Metrics.RecordReconnectAttempt();

            try
            {
                var wsEndpoint = _token != null
                    ? $"{_wsUrl}/ws/world?token={_token}"
                    : $"{_wsUrl}/ws/world";

                await ws.ConnectAsync(new Uri(wsEndpoint), ct);

                if (connectCount > 1)
                    Metrics.RecordReconnectSuccess();

                await SubscribeAsync(ws, ct);

                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open && DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                {
                    if (_options.SubmitCommands && _token != null && DateTime.UtcNow >= nextCommandAt)
                    {
                        await SendTaxCommandAsync(ct);
                        nextCommandAt = DateTime.UtcNow + _options.CommandInterval;
                    }

                    if (!reconnectedOnce && _options.TestReconnect && DateTime.UtcNow >= reconnectAt)
                    {
                        reconnectedOnce = true;
                        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect test", CancellationToken.None);
                        await Task.Delay(500, ct);
                        break;
                    }

                    using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    readCts.CancelAfter(TimeSpan.FromSeconds(3));

                    try
                    {
                        var msgBuffer = new StringBuilder();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), readCts.Token);
                            if (result.MessageType == WebSocketMessageType.Close)
                                goto disconnect;
                            if (result.MessageType == WebSocketMessageType.Text)
                                msgBuffer.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                        } while (!result.EndOfMessage);

                        if (msgBuffer.Length > 0)
                        {
                            var json = msgBuffer.ToString();
                            HandleMessage(json);
                        }
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Read timeout: keep the socket open and allow command/reconnect timers to progress.
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Metrics.RecordError();
                Console.Error.WriteLine($"[{Metrics.ClientId}] ws error: {ex.Message}");
                await Task.Delay(2000, ct).ContinueWith(_ => { }, CancellationToken.None);
            }

            disconnect:;
        }
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { username = _options.Username, password = _options.Password });
        var response = await Http.PostAsync(
            $"{_options.BaseUrl}/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"),
            ct);

        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        _token = GetString(doc.RootElement, "token");
        _actorId = GetString(doc.RootElement, "actor_id");
        _controlledCountryId = GetString(doc.RootElement, "controlled_country_id");
    }

    private async Task LoadCountryAssignmentAsync(CancellationToken ct)
    {
        if (_controlledCountryId == null)
            return;

        var response = await Http.GetAsync($"{_options.BaseUrl}/api/world/provinces", ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        foreach (var province in doc.RootElement.EnumerateArray())
        {
            if (GetString(province, "owner_id") == _controlledCountryId)
            {
                _provinceId = GetString(province, "id");
                return;
            }
        }
    }

    private async Task TestStaleTokenAsync(CancellationToken ct)
    {
        if (_staleTokenTested || _token == null)
            return;

        _staleTokenTested = true;
        using var logout = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/api/auth/logout");
        logout.Headers.Add("Authorization", $"Bearer {_token}");
        await Http.SendAsync(logout, ct);

        using var staleRequest = new HttpRequestMessage(HttpMethod.Get, $"{_options.BaseUrl}/api/auth/me");
        staleRequest.Headers.Add("Authorization", $"Bearer {_token}");
        var staleResponse = await Http.SendAsync(staleRequest, ct);
        Metrics.RecordStaleTokenAttempt(staleResponse.StatusCode == HttpStatusCode.Unauthorized);

        await LoginAsync(ct);
        Metrics.RecordLoginSuccess();
    }

    private async Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var topics = new List<string> { "world_summary", "country", "market" };
        if (_provinceId != null)
            topics.Add($"province:{_provinceId}");

        var body = JsonSerializer.Serialize(new { type = "subscribe", topics });
        await SendAsync(ws, body, ct);
        Metrics.RecordSubscription(topics.Count);
    }

    private void HandleMessage(string json)
    {
        var byteCount = Encoding.UTF8.GetByteCount(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = GetString(root, "type");

        Metrics.RecordMessage(type, byteCount);

        if (type == "world_update" || type == "reconnect_snapshot")
        {
            var tick = GetLong(root, "tick");
            if (tick > 0)
                _lastServerTick = tick;
            Metrics.RecordWorldUpdate(tick);
        }
        else if (type == "market_update")
        {
            Metrics.RecordMarketUpdate();
            var tick = GetLong(root, "tick");
            if (tick > 0)
                Metrics.LastTickSeen = Math.Max(Metrics.LastTickSeen, tick);
        }
        else if (type == "country_update")
        {
            Metrics.RecordCountryUpdate();
            var tick = GetLong(root, "tick");
            if (tick > 0)
                Metrics.LastTickSeen = Math.Max(Metrics.LastTickSeen, tick);
        }
        else if (type == "subscribed")
        {
            Metrics.RecordSubscriptionAck();
        }
        else if (type == "command_result")
        {
            var status = GetString(root, "status");
            Metrics.RecordCommandResult(status);
        }
    }

    private async Task SendTaxCommandAsync(CancellationToken ct)
    {
        if (_token == null || _controlledCountryId == null)
            return;

        var commandId = Guid.NewGuid().ToString();
        var idempotencyKey = $"load-{Metrics.ClientId}-{commandId}";
        var taxRate = Random.Shared.Next(5, 30);
        var body = BuildTaxCommand(commandId, idempotencyKey, taxRate);

        Metrics.RecordCommandSent();
        var first = await SendCommandRequestAsync(body, ct);
        Metrics.RecordCommandResponse(first);

        if (_options.TestDuplicateRetries)
        {
            Metrics.RecordDuplicateRetry();
            var retry = await SendCommandRequestAsync(body, ct);
            Metrics.RecordCommandResponse(retry);
        }
    }

    private string BuildTaxCommand(string commandId, string idempotencyKey, int taxRate)
    {
        return JsonSerializer.Serialize(new
        {
            commandId,
            idempotencyKey,
            expectedWorldTick = _lastServerTick > 0 ? _lastServerTick : (long?)null,
            commandType = "ChangeTaxRate",
            payload = new Dictionary<string, object>
            {
                ["countryId"] = _controlledCountryId ?? "",
                ["newTaxRate"] = taxRate
            }
        });
    }

    private async Task<HttpStatusCode> SendCommandRequestAsync(string body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_options.BaseUrl}/api/world/commands")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {_token}");
            var response = await Http.SendAsync(req, ct);
            return response.StatusCode;
        }
        catch
        {
            Metrics.RecordError();
            return 0;
        }
    }

    private static Task SendAsync(ClientWebSocket ws, string message, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        return ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
    }

    private static string? GetString(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static long GetLong(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var property) && property.TryGetInt64(out var value)
            ? value
            : 0;
    }
}
