# Day 16: WebSocket Realtime Channel

**Date:** 2026-04-21  
**Status:** Approved

## Goal

Move the Unity client from a pull/polling model to a push model. The server broadcasts world state after each tick and sends targeted command results to the submitting actor. The client receives live updates without manual refresh.

## Scope

- One WebSocket endpoint: `/ws/world`
- Two message types pushed by the server: `world_update`, `command_result`
- One keepalive message type: `ping` (server → client), `pong` (client → server)
- Unity client: reconnect-safe receive loop, main-thread dispatcher, events for UI

Out of scope: authentication, actor identity negotiation beyond passing actorId as a query param, binary frames, compression.

---

## Server Architecture

### `IWorldWebSocketHub` / `WorldWebSocketHub`

Singleton service. Owns the set of active connections.

```
IWorldWebSocketHub
  BroadcastWorldUpdateAsync(long tick, DateTime worldDate) → Task
  SendCommandResultAsync(string actorId, string commandId, string status, string? reason) → Task
  RegisterAsync(WebSocket socket, string? actorId) → Task  // blocks until socket closes
```

Internal state:
- `ConcurrentDictionary<WebSocket, string?>` — socket → actorId (null = anonymous)
- On send failure, remove the socket silently

### Message format

All frames are UTF-8 JSON text frames.

```json
{ "type": "world_update", "tick": 142, "world_date": "1800-05-02" }
{ "type": "command_result", "actor_id": "...", "command_id": "...", "status": "applied", "reason": null }
{ "type": "ping", "ts": 1713654321 }
```

`world_update` carries only what the clock service already holds in memory (tick + date). Clients use this as a "something changed" signal and re-fetch details via REST if needed. This avoids a DB query per tick.

Client replies to `ping` with:
```json
{ "type": "pong", "ts": 1713654321 }
```

### Endpoint registration (`Program.cs`)

```
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
app.Map("/ws/world", async context => {
    if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
    var actorId = context.Request.Query["actorId"].FirstOrDefault();
    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var hub = context.RequestServices.GetRequiredService<IWorldWebSocketHub>();
    await hub.RegisterAsync(socket, actorId);
});
```

### Changes to existing services

**`PersistentWorldClockService`**
- Constructor gains `IWorldWebSocketHub`
- After `ExecuteTickAsync` completes, call `hub.BroadcastWorldUpdateAsync(tickCount, worldTimestamp)` using data already held in memory — no extra DB query

**`ICommandOutcomeRecorder`** (Core interface)
- Add `actorId` parameter: `RecordOutcomeAsync(CommandId, ActorId, string status, string? reason, long tick)`
- `CommandProcessingStage` already has `command.ActorId` and passes it through

**`CommandOutcomeRecorder`** (Server impl)
- Constructor gains `IWorldWebSocketHub`
- After writing outcome to DB, call `hub.SendCommandResultAsync(actorId, commandId, status, reason)`

### Broadcast cadence

Every tick (default 1s). Configurable via `Server:WsBroadcastEveryNTicks` (default 1). Keeps it simple for now.

---

## Unity Client Architecture

### `WorldWebSocketClient` (MonoBehaviour)

Responsibilities:
- Connect on `Start()`, reconnect on disconnect
- Receive loop runs on background thread via `Task.Run`
- Messages enqueued into `ConcurrentQueue<string>`
- `Update()` drains queue on main thread, fires typed events
- `OnDestroy()` cancels and closes cleanly

```csharp
public event Action<WorldSummaryData> OnWorldUpdate;
public event Action<CommandResultData> OnCommandResult;
```

Reconnect backoff: 2s, 4s, 8s, 16s, 30s (capped). Resets to 2s on successful connection.

Actor identity: passed as query param `?actorId=<id>` when known. Anonymous connection receives only `world_update` messages.

### Main-thread dispatch

`ConcurrentQueue<Action>` drained in `Update()`. Background receive loop enqueues lambdas that parse JSON and invoke events. This is the standard Unity pattern for background → main thread handoff.

### UI wiring

**`WorldUIManager`**
- Subscribes to `WorldWebSocketClient.OnWorldUpdate`
- Updates world summary display without polling

**`ProvinceListUI`**
- Subscribes to `OnWorldUpdate` → calls `RefreshProvincesAsync()` to re-fetch province list from REST
- Manual refresh button remains

**`Bootstrap`**
- Instantiates or finds `WorldWebSocketClient` and passes reference to UI components

---

## Error Handling

| Scenario | Behavior |
|---|---|
| WS connection refused at startup | Log warning, retry with backoff |
| Socket closed mid-session | Log, reconnect with backoff |
| Malformed JSON from server | Log and skip message |
| Send fails (client disconnected) | Hub removes socket silently |
| Server shutdown | Close all sockets with status 1001 (going away) |

---

## Files Created / Modified

### Server
- **New:** `server/src/VictoriaLike.Server/Services/WorldWebSocketHub.cs`
- **Modified:** `server/src/VictoriaLike.Server/Program.cs` — add `UseWebSockets`, map `/ws/world`, register hub
- **Modified:** `server/src/VictoriaLike.Server/Services/PersistentWorldClockService.cs` — inject hub, broadcast on tick
- **Modified:** `server/src/VictoriaLike.Server/Services/CommandOutcomeRecorder.cs` — inject hub, send result

### Client
- **New:** `client-unity/Assets/Scripts/Api/WorldWebSocketClient.cs`
- **Modified:** `client-unity/Assets/Scripts/Bootstrap.cs` — wire up WS client
- **Modified:** `client-unity/Assets/Scripts/UI/WorldUIManager.cs` — subscribe to WS events
- **Modified:** `client-unity/Assets/Scripts/UI/ProvinceListUI.cs` — subscribe to WS events
