using Microsoft.Data.Sqlite;
using VictoriaLike.Core.Core.World;

namespace VictoriaLike.Core.Application.SaveLoad;

public sealed class SqliteSaveRepository(string connectionString) : ISaveRepository
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            CREATE TABLE IF NOT EXISTS SaveSlots (
                SlotName TEXT PRIMARY KEY,
                SavedAtUtc TEXT NOT NULL,
                SimulationDate TEXT NOT NULL,
                TreasurySummary TEXT NOT NULL,
                AverageNeedsFulfilled REAL NOT NULL
            );
            """;

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task SaveSnapshotAsync(string slotName, WorldState world, CancellationToken cancellationToken = default)
    {
        var playableCountry = world.Countries.Values.FirstOrDefault(country => country.IsPlayable);
        var treasury = playableCountry?.Treasury ?? 0m;

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT OR REPLACE INTO SaveSlots (SlotName, SavedAtUtc, SimulationDate, TreasurySummary, AverageNeedsFulfilled)
            VALUES ($slotName, $savedAtUtc, $simulationDate, $treasurySummary, $averageNeedsFulfilled);
            """;
        command.Parameters.AddWithValue("$slotName", slotName);
        command.Parameters.AddWithValue("$savedAtUtc", DateTime.UtcNow.ToString("O"));
        command.Parameters.AddWithValue("$simulationDate", world.Date.ToString());
        command.Parameters.AddWithValue("$treasurySummary", treasury.ToString(System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$averageNeedsFulfilled", world.Metrics.AverageNeedsFulfilled);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
