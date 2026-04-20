# Day 16: WebSocket Realtime Channel — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Unity client's manual polling with a live WebSocket channel — the server pushes a `world_update` message after every tick, and a `command_result` message to the submitting actor when a command outcome is recorded.

**Architecture:** A new `WorldWebSocketHub` singleton owns all active WebSocket connections; `PersistentWorldClockService` calls it after each tick; `CommandOutcomeRecorder` calls it when an outcome is written. The Unity `WorldWebSocketClient` MonoBehaviour receives frames on a background thread, dispatches to the main thread via `ConcurrentQueue<Action>`, and reconnects with exponential backoff.

**Tech Stack:** ASP.NET Core built-in WebSocket support (`app.UseWebSockets()`), `System.Net.WebSockets.ClientWebSocket` (Unity standalone), xunit for server build verification.

---

## File Map

| Action | Path | Responsibility |
|--------|------|----------------|
| Create | `server/src/VictoriaLike.Server/Services/WorldWebSocketHub.cs` | Hub: connection registry, broadcast, targeted send |
| Modify | `server/src/VictoriaLike.Core/Simulation/Systems/CommandProcessingStage.cs` | Add `actorId` to `ICommandOutcomeRecorder` interface + usages |
| Modify | `server/src/VictoriaLike.Server/Services/CommandOutcomeRecorder.cs` | Inject hub, send command result after DB write |
| Modify | `server/src/VictoriaLike.Server/Services/PersistentWorldClockService.cs` | Inject hub, broadcast after each tick |
| Modify | `server/src/VictoriaLike.Server/Program.cs` | `UseWebSockets`, map `/ws/world`, register hub singleton |
| Create | `client-unity/Assets/Scripts/Api/WorldWebSocketClient.cs` | Unity MonoBehaviour: connect, receive, reconnect, dispatch |
| Modify | `client-unity/Assets/Scripts/Bootstrap.cs` | Find WS client, pass reference to UI components |
| Modify | `client-unity/Assets/Scripts/UI/WorldUIManager.cs` | Subscribe to `OnWorldUpdate` event |
| Modify | `client-unity/Assets/Scripts/UI/ProvinceListUI.cs` | Subscribe to `OnWorldUpdate` → refresh province list |

---

## Task 1: Create `WorldWebSocketHub`

**Files:**
- Create: `server/src/VictoriaLike.Server/Services/WorldWebSocketHub.cs`

- [ ] **Step 1: Create the hub file**

```csharp
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    Task BroadcastWorldUpdateAsync(long tick, DateTime worldDate);
    Task SendCommandResultAsync(string actorId, string commandId, string status, string? reason);
    Task RegisterAsync(WebSocket socket, string? actorId, CancellationToken cancellationToken);
}

public class WorldWebSocketHub : IWorldWebSocketHub
{
    private readonly ILogger<WorldWebSocketHub> _logger;
    private readonly ConcurrentDictionary<WebSocket, string?> _connections = new();

    public WorldWebSocketHub(ILogger<WorldWebSocketHub> logger)
    {
        _logger = logger;
    }

    public async Task BroadcastWorldUpdateAsync(long tick, DateTime worldDate)
    {
        if (_connections.IsEmpty)
            return;

        var message = JsonSerializer.Serialize(new
        {
            type = "world_update",
            tick,
            world_date = worldDate.ToString("yyyy-MM-dd")
        });
        await SendToAllAsync(message);
    }

    public async Task SendCommandResultAsync(string actorId, string commandId, string status, string? reason)
    {
        var message = JsonSerializer.Serialize(new
        {
            type = "command_result",
            actor_id = actorId,
            command_id = commandId,
            status,
            reason
        });

        var targets = _connections
            .Where(kv => kv.Value == actorId && kv.Key.State == WebSocketState.Open)
            .Select(kv => kv.Key)
            .ToList();

        if (targets.Count == 0)
            return;

        var bytes = Encoding.UTF8.GetBytes(message);
        await Task.WhenAll(targets.Select(s => SendFrameAsync(s, bytes)));
    }

    public async Task RegisterAsync(WebSocket socket, string? actorId, CancellationToken cancellationToken)
    {
        _connections.TryAdd(socket, actorId);
        _logger.LogInformation("WebSocket connected: actor={Actor}, total={Count}",
            actorId ?? "anonymous", _connections.Count);

        try
        {
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed", CancellationToken.None);
                    break;
                }
                // pong messages are silently accepted — no action needed
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

    private async Task SendToAllAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        var dead = new List<WebSocket>();

        foreach (var (socket, _) in _connections)
        {
            if (socket.State != WebSocketState.Open)
            {
                dead.Add(socket);
                continue;
            }
            try
            {
                await SendFrameAsync(socket, bytes);
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Send failed for socket, removing: {Error}", ex.Message);
                dead.Add(socket);
            }
        }

        foreach (var socket in dead)
            _connections.TryRemove(socket, out _);
    }

    private static Task SendFrameAsync(WebSocket socket, byte[] bytes) =>
        socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            endOfMessage: true,
            CancellationToken.None);
}
```

