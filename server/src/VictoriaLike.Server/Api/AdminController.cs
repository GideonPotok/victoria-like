using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Data;
using VictoriaLike.Server.Services;

namespace VictoriaLike.Server.Api;

[ApiController]
[Route("api/admin")]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminInspectorService _adminInspectorService;
    private readonly ICommandRepository _commandRepository;

    public AdminController(IAdminInspectorService adminInspectorService, ICommandRepository commandRepository)
    {
        _adminInspectorService = adminInspectorService;
        _commandRepository = commandRepository;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummaryDto>> GetSummary(CancellationToken cancellationToken)
    {
        var summary = await _adminInspectorService.GetSummaryAsync(cancellationToken);
        return Ok(summary);
    }

    [HttpPost("snapshots")]
    public async Task<ActionResult<AdminSnapshotDto>> CreateSnapshot(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CreateSnapshotRequest? request,
        CancellationToken cancellationToken)
    {
        var snapshot = await _adminInspectorService.CreateSnapshotAsync(request?.Name, cancellationToken);
        if (snapshot == null)
            return NotFound("World state is not initialized");

        return Ok(snapshot);
    }

    [HttpGet("market")]
    public async Task<ActionResult<AdminMarketInspectorDto>> GetMarketInspector(CancellationToken cancellationToken)
    {
        var inspector = await _adminInspectorService.GetMarketInspectorAsync(cancellationToken);
        return Ok(inspector);
    }

    [HttpGet("provinces/{provinceId}")]
    public async Task<ActionResult<AdminProvinceInspectorDto>> GetProvinceInspector(
        string provinceId,
        CancellationToken cancellationToken)
    {
        var inspector = await _adminInspectorService.GetProvinceInspectorAsync(provinceId, cancellationToken);
        if (inspector == null)
            return NotFound("Province not found");

        return Ok(inspector);
    }

    [HttpGet("countries/{countryId}")]
    public async Task<ActionResult<AdminCountryInspectorDto>> GetCountryInspector(
        string countryId,
        CancellationToken cancellationToken)
    {
        var inspector = await _adminInspectorService.GetCountryInspectorAsync(countryId, cancellationToken);
        if (inspector == null)
            return NotFound("Country not found");

        return Ok(inspector);
    }

    [HttpGet("tick-profile")]
    public ActionResult<AdminTickProfileDto> GetTickProfile()
    {
        return Ok(_adminInspectorService.GetTickProfile());
    }

    [HttpGet("commands")]
    public async Task<ActionResult<AdminCommandAuditDto>> GetCommandAudit(
        [FromQuery] string? actorId = null,
        [FromQuery] string? countryId = null,
        [FromQuery] string? commandType = null,
        [FromQuery] string? outcome = null,
        [FromQuery] long? fromTick = null,
        [FromQuery] long? toTick = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = new CommandAuditQuery
        {
            ActorId = actorId,
            CountryId = countryId,
            CommandType = commandType,
            OutcomeStatus = outcome,
            FromTick = fromTick,
            ToTick = toTick,
            Limit = limit
        };

        var records = await _commandRepository.QueryAuditAsync(query, cancellationToken);

        var dto = new AdminCommandAuditDto
        {
            Total = records.Count,
            Filters = new Dictionary<string, string?>
            {
                ["actor_id"] = actorId,
                ["country_id"] = countryId,
                ["command_type"] = commandType,
                ["outcome"] = outcome,
                ["from_tick"] = fromTick?.ToString(),
                ["to_tick"] = toTick?.ToString(),
                ["limit"] = limit.ToString()
            },
            Records = records.ConvertAll(r => new AdminCommandAuditRecordDto
            {
                CommandId = r.CommandId,
                ActorId = r.ActorId,
                CountryId = r.CountryId,
                CommandType = r.CommandType,
                TargetIds = r.TargetIds,
                SubmittedAt = r.SubmittedAt,
                SubmittedTick = r.SubmittedTick,
                ExpectedWorldTick = r.ExpectedWorldTick,
                IdempotencyKey = r.IdempotencyKey,
                ExecutedTick = r.ExecutedTick,
                ExecutedAt = r.ExecutedAt,
                Outcome = r.OutcomeStatus,
                OutcomeReason = r.OutcomeReason,
                RejectionReasonCode = r.RejectionReasonCode
            })
        };

        return Ok(dto);
    }
}
