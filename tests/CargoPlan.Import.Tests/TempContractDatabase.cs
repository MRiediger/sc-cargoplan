using Microsoft.Data.Sqlite;

namespace CargoPlan.Import.Tests;

/// <summary>
/// A ContractTracker database file in the temp folder, built from <see cref="ImportFixtures.Schema"/> (or a variant of
/// it) and deleted on dispose. Writes go through unpooled connections, so no handle keeps the file open.
/// </summary>
internal sealed class TempContractDatabase : IDisposable
{
    private const string DefaultCapturedAt = "2026-01-01 12:00:00.0000000";

    public TempContractDatabase(string? schema = null)
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cargoplan-ct-{Guid.NewGuid():N}.db");
        Execute(schema ?? ImportFixtures.Schema);
    }

    public string Path { get; }

    /// <summary>One contract row (one commodity, as ContractTracker writes it) with its destination rows.</summary>
    public void AddContract(long id, string name, string? rawText, params (int Amount, string Destination)[] objectives) =>
        AddContract(id, name, rawText, DefaultCapturedAt, objectives);

    public void AddContract(long id, string name, string? rawText, string capturedAt,
                            params (int Amount, string Destination)[] objectives)
    {
        using var connection = Open();
        using var contract = connection.CreateCommand();
        contract.CommandText = """
            INSERT INTO "Contracts" ("Id", "Name", "CapturedAt", "RawOcrText") VALUES ($id, $name, $capturedAt, $raw)
            """;
        contract.Parameters.AddWithValue("$id", id);
        contract.Parameters.AddWithValue("$name", name);
        contract.Parameters.AddWithValue("$capturedAt", capturedAt);
        contract.Parameters.AddWithValue("$raw", (object?)rawText ?? DBNull.Value);
        contract.ExecuteNonQuery();

        foreach (var (amount, destination) in objectives)
        {
            using var objective = connection.CreateCommand();
            objective.CommandText = """
                INSERT INTO "ContractObjectives" ("ContractId", "Amount", "Destination") VALUES ($id, $amount, $destination)
                """;
            objective.Parameters.AddWithValue("$id", id);
            objective.Parameters.AddWithValue("$amount", amount);
            objective.Parameters.AddWithValue("$destination", destination);
            objective.ExecuteNonQuery();
        }
    }

    /// <summary>A writing connection, e.g. to hold a lock while the reader tries to read.</summary>
    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path,
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    public void Dispose()
    {
        foreach (var file in new[] { Path, Path + "-wal", Path + "-shm", Path + "-journal" })
        {
            File.Delete(file);
        }
    }

    private void Execute(string sql)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