- [ ] **Step 2: Verify it compiles**

```bash
cd server && dotnet build src/VictoriaLike.Server/VictoriaLike.Server.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Run existing tests to confirm nothing broke**

```bash
cd server && dotnet test tests/VictoriaLike.Core.Tests/
```

Expected: All existing tests pass.

- [ ] **Step 4: Commit**

```bash
git add server/src/VictoriaLike.Server/Services/WorldWebSocketHub.cs
git commit -m "feat: add WorldWebSocketHub for WebSocket connection management"
```

---

## Task 2: Update `ICommandOutcomeRecorder` to carry `actorId`

**Files:**
- Modify: `server/src/VictoriaLike.Core/Simulation/Systems/CommandProcessingStage.cs`

The `ICommandOutcomeRecorder` interface currently has:
```csharp
Task RecordOutcomeAsync(CommandId commandId, string outcomeStatus, string? reason, long appliedTick);
```

`CommandProcessingStage` already holds `command.ActorId` but doesn't pass it. We add it here.

- [ ] **Step 1: Update the interface and all call sites in `CommandProcessingStage.cs`**

Replace the entire file content:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Application.Logging;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation.TickPipeline;

namespace VictoriaLike.Core.Simulation.Systems;

public interface ICommandOutcomeRecorder
{
    Task RecordOutcomeAsync(CommandId commandId, ActorId actorId, string outcomeStatus, string? reason, long appliedTick);
}

public class CommandProcessingStage : ISimulationStage
{
    public string Name => "CommandProcessing";

    private readonly Dictionary<string, ICommandHandler> _handlers;
    private readonly ICommandOutcomeRecorder? _outcomeRecorder;

    public CommandProcessingStage(IEnumerable<ICommandHandler> handlers, ICommandOutcomeRecorder? outcomeRecorder = null)
    {
        _handlers = handlers.ToDictionary(h => h.CommandType);
        _outcomeRecorder = outcomeRecorder;
    }

    public void Execute(SimulationContext context)
    {
        // placeholder — server calls ProcessCommandsAsync directly
    }

    public async Task ProcessCommandsAsync(List<CommandEnvelope> commands, SimulationContext context, long currentTick)
    {
        foreach (var command in commands)
        {
            if (!_handlers.TryGetValue(command.CommandType, out var handler))
            {
                context.Log.LogCommandFailure(command.Id.ToString(), "Unknown command type");
                if (_outcomeRecorder != null)
                    await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, "rejected", "Unknown command type", currentTick);
                continue;
            }

            var result = handler.Handle(command, context.World, command.ActorId);

            if (result.IsSuccess)
            {
                context.Log.LogCommandSuccess(command.Id.ToString(), command.CommandType);
                if (_outcomeRecorder != null)
                    await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, "applied", null, currentTick);
            }
            else
            {
                context.Log.LogCommandFailure(command.Id.ToString(), result.ErrorMessage ?? "Unknown error");
                if (_outcomeRecorder != null)
                    await _outcomeRecorder.RecordOutcomeAsync(command.Id, command.ActorId, "rejected", result.ErrorMessage, currentTick);
            }
        }
    }
}
```

