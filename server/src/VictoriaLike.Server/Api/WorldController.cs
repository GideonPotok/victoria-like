using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using VictoriaLike.Core.Domain;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Auth;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;

namespace VictoriaLike.Server.Api;

[ApiController]
[Route("api/world")]
public class WorldController : ControllerBase
{
    private readonly IWorldQueryService _queryService;
    private readonly ICommandQueueService _commandQueue;
    private readonly ICommandRepository _commandRepository;
    private readonly ISessionRepository _sessions;
    private readonly IWorldStateDatabase _worldDatabase;
    private readonly IWorldClockService _worldClock;
    private readonly ICommandBudgetService _commandBudget;
    private readonly ILogger<WorldController> _logger;

    public WorldController(
        IWorldQueryService queryService,
        ICommandQueueService commandQueue,
        ICommandRepository commandRepository,
        ISessionRepository sessions,
        IWorldStateDatabase worldDatabase,
        IWorldClockService worldClock,
        ICommandBudgetService commandBudget,
        ILogger<WorldController> logger)
    {
        _queryService = queryService;
        _commandQueue = commandQueue;
        _commandRepository = commandRepository;
        _sessions = sessions;
        _worldDatabase = worldDatabase;
        _worldClock = worldClock;
        _commandBudget = commandBudget;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<WorldSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _queryService.GetWorldSummaryAsync(cancellationToken);
        if (summary == null)
            return NotFound();

        return Ok(summary);
    }

    [HttpGet("countries")]
    public async Task<ActionResult<List<CountryDto>>> ListCountries(CancellationToken cancellationToken)
    {
        var countries = await _queryService.ListCountriesAsync(cancellationToken);
        return Ok(countries);
    }

    [HttpGet("provinces")]
    public async Task<ActionResult<List<ProvinceDto>>> ListProvinces(
        [FromQuery] string? owner = null,
        [FromQuery] string? sort = null,
        [FromQuery] string? order = null,
        CancellationToken cancellationToken = default)
    {
        var provinces = await _queryService.ListProvincesAsync(owner, sort, order, cancellationToken);
        return Ok(provinces);
    }

    [HttpGet("provinces/{provinceId}")]
    public async Task<ActionResult<ProvinceDetailDto>> GetProvinceDetail(string provinceId, CancellationToken cancellationToken)
    {
        var province = await _queryService.GetProvinceDetailAsync(provinceId, cancellationToken);
        if (province == null)
            return NotFound();

        return Ok(province);
    }

    [HttpGet("countries/{countryId}/inspect")]
    public async Task<ActionResult<CountryInspectionDto>> InspectCountry(string countryId, CancellationToken cancellationToken)
    {
        var inspection = await _queryService.GetCountryInspectionAsync(countryId, cancellationToken);
        if (inspection == null)
            return NotFound();

        return Ok(inspection);
    }

    [HttpGet("provinces/{provinceId}/inspect")]
    public async Task<ActionResult<ProvinceInspectionDto>> InspectProvince(string provinceId, CancellationToken cancellationToken)
    {
        var inspection = await _queryService.GetProvinceInspectionAsync(provinceId, cancellationToken);
        if (inspection == null)
            return NotFound();

        return Ok(inspection);
    }

    [HttpGet("countries/{countryId}/budget-preview")]
    public async Task<ActionResult<BudgetAdjustmentPreviewDto>> GetBudgetPreview(
        string countryId,
        [FromQuery] string kind,
        [FromQuery] string target,
        [FromQuery] decimal value,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(kind) || string.IsNullOrWhiteSpace(target))
            return BadRequest("kind and target are required");

        var preview = await _queryService.GetBudgetAdjustmentPreviewAsync(countryId, kind, target, value, cancellationToken);
        if (preview == null)
            return NotFound();

