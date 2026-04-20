using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using VictoriaLike.Core.Application.Commands;
using VictoriaLike.Core.Domain;

namespace VictoriaLike.Server.Data;

public interface ICommandRepository
{
    Task<CommandSaveResult> SaveCommandAsync(CommandEnvelope command, CancellationToken cancellationToken = default);
    Task<CommandSaveResult?> FindExistingCommandAsync(CommandEnvelope command, CancellationToken cancellationToken = default);
    Task<List<CommandEnvelope>> GetCommandsByStatusAsync(string status, CancellationToken cancellationToken = default);
    Task UpdateCommandStatusAsync(CommandId commandId, string status, CancellationToken cancellationToken = default);
    Task UpdateCommandOutcomeAsync(CommandId commandId, string outcomeStatus, string? outcomeReason, long appliedTick, CommandRejectionReason? rejectionReasonCode = null, CancellationToken cancellationToken = default);
    Task<List<CommandHistory>> GetCommandHistoryAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task<List<CommandAuditRecord>> QueryAuditAsync(CommandAuditQuery query, CancellationToken cancellationToken = default);
}

public sealed record CommandSaveResult
{
    public required bool Inserted { get; init; }
    public required CommandEnvelope Command { get; init; }
    public required string Status { get; init; }
}

public class CommandHistory
{
    public string CommandId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string CommandType { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; }
    public DateTime ReceivedAt { get; set; }
    public long SubmittedTick { get; set; }
    public long? ExpectedWorldTick { get; set; }
    public string? IdempotencyKey { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? OutcomeStatus { get; set; }
    public string? OutcomeReason { get; set; }
    public long? AppliedTick { get; set; }
    public DateTime? AppliedAt { get; set; }
}

public class CommandAuditRecord
{
    public string CommandId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string? CountryId { get; set; }
    public string CommandType { get; set; } = string.Empty;
    public List<string> TargetIds { get; set; } = [];
    public DateTime SubmittedAt { get; set; }
    public long SubmittedTick { get; set; }
    public long? ExpectedWorldTick { get; set; }
    public string? IdempotencyKey { get; set; }
    public long? ExecutedTick { get; set; }
    public DateTime? ExecutedAt { get; set; }
    public string OutcomeStatus { get; set; } = string.Empty;
    public string? OutcomeReason { get; set; }
    public string? RejectionReasonCode { get; set; }
}

public class CommandAuditQuery
{
    public string? ActorId { get; set; }
    public string? CountryId { get; set; }
    public string? CommandType { get; set; }
    public string? OutcomeStatus { get; set; }
    public long? FromTick { get; set; }
    public long? ToTick { get; set; }
    public int Limit { get; set; } = 100;
}

public class CommandRepository : ICommandRepository
{
    private readonly string _connectionString;
    private readonly ILogger<CommandRepository> _logger;