- [ ] **Step 2: Verify compilation**

```bash
cd server && dotnet build src/VictoriaLike.Core/VictoriaLike.Core.csproj
```

Expected: `Build succeeded.` (Server will fail until Task 3 fixes the recorder — that's OK.)

- [ ] **Step 3: Commit**

```bash
git add server/src/VictoriaLike.Core/Simulation/Systems/CommandProcessingStage.cs
git commit -m "feat: add actorId to ICommandOutcomeRecorder interface"
```

---

## Task 3: Update `CommandOutcomeRecorder` to inject hub and send results

**Files:**
- Modify: `server/src/VictoriaLike.Server/Services/CommandOutcomeRecorder.cs`

- [ ] **Step 1: Replace `CommandOutcomeRecorder.cs`**

```csharp
using System.Threading.Tasks;
using VictoriaLike.Core.Domain;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Services;

public class CommandOutcomeRecorder : ICommandOutcomeRecorder
{
    private readonly ICommandRepository _repository;
    private readonly IWorldWebSocketHub _hub;

    public CommandOutcomeRecorder(ICommandRepository repository, IWorldWebSocketHub hub)
    {
        _repository = repository;
        _hub = hub;
    }

    public async Task RecordOutcomeAsync(CommandId commandId, ActorId actorId, string outcomeStatus, string? reason, long appliedTick)
    {
        await _repository.UpdateCommandOutcomeAsync(commandId, outcomeStatus, reason, appliedTick);
        await _hub.SendCommandResultAsync(
            actorId.ToString(),
            commandId.ToString(),
            outcomeStatus,
            reason);
    }
}
```

- [ ] **Step 2: Verify the full server builds**

```bash
cd server && dotnet build src/VictoriaLike.Server/VictoriaLike.Server.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Run all tests**

```bash
cd server && dotnet test tests/VictoriaLike.Core.Tests/
```

Expected: All pass.

- [ ] **Step 4: Commit**

```bash
git add server/src/VictoriaLike.Server/Services/CommandOutcomeRecorder.cs
git commit -m "feat: CommandOutcomeRecorder sends targeted WS command_result via hub"
```

---

## Task 4: Update `PersistentWorldClockService` to broadcast on each tick

**Files:**
- Modify: `server/src/VictoriaLike.Server/Services/PersistentWorldClockService.cs`

- [ ] **Step 1: Add `IWorldWebSocketHub` to the constructor**

Find the constructor block (lines 72–91) and add the hub field + parameter:

```csharp
    private readonly IWorldWebSocketHub _webSocketHub;
```

Add to constructor parameters:

```csharp
    public PersistentWorldClockService(
        ILogger<PersistentWorldClockService> logger,
        IConfiguration configuration,
        IWorldStateRepository stateRepository,
        ICommandQueueService commandQueue,
        IWorldStateDatabase worldStateDb,
        CommandProcessingStage commandProcessingStage,
        ICommandOutcomeRecorder outcomeRecorder,
        IWorldWebSocketHub webSocketHub)
    {
        _logger = logger;
        _configuration = configuration;
        _stateRepository = stateRepository;
        _commandQueue = commandQueue;
        _worldStateDb = worldStateDb;
        _commandProcessingStage = commandProcessingStage;
        _outcomeRecorder = outcomeRecorder;
        _webSocketHub = webSocketHub;
        _tickIntervalMs = configuration.GetValue<int>("Server:TickIntervalMs", 1000);
        _saveIntervalTicks = configuration.GetValue<int>("Server:SaveIntervalTicks", 100);
        _stoppingCts = new CancellationTokenSource();
    }
```

- [ ] **Step 2: Add broadcast call at the end of `ExecuteTickAsync`**

Find `ExecuteTickAsync`. After the periodic saves block (but before the method ends), add:

```csharp
        // Broadcast live update to all connected WebSocket clients
        await _webSocketHub.BroadcastWorldUpdateAsync(_tickCount, _worldTimestamp);
```

The full `ExecuteTickAsync` method after the change:

```csharp
    private async Task ExecuteTickAsync(CancellationToken cancellationToken)
    {
        lock (_metricsLock)
        {
            _tickCount++;
            _worldTimestamp = _worldTimestamp.AddDays(1);
            _ticksSinceLastSave++;
        }

        await ProcessPendingCommandsAsync(cancellationToken);

        var metrics = CurrentMetrics;

        _logger.LogDebug(
            "Tick {TickCount} - World: {Date}, Duration: {DurationMs}ms, Rate: {TickRate} ticks/sec",
            metrics.TickCount,
            metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            metrics.TickDurationMs,
            string.Format("{0:F2}", metrics.TickRate));

        if (_tickCount % 10 == 0)
        {
            _logger.LogInformation(
                "Tick {TickCount} checkpoint - Running {Elapsed}ms, World: {Date}",
                metrics.TickCount,
                metrics.TickDurationMs,
                metrics.WorldTimestamp.ToString("yyyy-MM-dd"));
        }

        if (_ticksSinceLastSave >= _saveIntervalTicks)
        {
            await SaveWorldStateAsync(cancellationToken);
            _ticksSinceLastSave = 0;
        }

        await _webSocketHub.BroadcastWorldUpdateAsync(_tickCount, _worldTimestamp);
    }
```

- [ ] **Step 3: Verify build**

```bash
cd server && dotnet build src/VictoriaLike.Server/VictoriaLike.Server.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add server/src/VictoriaLike.Server/Services/PersistentWorldClockService.cs
git commit -m "feat: broadcast world_update WebSocket message after each tick"
```

---

## Task 5: Wire up server — `Program.cs` and DI registration

**Files:**
- Modify: `server/src/VictoriaLike.Server/Program.cs`

- [ ] **Step 1: Register `WorldWebSocketHub` as singleton**

In `Program.cs`, after the line `builder.Services.AddSingleton<IWorldQueryService, WorldQueryService>();`, add:

```csharp
builder.Services.AddSingleton<IWorldWebSocketHub, WorldWebSocketHub>();
```

- [ ] **Step 2: Enable WebSocket middleware**

In `Program.cs`, immediately after `var app = builder.Build();` (before any `app.Map*` calls), add:

```csharp
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});
```

- [ ] **Step 3: Map the `/ws/world` endpoint**

After `app.MapControllers();`, add:

```csharp
app.Map("/ws/world", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var actorId = context.Request.Query["actorId"].FirstOrDefault();
    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var hub = context.RequestServices.GetRequiredService<IWorldWebSocketHub>();
    await hub.RegisterAsync(socket, actorId, context.RequestAborted);
});
```

- [ ] **Step 4: Final build and test**

```bash
cd server && dotnet build src/VictoriaLike.Server/VictoriaLike.Server.csproj && dotnet test tests/VictoriaLike.Core.Tests/
```

Expected: Build succeeded, all tests pass.

- [ ] **Step 5: Manual smoke test**

Start the server:
```bash
cd server && dotnet run --project src/VictoriaLike.Server/
```

In a second terminal, connect with `websocat` (or `wscat`) and observe tick messages:
```bash
websocat ws://localhost:5001/ws/world
```

Expected output every ~1 second:
```json
{"type":"world_update","tick":1,"world_date":"1800-01-02"}
{"type":"world_update","tick":2,"world_date":"1800-01-03"}
```

- [ ] **Step 6: Commit**

```bash
git add server/src/VictoriaLike.Server/Program.cs
git commit -m "feat: register WorldWebSocketHub and map /ws/world endpoint"
```

---

## Task 6: Create `WorldWebSocketClient` Unity MonoBehaviour

**Files:**
- Create: `client-unity/Assets/Scripts/Api/WorldWebSocketClient.cs`

- [ ] **Step 1: Create the file**

```csharp
using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VictoriaLike.Client.Api
{
    [Serializable]
    public class WsWorldUpdateData
    {
        public long tick;
        public string world_date;
    }

    [Serializable]
    public class WsCommandResultData
    {
        public string actor_id;
        public string command_id;
        public string status;
        public string reason;
    }

    public class WorldWebSocketClient : MonoBehaviour
    {
        [SerializeField] private string serverUrl = "ws://localhost:5001/ws/world";
        [SerializeField] public string actorId = "";

        public event Action<WsWorldUpdateData> OnWorldUpdate;
        public event Action<WsCommandResultData> OnCommandResult;

        private readonly ConcurrentQueue<Action> _mainThreadQueue = new();
        private CancellationTokenSource _cts;

        private static readonly int[] BackoffSeconds = { 2, 4, 8, 16, 30 };

        private void Start()
        {
            _cts = new CancellationTokenSource();
            _ = ConnectWithRetryAsync(_cts.Token);
        }

        private void Update()
        {
            while (_mainThreadQueue.TryDequeue(out var action))
                action();
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
        }

        private async Task ConnectWithRetryAsync(CancellationToken cancellationToken)
        {
            int attempt = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var url = string.IsNullOrEmpty(actorId)
                        ? serverUrl
                        : $"{serverUrl}?actorId={Uri.EscapeDataString(actorId)}";

                    using var ws = new ClientWebSocket();
                    await ws.ConnectAsync(new Uri(url), cancellationToken);

                    attempt = 0;
                    Enqueue(() => Debug.Log($"[WS] Connected to {url}"));

                    await ReceiveLoopAsync(ws, cancellationToken);
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex)
                {
                    var delay = BackoffSeconds[Math.Min(attempt, BackoffSeconds.Length - 1)];
                    Enqueue(() => Debug.LogWarning($"[WS] Disconnected ({ex.Message}), retrying in {delay}s"));

                    try { await Task.Delay(delay * 1000, cancellationToken); }
                    catch (OperationCanceledException) { return; }

                    attempt++;
                }
            }
        }

        private async Task ReceiveLoopAsync(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            var sb = new StringBuilder();

            while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                sb.Clear();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                while (!result.EndOfMessage);

                ProcessMessage(sb.ToString());
            }
        }

        private void ProcessMessage(string json)
        {
            try
            {
                var envelope = JsonUtility.FromJson<WsEnvelope>(json);
                if (envelope == null) return;

                switch (envelope.type)
                {
                    case "world_update":
                        var update = JsonUtility.FromJson<WsWorldUpdateData>(json);
                        Enqueue(() => OnWorldUpdate?.Invoke(update));
                        break;
                    case "command_result":
                        var cmd = JsonUtility.FromJson<WsCommandResultData>(json);
                        Enqueue(() => OnCommandResult?.Invoke(cmd));
                        break;
                }
            }
            catch (Exception ex)
            {
                Enqueue(() => Debug.LogWarning($"[WS] Failed to parse message: {ex.Message}\n{json}"));
            }
        }

        private void Enqueue(Action action) => _mainThreadQueue.Enqueue(action);

        [Serializable]
        private class WsEnvelope { public string type; }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add client-unity/Assets/Scripts/Api/WorldWebSocketClient.cs