        return Ok(preview);
    }

    [HttpGet("provinces/{provinceId}/construction-options")]
    public async Task<ActionResult<List<ConstructionOptionPreviewDto>>> GetConstructionOptions(
        string provinceId,
        CancellationToken cancellationToken)
    {
        var options = await _queryService.GetConstructionOptionsAsync(provinceId, cancellationToken);
        return Ok(options);
    }

    [HttpPost("commands")]
    public async Task<ActionResult<CommandResponse>> SubmitCommand(
        [FromBody] SubmitCommandRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CommandType))
            return BadRequest("CommandType is required");

        if (request.ExpectedWorldTick is < 0)
            return BadRequest("ExpectedWorldTick must be non-negative");

        if (request.IdempotencyKey is { Length: > 128 })
            return BadRequest("IdempotencyKey must be 128 characters or fewer");

        // Gate 1: known command type
        if (!KnownCommandTypes.Contains(request.CommandType))
            return BadRequest($"Unknown command type '{request.CommandType}'. Valid: {string.Join(", ", KnownCommandTypes)}");

        // Gate 2: actor identity (prefer session token; request body actorId is dev-only fallback)
        ActorId actorId;
        var bearerToken = AuthController.ExtractBearerToken(Request);
        if (bearerToken != null)
        {
            var sessionActor = await _sessions.ValidateSessionAsync(bearerToken, cancellationToken);
            if (sessionActor == null)
                return Unauthorized("Invalid or expired session");
            actorId = new ActorId(sessionActor.Value);
        }
        else if (!string.IsNullOrWhiteSpace(request.ActorId))
        {
            try { actorId = ActorId.Parse(request.ActorId); }
            catch (FormatException) { return BadRequest("Invalid ActorId format"); }
        }
        else
        {
            return Unauthorized("Authorization: Bearer <token> required, or provide actorId in request body");
        }

        // Gate 3: actor must have a player account (controls a country)
        var playerAccount = await _worldDatabase.GetPlayerAccountAsync(actorId.Value, cancellationToken);
        if (playerAccount == null)
            return StatusCode(403, $"Actor {actorId} has no player account and cannot issue gameplay commands");

        CommandId commandId;
        if (!string.IsNullOrWhiteSpace(request.CommandId))
        {
            try { commandId = CommandId.Parse(request.CommandId); }
            catch (FormatException) { return BadRequest("Invalid CommandId format"); }
        }
        else
        {
            commandId = CommandId.New();
        }

        var now = DateTime.UtcNow;
        var submittedTick = _worldClock.CurrentMetrics.TickCount;
        var command = new CommandEnvelope(actorId, request.CommandType, request.Payload)
        {
            Id = commandId,
            IssuedAt = now,
            ReceivedAt = now,
            SubmittedTick = submittedTick,
            ExpectedWorldTick = request.ExpectedWorldTick,
            IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey,
            CountryId = playerAccount.ControlledCountry.Value,
            TargetIds = ExtractTargetIds(request.CommandType, request.Payload)
        };

        // Idempotency is checked before budgets so a safe client retry is not
        // rejected by cooldown/rate limits that were consumed by the original.
        var existingCommand = await _commandRepository.FindExistingCommandAsync(command, cancellationToken);
        if (existingCommand != null)
        {
            return Accepted(CreateCommandResponse(
                existingCommand.Command,
                existingCommand.Status,
                "Duplicate command ignored; returning existing command status.",
                rejectionReason: existingCommand.Status == "rejected" ? "DuplicateCommand" : null));
        }

        var budget = _commandBudget.Evaluate(
            actorId.Value,
            playerAccount.ControlledCountry.Value,
            request.CommandType,
            submittedTick,
            now);

        if (!budget.Accepted)
        {
            _logger.LogWarning(
                "Command rejected by budget actor={ActorId} country={CountryId} command_type={CommandType} reason={Reason}",
                actorId,
                playerAccount.ControlledCountry,
                request.CommandType,
                budget.Reason);

            return StatusCode(429, CreateCommandResponse(
                command,
                "rejected",
                budget.Reason,
                rejectionReason: budget.RetryAfterTicks.HasValue ? "StrategicCooldown" : "HardRateLimit",
                remainingInWindow: budget.RemainingInWindow,
                retryAfterTicks: budget.RetryAfterTicks,
                retryAfterSeconds: budget.RetryAfter?.TotalSeconds));
        }

        if (budget.SoftLimited)
        {
            _logger.LogInformation(
                "Command accepted after soft budget limit actor={ActorId} country={CountryId} command_type={CommandType} remaining={Remaining}",
                actorId,
                playerAccount.ControlledCountry,
                request.CommandType,
                budget.RemainingInWindow);
        }

        var enqueueResult = await _commandQueue.EnqueueAsync(command);
        if (!enqueueResult.IsDuplicate)
        {
            _commandBudget.RecordAccepted(
                actorId.Value,
                playerAccount.ControlledCountry.Value,
                request.CommandType,
                submittedTick,
                now);
        }

        return Accepted(CreateCommandResponse(
            enqueueResult.Command,
            enqueueResult.IsDuplicate ? enqueueResult.Status : budget.Status,
            GetAcceptedMessage(enqueueResult.IsDuplicate, budget),
            softLimited: budget.SoftLimited,
            remainingInWindow: budget.RemainingInWindow));
    }

    // Authoritative list of command types the pipeline can handle.
    // This is checked before enqueuing so unknown types are rejected immediately.
    private static readonly IReadOnlySet<string> KnownCommandTypes =
        new HashSet<string>(StringComparer.Ordinal) { "ChangeTaxRate", "QueueBuilding", "ChangeStrataTax", "ChangeSpending", "MoveArmy", "DeclareWar", "MakePeace" };

    private static CommandResponse CreateCommandResponse(
        CommandEnvelope command,
        string status,
        string? message,
        string? rejectionReason = null,
        bool softLimited = false,
        int? remainingInWindow = null,
        long? retryAfterTicks = null,
        double? retryAfterSeconds = null)
    {
        return new CommandResponse
        {
            CommandId = command.Id.ToString(),
            ActorId = command.ActorId.ToString(),
            CommandType = command.CommandType,
            IssuedAt = command.IssuedAt,
            ReceivedAt = command.ReceivedAt,
            SubmittedTick = command.SubmittedTick,
            ExpectedWorldTick = command.ExpectedWorldTick,
            IdempotencyKey = command.IdempotencyKey,
            Status = status,
            Message = string.IsNullOrWhiteSpace(message) ? GetDefaultMessage(status, command.CommandType) : message,
            RejectionReason = rejectionReason,
            SoftLimited = softLimited,
            RemainingInWindow = remainingInWindow,
            RetryAfterTicks = retryAfterTicks,
            RetryAfterSeconds = retryAfterSeconds
        };
    }

    private static string GetAcceptedMessage(bool isDuplicate, CommandBudgetDecision budget)
    {
        if (isDuplicate)
            return "Duplicate command ignored; returning existing command status.";
        if (!string.IsNullOrWhiteSpace(budget.Reason))
            return budget.Reason;
        if (budget.Status == "queued_soft_limited")
            return "Command queued after soft rate limiting.";
        return "Command accepted and queued.";
    }

    private static string GetDefaultMessage(string status, string commandType)
    {
        return status switch
        {
            "queued" => "Command accepted and queued.",
            "queued_soft_limited" => "Command queued after soft rate limiting.",
            "accepted" => "Command accepted.",
            "applied" => "Command applied.",
            "rejected" => $"{commandType} was rejected.",
            "failed" => $"{commandType} failed.",
            _ => $"{commandType} status: {status}"
        };
    }

    // Extract the primary target IDs from the payload for audit trail purposes.
    private static List<string> ExtractTargetIds(string commandType, Dictionary<string, object>? payload)
    {
        if (payload == null) return [];
        var targets = new List<string>();

        string? TryGetString(string key)
        {
            if (!payload.TryGetValue(key, out var val)) return null;
            if (val is System.Text.Json.JsonElement el && el.ValueKind == System.Text.Json.JsonValueKind.String)
                return el.GetString();
            return val?.ToString();
        }

        switch (commandType)
        {
            case "ChangeTaxRate":
            case "ChangeStrataTax":
            case "ChangeSpending":
                var cId = TryGetString("countryId");
                if (cId != null) targets.Add(cId);
                break;
            case "QueueBuilding":
                var pId = TryGetString("provinceId");
                if (pId != null) targets.Add(pId);
                break;
            case "MoveArmy":
                var aId = TryGetString("armyId");
                var destId = TryGetString("destinationProvinceId");
                if (aId != null) targets.Add(aId);
                if (destId != null) targets.Add(destId);
                break;
            case "DeclareWar":
            case "MakePeace":
                var targetCountryId = TryGetString("targetCountryId");
                if (targetCountryId != null) targets.Add(targetCountryId);
                break;
        }

        return targets;
    }

    [HttpGet("armies")]
    public async Task<ActionResult<List<ArmyStackDto>>> ListArmies(
        [FromQuery] string? countryId = null,
        CancellationToken cancellationToken = default)
    {
        var armies = await _queryService.ListArmiesAsync(countryId, cancellationToken);
        return Ok(armies);
    }

    [HttpGet("wars")]
    public async Task<ActionResult<List<WarDto>>> ListWars(CancellationToken cancellationToken)
    {
        var wars = await _queryService.ListWarsAsync(cancellationToken);
        return Ok(wars);
    }

    [HttpGet("buildings/queue")]
    public async Task<ActionResult<List<BuildingQueueItemDto>>> GetBuildingQueue(CancellationToken cancellationToken)
    {
        var queue = await _queryService.GetBuildingQueueAsync(cancellationToken);
        return Ok(queue);
    }

    [HttpGet("market")]
    public async Task<ActionResult<MarketSummaryDto>> GetMarketSummary(CancellationToken cancellationToken)
    {
        var summary = await _queryService.GetMarketSummaryAsync(cancellationToken);
        if (summary == null)
            return NotFound();

        return Ok(summary);
    }

    [HttpGet("events")]
    public async Task<ActionResult<List<WorldEventDto>>> GetEvents(
        [FromQuery] string? countryId = null,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        var events = await _queryService.GetEventFeedAsync(countryId, limit, cancellationToken);
        return Ok(events);
    }

    [HttpGet("commands")]
    public async Task<ActionResult<List<CommandHistoryDto>>> GetCommandHistory(
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var history = await _commandRepository.GetCommandHistoryAsync(limit, cancellationToken);

        var dtos = history.ConvertAll(h => new CommandHistoryDto
        {
            CommandId = h.CommandId,
            ActorId = h.ActorId,
            CommandType = h.CommandType,
            IssuedAt = h.IssuedAt,
            ReceivedAt = h.ReceivedAt,
            SubmittedTick = h.SubmittedTick,
            ExpectedWorldTick = h.ExpectedWorldTick,
            IdempotencyKey = h.IdempotencyKey,
            Status = h.Status,
            OutcomeStatus = h.OutcomeStatus,
            OutcomeReason = h.OutcomeReason,
            AppliedTick = h.AppliedTick,
            AppliedAt = h.AppliedAt
        });

        return Ok(dtos);
    }
}
