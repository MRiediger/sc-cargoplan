using CargoPlan.App.Editor;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>Turns a plan into a <see cref="ShipScene"/>. Reads Core's results, plans nothing.</summary>
public static class ShipViewBuilder
{
    public const string DuplicateText = "⚠ duplicate?";

    public const string TickToDropText = " – tick to drop";

    public const string FitsNowText = "fits now, placed at the next sync";

    /// <summary>The legend's header over a pickup list: "From A, B" (T7.4d, D68).</summary>
    public const string FromText = "From ";

    private const string Separator = " · ";

    /// <summary>A plan without missions: no markings, no unplaced lines (plans built in tests and by the planner alone).</summary>
    /// <param name="violations">The result of <c>LoadPlanner.Validate</c> for this plan.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static ShipScene Build(LoadPlan plan, Ship ship, IReadOnlyList<PlanWarning> violations) => Build(plan, null, ship, violations);

    /// <summary>
    /// The plan with its missions (D42): the legend marks per stop what is uncertain about its missions (T5.4) and lists
    /// the missions that didn't fit (D41).
    /// </summary>
    /// <param name="violations">The result of <c>LoadPlanner.Validate</c> for this plan.</param>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public static ShipScene Build(SavedPlan saved, Ship ship, IReadOnlyList<PlanWarning> violations) => Build(saved.Plan, saved, ship, violations);

    private static ShipScene Build(LoadPlan plan, SavedPlan? saved, Ship ship, IReadOnlyList<PlanWarning> violations)
    {
        plan.EnsureBelongsTo(ship);

        var palette = MissionPalette.Assign(plan);
        var violated = violations.Select(v => v.RackId).ToHashSet();

        SceneRack[] racks = [.. ship.Racks.Select(r => BuildRack(r, plan, palette, violated.Contains(r.Id)))];

        // The order of the crate list, as the player entered the stops: display only, no route (D64); the palette's too.
        var duplicates = saved is null ? new HashSet<int>() : ContractSync.Duplicates(saved);
        var reserveDuplicates = saved is null ? new HashSet<int>() : ContractSync.Duplicates(OnBoard(saved));
        var placements = plan.Placements.ToLookup(p => p.Destination);
        Mission[] unplaced = saved is null ? [] : [.. saved.Missions.Where(m => m.Unplaced)];
        LegendEntry[] legend = [.. LegendLines(plan, saved).Select(line => new LegendEntry(
            palette[line.Destination],
            line.Destination,
            line.Scu,
            placements[line.Destination].All(p => p.Delivered),
            Markings(saved, line.Destination, duplicates, line.Missions),
            Markings(saved, line.Destination, reserveDuplicates, line.Missions),
            line.Header))];

        return new ShipScene(
            racks,
            ship.Bounds,
            legend,
            [.. violations.Select(v => v.Message)],
            MissionPalette.Repeats(plan),
            plan,
            ship,
            saved is null ? [] : [.. unplaced.Select(m => Unplaced(saved, ship, m, duplicates))],
            unplaced.Sum(m => m.TotalScu));
    }

    /// <summary>
    /// The legend's lines (T7.4d, D68): one per destination in the route without any pickup in the plan, as before;
    /// otherwise one per pickup list and destination, lists in the order they first appear along the route, a list's
    /// destinations in route order, the header on its first line. A list is a set of places, any case.
    /// </summary>
    private static IEnumerable<(string? Header, string Destination, int Scu, IReadOnlySet<int>? Missions)> LegendLines(LoadPlan plan, SavedPlan? saved)
    {
        var crates = plan.Route.SelectMany(d => plan.Crates.Where(c => c.Destination == d))
            .Select(c => (Crate: c, Pickup: saved?.Missions.FirstOrDefault(m => m.Id == c.Mission)?.Items.ElementAtOrDefault(c.Item).Item?.Pickup))
            .ToArray();
        if (crates.All(c => c.Pickup is null))
        {
            return plan.Route.Select(d => ((string?)null, d, crates.Where(c => c.Crate.Destination == d).Sum(c => (int)c.Crate.CrateSize), (IReadOnlySet<int>?)null));
        }

        return crates.GroupBy(c => c.Pickup is { } pickup ? string.Join('\n', pickup.Select(p => p.ToUpperInvariant()).Order(StringComparer.Ordinal)) : "")
            .SelectMany(list => list.GroupBy(c => c.Crate.Destination).Select((stop, index) => (
                index == 0 ? (list.First().Pickup is { } pickup ? FromText + string.Join(", ", pickup) : "") : null,
                stop.Key,
                stop.Sum(c => (int)c.Crate.CrateSize),
                (IReadOnlySet<int>?)stop.Select(c => c.Crate.Mission).ToHashSet())));
    }