git commit -m "feat: add WorldWebSocketClient MonoBehaviour with reconnect backoff"
```

---

## Task 7: Wire up UI to WebSocket events

**Files:**
- Modify: `client-unity/Assets/Scripts/Bootstrap.cs`
- Modify: `client-unity/Assets/Scripts/UI/WorldUIManager.cs`
- Modify: `client-unity/Assets/Scripts/UI/ProvinceListUI.cs`

- [ ] **Step 1: Replace `Bootstrap.cs`**

```csharp
using UnityEngine;
using VictoriaLike.Client.Api;
using VictoriaLike.Client.UI;

namespace VictoriaLike.Client
{
    public class Bootstrap : MonoBehaviour
    {
        [SerializeField] private WorldWebSocketClient wsClient;
        [SerializeField] private WorldUIManager worldUIManager;
        [SerializeField] private ProvinceListUI provinceListUI;

        private void Start()
        {
            Debug.Log("Victoria-Like Client Starting...");

            if (wsClient == null)
                wsClient = FindObjectOfType<WorldWebSocketClient>();

            if (worldUIManager != null && wsClient != null)
                worldUIManager.ConnectWebSocket(wsClient);

            if (provinceListUI != null && wsClient != null)
                provinceListUI.ConnectWebSocket(wsClient);
        }
    }
}
```

- [ ] **Step 2: Replace `WorldUIManager.cs`**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    public class WorldUIManager : MonoBehaviour
    {
        private IWorldApiClient _apiClient;

        private void Start()
        {
            _apiClient = new WorldApiClient("http://localhost:5001");
            _ = RefreshWorldDataAsync();
        }

        public void ConnectWebSocket(WorldWebSocketClient wsClient)
        {
            wsClient.OnWorldUpdate += data =>
            {
                Debug.Log($"[WS] World tick {data.tick} — {data.world_date}");
            };
        }

        public async Task RefreshWorldDataAsync()
        {
            try
            {
                var summary = await _apiClient.GetWorldSummaryAsync();
                Debug.Log($"World Summary: Tick {summary.tick}, Date {summary.world_date}");

                var countries = await _apiClient.ListCountriesAsync();
                Debug.Log($"Loaded {countries.Count} countries");

                var provinces = await _apiClient.ListProvincesAsync();
                Debug.Log($"Loaded {provinces.Count} provinces");

                DisplayWorldData(summary, countries, provinces);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error fetching world data: {ex.Message}");
            }
        }

        private void DisplayWorldData(
            WorldSummaryData summary,
            List<CountryData> countries,
            List<ProvinceData> provinces)
        {
            var output = new System.Text.StringBuilder();

            output.AppendLine("=== WORLD STATE ===");
            output.AppendLine($"Tick: {summary.tick}");
            output.AppendLine($"Date: {summary.world_date}");
            output.AppendLine($"Countries: {summary.country_count}");
            output.AppendLine($"Provinces: {summary.province_count}");
            output.AppendLine($"Markets: {summary.market_count}");
            output.AppendLine();

            output.AppendLine("=== COUNTRIES ===");
            foreach (var country in countries)
            {
                output.AppendLine($"[{country.tag}] {country.name}");
                output.AppendLine($"  Tax Rate: {country.tax_rate}%");
                output.AppendLine($"  Provinces: {country.province_count}");
            }
            output.AppendLine();

            output.AppendLine("=== PROVINCES ===");
            foreach (var province in provinces)
            {
                output.AppendLine($"{province.name}");
                output.AppendLine($"  Owner: {province.owner_name}");
                output.AppendLine($"  Population: {province.population}");
            }

            Debug.Log(output.ToString());
        }
    }
}
```

