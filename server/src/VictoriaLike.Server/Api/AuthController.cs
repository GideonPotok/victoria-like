using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VictoriaLike.Server.Api.Dtos;
using VictoriaLike.Server.Auth;
using VictoriaLike.Server.Data;

namespace VictoriaLike.Server.Api;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ISessionRepository _sessions;
    private readonly IPasswordHasher _hasher;
    private readonly IWorldStateDatabase _worldDb;

    public AuthController(ISessionRepository sessions, IPasswordHasher hasher, IWorldStateDatabase worldDb)
    {
        _sessions = sessions;
        _hasher = hasher;
        _worldDb = worldDb;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("username and password are required");

        var credentials = await _sessions.GetCredentialsAsync(request.Username, cancellationToken);
        if (credentials == null)
            return Unauthorized("Invalid username or password");

        var (actorId, passwordHash) = credentials.Value;

        if (string.IsNullOrEmpty(passwordHash) || !_hasher.Verify(request.Password, passwordHash))
            return Unauthorized("Invalid username or password");

        var token = await _sessions.CreateSessionAsync(actorId, cancellationToken);

        var world = await _worldDb.LoadWorldAsync(cancellationToken);
        var player = world?.Players.Find(p => p.Id.Value == actorId);

        return Ok(new LoginResponse
        {
            Token = token,
            ActorId = actorId.ToString(),
            Username = request.Username,
            ControlledCountryId = player?.ControlledCountry.Value.ToString() ?? string.Empty
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(Request);
        if (token != null)
            await _sessions.DeleteSessionAsync(token, cancellationToken);

        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<MeResponse>> Me(CancellationToken cancellationToken)
    {
        var token = ExtractBearerToken(Request);
        if (token == null)
            return Unauthorized("Authorization: Bearer <token> required");

        var actorId = await _sessions.ValidateSessionAsync(token, cancellationToken);
        if (actorId == null)
            return Unauthorized("Invalid or expired session");

        var world = await _worldDb.LoadWorldAsync(cancellationToken);
        var player = world?.Players.Find(p => p.Id.Value == actorId.Value);
        if (player == null)
            return NotFound("Player account not found");

        return Ok(new MeResponse
        {
            ActorId = actorId.Value.ToString(),
            Username = player.Username,
            ControlledCountryId = player.ControlledCountry.Value.ToString()
        });
    }

    internal static string? ExtractBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return header["Bearer ".Length..].Trim();
        return null;
    }
}
