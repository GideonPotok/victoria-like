using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using NBomber.CSharp;
using NBomber.Contracts;
using VictoriaLike.NBomberLoadTest.Metrics;

namespace VictoriaLike.NBomberLoadTest;

public sealed class VictoriaClientSession
{
    private static readonly HttpClient Http = new();

    private readonly LoadTestConfig _config;
    private readonly VictoriaCounters _metrics;
    private readonly TestCredential? _credential;
    private readonly string _clientId;
    private readonly string _wsUrl;
    private string? _token;
    private string? _actorId;
    private string? _controlledCountryId;
    private string? _provinceId;
    private string? _ownedMoveDestinationProvinceId;
    private string? _armyId;
    private string? _foreignCountryId;
    private bool _warCommandIssued;
    private bool _peaceCommandIssued;
    private long _lastServerTick;
    private int _connectCount;
    private int _reconnectSuccessCount;
    private int _subscriptionAckCount;
    private int _reconnectSnapshotCount;
    private int _worldUpdateCount;
    private int _commandsAttempted;
    private int _commandHttpErrorCount;
    private int _commandResultCount;
    private int _commandResultFailedCount;
    private DateTime _connectTime;
    private DateTime _lastTickTime;
    private double _tickIntervalTotalMs;
    private int _tickIntervalCount;
    private double _maxTickIntervalMs;
    private long _lastTickMetricValue;
    private bool _recordedTimeToFirstWorldUpdate;

    public VictoriaClientSession(LoadTestConfig config, VictoriaCounters metrics, int instanceNumber, bool forceAuthenticated)
    {
        _config = config;
        _metrics = metrics;
        _credential = forceAuthenticated
            ? config.Credentials[instanceNumber % config.Credentials.Count]
            : config.GetCredential(instanceNumber);
        _clientId = _credential == null
            ? $"anon-{instanceNumber:D2}"
            : $"auth-{instanceNumber:D2}-{_credential.Username}";
        _wsUrl = config.BaseUrl.Replace("http://", "ws://", StringComparison.OrdinalIgnoreCase)
            .Replace("https://", "wss://", StringComparison.OrdinalIgnoreCase);
    }

    public async Task LoginAsync(CancellationToken ct)
    {
        if (_credential == null)
            return;

        var body = JsonSerializer.Serialize(new { username = _credential.Username, password = _credential.Password });
        var response = await Http.PostAsync(
            $"{_config.BaseUrl}/api/auth/login",
            new StringContent(body, Encoding.UTF8, "application/json"),
            ct);

        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        _token = GetString(doc.RootElement, "token");
        _actorId = GetString(doc.RootElement, "actor_id");
        _controlledCountryId = GetString(doc.RootElement, "controlled_country_id");
    }

    public async Task LoadCountryAssignmentAsync(CancellationToken ct)
    {
        if (_controlledCountryId == null)
            return;

        var response = await Http.GetAsync($"{_config.BaseUrl}/api/world/provinces", ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        foreach (var province in doc.RootElement.EnumerateArray())
        {
            var ownerId = GetString(province, "owner_id");
            var provinceId = GetString(province, "id");
            if (ownerId == _controlledCountryId)
            {
                if (_provinceId == null)
                    _provinceId = provinceId;
                else if (_ownedMoveDestinationProvinceId == null)
                    _ownedMoveDestinationProvinceId = provinceId;
            }
            else if (_foreignCountryId == null && ownerId != null)
            {
                _foreignCountryId = ownerId;
            }
        }

        await LoadArmyAssignmentAsync(ct);
    }

    private async Task LoadArmyAssignmentAsync(CancellationToken ct)
    {
        if (_controlledCountryId == null)
            return;

        var response = await Http.GetAsync(
            $"{_config.BaseUrl}/api/world/armies?countryId={Uri.EscapeDataString(_controlledCountryId)}",
            ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));

        foreach (var army in doc.RootElement.EnumerateArray())
        {
            _armyId = GetString(army, "id");
            var location = GetString(army, "location_province_id");
            if (_ownedMoveDestinationProvinceId == null || _ownedMoveDestinationProvinceId == location)
                _ownedMoveDestinationProvinceId = _provinceId;

            if (_armyId != null)
                return;
        }
    }