    /// <summary>
    /// What is uncertain about the missions going to <paramref name="destination"/> (only those in <paramref name="only"/>,
    /// if given), in a fixed order: the crate size first ("4 SCU?", it belongs to the quantity), then missing
    /// destinations, then a possible duplicate.
    /// </summary>
    private static string Markings(SavedPlan? saved, string destination, IReadOnlySet<int> duplicates, IReadOnlySet<int>? only)
    {
        if (saved is null)
        {
            return "";
        }

        Mission[] missions = [.. saved.Missions.Where(m => !m.Unplaced && m.Stops.Any(s => s.Destination == destination) && (only is null || only.Contains(m.Id)))];
        return Joined(
        [
            AssumedSizes(missions.Where(m => m.Contract?.CrateSizeAssumed == true).SelectMany(m => m.Stops.Where(s => s.Destination == destination))),
            .. missions.Select(Incomplete).Distinct(),
            missions.Any(m => duplicates.Contains(m.Id)) ? DuplicateText : "",
        ]);
    }

    /// <summary>"Hurston + 1 · 16 SCU · ⚠ doesn't fit (16 SCU, 8 free) – tick to drop", with the mission's own markings.</summary>
    private static UnplacedEntry Unplaced(SavedPlan saved, Ship ship, Mission mission, IReadOnlySet<int> duplicates)
    {
        var name = MissionText.Name(mission);
        var reason = LoadPlanner.PlaceProblem(saved, ship, mission.Id) ?? FitsNowText;
        return new UnplacedEntry(mission.Id, Joined(
        [
            Invariant($"{name} · {mission.TotalScu} SCU"),
            mission.Contract?.CrateSizeAssumed == true ? AssumedSizes(mission.Stops) : "",
            Incomplete(mission),
            duplicates.Contains(mission.Id) ? DuplicateText : "",
            $"⚠ {reason}{TickToDropText}",
        ]));
    }

    /// <summary>"4 SCU?", or "4/2 SCU?" for items with different max crate sizes; empty without items.</summary>
    private static string AssumedSizes(IEnumerable<MissionStop> stops)
    {
        int[] sizes = [.. stops.SelectMany(s => s.Items).Select(i => (int)i.MaxCrateSize).Distinct().OrderDescending()];
        return sizes.Length == 0 ? "" : Invariant($"{string.Join("/", sizes)} SCU?");
    }

    /// <summary>"⚠ 1 of 3 destinations" if the contract's drop-off list names more than were read; else empty.</summary>
    private static string Incomplete(Mission mission) =>
        mission.Contract?.ListedDestinations is { } listed && listed > mission.Stops.Count
            ? Invariant($"⚠ {mission.Stops.Count} of {listed} destinations")
            : "";

    private static string Joined(IEnumerable<string> parts) => string.Join(Separator, parts.Where(p => p.Length > 0));

    /// <summary>The plan with every crate on board: the legend's reserve keeps the markings a tick takes away (D16).</summary>
    private static SavedPlan OnBoard(SavedPlan saved) =>
        new(saved.Missions, saved.Plan with { Crates = [.. saved.Plan.Crates.Select(c => c with { Delivered = false })] }, saved.Watermark);