    public CommandRepository(string connectionString, ILogger<CommandRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<CommandSaveResult> SaveCommandAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO command_log (
                command_id, actor_id, command_type, payload, issued_at, received_at, status,
                country_id, target_ids, submitted_tick, expected_world_tick, idempotency_key)
            VALUES (
                @command_id, @actor_id, @command_type, @payload, @issued_at, @received_at, 'accepted',
                @country_id, @target_ids, @submitted_tick, @expected_world_tick, @idempotency_key)
            ON CONFLICT DO NOTHING
            RETURNING command_id, actor_id, command_type, payload, issued_at, received_at, status,
                      country_id, target_ids, submitted_tick, expected_world_tick, idempotency_key;";

        cmd.Parameters.AddWithValue("@command_id", command.Id.Value);
        cmd.Parameters.AddWithValue("@actor_id", command.ActorId.Value);
        cmd.Parameters.AddWithValue("@command_type", command.CommandType);
        cmd.Parameters.Add(new NpgsqlParameter("@payload", NpgsqlDbType.Jsonb)
        {
            Value = JsonSerializer.Serialize(command.Payload)
        });
        cmd.Parameters.AddWithValue("@issued_at", command.IssuedAt);
        cmd.Parameters.AddWithValue("@received_at", command.ReceivedAt);
        cmd.Parameters.Add(new NpgsqlParameter("@country_id", NpgsqlDbType.Uuid)
        {
            Value = command.CountryId.HasValue ? (object)command.CountryId.Value : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("@target_ids", NpgsqlDbType.Jsonb)
        {
            Value = command.TargetIds.Count > 0
                ? JsonSerializer.Serialize(command.TargetIds)
                : (object)DBNull.Value
        });
        cmd.Parameters.AddWithValue("@submitted_tick", command.SubmittedTick);
        cmd.Parameters.Add(new NpgsqlParameter("@expected_world_tick", NpgsqlDbType.Bigint)
        {
            Value = command.ExpectedWorldTick.HasValue ? (object)command.ExpectedWorldTick.Value : DBNull.Value
        });
        cmd.Parameters.Add(new NpgsqlParameter("@idempotency_key", NpgsqlDbType.Varchar)
        {
            Value = string.IsNullOrWhiteSpace(command.IdempotencyKey) ? DBNull.Value : command.IdempotencyKey
        });

        using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
        {
            if (await reader.ReadAsync(cancellationToken))
            {
                var insertedCommand = ReadCommandEnvelope(reader);
                _logger.LogDebug("Command persisted: {CommandId}", insertedCommand.Id);
                return new CommandSaveResult
                {
                    Inserted = true,
                    Command = insertedCommand,
                    Status = reader.GetString(6)
                };
            }
        }

        var existing = await FindExistingCommandAsync(connection, command, cancellationToken)
            ?? throw new InvalidOperationException($"Command {command.Id} conflicted during insert but existing row was not found");

        _logger.LogInformation(
            "Duplicate command ignored: submitted={SubmittedCommandId} existing={ExistingCommandId} actor={ActorId} idempotency_key={IdempotencyKey}",
            command.Id,
            existing.Command.Id,
            command.ActorId,
            command.IdempotencyKey);

        return existing with { Inserted = false };
    }

    public async Task<CommandSaveResult?> FindExistingCommandAsync(CommandEnvelope command, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var existing = await FindExistingCommandAsync(connection, command, cancellationToken);
        return existing == null ? null : existing with { Inserted = false };
    }

    public async Task<List<CommandEnvelope>> GetCommandsByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        var commands = new List<CommandEnvelope>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT command_id, actor_id, command_type, payload, issued_at, received_at, status,
                   country_id, target_ids, submitted_tick, expected_world_tick, idempotency_key
            FROM command_log
            WHERE status = @status
            ORDER BY submitted_tick ASC, received_at ASC, command_id ASC;";

        cmd.Parameters.AddWithValue("@status", status);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            commands.Add(ReadCommandEnvelope(reader));
        }

        return commands;
    }