- [ ] **Step 3: Replace `ProvinceListUI.cs` with the updated version**

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using VictoriaLike.Client.Api;

namespace VictoriaLike.Client.UI
{
    public class ProvinceListUI : MonoBehaviour
    {
        [SerializeField] private Transform provincesContainer;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button provincePrefab;
        [SerializeField] private Text loadingText;

        private IWorldApiClient _apiClient;
        private List<ProvinceData> _provinces;
        public event Action<ProvinceData> OnProvinceSelected;

        private void Start()
        {
            _apiClient = new WorldApiClient("http://localhost:5001");
            _provinces = new List<ProvinceData>();

            if (refreshButton != null)
                refreshButton.onClick.AddListener(() => _ = RefreshProvincesAsync());

            _ = RefreshProvincesAsync();
        }

        public void ConnectWebSocket(WorldWebSocketClient wsClient)
        {
            wsClient.OnWorldUpdate += _ => _ = RefreshProvincesAsync();
        }

        public async Task RefreshProvincesAsync()
        {
            try
            {
                if (loadingText != null)
                    loadingText.text = "Loading provinces...";

                _provinces = await _apiClient.ListProvincesAsync();

                UpdateProvinceList();

                if (loadingText != null)
                    loadingText.text = "";

                Debug.Log($"Loaded {_provinces.Count} provinces");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error loading provinces: {ex.Message}");
                if (loadingText != null)
                    loadingText.text = $"Error: {ex.Message}";
            }
        }