    /// <summary>
    /// The rack's crates where the plan stores them (D24), then the cells no crate takes. No layout here: the only one is
    /// <see cref="CrateLayout"/> in Core.
    /// </summary>
    private static SceneRack BuildRack(Rack rack, LoadPlan plan, IReadOnlyDictionary<string, int> palette, bool isViolated)
    {
        var crates = plan.CratesForRack(rack.Id);

        // Only a red rack can hold a faulty crate; valid plans skip the check of every pair.
        IReadOnlySet<Crate> faulty = isViolated ? CrateLayout.Faulty(rack, crates) : new HashSet<Crate>();
        var drawn = Drawn(crates);

        SceneBox[] cargo = [.. plan.PlacementsForRack(rack.Id).SelectMany(placement =>
        {
            Crate[] own = [.. drawn.Where(c => Belongs(c, placement))];
            var fill = placement.Delivered ? CellFill.Delivered : CellFill.Mission;
            return own.Select((crate, i) => new SceneBox(
                rack.CellBox(crate.Position, crate.CrateSize),
                fill,
                palette[placement.Destination],
                ToolTip(rack, placement, i + 1, own.Length),
                placement,
                crate,
                faulty.Contains(crate)));
        })];

        HashSet<GridPosition> taken = [.. drawn.SelectMany(c => c.Cells)];
        HashSet<int> loadedLayers = [.. taken.Select(c => c.Stack)];
        var grid = rack.Grid;
        SceneBox[] free =
        [
            .. from l in Enumerable.Range(0, grid.Length)
               from w in Enumerable.Range(0, grid.Width)
               from s in Enumerable.Range(0, grid.Stack)
               let cell = new GridPosition(l, w, s)
               where !taken.Contains(cell)
               select new SceneBox(rack.CellBox(cell, CrateSize.One), CellFill.Free, null, null, null, null, IsViolated: false, loadedLayers.Contains(s)),
        ];

        return new SceneRack(rack.Id, rack.Number, isViolated, rack.Bounds, [.. cargo, .. free], new RackLoad(rack.Number, plan.ScuOnBoard(rack.Id), rack.CapacityScu));
    }

    /// <summary>
    /// The crates of a rack to draw: every crate on board, and a delivered one only while its cells are free (D24 addendum:
    /// delivered cargo is off the ship). Of two delivered crates in the same cells, the later in the plan, the latest visit.
    /// </summary>
    private static IReadOnlyList<Crate> Drawn(IReadOnlyList<Crate> crates)
    {
        HashSet<GridPosition> taken = [.. crates.Where(c => !c.Delivered).SelectMany(c => c.Cells)];
        HashSet<Crate> hidden = new(ReferenceEqualityComparer.Instance);
        foreach (var crate in crates.Where(c => c.Delivered).Reverse())
        {
            if (crate.Cells.Any(taken.Contains))
            {
                hidden.Add(crate);
            }
            else
            {
                taken.UnionWith(crate.Cells);
            }
        }

        return [.. crates.Where(c => !hidden.Contains(c))];
    }

    /// <summary>The crate is one of the placement's: the grouping of <c>LoadPlan.Placements</c>, within one rack.</summary>
    private static bool Belongs(Crate crate, Placement placement) =>
        crate.Destination == placement.Destination
        && crate.Commodity == placement.Item.Commodity
        && crate.CrateSize == placement.Item.CrateSize
        && crate.Delivered == placement.Delivered;

    /// <summary>
    /// "Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)": <paramref name="number"/> counts the crate within
    /// its placement in plan order, <paramref name="count"/> is the placement's number of crates. A rack without a
    /// position (ship draft, T7.1b) leaves that segment out.
    /// </summary>
    private static string ToolTip(Rack rack, Placement placement, int number, int count)
    {
        var item = placement.Item;
        var position = rack.Position is { } p ? $"{p.Side} {p.Level} · ".ToLowerInvariant() : "";
        var text = Invariant($"{placement.Destination} · Rack {rack.Number} · {position}{(int)item.CrateSize} SCU {item.Commodity} crate ({number} of {count})");
        return placement.Delivered ? text + " (delivered)" : text;
    }
}
