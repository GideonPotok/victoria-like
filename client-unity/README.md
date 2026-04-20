# Unity Client

Presentation-only Unity client for the Victoria-Like server.

The current client lives under [`v2/`](v2/) and is built for Unity 2023 LTS. Open the `v2` folder as a Unity project; the entry point is `My project/Assets/Scripts/Bootstrap.cs`.

## What It Does Today

- Connects to the local server at `http://localhost:5001`.
- Inspects country, province, POP groups, market prices, treasury, tax rate, RGO output, and factories via REST polling.
- Submits server-validated commands for tax changes, budget controls, and building queues (where the UI exposes them).

## Hard Rules (from `docs/architecture.md`)

- The server is the single source of truth. No authoritative gameplay logic lives here.
- Every player action becomes a server-validated command.
- Local UI state (selections, drafts, debounce, retry) is fine; authoritative simulation outcomes are not.

## Status

The client is the most under-built part of the project. WebSocket live updates are wired on the server but the client still uses REST polling for most views. See [docs/current_status.md](../docs/current_status.md) and [docs/status/pop_known_issues.md](../docs/status/pop_known_issues.md) (issue P7: `JsonUtility` can't deserialize dictionary fields) for current limitations.

## Running

1. Start the server: from the repo root, `make up && make run-albion`.
2. Open `client-unity/v2` in Unity 2023 LTS.
3. Press Play. The Bootstrap script should connect to `http://localhost:5001`.

Unity build output (`Library/`, `Logs/`, `UserSettings/`) is gitignored.
