using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;
using Serilog.Formatting.Compact;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using Npgsql;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Scenarios;
using VictoriaLike.Core.Simulation;
using VictoriaLike.Core.Simulation.Systems;
using VictoriaLike.Server.Auth;
using VictoriaLike.Server.Services;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Api;
using ICommandOutcomeRecorder = VictoriaLike.Core.Simulation.Systems.ICommandOutcomeRecorder;

var builder = WebApplication.CreateBuilder(args);

// Structured logging with Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
var configuration = builder.Configuration;
var dbConnectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is required");

// Register data services
builder.Services.AddSingleton<IMigrationRunner>(sp => new MigrationRunner(
    dbConnectionString,
    sp.GetRequiredService<ILogger<MigrationRunner>>()));

builder.Services.AddSingleton<IWorldStateRepository>(sp => new WorldStateRepository(
    dbConnectionString,
    sp.GetRequiredService<ILogger<WorldStateRepository>>()));

builder.Services.AddSingleton<IWorldStateDatabase>(sp => new WorldStateDatabase(
    dbConnectionString,
    sp.GetRequiredService<ILogger<WorldStateDatabase>>()));

builder.Services.AddSingleton<ICommandRepository>(sp => new CommandRepository(
    dbConnectionString,
    sp.GetRequiredService<ILogger<CommandRepository>>()));

builder.Services.AddSingleton<IScenarioLoader, ScenarioLoader>();
builder.Services.AddSingleton<IGoodsService, GoodsService>();

builder.Services.AddSingleton<IMarketHistoryService, MarketHistoryService>();
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ISessionRepository>(sp => new SessionRepository(
    dbConnectionString,
    sp.GetRequiredService<ILogger<SessionRepository>>()));

builder.Services.AddSingleton<IWorldInitializationService, WorldInitializationService>();

// Register command services
builder.Services.AddSingleton<ICommandQueueService, CommandQueueService>();
builder.Services.AddSingleton<ICommandBudgetService, CommandBudgetService>();
builder.Services.AddSingleton<ICommandHandler, ChangeTaxRateCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, QueueBuildingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, ChangeStrataTaxCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, ChangeSpendingCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, MoveArmyCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, DeclareWarCommandHandler>();
builder.Services.AddSingleton<ICommandHandler, MakePeaceCommandHandler>();
builder.Services.AddSingleton<IWorldWebSocketHub, WorldWebSocketHub>();
builder.Services.AddSingleton<ICommandOutcomeRecorder, CommandOutcomeRecorder>();
builder.Services.AddSingleton<IWorldSnapshotService, WorldSnapshotService>();
builder.Services.AddSingleton(sp => new CommandProcessingStage(
    sp.GetRequiredService<IEnumerable<ICommandHandler>>(),
    sp.GetRequiredService<ICommandOutcomeRecorder>()));

builder.Services.AddSingleton(new SimulationOrchestrator([
    new ArmyMovementStage(),
    new BattleResolutionStage(),
    new BuildingConstructionStage(),
    new EmploymentAssignmentStage(),
    new ProvinceProductionStage(),
    new FactoryProductionStage(),
    new ArtisanProductionStage(),
    new NationalDistributionStage(),
    new MarketPricingStage(),
    new PopNeedsStage(),
    new MonthlyPopUpdateStage(),
    new BudgetStage(),
]));
builder.Services.AddSingleton<TickExecutor>();

// Register API services
builder.Services.AddSingleton<IWorldQueryService, WorldQueryService>();
builder.Services.AddSingleton<IWorldExplanationService, WorldExplanationService>();
builder.Services.AddSingleton<IAdminInspectorService, AdminInspectorService>();
builder.Services.AddControllers();

// Bind TickOptions from existing config keys (Server:* and World:Snapshots:IntervalTicks)
builder.Services.AddSingleton<IConfigureOptions<TickOptions>>(sp => new ConfigureOptions<TickOptions>(opts =>
{
    opts.TickIntervalMs = configuration.GetValue<int>("Server:TickIntervalMs", 1000);
    opts.SaveIntervalTicks = configuration.GetValue<int>("Server:SaveIntervalTicks", 100);
    opts.SnapshotIntervalTicks = configuration.GetValue<int>("World:Snapshots:IntervalTicks", 25);
}));

// Register persistent world clock service
builder.Services.AddSingleton<IWorldClockService, PersistentWorldClockService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<IWorldClockService>());

builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database")
    .AddCheck<RedisHealthCheck>("redis", tags: new[] { "ready" });

var app = builder.Build();

Log.Information("Victoria World Server starting...");
Log.Information("Environment: {Environment}", app.Environment.EnvironmentName);

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// Health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        };
        await context.Response.WriteAsJsonAsync(response);
    }
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = (check) => check.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var response = new { ready = report.Status == HealthStatus.Healthy };
        await context.Response.WriteAsJsonAsync(response);
    }
});

// Map controllers (World API endpoints)
app.MapControllers();