    public async Task<Response<string>> TestStaleTokenAsync(CancellationToken ct)
    {
        if (_token == null)
            return Response.Ok<string>(payload: "skipped", statusCode: "skipped");

        _metrics.StaleTokenAttempts.Add(1);
        using var logout = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/api/auth/logout");
        logout.Headers.Add("Authorization", $"Bearer {_token}");
        await Http.SendAsync(logout, ct);

        using var staleRequest = new HttpRequestMessage(HttpMethod.Get, $"{_config.BaseUrl}/api/auth/me");
        staleRequest.Headers.Add("Authorization", $"Bearer {_token}");
        var staleResponse = await Http.SendAsync(staleRequest, ct);
        if (staleResponse.StatusCode != HttpStatusCode.Unauthorized)
            return Response.Fail<string>("unexpected stale-token response", statusCode: ((int)staleResponse.StatusCode).ToString());

        _metrics.StaleTokenRejected.Add(1);
        await LoginAsync(ct);
        return Response.Ok<string>(payload: "rejected", statusCode: "401");
    }

    public async Task<Response<string>> RunWebsocketAsync(
        TimeSpan duration,
        bool submitCommands,
        bool reconnectEnabled,
        CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + duration;
        var nextCommandAt = DateTime.UtcNow.AddSeconds(5 + Random.Shared.Next(5));
        var reconnectAt = reconnectEnabled
            ? DateTime.UtcNow.AddSeconds(Math.Min(15 + Random.Shared.Next(15), Math.Max(3, duration.TotalSeconds / 2)))
            : DateTime.MaxValue;
        var reconnectedOnce = false;

        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            _connectCount++;
            _metrics.WebsocketConnectAttempts.Add(1);
            if (_connectCount > 1)
                _metrics.ReconnectAttempts.Add(1);

            try
            {
                var endpoint = _token != null
                    ? $"{_wsUrl}/ws/world?token={Uri.EscapeDataString(_token)}"
                    : $"{_wsUrl}/ws/world";

                await ws.ConnectAsync(new Uri(endpoint), ct);
                _connectTime = DateTime.UtcNow;

                if (_connectCount > 1)
                {
                    _reconnectSuccessCount++;
                    _metrics.ReconnectSuccesses.Add(1);
                }

                await SubscribeAsync(ws, ct);

                var buffer = new byte[8192];
                while (ws.State == WebSocketState.Open && DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
                {
                    if (submitCommands && _token != null && DateTime.UtcNow >= nextCommandAt)
                    {
                        await SendGameplayCommandAsync(ct);
                        nextCommandAt = DateTime.UtcNow + _config.CommandInterval;
                    }

                    if (!reconnectedOnce && reconnectEnabled && DateTime.UtcNow >= reconnectAt)
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
                        var message = await ReceiveTextAsync(ws, buffer, readCts.Token);
                        if (message == null)
                            break;

                        HandleMessage(message);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // Receive timeout keeps command and reconnect timers moving.
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                _metrics.WebsocketConnectFailures.Add(1);
                _metrics.UnexpectedWebsocketErrors.Add(1);
                await Task.Delay(2_000, ct).ContinueWith(_ => { }, CancellationToken.None);
            }
        }

        if (submitCommands && _config.IsTwoPlayerSoak)
            return ValidateTwoPlayerSoakSession(reconnectEnabled);

        return Response.Ok<string>(payload: "completed");
    }

    private async Task SubscribeAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var topics = new List<string> { "world_summary", "country", "market" };
        if (_provinceId != null)
            topics.Add($"province:{_provinceId}");

        var body = JsonSerializer.Serialize(new { type = "subscribe", topics });
        await SendAsync(ws, body, ct);
        _metrics.SubscriptionMessagesSent.Add(1);
    }

    private async Task SendGameplayCommandAsync(CancellationToken ct)
    {
        if (_token == null || _controlledCountryId == null)
            return;

        var commandId = Guid.NewGuid().ToString();
        var idempotencyKey = $"load-{_clientId}-{commandId}";
        var body = BuildGameplayCommand(commandId, idempotencyKey);

        _commandsAttempted++;
        _metrics.CommandsSent.Add(1);
        if (_config.IsPeacefulCommandMix)
            _metrics.PeacefulCommandsSent.Add(1);

        RecordCommandResponse(await SendCommandRequestAsync(body, ct));

        if (!_config.DuplicateRetriesEnabled)
            return;

        _metrics.DuplicateRetries.Add(1);
        RecordCommandResponse(await SendCommandRequestAsync(body, ct));
    }