        private void UpdateProvinceList()
        {
            foreach (Transform child in provincesContainer)
            {
                if (child.gameObject != provincePrefab.gameObject)
                    Destroy(child.gameObject);
            }

            foreach (var province in _provinces)
            {
                var button = Instantiate(provincePrefab, provincesContainer);
                button.gameObject.SetActive(true);

                var text = button.GetComponentInChildren<Text>();
                if (text != null)
                    text.text = $"{province.name} ({province.owner_name}) - Pop: {province.population}";

                button.onClick.AddListener(() => SelectProvince(province));
            }
        }

        private void SelectProvince(ProvinceData province)
        {
            Debug.Log($"Selected province: {province.name}");
            OnProvinceSelected?.Invoke(province);
        }
    }
}
```

- [ ] **Step 4: Commit**

```bash
git add client-unity/Assets/Scripts/Bootstrap.cs \
        client-unity/Assets/Scripts/UI/WorldUIManager.cs \
        client-unity/Assets/Scripts/UI/ProvinceListUI.cs
git commit -m "feat: wire UI to WebSocket events — live tick and province refresh"
```

---

## Task 8: Unity Scene Setup (manual)

The `WorldWebSocketClient` and `Bootstrap` need to be wired in the Unity scene. These steps are done in the Unity Editor.

- [ ] **Step 1:** In the Unity scene hierarchy, add an empty GameObject named `WebSocketClient` and attach the `WorldWebSocketClient` script to it.

- [ ] **Step 2:** Select the `Bootstrap` GameObject. In the Inspector, assign the `WorldWebSocketClient`, `WorldUIManager`, and `ProvinceListUI` references in the `Bootstrap` component's serialized fields.

- [ ] **Step 3:** Press Play. In the Unity Console, verify:
  - `[WS] Connected to ws://localhost:5001/ws/world` appears
  - `[WS] World tick N — 1800-xx-xx` logs appear every ~1 second
  - Stopping the server and restarting causes the client to log a reconnect warning and then reconnect

- [ ] **Step 4:** Kill the server mid-session. Verify the console shows:
  ```
  [WS] Disconnected (...), retrying in 2s
  [WS] Connected to ws://localhost:5001/ws/world    ← after restart
  ```

- [ ] **Step 5:** Commit scene changes from Unity (if your project commits `.unity` files).

---

## Verification Checklist

- [ ] `dotnet build` passes cleanly after all server tasks
- [ ] `dotnet test` — all existing Core tests still pass
- [ ] `websocat ws://localhost:5001/ws/world` receives `world_update` messages every tick
- [ ] Submitting a command and connecting with `?actorId=<id>` causes a `command_result` frame to arrive
- [ ] Unity client logs `[WS] Connected` on startup
- [ ] Unity console shows live tick logs without manual refresh
- [ ] Killing and restarting the server causes the Unity client to reconnect automatically
