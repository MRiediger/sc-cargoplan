using System.Globalization;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Import;
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

    /// <param name="cap">The ship's cap (D70): the crates column shows a derived mix split up to it.</param>
    public static MissionRowsResult ToStops(IReadOnlyList<MissionRow> rows, CrateCap cap)
    {
        var stopNumbers = new int?[rows.Count];
        var errors = new string?[rows.Count];
        var crateTexts = new string[rows.Count];

        // "Riker" and "riker" are the same cargo lift, so are "Thundering Express" and "Thundering Express Station" (B1);
        // the stop keeps the longer name.
        Dictionary<string, int> stopOf = new(DestinationNames.Comparer);
        List<(string Destination, List<MissionItem> Items)> stops = [];

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            crateTexts[i] = CratesText(row, cap);
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

                stops[stop] = (DestinationNames.Preferred(stops[stop].Destination, destination), stops[stop].Items);

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

    /// <summary>One row per item, stops in their order; an override (or a schema 1 item's explicit crates) as text.</summary>
    public static IReadOnlyList<MissionRow> FromStops(IEnumerable<MissionStop> stops) =>
        [.. stops.OrderBy(s => s.Order).SelectMany(s => s.Items.Select(item => Row(s.Destination, item, null)))];

    /// <summary>
    /// One row per item of the plan's missions (D42), destinations in <paramref name="order"/> (the legend's), within a
    /// destination missions in plan order; each row knows its mission and where it came from (T5.4).
    /// </summary>
    public static IReadOnlyList<MissionRow> FromMissions(IReadOnlyList<Mission> missions, IReadOnlyList<string> order) =>
    [
        .. order.SelectMany(destination => missions.SelectMany(m => m.Stops
            .Where(s => s.Destination == destination)
            .SelectMany(s => s.Items.Select(item => Row(destination, item, m))))),
    ];

    /// <summary>
    /// Valid rows as missions for <c>LoadPlanner.Plan</c> (D42), with the route: rows of a ContractTracker capture of
    /// <paramref name="known"/> stay that mission with its ids (its size stays assumed while one of its rows does); typed
    /// rows of one destination are one mission, numbered above the known ids. Destinations take the spelling of their stop.
    /// </summary>
    /// <exception cref="InvalidOperationException">A row is invalid (<see cref="ToStops"/> has a problem).</exception>
    public static (IReadOnlyList<Mission> Missions, IReadOnlyList<string> Route) ToMissions(IReadOnlyList<MissionRow> rows, IReadOnlyList<Mission> known)
    {
        // The crates column isn't read here: the planner splits the mixes for its ship itself.
        var result = ToStops(rows, CrateCap.None);
        if (result.Problem is { } problem)
        {
            throw new InvalidOperationException(problem);
        }

        string[] route = [.. result.Stops.Select(s => s.Destination)];
        var spelling = route.ToDictionary(d => d, d => d, DestinationNames.Comparer);
        var captures = known.Where(m => m.Contract is not null).ToDictionary(m => m.Id);
        var nextId = known.Count == 0 ? 1 : known.Max(m => m.Id) + 1;

        // A capture's rows by its id, typed rows by destination; in the order of each group's first row.
        List<(int? Capture, List<(string Destination, MissionItem Item, bool Assumed)> Items)> groups = [];
        foreach (var row in rows.Where(r => !r.IsBlank))
        {
            var destination = spelling[row.Destination.Trim()];
            var item = Check(row, destination).Item ?? throw new InvalidOperationException("A valid row has an item.");
            int? capture = row.MissionId is { } id && captures.ContainsKey(id) ? id : null;
            var group = groups.FindIndex(g => capture is not null
                ? g.Capture == capture
                : g.Capture is null && string.Equals(g.Items[0].Destination, destination, StringComparison.Ordinal));
            if (group < 0)
            {
                groups.Add((capture, []));
                group = groups.Count - 1;
            }

            groups[group].Items.Add((destination, item, row.MaxCrateSizeAssumed));
        }

        Mission[] missions =
        [
            .. groups.Select(g =>
            {
                MissionStop[] stops = [.. g.Items.GroupBy(i => i.Destination)
                    .Select((stop, index) => new MissionStop(stop.Key, index + 1, [.. stop.Select(i => i.Item)]))];
                return g.Capture is { } id
                    ? new Mission(id, stops, captures[id].Contract! with { CrateSizeAssumed = g.Items.Any(i => i.Assumed) })
                    : new Mission(nextId++, stops);
            }),
        ];
        return (missions, route);
    }

    private static MissionRow Row(string destination, MissionItem item, Mission? mission) => new()
    {
        Destination = destination,
        Commodity = item.Commodity,
        Scu = item.Scu.ToString(CultureInfo.InvariantCulture),
        MaxCrateSize = (int)item.MaxCrateSize,
        Crates = item.Crates is { } crates ? Format(crates) : "",
        Pickup = Pickups.Text(item.Pickup),
        MissionId = mission?.Id,
        Source = MissionText.Source(mission?.Contract),
        ContractId = mission?.Contract?.ContractIds.Min(),

        // After MaxCrateSize, whose change ends the assumption.
        MaxCrateSizeAssumed = mission?.Contract?.CrateSizeAssumed == true,
    };

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
        var pickup = Pickups.Parse(row.Pickup);
        if (!row.IsCrateOverride)
        {
            return (new MissionItem(commodity, scu, max, Pickup: pickup), null);
        }

        if (ParseCrates(row.Crates) is not { } crates)
        {
            return (null, CratesInvalidText);
        }

        if (CrateMix.Problem(scu, max, crates) is { } problem)
        {
            return (null, problem);
        }

        return (new MissionItem(commodity, scu, max, [.. crates.Select(c => (CrateSize)c)], pickup), null);
    }

    /// <summary>
    /// The crates column: an override normalised ("2 + 2+2" → "2+2+2") or as typed if it can't be read, else the mix
    /// derived up to <paramref name="cap"/>, as the planner will lay it (D70).
    /// </summary>
    private static string CratesText(MissionRow row, CrateCap cap)
    {
        if (row.IsCrateOverride)
        {
            return ParseCrates(row.Crates) is { } crates ? string.Join(CrateSeparator, crates) : row.Crates.Trim();
        }

        return ParseScu(row.Scu) is { } scu && Enum.IsDefined((CrateSize)row.MaxCrateSize)
            ? Format(CrateMix.Decompose(scu, (CrateSize)row.MaxCrateSize, cap))
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