    public async Task UpdateCommandStatusAsync(CommandId commandId, string status, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE command_log SET status = @status WHERE command_id = @command_id;";
        cmd.Parameters.AddWithValue("@status", status);
        cmd.Parameters.AddWithValue("@command_id", commandId.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateCommandOutcomeAsync(
        CommandId commandId,
        string outcomeStatus,
        string? outcomeReason,
        long appliedTick,
        CommandRejectionReason? rejectionReasonCode = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE command_log
            SET status = @outcome_status,
                result_reason = @outcome_reason,
                outcome_status = @outcome_status,
                outcome_reason = @outcome_reason,
                applied_at = CURRENT_TIMESTAMP,
                applied_tick = @applied_tick,
                rejection_reason_code = @rejection_reason_code
            WHERE command_id = @command_id;";

        cmd.Parameters.AddWithValue("@outcome_status", outcomeStatus);
        cmd.Parameters.Add(new NpgsqlParameter("@outcome_reason", NpgsqlDbType.Text) { Value = (object?)outcomeReason ?? DBNull.Value });
        cmd.Parameters.AddWithValue("@applied_tick", appliedTick);
        cmd.Parameters.Add(new NpgsqlParameter("@rejection_reason_code", NpgsqlDbType.Varchar)
        {
            Value = rejectionReasonCode.HasValue ? (object)rejectionReasonCode.Value.ToString() : DBNull.Value
        });
        cmd.Parameters.AddWithValue("@command_id", commandId.Value);

        await cmd.ExecuteNonQueryAsync(cancellationToken);
        _logger.LogDebug("Command outcome recorded: {CommandId} -> {Status}", commandId, outcomeStatus);
    }

    public async Task<List<CommandHistory>> GetCommandHistoryAsync(int limit = 100, CancellationToken cancellationToken = default)
    {
        var history = new List<CommandHistory>();

        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT command_id, actor_id, command_type, issued_at, received_at, submitted_tick,
                   expected_world_tick, idempotency_key, status,
                   outcome_status, outcome_reason, applied_tick, applied_at
            FROM command_log
            ORDER BY submitted_tick DESC, received_at DESC, command_id DESC
            LIMIT @limit;";

        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            history.Add(new CommandHistory
            {
                CommandId = reader.GetGuid(0).ToString(),
                ActorId = reader.GetGuid(1).ToString(),
                CommandType = reader.GetString(2),
                IssuedAt = reader.GetDateTime(3),
                ReceivedAt = reader.GetDateTime(4),
                SubmittedTick = reader.GetInt64(5),
                ExpectedWorldTick = reader.IsDBNull(6) ? null : reader.GetInt64(6),
                IdempotencyKey = reader.IsDBNull(7) ? null : reader.GetString(7),
                Status = reader.GetString(8),
                OutcomeStatus = reader.IsDBNull(9) ? null : reader.GetString(9),
                OutcomeReason = reader.IsDBNull(10) ? null : reader.GetString(10),
                AppliedTick = reader.IsDBNull(11) ? null : reader.GetInt64(11),
                AppliedAt = reader.IsDBNull(12) ? null : reader.GetDateTime(12)
            });
        }

        return history;
    }

    public async Task<List<CommandAuditRecord>> QueryAuditAsync(CommandAuditQuery query, CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = new StringBuilder(@"
            SELECT command_id, actor_id, country_id, command_type, target_ids,
                   issued_at, submitted_tick, expected_world_tick, idempotency_key,
                   applied_tick, applied_at,
                   COALESCE(outcome_status, status), outcome_reason, rejection_reason_code
            FROM command_log
            WHERE 1=1");

        using var cmd = connection.CreateCommand();

        if (query.ActorId != null && Guid.TryParse(query.ActorId, out var actorGuid))
        {
            sql.Append(" AND actor_id = @actor_id");
            cmd.Parameters.AddWithValue("@actor_id", actorGuid);
        }

        if (query.CountryId != null && Guid.TryParse(query.CountryId, out var countryGuid))
        {
            sql.Append(" AND country_id = @country_id");
            cmd.Parameters.AddWithValue("@country_id", countryGuid);
        }

        if (!string.IsNullOrWhiteSpace(query.CommandType))
        {
            sql.Append(" AND command_type = @command_type");
            cmd.Parameters.AddWithValue("@command_type", query.CommandType);
        }

        if (!string.IsNullOrWhiteSpace(query.OutcomeStatus))
        {
            sql.Append(" AND COALESCE(outcome_status, status) = @outcome_status");
            cmd.Parameters.AddWithValue("@outcome_status", query.OutcomeStatus);
        }

        if (query.FromTick.HasValue)
        {
            sql.Append(" AND submitted_tick >= @from_tick");
            cmd.Parameters.AddWithValue("@from_tick", query.FromTick.Value);
        }

        if (query.ToTick.HasValue)
        {
            sql.Append(" AND submitted_tick <= @to_tick");
            cmd.Parameters.AddWithValue("@to_tick", query.ToTick.Value);
        }

        sql.Append(" ORDER BY submitted_tick DESC, received_at DESC, command_id DESC LIMIT @limit");
        cmd.Parameters.AddWithValue("@limit", Math.Clamp(query.Limit, 1, 1000));

        cmd.CommandText = sql.ToString();

        var records = new List<CommandAuditRecord>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var targetIds = new List<string>();
            if (!reader.IsDBNull(4))
            {
                var json = reader.GetString(4);
                targetIds = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }

            records.Add(new CommandAuditRecord
            {
                CommandId = reader.GetGuid(0).ToString(),
                ActorId = reader.GetGuid(1).ToString(),
                CountryId = reader.IsDBNull(2) ? null : reader.GetGuid(2).ToString(),
                CommandType = reader.GetString(3),
                TargetIds = targetIds,
                SubmittedAt = reader.GetDateTime(5),
                SubmittedTick = reader.GetInt64(6),
                ExpectedWorldTick = reader.IsDBNull(7) ? null : reader.GetInt64(7),
                IdempotencyKey = reader.IsDBNull(8) ? null : reader.GetString(8),
                ExecutedTick = reader.IsDBNull(9) ? null : reader.GetInt64(9),
                ExecutedAt = reader.IsDBNull(10) ? null : reader.GetDateTime(10),
                OutcomeStatus = reader.GetString(11),
                OutcomeReason = reader.IsDBNull(12) ? null : reader.GetString(12),
                RejectionReasonCode = reader.IsDBNull(13) ? null : reader.GetString(13)
            });
        }

        return records;
    }

    private static CommandEnvelope ReadCommandEnvelope(NpgsqlDataReader reader)
    {
        var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(reader.GetString(3)) ?? new();
        var targetIds = new List<string>();
        if (!reader.IsDBNull(8))
            targetIds = JsonSerializer.Deserialize<List<string>>(reader.GetString(8)) ?? [];

        return new CommandEnvelope
        {
            Id = new CommandId(reader.GetGuid(0)),
            ActorId = new ActorId(reader.GetGuid(1)),
            CommandType = reader.GetString(2),
            Payload = payload,
            IssuedAt = reader.GetDateTime(4),
            ReceivedAt = reader.GetDateTime(5),
            CountryId = reader.IsDBNull(7) ? null : reader.GetGuid(7),
            TargetIds = targetIds,
            SubmittedTick = reader.GetInt64(9),
            ExpectedWorldTick = reader.IsDBNull(10) ? null : reader.GetInt64(10),
            IdempotencyKey = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }

    private static async Task<CommandSaveResult?> FindExistingCommandAsync(
        NpgsqlConnection connection,
        CommandEnvelope command,
        CancellationToken cancellationToken)
    {
        using var existingCmd = connection.CreateCommand();
        existingCmd.CommandText = @"
            SELECT command_id, actor_id, command_type, payload, issued_at, received_at, status,
                   country_id, target_ids, submitted_tick, expected_world_tick, idempotency_key
            FROM command_log
            WHERE (command_id = @command_id AND actor_id = @actor_id)
               OR (@idempotency_key IS NOT NULL AND actor_id = @actor_id AND idempotency_key = @idempotency_key)
            ORDER BY received_at ASC
            LIMIT 1;";
        existingCmd.Parameters.AddWithValue("@command_id", command.Id.Value);
        existingCmd.Parameters.AddWithValue("@actor_id", command.ActorId.Value);
        existingCmd.Parameters.Add(new NpgsqlParameter("@idempotency_key", NpgsqlDbType.Varchar)
        {
            Value = string.IsNullOrWhiteSpace(command.IdempotencyKey) ? DBNull.Value : command.IdempotencyKey
        });

        using var reader = await existingCmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new CommandSaveResult
        {
            Inserted = true,
            Command = ReadCommandEnvelope(reader),
            Status = reader.GetString(6)
        };
    }
}
