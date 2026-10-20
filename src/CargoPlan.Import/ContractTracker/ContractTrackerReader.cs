using System.Globalization;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using Microsoft.Data.Sqlite;

namespace CargoPlan.Import.ContractTracker;

/// <summary>
/// Reads ContractTracker's database read-only (D38) and turns every capture into a candidate (D39). Table and column
/// names are listed in docs/contracttracker-schema.md; a ContractTracker update that changes them is caught by the
/// fingerprint, and nothing is read.
/// </summary>
public static class ContractTrackerReader
{
    public const string SchemaNotSupportedNotice =
        "ContractTracker schema not supported – enter missions in the editor or use Import…";

    // SQLite primary result codes: https://www.sqlite.org/rescode.html
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;

    /// <summary>
    /// How long a busy or locked database is retried, in seconds. Microsoft.Data.Sqlite retries by itself until the
    /// command timeout: https://learn.microsoft.com/dotnet/standard/data/sqlite/database-errors
    /// </summary>
    private const int DefaultBusyTimeoutSeconds = 5;

    /// <summary>Every column read, with its declared type (docs/contracttracker-schema.md, "Fingerprint").</summary>
    private static readonly (string Table, string Column, string Type)[] Fingerprint =
    [
        ("Contracts", "Id", "INTEGER"),
        ("Contracts", "Name", "TEXT"),
        ("Contracts", "CapturedAt", "TEXT"),
        ("Contracts", "RawOcrText", "TEXT"),
        ("ContractObjectives", "Id", "INTEGER"),
        ("ContractObjectives", "ContractId", "INTEGER"),
        ("ContractObjectives", "Amount", "INTEGER"),
        ("ContractObjectives", "Destination", "TEXT"),
    ];

    /// <summary>The captures whose ids are all above <paramref name="lastId"/> (the plan's watermark, T5.4).</summary>
    /// <remarks>
    /// <c>Mode=ReadOnly</c> writes nothing into the database, but SQLite still creates <c>-wal</c> and <c>-shm</c>
    /// next to it while ContractTracker is closed, and may not delete them (D38). Left as they are.
    /// </remarks>
    /// <exception cref="SqliteException">The file is not a readable SQLite database.</exception>
    public static ContractTrackerReadResult ReadSince(string databasePath, long lastId) =>
        ReadSince(databasePath, lastId, DefaultBusyTimeoutSeconds);

    internal static ContractTrackerReadResult ReadSince(string databasePath, long lastId, int busyTimeoutSeconds)
    {
        if (!File.Exists(databasePath))
        {
            // "ContractTracker not found" is the settings window's notice (T5.5), not the reader's.
            return ContractTrackerReadResult.Nothing;
        }

        try
        {
            using var connection = new SqliteConnection(ConnectionString(databasePath, busyTimeoutSeconds));
            connection.Open();
            return MatchesFingerprint(connection)
                ? new([.. Captures(ReadContracts(connection, lastId)).Select(ToCandidate)], null)
                : new([], SchemaNotSupportedNotice);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is SqliteBusy or SqliteLocked)
        {
            // Still locked after the retries: ContractTracker is writing. Nothing read and no notice; the next read
            // (the next file event, T5.4) catches up, because the watermark has not moved.
            return ContractTrackerReadResult.Nothing;
        }
    }

