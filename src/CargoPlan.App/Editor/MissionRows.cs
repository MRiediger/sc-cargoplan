using System.Globalization;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.App.Editor;

/// <summary>What <see cref="MissionRows.ToStops"/> makes of the editor rows.</summary>
/// <param name="Stops">The route as entered, empty while any row is invalid or no row is filled.</param>
/// <param name="StopNumbers">Per row, its stop; null for a blank row or one without destination.</param>
/// <param name="Errors">Per row, what is wrong with it; null = fine.</param>
/// <param name="CrateTexts">Per row, the crates column: the derived mix or the override ("4+2"); empty without valid SCU.</param>
/// <param name="Problem">For the status line: the first invalid row, or that no row is filled; null = ready to plan.</param>
public sealed record MissionRowsResult(
    IReadOnlyList<MissionStop> Stops,
    IReadOnlyList<int?> StopNumbers,
    IReadOnlyList<string?> Errors,
    IReadOnlyList<string> CrateTexts,
    string? Problem)
{
    public bool IsValid => Problem is null;
}

/// <summary>
/// Editor rows to stops and back (D19). A row is one contract item: SCU and max crate size, optionally the crates by
/// hand (D23); rows of the same destination form one stop, and stops follow the first appearance of their destination.
/// The checks are the mission file's rules, worded for the editor; the crates are checked by Core's
/// <see cref="CrateMix.Problem"/> itself. Expanding into cargo items is Core's job (<see cref="MissionItem.ToCargoItems"/>).
/// </summary>
public static class MissionRows
{
    public const string DestinationEmptyText = "Destination is empty.";
    public const string CommodityEmptyText = "Commodity is empty.";
    public const string ScuInvalidText = "SCU must be a whole number above 0.";
    public const string MaxCrateSizeInvalidText = "Pick a max crate size.";
    public const string CratesInvalidText = "Crates must be crate sizes joined by +, e.g. 2+2+2.";
    public const string NoMissionsText = "Enter at least one mission.";

    public const string CratesToolTipText =
        "Crates as the kiosk delivers them, e.g. 2+2+2. Leave empty to derive them from SCU and max crate size.";

    /// <summary>The first row's max crate size: "4 SCU or smaller" is the most common entry-level contract (D23).</summary>
    public const int DefaultMaxCrateSize = 4;

    private const char CrateSeparator = '+';

    /// <summary>The choices of the max crate size column, smallest first.</summary>
    public static IReadOnlyList<int> MaxCrateSizes { get; } = [.. Enum.GetValues<CrateSize>().Select(s => (int)s).Order()];

    public static MissionRowsResult ToStops(IReadOnlyList<MissionRow> rows)
    {
        var stopNumbers = new int?[rows.Count];
        var errors = new string?[rows.Count];
        var crateTexts = new string[rows.Count];

        // Case-insensitive like the mission file and the planner: "Riker" and "riker" are the same cargo lift.
        Dictionary<string, int> stopOf = new(StringComparer.OrdinalIgnoreCase);
        List<(string Destination, List<MissionItem> Items)> stops = [];

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            crateTexts[i] = CratesText(row);
            if (row.IsBlank)
            {
                continue;
            }

            var destination = row.Destination.Trim();
            if (destination.Length > 0)
            {
                if (!stopOf.TryGetValue(destination, out var stop))
                {
                    stop = stops.Count;
                    stopOf[destination] = stop;
                    stops.Add((destination, []));
                }

                stopNumbers[i] = stop + 1;
            }

            (var item, errors[i]) = Check(row, destination);
            if (item is not null && stopNumbers[i] is { } number)
            {
                stops[number - 1].Items.Add(item);
            }
        }

        var problem = FirstProblem(rows, errors);
        IReadOnlyList<MissionStop> route = problem is null
            ? [.. stops.Select((s, index) => new MissionStop(s.Destination, index + 1, s.Items))]
            : [];
        return new MissionRowsResult(route, stopNumbers, errors, crateTexts, problem);
    }

    /// <summary>One row per item, stops in route order; an override (or a schema 1 item's explicit crates) as text.</summary>
    public static IReadOnlyList<MissionRow> FromStops(IEnumerable<MissionStop> stops) =>
    [
        .. stops.OrderBy(s => s.Order).SelectMany(s => s.Items.Select(item => new MissionRow
        {
            Destination = s.Destination,
            Commodity = item.Commodity,
            Scu = item.Scu.ToString(CultureInfo.InvariantCulture),
            MaxCrateSize = (int)item.MaxCrateSize,
            Crates = item.Crates is { } crates ? Format(crates) : "",
        })),
    ];

    /// <summary>
    /// The SCU rule repeats MissionItem's, with a message fit for the status line (ArgumentException appends the
    /// parameter name); a test pins both together. After these checks the MissionItem constructor can't throw.
    /// </summary>
    private static (MissionItem? Item, string? Error) Check(MissionRow row, string destination)
    {
        if (destination.Length == 0)
        {
            return (null, DestinationEmptyText);
        }

        var commodity = row.Commodity.Trim();
        if (commodity.Length == 0)
        {
            return (null, CommodityEmptyText);
        }

        if (ParseScu(row.Scu) is not { } scu)
        {
            return (null, ScuInvalidText);
        }

        if (!Enum.IsDefined((CrateSize)row.MaxCrateSize))
        {
            return (null, MaxCrateSizeInvalidText);
        }

        var max = (CrateSize)row.MaxCrateSize;
        if (!row.IsCrateOverride)
        {
            return (new MissionItem(commodity, scu, max), null);
        }

        if (ParseCrates(row.Crates) is not { } crates)
        {
            return (null, CratesInvalidText);
        }

        if (CrateMix.Problem(scu, max, crates) is { } problem)
        {
            return (null, problem);
        }

        return (new MissionItem(commodity, scu, max, [.. crates.Select(c => (CrateSize)c)]), null);
    }

    /// <summary>The crates column: an override normalised ("2 + 2+2" → "2+2+2") or as typed if it can't be read, else the derived mix.</summary>
    private static string CratesText(MissionRow row)
    {
        if (row.IsCrateOverride)
        {
            return ParseCrates(row.Crates) is { } crates ? string.Join(CrateSeparator, crates) : row.Crates.Trim();
        }

        return ParseScu(row.Scu) is { } scu && Enum.IsDefined((CrateSize)row.MaxCrateSize)
            ? Format(CrateMix.Decompose(scu, (CrateSize)row.MaxCrateSize))
            : "";
    }

    private static string Format(IEnumerable<CrateSize> crates) => string.Join(CrateSeparator, crates.Select(c => (int)c));

    // NumberStyles.None: digits only, no sign, no decimal point, no thousands separator.
    private static int? ParseScu(string text) =>
        int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var scu) && scu > 0 ? scu : null;

    /// <summary>Whole numbers joined by +, spaces allowed; null if any part is not a number. Sizes are Core's check.</summary>
    private static List<int>? ParseCrates(string text)
    {
        List<int> crates = [];
        foreach (var part in text.Split(CrateSeparator))
        {
            if (!int.TryParse(part.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var crate))
            {
                return null;
            }

            crates.Add(crate);
        }

        return crates;
    }

    private static string? FirstProblem(IReadOnlyList<MissionRow> rows, string?[] errors)
    {
        var invalid = Array.FindIndex(errors, e => e is not null);
        if (invalid >= 0)
        {
            return Invariant($"Row {invalid + 1}: {errors[invalid]}");
        }

        return rows.All(r => r.IsBlank) ? NoMissionsText : null;
    }
}
