using System.Globalization;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.App.Editor;

/// <summary>What <see cref="MissionRows.ToStops"/> makes of the editor rows.</summary>
/// <param name="Stops">The route, empty while any row is invalid or no row is filled.</param>
/// <param name="StopNumbers">Per row, its stop; null for a blank row or one without destination.</param>
/// <param name="Errors">Per row, what is wrong with it; null = fine.</param>
/// <param name="Problem">For the status line: the first invalid row, or that no row is filled; null = ready to plan.</param>
public sealed record MissionRowsResult(
    IReadOnlyList<Stop> Stops, IReadOnlyList<int?> StopNumbers, IReadOnlyList<string?> Errors, string? Problem)
{
    public bool IsValid => Problem is null;
}

/// <summary>
/// Editor rows to stops and back (D19). A row is one cargo item; rows of the same destination form one stop, and stops
/// follow the first appearance of their destination. The checks are the mission file's rules, worded for the editor.
/// </summary>
public static class MissionRows
{
    public const string DestinationEmptyText = "Destination is empty.";
    public const string CommodityEmptyText = "Commodity is empty.";
    public const string ScuInvalidText = "SCU must be a whole number above 0.";
    public const string CrateSizeInvalidText = "Pick a crate size.";
    public const string NoMissionsText = "Enter at least one mission.";

    /// <summary>The choices of the crate size column, smallest first.</summary>
    public static IReadOnlyList<int> CrateSizes { get; } = [.. Enum.GetValues<CrateSize>().Select(s => (int)s).Order()];

    public static MissionRowsResult ToStops(IReadOnlyList<MissionRow> rows)
    {
        var stopNumbers = new int?[rows.Count];
        var errors = new string?[rows.Count];

        // Case-insensitive like the mission file and the planner: "Riker" and "riker" are the same cargo lift.
        Dictionary<string, int> stopOf = new(StringComparer.OrdinalIgnoreCase);
        List<(string Destination, List<CargoItem> Items)> stops = [];

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
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
        IReadOnlyList<Stop> route = problem is null
            ? [.. stops.Select((s, index) => new Stop(s.Destination, index + 1, s.Items))]
            : [];
        return new MissionRowsResult(route, stopNumbers, errors, problem);
    }

    /// <summary>One row per item, stops in route order.</summary>
    public static IReadOnlyList<MissionRow> FromStops(IEnumerable<Stop> stops) =>
    [
        .. stops.OrderBy(s => s.Order).SelectMany(s => s.Items.Select(item => new MissionRow
        {
            Destination = s.Destination,
            Commodity = item.Commodity,
            Scu = item.Scu.ToString(CultureInfo.InvariantCulture),
            CrateSize = (int)item.CrateSize,
        })),
    ];

    /// <summary>
    /// The SCU rule repeats CargoItem's, with a message fit for the status line (ArgumentException appends the parameter
    /// name); a test pins both together. After these checks the CargoItem constructor can't throw.
    /// </summary>
    private static (CargoItem? Item, string? Error) Check(MissionRow row, string destination)
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

        // NumberStyles.None: digits only, no sign, no decimal point, no thousands separator.
        if (!int.TryParse(row.Scu.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var scu) || scu <= 0)
        {
            return (null, ScuInvalidText);
        }

        if (!Enum.IsDefined((CrateSize)row.CrateSize))
        {
            return (null, CrateSizeInvalidText);
        }

        if (scu % row.CrateSize != 0)
        {
            return (null, Invariant($"{scu} SCU can't be packed in {row.CrateSize} SCU crates."));
        }

        return (new CargoItem(commodity, scu, (CrateSize)row.CrateSize), null);
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
