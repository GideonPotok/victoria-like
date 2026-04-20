using System;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VictoriaLike.Server.Auth;

public interface ISessionRepository
{
    Task<(Guid actorId, string passwordHash)?> GetCredentialsAsync(string username, CancellationToken ct = default);
    Task<string> CreateSessionAsync(Guid actorId, CancellationToken ct = default);
    Task<Guid?> ValidateSessionAsync(string token, CancellationToken ct = default);
    Task DeleteSessionAsync(string token, CancellationToken ct = default);
    Task<int> CountActiveSessionsAsync(CancellationToken ct = default);
}

public class SessionRepository : ISessionRepository
{
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(24);

    private readonly string _connectionString;
    private readonly ILogger<SessionRepository> _logger;

    public SessionRepository(string connectionString, ILogger<SessionRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<(Guid actorId, string passwordHash)?> GetCredentialsAsync(string username, CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT actor_id, password_hash FROM player_accounts WHERE username = @username;";
        cmd.Parameters.AddWithValue("@username", username);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return (reader.GetGuid(0), reader.GetString(1));
    }

    public async Task<string> CreateSessionAsync(Guid actorId, CancellationToken ct = default)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
        var now = DateTime.UtcNow;
        var expires = now.Add(SessionLifetime);

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO sessions (token, actor_id, created_at, expires_at, last_activity_at)
            VALUES (@token, @actor_id, @now, @expires, @now);";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@actor_id", actorId);
        cmd.Parameters.AddWithValue("@now", now);
        cmd.Parameters.AddWithValue("@expires", expires);

        await cmd.ExecuteNonQueryAsync(ct);
        _logger.LogDebug("Session created for actor {ActorId}, expires {Expires}", actorId, expires);
        return token;
    }

    public async Task<Guid?> ValidateSessionAsync(string token, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return null;

        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE sessions
            SET last_activity_at = @now
            WHERE token = @token AND expires_at > @now
            RETURNING actor_id;";
        cmd.Parameters.AddWithValue("@token", token);
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

        var result = await cmd.ExecuteScalarAsync(ct);
        if (result is Guid actorId)
            return actorId;

        return null;
    }

    public async Task DeleteSessionAsync(string token, CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM sessions WHERE token = @token;";
        cmd.Parameters.AddWithValue("@token", token);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<int> CountActiveSessionsAsync(CancellationToken ct = default)
    {
        using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sessions WHERE expires_at > @now;";
        cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }
}