    private string BuildGameplayCommand(string commandId, string idempotencyKey)
    {
        if (_config.IsPeacefulCommandMix)
            return BuildPeacefulCommand(commandId, idempotencyKey);

        if (!_warCommandIssued && _foreignCountryId != null)
        {
            _warCommandIssued = true;
            return BuildTargetCountryCommand(commandId, idempotencyKey, "DeclareWar", _foreignCountryId);
        }

        if (_armyId != null && _ownedMoveDestinationProvinceId != null && Random.Shared.Next(100) < 35)
            return BuildMoveArmyCommand(commandId, idempotencyKey, _armyId, _ownedMoveDestinationProvinceId);

        if (_warCommandIssued && !_peaceCommandIssued && _foreignCountryId != null && Random.Shared.Next(100) < 20)
        {
            _peaceCommandIssued = true;
            return BuildTargetCountryCommand(commandId, idempotencyKey, "MakePeace", _foreignCountryId);
        }

        return BuildTaxCommand(commandId, idempotencyKey, Random.Shared.Next(5, 30));
    }

    private string BuildPeacefulCommand(string commandId, string idempotencyKey)
    {
        return Random.Shared.Next(3) switch
        {
            0 => BuildTaxCommand(commandId, idempotencyKey, Random.Shared.Next(5, 30)),
            1 => BuildStrataTaxCommand(
                commandId,
                idempotencyKey,
                Pick(["poor", "middle", "rich"]),
                Random.Shared.Next(5, 35)),
            _ => BuildSpendingCommand(
                commandId,
                idempotencyKey,
                Pick(["education", "military", "administration"]),
                Random.Shared.Next(20, 90))
        };
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

    private string BuildMoveArmyCommand(string commandId, string idempotencyKey, string armyId, string destinationProvinceId)
    {
        return JsonSerializer.Serialize(new
        {
            commandId,
            idempotencyKey,
            expectedWorldTick = _lastServerTick > 0 ? _lastServerTick : (long?)null,
            commandType = "MoveArmy",
            payload = new Dictionary<string, object>
            {
                ["armyId"] = armyId,
                ["destinationProvinceId"] = destinationProvinceId
            }
        });
    }

    private string BuildStrataTaxCommand(string commandId, string idempotencyKey, string strata, int rate)
    {
        return JsonSerializer.Serialize(new
        {
            commandId,
            idempotencyKey,
            expectedWorldTick = _lastServerTick > 0 ? _lastServerTick : (long?)null,
            commandType = "ChangeStrataTax",
            payload = new Dictionary<string, object>
            {
                ["countryId"] = _controlledCountryId ?? "",
                ["strata"] = strata,
                ["rate"] = rate
            }
        });
    }

    private string BuildSpendingCommand(string commandId, string idempotencyKey, string category, int level)
    {
        return JsonSerializer.Serialize(new
        {
            commandId,
            idempotencyKey,
            expectedWorldTick = _lastServerTick > 0 ? _lastServerTick : (long?)null,
            commandType = "ChangeSpending",
            payload = new Dictionary<string, object>
            {
                ["countryId"] = _controlledCountryId ?? "",
                ["category"] = category,
                ["level"] = level
            }
        });
    }

    private string BuildTargetCountryCommand(string commandId, string idempotencyKey, string commandType, string targetCountryId)
    {
        return JsonSerializer.Serialize(new
        {
            commandId,
            idempotencyKey,
            expectedWorldTick = _lastServerTick > 0 ? _lastServerTick : (long?)null,
            commandType,
            payload = new Dictionary<string, object>
            {
                ["targetCountryId"] = targetCountryId
            }
        });
    }

    private async Task<HttpStatusCode> SendCommandRequestAsync(string body, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_config.BaseUrl}/api/world/commands")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
            req.Headers.Add("Authorization", $"Bearer {_token}");
            var response = await Http.SendAsync(req, ct);
            return response.StatusCode;
        }
        catch
        {
            return 0;
        }
    }

    private void RecordCommandResponse(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        if (code >= 200 && code < 300)
            _metrics.CommandHttpAccepted.Add(1);
        else if (code is 401 or 403 or 409 or 422 or 429)
            _metrics.CommandHttpRejected.Add(1);
        else
        {
            _commandHttpErrorCount++;
            _metrics.CommandHttpErrored.Add(1);
        }
    }

    private void HandleMessage(string json)
    {
        var byteCount = Encoding.UTF8.GetByteCount(json);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var type = GetString(root, "type") ?? "unknown";

        _metrics.MessagesReceived.Add(1);
        _metrics.BytesReceived.Add(byteCount);

        if (type == "world_update" || type == "reconnect_snapshot")
        {
            if (type == "world_update")
            {
                _worldUpdateCount++;
                _metrics.WorldUpdates.Add(1);
            }
            else
            {
                _reconnectSnapshotCount++;
                _metrics.ReconnectSnapshots.Add(1);
            }

            var tick = GetLong(root, "tick");
            if (tick > 0)
            {
                _lastServerTick = tick;
                SetLastTick(tick);
            }

            RecordTickTiming();
        }
        else if (type == "market_update")
        {
            _metrics.MarketUpdates.Add(1);
            RecordMessageTick(root);
        }
        else if (type == "country_update")
        {
            _metrics.CountryUpdates.Add(1);
            RecordMessageTick(root);
        }
        else if (type == "subscribed")
        {
            _subscriptionAckCount++;
            _metrics.SubscriptionAcks.Add(1);
        }
        else if (type == "command_result")
        {
            _commandResultCount++;
            var status = GetString(root, "status");
            if (status == "applied")
                _metrics.CommandResultsApplied.Add(1);
            else if (status == "rejected")
                _metrics.CommandResultsRejected.Add(1);
            else if (status == "failed")
            {
                _commandResultFailedCount++;
                _metrics.CommandResultsFailed.Add(1);
            }
        }
    }

    private Response<string> ValidateTwoPlayerSoakSession(bool reconnectEnabled)
    {
        var failures = new List<string>();

        if (_token == null || _actorId == null || _controlledCountryId == null)
            failures.Add("authenticated player session was not established");
        if (_connectCount == 0)
            failures.Add("websocket never connected");
        if (_worldUpdateCount == 0)
            failures.Add("no world_update received");
        if (_subscriptionAckCount < (reconnectEnabled ? 2 : 1))
            failures.Add("missing subscription acknowledgements");
        if (reconnectEnabled && _reconnectSuccessCount == 0)
            failures.Add("reconnect did not succeed");
        if (reconnectEnabled && _reconnectSnapshotCount == 0)
            failures.Add("no reconnect_snapshot received");
        if (_commandsAttempted == 0)
            failures.Add("no gameplay commands were submitted");
        if (_commandResultCount == 0)
            failures.Add("no command_result received");
        if (_commandHttpErrorCount > 0)
            failures.Add($"{_commandHttpErrorCount} command HTTP infrastructure error(s)");
        if (_commandResultFailedCount > 0)
            failures.Add($"{_commandResultFailedCount} command_result failure(s)");

        return failures.Count == 0
            ? Response.Ok<string>(payload: "completed")
            : Response.Fail<string>(string.Join("; ", failures), statusCode: "failed");
    }

    private void RecordMessageTick(JsonElement root)
    {
        var tick = GetLong(root, "tick");
        if (tick > 0)
        {
            _lastServerTick = Math.Max(_lastServerTick, tick);
            SetLastTick(tick);
        }
    }

    private void RecordTickTiming()
    {
        var now = DateTime.UtcNow;
        if (_connectTime != DateTime.MinValue && !_recordedTimeToFirstWorldUpdate)
        {
            _recordedTimeToFirstWorldUpdate = true;
            _metrics.TimeToFirstWorldUpdateMs.Set((now - _connectTime).TotalMilliseconds);
        }

        if (_lastTickTime != DateTime.MinValue)
        {
            var interval = (now - _lastTickTime).TotalMilliseconds;
            _tickIntervalTotalMs += interval;
            _tickIntervalCount++;
            _maxTickIntervalMs = Math.Max(_maxTickIntervalMs, interval);
            _metrics.MeanTickIntervalMs.Set(_tickIntervalTotalMs / _tickIntervalCount);
            _metrics.MaxTickIntervalMs.Set(_maxTickIntervalMs);
        }

        _lastTickTime = now;
    }

    private void SetLastTick(long tick)
    {
        _lastTickMetricValue = Math.Max(_lastTickMetricValue, tick);
        _metrics.LastTickSeen.Set(_lastTickMetricValue);
    }

    private static async Task<string?> ReceiveTextAsync(ClientWebSocket ws, byte[] buffer, CancellationToken ct)
    {
        var message = new StringBuilder();
        WebSocketReceiveResult result;
        do
        {
            result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (result.MessageType == WebSocketMessageType.Text)
                message.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
        } while (!result.EndOfMessage);

        return message.Length == 0 ? null : message.ToString();
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

    private static string Pick(IReadOnlyList<string> values) => values[Random.Shared.Next(values.Count)];
}
