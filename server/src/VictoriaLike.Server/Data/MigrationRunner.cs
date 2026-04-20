using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace VictoriaLike.Server.Data;

public interface IMigrationRunner
{
    Task RunPendingMigrationsAsync(CancellationToken cancellationToken = default);
}

public class MigrationRunner : IMigrationRunner
{
    private readonly string _connectionString;
    private readonly ILogger<MigrationRunner> _logger;
    private readonly string _migrationsPath;

    public MigrationRunner(string connectionString, ILogger<MigrationRunner> logger)
    {
        _connectionString = connectionString;
        _logger = logger;
        _migrationsPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "..", "migrations");
    }

    public async Task RunPendingMigrationsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Ensure migration history table exists
            using var createTableCommand = connection.CreateCommand();
            createTableCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS schema_versions (
                    id SERIAL PRIMARY KEY,
                    version_number INT NOT NULL UNIQUE,
                    description TEXT NOT NULL,
                    applied_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
                );";
            await createTableCommand.ExecuteNonQueryAsync(cancellationToken);

            // Find and run migration files
            if (!Directory.Exists(_migrationsPath))
            {
                _logger.LogWarning("Migrations directory not found at {Path}", _migrationsPath);
                return;
            }

            var migrationFiles = Directory.GetFiles(_migrationsPath, "*.sql")
                .OrderBy(f => f)
                .ToList();

            _logger.LogInformation("Found {Count} migration files", migrationFiles.Count);

            foreach (var file in migrationFiles)
            {
                var fileName = Path.GetFileName(file);
                var versionMatch = System.Text.RegularExpressions.Regex.Match(fileName, @"^(\d+)_");

                if (!versionMatch.Success)
                {
                    _logger.LogWarning("Skipping migration file with invalid name: {FileName}", fileName);
                    continue;
                }

                var versionNumber = int.Parse(versionMatch.Groups[1].Value);

                // Check if already applied
                using var checkCommand = connection.CreateCommand();
                checkCommand.CommandText = "SELECT 1 FROM schema_versions WHERE version_number = @version;";
                checkCommand.Parameters.AddWithValue("@version", versionNumber);

                var alreadyApplied = await checkCommand.ExecuteScalarAsync(cancellationToken) != null;
                if (alreadyApplied)
                {
                    _logger.LogDebug("Migration {FileName} already applied", fileName);
                    continue;
                }

                // Run migration
                var sql = await File.ReadAllTextAsync(file, cancellationToken);

                using var transaction = connection.BeginTransaction();
                try
                {
                    using var migrationCommand = connection.CreateCommand();
                    migrationCommand.CommandText = sql;
                    migrationCommand.Transaction = transaction;
                    await migrationCommand.ExecuteNonQueryAsync(cancellationToken);

                    transaction.Commit();
                    _logger.LogInformation("Applied migration {FileName}", fileName);
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    _logger.LogError(ex, "Failed to apply migration {FileName}", fileName);
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration error");
            throw;
        }
    }
}
