using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VictoriaLike.Server.Data;

public class WorldState
{
    public long TickNumber { get; set; }
    public DateTime WorldTimestamp { get; set; }
    public DateTime LastSavedAt { get; set; }
}

public interface IWorldStateRepository
{
    Task<WorldState?> LoadLatestAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(WorldState state, CancellationToken cancellationToken = default);
}

public class WorldStateRepository : IWorldStateRepository
{
    private readonly string _connectionString;
    private readonly ILogger<WorldStateRepository> _logger;

    public WorldStateRepository(string connectionString, ILogger<WorldStateRepository> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
    }

    public async Task<WorldState?> LoadLatestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT tick_number, world_timestamp, last_saved_at FROM world_state ORDER BY id DESC LIMIT 1;";

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new WorldState
                {
                    TickNumber = reader.GetInt64(0),
                    WorldTimestamp = reader.GetDateTime(1),
                    LastSavedAt = reader.GetDateTime(2)
                };
            }

            _logger.LogInformation("No persisted world state found (fresh start)");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading world state from database");
            throw;
        }
    }

    public async Task SaveAsync(WorldState state, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Delete old row (singleton pattern)
            using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = "DELETE FROM world_state;";
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);

            // Insert new state
            using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT INTO world_state (tick_number, world_timestamp, last_saved_at)
                VALUES (@tick, @timestamp, CURRENT_TIMESTAMP)
                RETURNING last_saved_at;";

            insertCommand.Parameters.AddWithValue("@tick", state.TickNumber);
            insertCommand.Parameters.AddWithValue("@timestamp", state.WorldTimestamp);

            var savedAt = await insertCommand.ExecuteScalarAsync(cancellationToken);
            _logger.LogDebug("World state saved: tick {Tick}, timestamp {Timestamp}",
                state.TickNumber, state.WorldTimestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving world state to database");
            throw;
        }
    }
}
