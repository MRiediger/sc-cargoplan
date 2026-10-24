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

    /// <summary>
    /// %APPDATA%\SCContractTracker\contracts.db, the one place ContractTracker keeps its database (D38). The settings
    /// window can set another path (D44).
    /// </summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SCContractTracker", "contracts.db");

    /// <summary>Every column read, with its declared type (docs/contracttracker-schema.md, "Fingerprint").</summary>
    private static readonly (string Table, string Column, string Type)[] Fingerprint =
    [
        ("Contracts", "Id", "INTEGER"),
        ("Contracts", "Name", "TEXT"),
        ("Contracts", "PickupLocation", "TEXT"),
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
        Read(databasePath, lastId, [], DefaultBusyTimeoutSeconds);

    internal static ContractTrackerReadResult ReadSince(string databasePath, long lastId, int busyTimeoutSeconds) =>
        Read(databasePath, lastId, [], busyTimeoutSeconds);

    /// <summary>
    /// <see cref="ReadSince(string, long)"/>, and which of <paramref name="knownIds"/> (the plan's captures) are still in
    /// ContractTracker, for the deletion rule (D39 addendum). A missing file, another schema or a busy database read
    /// nothing: <see cref="ContractTrackerReadResult.Present"/> is null then, not empty, so nothing counts as deleted.
    /// </summary>
    /// <exception cref="SqliteException">The file is not a readable SQLite database.</exception>
    public static ContractTrackerReadResult Read(string databasePath, long lastId, IReadOnlyCollection<long> knownIds) =>
        Read(databasePath, lastId, knownIds, DefaultBusyTimeoutSeconds);

    private static ContractTrackerReadResult Read(string databasePath, long lastId, IReadOnlyCollection<long> knownIds, int busyTimeoutSeconds)
    {
        if (!File.Exists(databasePath))
        {
            // "ContractTracker not found" is the settings window's and the manual sync's notice (D44), not the reader's.
            return ContractTrackerReadResult.Nothing;
        }

        try
        {
            using var connection = new SqliteConnection(ConnectionString(databasePath, busyTimeoutSeconds));
            connection.Open();
            if (!MatchesFingerprint(connection))
            {
                return new([], SchemaNotSupportedNotice);
            }

            // A new file starts its ids at 1 again; above the old watermark nothing would ever come (D44).
            var looksNew = HighestIdEverGiven(connection) < lastId;
            return new(
                [.. Captures(ReadContracts(connection, looksNew ? 0 : lastId)).Select(ToCandidate)],
                null,
                looksNew ? new HashSet<long>() : Present(connection, knownIds),
                looksNew);
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
            SELECT c."Id", c."Name", c."CapturedAt", c."RawOcrText", o."Amount", o."Destination", c."PickupLocation"
            FROM "Contracts" c LEFT JOIN "ContractObjectives" o ON o."ContractId" = c."Id"
            WHERE c."Id" > $lastId
            ORDER BY c."Id", o."Id"
            """;
        command.Parameters.AddWithValue("$lastId", lastId);

        List<(long Id, string Name, string? CapturedAt, string? RawText, long? Amount, string? Destination, string? Pickup)> rows = [];
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((reader.GetInt64(0), StringOrNull(reader, 1) ?? "", StringOrNull(reader, 2), StringOrNull(reader, 3),
                          reader.IsDBNull(4) ? null : reader.GetInt64(4), StringOrNull(reader, 5), StringOrNull(reader, 6)));
            }
        }

        return
        [
            .. rows.GroupBy(r => r.Id).Select(g => new Contract(g.Key, g.First().Name, g.First().CapturedAt, g.First().RawText,
            [
                .. g.Where(r => r is { Amount: > 0 and <= int.MaxValue, Destination: not null })
                    .Select(r => ((int)r.Amount!.Value, r.Destination!)),
            ], g.First().Pickup)),
        ];
    }

    /// <summary>
    /// The known ids that still have a row. All ids, not a parameter list: the database holds a session's contracts, a few
    /// dozen rows. A second statement, so a row deleted between the two reads counts as gone, which the next read confirms.
    /// </summary>
    private static HashSet<long> Present(SqliteConnection connection, IReadOnlyCollection<long> knownIds)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """SELECT "Id" FROM "Contracts" """;

        HashSet<long> present = [];
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (knownIds.Contains(reader.GetInt64(0)))
            {
                present.Add(reader.GetInt64(0));
            }
        }

        return present;
    }

    /// <summary>
    /// The highest id the contracts table ever gave. SQLite keeps it in sqlite_sequence for an AUTOINCREMENT table; deleting
    /// rows, the newest or all of them (F5), never lowers it, unlike MAX(Id). A new file has no row yet: 0. null = can't
    /// tell (no AUTOINCREMENT, but rows), so nothing is guessed.
    /// Source: https://www.sqlite.org/autoinc.html
    /// </summary>
    private static long? HighestIdEverGiven(SqliteConnection connection)
    {
        // sqlite_sequence exists only once an AUTOINCREMENT table does; naming it otherwise fails to prepare.
        var sequence = Scalar(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'sqlite_sequence'") > 0
            ? Scalar(connection, "SELECT seq FROM sqlite_sequence WHERE name = 'Contracts' COLLATE NOCASE")
            : null;
        return sequence ?? (Scalar(connection, """SELECT count(*) FROM "Contracts" """) == 0 ? 0 : null);
    }

    private static long? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar() is long value ? value : null;
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
            Stops(usedFallback ? FallbackDeliveries(capture) : fromText, max, PickupsByCommodity(capture)),
            CrateSizeAssumed: max is null,
            UsedFallbackRows: usedFallback,
            parsed?.ListedDestinationCount,
            parsed?.Reward,
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

    /// <summary>
    /// One stop per place (<see cref="DestinationNames"/>) under its preferred name, one item per commodity, in the order
    /// of the text; each item with the pickup of its commodity's rows.
    /// </summary>
    private static IReadOnlyList<MissionStop> Stops(
        IReadOnlyList<ParsedDelivery> deliveries, CrateSize? max, IReadOnlyDictionary<string, IReadOnlyList<string>?> pickups) =>
    [
        .. deliveries.GroupBy(d => d.Destination, DestinationNames.Comparer)
            .Select((stop, index) => new MissionStop(stop.Select(d => d.Destination).Aggregate(DestinationNames.Preferred), index + 1,
            [
                .. stop.GroupBy(d => d.Commodity, StringComparer.OrdinalIgnoreCase)
                    .Select(item => Item(item.First().Commodity, item.Sum(d => d.Scu), max, pickups.GetValueOrDefault(item.Key))),
            ])),
    ];

    /// <summary>Without a size in the text: the largest crate that fits the SCU (T5.3), marked as assumed.</summary>
    private static MissionItem Item(string commodity, int scu, CrateSize? max, IReadOnlyList<string>? pickup) =>
        new(commodity, scu, max ?? CrateMix.Sizes.First(size => (int)size <= scu), Pickup: pickup);

    /// <summary>
    /// The pickup per commodity (D68): a contract row is one commodity (D38), its pickup text split at "/" and normalized
    /// as the editor does (<see cref="Pickups.Parse"/>), the OCR text itself unchanged. Several pickups are joined with
    /// " / " in that text, without amounts, so the item gets the list. Rows of one commodity share theirs.
    /// </summary>
    private static Dictionary<string, IReadOnlyList<string>?> PickupsByCommodity(IReadOnlyList<Contract> capture) =>
        capture.GroupBy(c => ContractText.NormalizeCommodity(c.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => Pickups.Normalized(g.SelectMany(c => Pickups.Parse(c.Pickup) ?? [])), StringComparer.OrdinalIgnoreCase);

    /// <summary>ContractTracker writes UTC without a marker (D38).</summary>
    private static DateTimeOffset? CapturedAt(string? text) =>
        DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                          out var utc)
            ? new DateTimeOffset(utc)
            : null;

    private sealed record Contract(
        long Id, string Name, string? CapturedAt, string? RawText, IReadOnlyList<(int Amount, string Destination)> Objectives, string? Pickup);
}
