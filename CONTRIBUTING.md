# Contributing

Victoria-Like is early, but contributions are welcome when they make the simulation easier to run, inspect, test, explain, or mod.

## Good First Areas

- Scenario and content data in `server/content`.
- Documentation for mechanics, setup, and modding.
- Focused xUnit tests around simulation stages and invariants.
- Unity UI polish for inspection and command flows.
- Explanation text that helps players understand market, POP, and budget changes.

For a curated list of scoped starter tasks with file pointers and acceptance criteria, see [docs/good-first-issues.md](docs/good-first-issues.md). The bigger picture lives in [ROADMAP.md](ROADMAP.md).

## Local Setup

```bash
make up
dotnet build server/VictoriaLike.Server.sln
dotnet test server/VictoriaLike.Server.sln
make run-albion
```

The server normally runs on `http://localhost:5001`.

## Coding Guidelines

- Keep deterministic gameplay logic in `server/src/VictoriaLike.Core`.
- Keep API, persistence, WebSocket, auth, and health checks in `server/src/VictoriaLike.Server`.
- Keep Unity presentation-only; server commands are authoritative.
- Use nullable-aware C# with four-space indentation.
- Use PascalCase for public types and members, camelCase for locals and parameters, and `Async` suffixes for async methods.
- Add focused tests when changing simulation behavior, command validation, loaders, invariants, or API-adjacent DTO behavior.

## Pull Requests

Include:

- A short summary of the change.
- Tests run.
- Any known limitations.
- Screenshots or short clips for Unity UI changes when useful.

Avoid committing local secrets, generated build output, Unity `Library/`, scratch files, crash dumps, or local logs.

## Issue Labels

Useful labels for public triage:

- `good first issue`
- `content`
- `simulation`
- `Unity UI`
- `docs`
- `scenario data`
- `Vic2 research`
- `economy balancing`
- `tests`
- `architecture`