    // Unpooled, so no handle stays open after the read: ContractTracker deletes its -wal and -shm on exit (D38).
    private static string ConnectionString(string databasePath, int busyTimeoutSeconds) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = busyTimeoutSeconds,
        }.ToString();

    /// <summary>Each fingerprint column exists with its declared type; more tables or columns are fine (D38).</summary>
    private static bool MatchesFingerprint(SqliteConnection connection)
    {
        foreach (var table in Fingerprint.GroupBy(f => f.Table))
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, type FROM pragma_table_info($table)";
            command.Parameters.AddWithValue("$table", table.Key);

            // SQLite treats column names and type names case-insensitively.
            Dictionary<string, string> columns = new(StringComparer.OrdinalIgnoreCase);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    columns[reader.GetString(0)] = reader.GetString(1);
                }
            }

            if (table.Any(f => !columns.TryGetValue(f.Column, out var type) || !type.Equals(f.Type, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>One statement, so contracts and destination rows come from the same snapshot.</summary>
    private static IReadOnlyList<Contract> ReadContracts(SqliteConnection connection, long lastId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT c."Id", c."Name", c."CapturedAt", c."RawOcrText", o."Amount", o."Destination"
            FROM "Contracts" c LEFT JOIN "ContractObjectives" o ON o."ContractId" = c."Id"
            WHERE c."Id" > $lastId
            ORDER BY c."Id", o."Id"
            """;
        command.Parameters.AddWithValue("$lastId", lastId);

        List<(long Id, string Name, string? CapturedAt, string? RawText, long? Amount, string? Destination)> rows = [];
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((reader.GetInt64(0), StringOrNull(reader, 1) ?? "", StringOrNull(reader, 2), StringOrNull(reader, 3),
                          reader.IsDBNull(4) ? null : reader.GetInt64(4), StringOrNull(reader, 5)));
            }
        }

        return
        [
            .. rows.GroupBy(r => r.Id).Select(g => new Contract(g.Key, g.First().Name, g.First().CapturedAt, g.First().RawText,
            [
                .. g.Where(r => r is { Amount: > 0 and <= int.MaxValue, Destination: not null })
                    .Select(r => ((int)r.Amount!.Value, r.Destination!)),
            ])),
        ];
    }

    private static string? StringOrNull(SqliteDataReader reader, int ordinal) => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    /// <summary>
    /// Rows of one capture: consecutive ids and byte-identical raw text (D38: one transaction per capture). A capture
    /// without raw text stays alone.
    /// </summary>
    private static IEnumerable<IReadOnlyList<Contract>> Captures(IReadOnlyList<Contract> contracts)
    {
        List<Contract> capture = [];
        foreach (var contract in contracts)
        {
            if (capture is [.., var last]
                && !(contract.Id == last.Id + 1 && contract.RawText is not null
                     && string.Equals(contract.RawText, last.RawText, StringComparison.Ordinal)))
            {
                yield return capture;
                capture = [];
            }

            capture.Add(contract);
        }

        if (capture.Count > 0)
        {
            yield return capture;
        }
    }

    private static ContractTrackerCandidate ToCandidate(IReadOnlyList<Contract> capture)
    {
        var first = capture[0];
        var parsed = first.RawText is { } raw ? ContractTextParser.Parse(raw) : null;
        var fromText = parsed?.Deliveries ?? [];
        var usedFallback = fromText.Count == 0;
        var max = parsed?.MaxCrateSize;

        return new(
            [.. capture.Select(c => c.Id)],
            CapturedAt(first.CapturedAt),
            Stops(usedFallback ? FallbackDeliveries(capture) : fromText, max),
            CrateSizeAssumed: max is null,
            UsedFallbackRows: usedFallback,
            parsed?.ListedDestinationCount,
            first.RawText);
    }

    /// <summary>ContractTracker's own destination rows, through the same normalisation as the parser (D40).</summary>
    private static IReadOnlyList<ParsedDelivery> FallbackDeliveries(IReadOnlyList<Contract> capture) =>
    [
        .. from contract in capture
           from objective in contract.Objectives
           let destination = ContractText.NormalizeDestination(objective.Destination)
           let commodity = ContractText.NormalizeCommodity(contract.Name)
           where destination.Length > 0 && commodity.Length > 0
           select new ParsedDelivery(destination, commodity, objective.Amount),
    ];

    /// <summary>One stop per destination, one item per commodity, in the order of the text.</summary>
    private static IReadOnlyList<MissionStop> Stops(IReadOnlyList<ParsedDelivery> deliveries, CrateSize? max) =>
    [
        .. deliveries.GroupBy(d => d.Destination, StringComparer.OrdinalIgnoreCase)
            .Select((stop, index) => new MissionStop(stop.First().Destination, index + 1,
            [
                .. stop.GroupBy(d => d.Commodity, StringComparer.OrdinalIgnoreCase)
                    .Select(item => Item(item.First().Commodity, item.Sum(d => d.Scu), max)),
            ])),
    ];

    /// <summary>Without a size in the text: the largest crate that fits the SCU (T5.3), marked as assumed.</summary>
    private static MissionItem Item(string commodity, int scu, CrateSize? max) =>
        new(commodity, scu, max ?? CrateMix.Sizes.First(size => (int)size <= scu));

    /// <summary>ContractTracker writes UTC without a marker (D38).</summary>
    private static DateTimeOffset? CapturedAt(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                          out var utc)
            ? new DateTimeOffset(utc)
            : null;

    private sealed record Contract(
        long Id, string Name, string? CapturedAt, string? RawText, IReadOnlyList<(int Amount, string Destination)> Objectives);
}
