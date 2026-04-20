using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Services;

namespace VictoriaLike.Server.Api;

[ApiController]
[Route("api/explain")]
public sealed class ExplainController : ControllerBase
{
    private readonly IWorldExplanationService _explanations;

    public ExplainController(IWorldExplanationService explanations)
    {
        _explanations = explanations;
    }

    [HttpGet("good/{goodId}")]
    public async Task<ActionResult<ExplanationDto>> ExplainGood(string goodId, CancellationToken cancellationToken)
    {
        var explanation = await _explanations.ExplainGoodAsync(goodId, cancellationToken);
        return explanation == null ? NotFound() : Ok(explanation);
    }

    [HttpGet("pop/{popId}/needs")]
    public async Task<ActionResult<ExplanationDto>> ExplainPopNeeds(string popId, CancellationToken cancellationToken)
    {
        var explanation = await _explanations.ExplainPopNeedsAsync(popId, cancellationToken);
        return explanation == null ? NotFound() : Ok(explanation);
    }

    [HttpGet("province/{provinceId}/employment")]
    public async Task<ActionResult<ExplanationDto>> ExplainProvinceEmployment(string provinceId, CancellationToken cancellationToken)
    {
        var explanation = await _explanations.ExplainProvinceEmploymentAsync(provinceId, cancellationToken);
        return explanation == null ? NotFound() : Ok(explanation);
    }

    [HttpGet("country/{countryId}/budget")]
    public async Task<ActionResult<ExplanationDto>> ExplainCountryBudget(string countryId, CancellationToken cancellationToken)
    {
        var explanation = await _explanations.ExplainCountryBudgetAsync(countryId, cancellationToken);
        return explanation == null ? NotFound() : Ok(explanation);
    }

    [HttpGet("war/{warId}")]
    public async Task<ActionResult<ExplanationDto>> ExplainWar(string warId, CancellationToken cancellationToken)
    {
        var explanation = await _explanations.ExplainWarAsync(warId, cancellationToken);
        return explanation == null ? NotFound() : Ok(explanation);
    }

    [HttpGet("battle/{battleId}")]
    public async Task<ActionResult<ExplanationDto>> ExplainBattle(string battleId, CancellationToken cancellationToken)
    {
        var explanation = await _explanations.ExplainBattleAsync(battleId, cancellationToken);
        return explanation == null ? NotFound() : Ok(explanation);
    }
}
