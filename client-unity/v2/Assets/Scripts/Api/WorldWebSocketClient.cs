using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VictoriaLike.Client.Api
{
    public enum WsConnectionState { Disconnected, Connecting, Connected, Reconnecting }

    public class MarketUpdateData
    {
        public long Tick;
        public Dictionary<string, float> Prices = new();
        public Dictionary<string, float> Supply = new();
        public Dictionary<string, float> Demand = new();
    }

    public class CountryUpdateData
    {
        public long Tick;
        public string CountryId;
        public int TaxRate;
        public float Treasury;
    }

    public class CommandResultData
    {
        public string CommandId;
        public string CommandType;
        public string Status;
        public string Message;
        public string Reason;
        public string RejectionReason;
        public int RetryAfterTicks;
    }

    public class WorldWebSocketClient : MonoBehaviour
    {
        private const string DefaultServerUrl = "ws://localhost:5001";
        private const int ReconnectDelayMs = 3000;
        private const int ReceiveBufferSize = 16384;

        [SerializeField] private string serverUrl = DefaultServerUrl;

        public WsConnectionState ConnectionState { get; private set; } = WsConnectionState.Disconnected;
        public long LastTickSeen { get; private set; }
        public string LastWorldDate { get; private set; } = "—";
        public Dictionary<string, float> CurrentMarketPrices { get; private set; } = new();

        public event Action<MarketUpdateData> OnMarketUpdate;
        public event Action<CountryUpdateData> OnCountryUpdate;
        public event Action<CommandResultData> OnCommandResult;
        public event Action<WsConnectionState> OnConnectionStateChanged;

        private CancellationTokenSource _cts;
        private readonly ConcurrentQueue<string> _incomingMessages = new();

        private void Start()
        {
            if (PlayerSession.IsLoggedIn)
                Connect();
        }

        private void Update()
        {
            while (_incomingMessages.TryDequeue(out var msg))
                HandleMessage(msg);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
        }

        public void Connect()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            _ = ConnectLoopAsync(_cts.Token);
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            SetState(WsConnectionState.Disconnected);
        }

        private async Task ConnectLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                SetState(WsConnectionState.Connecting);
                ClientWebSocket ws = null;
                try
                {
                    ws = new ClientWebSocket();
                    var wsUrl = $"{serverUrl}/ws/world?token={PlayerSession.Token}";
                    await ws.ConnectAsync(new Uri(wsUrl), ct);
                    SetState(WsConnectionState.Connected);

                    await SendTextAsync(ws, @"{""type"":""subscribe"",""topics"":[""market"",""country""]}", ct);

                    await ReceiveLoopAsync(ws, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[WS] Disconnected: {ex.Message}. Reconnecting in {ReconnectDelayMs}ms...");
                }
                finally
                {
                    try { ws?.Dispose(); } catch { }
                }

                if (!ct.IsCancellationRequested)
                {
                    SetState(WsConnectionState.Reconnecting);
                    await Task.Delay(ReconnectDelayMs, ct).ContinueWith(_ => { });
                }
            }

            SetState(WsConnectionState.Disconnected);
        }

        private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct)
        {
            var buffer = new byte[ReceiveBufferSize];
            var messageBuilder = new StringBuilder();

            while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                messageBuilder.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    if (result.MessageType == WebSocketMessageType.Text)
                        messageBuilder.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                if (messageBuilder.Length > 0)
                    _incomingMessages.Enqueue(messageBuilder.ToString());
            }
        }

        private void HandleMessage(string json)
        {
            var type = ExtractStringField(json, "type");
            if (string.IsNullOrEmpty(type))
                return;

            switch (type)
            {
                case "reconnect_snapshot":
                case "world_update":
                {
                    var tick = ExtractLongField(json, "tick");
                    var date = ExtractStringField(json, "world_date");
                    if (tick > 0) LastTickSeen = tick;
                    if (!string.IsNullOrEmpty(date)) LastWorldDate = date;
                    break;
                }

                case "market_update":
                {
                    var data = new MarketUpdateData
                    {
                        Tick = ExtractLongField(json, "tick"),
                        Prices = ParseFloatDict(json, "prices"),
                        Supply = ParseFloatDict(json, "supply"),
                        Demand = ParseFloatDict(json, "demand")
                    };
                    CurrentMarketPrices = data.Prices;
                    if (data.Tick > 0) LastTickSeen = data.Tick;
                    OnMarketUpdate?.Invoke(data);
                    break;
                }

                case "country_update":
                {
                    var data = new CountryUpdateData
                    {
                        Tick = ExtractLongField(json, "tick"),
                        CountryId = ExtractStringField(json, "country_id"),
                        TaxRate = (int)ExtractLongField(json, "tax_rate"),
                        Treasury = ExtractFloatField(json, "treasury")
                    };
                    if (data.Tick > 0) LastTickSeen = data.Tick;
                    OnCountryUpdate?.Invoke(data);
                    break;
                }

                case "command_result":
                {
                    OnCommandResult?.Invoke(new CommandResultData
                    {
                        CommandId = ExtractStringField(json, "command_id"),
                        CommandType = ExtractStringField(json, "command_type"),
                        Status = ExtractStringField(json, "status"),
                        Message = ExtractStringField(json, "message"),
                        Reason = ExtractStringField(json, "reason"),
                        RejectionReason = ExtractStringField(json, "rejection_reason"),
                        RetryAfterTicks = (int)ExtractLongField(json, "retry_after_ticks")
                    });
                    break;
                }

                case "subscribed":
                    Debug.Log($"[WS] Subscribed: {ExtractStringField(json, "topics")}");
                    break;
            }
        }

        private void SetState(WsConnectionState state)
        {
            if (ConnectionState == state) return;
            ConnectionState = state;
            OnConnectionStateChanged?.Invoke(state);
        }

        private static async Task SendTextAsync(ClientWebSocket ws, string message, CancellationToken ct)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
        }

        // --- Minimal JSON field extractors (avoids JsonUtility dict limitation) ---

        private static string ExtractStringField(string json, string key)
        {
            var search = $"\"{key}\":\"";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return null;
            start += search.Length;
            var end = json.IndexOf('"', start);
            return end >= 0 ? json.Substring(start, end - start) : null;
        }

        private static long ExtractLongField(string json, string key)
        {
            var search = $"\"{key}\":";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return 0;
            start += search.Length;
            while (start < json.Length && json[start] == ' ') start++;
            var end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-')) end++;
            return end > start && long.TryParse(json.Substring(start, end - start), out var v) ? v : 0;
        }

        private static float ExtractFloatField(string json, string key)
        {
            var search = $"\"{key}\":";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return 0f;
            start += search.Length;
            while (start < json.Length && json[start] == ' ') start++;
            var end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '-' || json[end] == '.')) end++;
            return end > start && float.TryParse(json.Substring(start, end - start),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
        }

        private static Dictionary<string, float> ParseFloatDict(string json, string key)
        {
            var result = new Dictionary<string, float>();
            var search = $"\"{key}\":{{";
            var start = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return result;
            start += search.Length;
            var end = json.IndexOf('}', start);
            if (end < 0) return result;

            var content = json.Substring(start, end - start);
            foreach (var pair in content.Split(','))
            {
                var colon = pair.IndexOf(':');
                if (colon < 0) continue;
                var k = pair.Substring(0, colon).Trim().Trim('"');
                var valStr = pair.Substring(colon + 1).Trim();
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v))
                    result[k] = v;
            }
            return result;
        }
    }
}