app.Map("/ws/world", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket request.");
        return;
    }

    // Resolve actor identity: session token takes precedence over legacy actorId param
    string? resolvedActorId = null;
    var token = context.Request.Query["token"].ToString();
    if (!string.IsNullOrWhiteSpace(token))
    {
        var sessions = context.RequestServices.GetRequiredService<ISessionRepository>();
        var actorGuid = await sessions.ValidateSessionAsync(token, context.RequestAborted);
        if (actorGuid == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Invalid or expired session token.");
            return;
        }
        resolvedActorId = actorGuid.Value.ToString();
    }
    else
    {
        var legacyActorId = context.Request.Query["actorId"].ToString();
        if (!string.IsNullOrWhiteSpace(legacyActorId))
            resolvedActorId = legacyActorId;
    }

    // Build reconnect snapshot to send immediately on connect
    byte[]? initialMessage = null;
    if (resolvedActorId != null)
    {
        var clock = context.RequestServices.GetRequiredService<IWorldClockService>();
        var metrics = clock.CurrentMetrics;
        var snapshot = JsonSerializer.Serialize(new
        {
            type = "reconnect_snapshot",
            tick = metrics.TickCount,
            world_date = metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            actor_id = resolvedActorId
        });
        initialMessage = Encoding.UTF8.GetBytes(snapshot);
    }

    var socket = await context.WebSockets.AcceptWebSocketAsync();
    var hub = context.RequestServices.GetRequiredService<IWorldWebSocketHub>();
    await hub.RegisterAsync(socket, resolvedActorId, context.RequestAborted, initialMessage);
});

app.MapGet("/admin", () => Results.Content(AdminDashboardPage.Html, "text/html"));

// World clock endpoints (dev mode only)
if (app.Environment.EnvironmentName == "Development")
{
    app.MapPost("/dev/seed-passwords", async (IPasswordHasher hasher, System.Threading.CancellationToken ct) =>
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required");

        var seedSection = configuration.GetSection("Dev:SeedPasswords");
        var credentials = seedSection.GetChildren()
            .Where(c => !string.IsNullOrWhiteSpace(c.Value))
            .Select(c => (Username: c.Key, Password: c.Value!))
            .ToList();

        if (credentials.Count == 0)
            return Results.BadRequest(new { error = "No Dev:SeedPasswords configured" });

        var updated = new List<string>();
        using var conn = new Npgsql.NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        foreach (var (username, password) in credentials)
        {
            var hash = hasher.Hash(password);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE player_accounts SET password_hash = @h WHERE username = @u";
            cmd.Parameters.AddWithValue("@h", hash);
            cmd.Parameters.AddWithValue("@u", username);
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows > 0)
                updated.Add(username);
        }
        return Results.Ok(new { updated });
    }).WithName("SeedPasswords");

    app.MapGet("/dev/metrics", (IWorldClockService clock) =>
    {
        var metrics = clock.CurrentMetrics;
        return new
        {
            tick_count = metrics.TickCount,
            tick_duration_ms = metrics.TickDurationMs,
            world_timestamp = metrics.WorldTimestamp.ToString("yyyy-MM-dd"),
            tick_rate = metrics.TickRate,
            is_paused = clock.IsPaused
        };
    }).WithName("GetMetrics");

    app.MapPost("/dev/clock/pause", (IWorldClockService clock) =>
    {
        clock.Pause();
        return new { status = "paused" };
    }).WithName("PauseClock");

    app.MapPost("/dev/clock/resume", (IWorldClockService clock) =>
    {
        clock.Resume();
        return new { status = "resumed" };
    }).WithName("ResumeClock");
}

// Startup validation
try
{
    Log.Information("Validating dependencies...");

    // Test database
    var dbConnString = configuration.GetConnectionString("DefaultConnection");
    using (var testConn = new NpgsqlConnection(dbConnString))
    {
        await testConn.OpenAsync();
        Log.Information("✓ PostgreSQL connected successfully");
        await testConn.CloseAsync();
    }

    // Test Redis
    var redisConn = configuration.GetConnectionString("Redis");
    var options = ConfigurationOptions.Parse(redisConn);
    using (var redisConnection = await ConnectionMultiplexer.ConnectAsync(options))
    {
        if (redisConnection.IsConnected)
        {
            Log.Information("✓ Redis connected successfully");
        }
        else
        {
            throw new InvalidOperationException("Redis is not connected");
        }
    }

    // Run migrations
    var migrationRunner = app.Services.GetRequiredService<IMigrationRunner>();
    Log.Information("Running database migrations...");
    await migrationRunner.RunPendingMigrationsAsync();

    // Initialize world (seed if fresh, load if exists)
    var worldInit = app.Services.GetRequiredService<IWorldInitializationService>();
    Log.Information("Initializing world state...");
    await worldInit.InitializeWorldAsync();

    Log.Information("All dependencies validated. Starting server on port {Port}",
        configuration.GetValue<int>("Server:Port", 5001));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Server failed to start. Check database and Redis connectivity.");
    Environment.Exit(1);
}
finally
{
    Log.Information("Server shutdown");
    await Log.CloseAndFlushAsync();
}
