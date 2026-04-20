# Repository Guidelines

## Project Structure & Module Organization

This is a client-server grand strategy simulation. The authoritative simulation lives under `server/src`: `VictoriaLike.Core` contains deterministic domain and simulation logic, while `VictoriaLike.Server` hosts the ASP.NET Core API, WebSocket hub, persistence, auth, and health checks. Server content data is in `server/content`, migrations are in `server/migrations`, xUnit tests are in `server/tests/VictoriaLike.Core.Tests`, and the fake-client load harness is in `server/tests/VictoriaLike.LoadTest`. Unity client code is under `client-unity/Assets`; experiments also exist in `client-unity/v2` and `vic2`. Design and scope docs are in `docs/`.

## Build, Test, and Development Commands

- `make up`: starts local PostgreSQL and Redis via Docker Compose.
- `make down`: stops local services.
- `make test-connections`: verifies PostgreSQL and Redis connectivity.
- `dotnet build server/VictoriaLike.Server.sln`: builds core, server, and test projects.
- `dotnet test server/VictoriaLike.Server.sln`: runs the xUnit test suite.
- `dotnet run --project server/src/VictoriaLike.Server`: runs the API server locally, normally on `http://localhost:5001`.
- `dotnet run --project server/tests/VictoriaLike.LoadTest -- --url=http://localhost:5001 --clients=20 --duration=120`: runs the fake-client load harness.

## Coding Style & Naming Conventions

C# projects target `net10.0` with nullable reference types enabled. Use four-space indentation, PascalCase for public types and members, camelCase for locals and parameters, and `Async` suffixes for asynchronous methods. Match namespace style to nearby files. Keep gameplay rules in `VictoriaLike.Core`; the Unity client is presentation-only and the server validates player commands. Prefer small services and explicit DTOs over broad shared state.

## Testing Guidelines

Tests use xUnit. Add focused tests in `server/tests/VictoriaLike.Core.Tests` for simulation, command validation, loaders, invariants, and API-adjacent behavior. Name test files after the unit under test, for example `ScenarioLoaderTests.cs`, and use descriptive method names. Run `dotnet test server/VictoriaLike.Server.sln` before opening a PR; run the load harness when changing WebSocket, auth, reconnect, or command-processing behavior.

## Commit & Pull Request Guidelines

Recent history uses short conventional prefixes such as `feat:`, `fix:`, and `chore:`, often with day or milestone context. Keep commits scoped and imperative, for example `fix: reject stale command tokens`. PRs should include a concise summary, tests run, linked issue or planning doc when applicable, and screenshots or logs for Unity UI, admin tooling, soak, or load-test changes.

## Security & Configuration Tips

Do not commit local secrets. `.env.local` and development appsettings are for local services only. Treat PostgreSQL and Redis as required dependencies for full server runs, and keep generated `bin/`, `obj/`, Unity `Library/`, and soak output out of commits unless explicitly needed as evidence.
