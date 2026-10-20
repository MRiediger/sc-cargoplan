using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using static System.FormattableString;

using Score = (int Splits, int ItemCuts, int Squares);

namespace CargoPlan.Core.Planning;

/// <summary>Decides which rack each stop of a route goes into, and where its crates lie there (D24).</summary>
public static class LoadPlanner
{
    /// <summary>
    /// Plans the stops into the ship's racks. Racks fill in rack order (Rack 1 first) in route order, one rack
    /// per stop while racks last. With more stops than racks, only route-neighbours share a rack, spread evenly.
    /// A stop is split over neighbouring racks only if no plan without a split exists, preferably between
    /// its items. Every shared rack and every rack holding part of a split stop gets a warning. Then each rack's
    /// crates are laid out in <paramref name="fillOrder"/> (<see cref="CrateLayout.LayOut"/>), stored with the plan.
    /// </summary>
    /// <exception cref="ArgumentException">Two stops share an order or a destination.</exception>
    /// <exception cref="PlanException">
    /// A crate's shape fits no rack, the stops exceed the ship, or their crates don't fit in route order or in a rack's grid.
    /// </exception>
    public static LoadPlan Plan(Ship ship, IReadOnlyList<Stop> stops, FillOrder fillOrder = FillOrder.BowFirst)
    {
        EnsureUnique(stops);

        Stop[] route = [.. stops.OrderBy(s => s.Order)];
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        EnsureCratesFitShip(ship, route);
        EnsureFitsShip(ship, route);

        var line = CrateLine.Of(route);
        if (line.Length == 0)
        {
            return new LoadPlan(ship.Id, [], [], fillOrder);
        }

        var segments = Partition(racks, line);

        // Crates of one item lie next to each other, so each item becomes one placement per rack.
        List<Placement> placements = [.. segments.SelectMany(segment => line.Crates[segment.Start..segment.End]
            .GroupBy(c => (c.Stop, c.Item))
            .Select(g => Place(line.Stops[g.Key.Stop], g.Key.Item, segment.Rack, g.Sum(c => c.Scu))))];

        return WithHints(new LoadPlan(ship.Id, CrateLayout.LayOut(ship, placements, fillOrder), [], fillOrder), ship);
    }

    /// <summary>
    /// Puts one crate at a cell of a rack, as the player sees it at the cargo lift (D24): <see cref="MoveCrates"/> with one move.
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    /// <exception cref="PlanException">See <see cref="MoveCrates"/>.</exception>
    public static LoadPlan MoveCrate(LoadPlan plan, Ship ship, Crate crate, string targetRackId, GridPosition targetPosition) =>
        MoveCrates(plan, ship, [new CrateMove(crate, targetRackId, targetPosition)]);

    /// <summary>
    /// Puts crates at cells of racks, all at once (D25): every crate leaves its cells, then all take their new ones, so
    /// crates of one move may carry each other. Each must fit its grid, find its cells free and rest on the plate or
    /// other crates; no crate may lose its support. Nothing slides down: what doesn't hold is refused with the reason
    /// of <see cref="MoveProblem"/>, the player sees what they do. Crates keep their index, so placements keep their
    /// order; hints are derived anew. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, the ship has no such rack, or a crate isn't part of the plan, is delivered or
    /// moves twice.
    /// </exception>
    /// <exception cref="PlanException">A crate doesn't fit where it goes, or another crate would lose its support.</exception>
    public static LoadPlan MoveCrates(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves)
    {
        var crates = Moved(plan, ship, moves);
        if (moves.Count == 0)
        {
            return plan;
        }

        return Problem(plan, ship, moves, crates) is { } problem
            ? throw new PlanException(problem)
            : WithHints(plan with { Crates = crates }, ship);
    }

    /// <summary>
    /// Why <see cref="MoveCrates"/> would refuse these moves, or null if it wouldn't: the first new problem of
    /// <see cref="CrateLayout.NewProblem"/> in the racks the crates leave or enter, in rack order. One check for the
    /// overlay's preview while dragging and for the drop, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    public static string? MoveProblem(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves) =>
        Problem(plan, ship, moves, Moved(plan, ship, moves));

    /// <summary>
    /// Moves every crate of a stop still on board to another rack and lays them out there around the crates it holds,
    /// split parts included (D24); the overlay's drag of a legend line (D25). Delivered cargo is off the ship and stays,
    /// so a delivered stop moves nothing; a stop whose crates on board all lie in the target rack already stays as it is,
    /// it isn't laid out anew. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, has no stop <paramref name="destination"/>, or the ship has no such rack.
    /// </exception>
    /// <exception cref="PlanException">The crates don't fit the free space of the target rack.</exception>
    public static LoadPlan MoveStop(LoadPlan plan, Ship ship, string destination, string targetRackId)
    {
        plan.EnsureBelongsTo(ship);
        var target = RackOf(ship, targetRackId, nameof(targetRackId));
        EnsureStop(plan, destination);

        return Relayout(plan, ship, target, c => c.Destination == destination && !c.Delivered, destination);
    }

    /// <summary>
    /// Marks every crate of a stop as delivered, or back. A stop is unloaded in one visit, split parts included,
    /// so the unit is the stop. The hints are derived anew without delivered cargo; the crates keep their cells (D16,
    /// D24), so ticking back always gives the plan before. The original plan stays unchanged, crate order too.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no stop <paramref name="destination"/>.</exception>
    public static LoadPlan SetDelivered(LoadPlan plan, Ship ship, string destination, bool delivered)
    {
        plan.EnsureBelongsTo(ship);
        EnsureStop(plan, destination);

        return WithHints(plan with { Crates = [.. plan.Crates.Select(c => c.Destination == destination ? c with { Delivered = delivered } : c)] }, ship);
    }

    /// <summary>
    /// Splits a crate the way the kiosk delivered it (D27): <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>
    /// with <see cref="CrateMix.DefaultSplit"/>, the two largest parts.
    /// </summary>
    /// <exception cref="ArgumentException">See the overload with parts.</exception>
    /// <exception cref="PlanException">See the overload with parts.</exception>
    public static SavedPlan SplitCrate(SavedPlan saved, Ship ship, Crate crate) =>
        SplitCrate(saved, ship, crate, CrateMix.DefaultSplit(crate.CrateSize));

    /// <summary>
    /// Splits a crate into <paramref name="parts"/> that lie in its footprint (D27): every cell stays taken, so what lies
    /// on and next to it keeps its support, and the hints stay. The parts take the crate's place in the plan, largest
    /// first; its contract item takes the new crates as an override (<see cref="MissionItem.WithCrateList"/>). The
    /// original stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, or the crate isn't part of the plan or is delivered.
    /// </exception>
    /// <exception cref="PlanException">The crate can't be split into these parts (<see cref="SplitProblem(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>).</exception>
    public static SavedPlan SplitCrate(SavedPlan saved, Ship ship, Crate crate, IReadOnlyList<CrateSize> parts) =>
        Split(saved, ship, crate, parts).Applied;

    /// <summary>Why <see cref="SplitCrate(SavedPlan, Ship, Crate)"/> would refuse; null if it wouldn't.</summary>
    /// <exception cref="ArgumentException">See <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>.</exception>
    public static string? SplitProblem(SavedPlan saved, Ship ship, Crate crate) =>
        SplitProblem(saved, ship, crate, CrateMix.DefaultSplit(crate.CrateSize));

    /// <summary>
    /// Why <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/> would refuse, or null if it
    /// wouldn't: a 1 SCU crate, parts that are no split of the crate or don't fit its footprint. One check for the
    /// overlay's button and the split itself, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="SplitCrate(SavedPlan, Ship, Crate, IReadOnlyList{CrateSize})"/>.</exception>
    public static string? SplitProblem(SavedPlan saved, Ship ship, Crate crate, IReadOnlyList<CrateSize> parts) =>
        Split(saved, ship, crate, parts).Problem;

    /// <summary>
    /// Merges crates into one, the way the kiosk delivered them (D27): one destination, one commodity, one rack, sizes
    /// adding up to a crate size the contract allows, and together exactly that crate's box. The new crate takes their
    /// cells, so support and hints stay, and the place of the first of them in the plan; their contract item takes the
    /// new crates (<see cref="MissionItem.WithCrateList"/>). The original stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, there are fewer than two crates, or one isn't part of the plan, is delivered or
    /// named twice.
    /// </exception>
    /// <exception cref="PlanException">The crates can't be merged (<see cref="MergeProblem"/>).</exception>
    public static SavedPlan MergeCrates(SavedPlan saved, Ship ship, IReadOnlyList<Crate> crates) =>
        Merge(saved, ship, crates).Applied;

    /// <summary>
    /// Why <see cref="MergeCrates"/> would refuse, or null if it wouldn't. One check for the overlay's button and the
    /// merge itself, so both always agree (D25).
    /// </summary>
    /// <exception cref="ArgumentException">See <see cref="MergeCrates"/>.</exception>
    public static string? MergeProblem(SavedPlan saved, Ship ship, IReadOnlyList<Crate> crates) =>
        Merge(saved, ship, crates).Problem;

    /// <summary>
    /// Why the plan can't be loaded as it is, per rack in rack order (<see cref="CrateLayout.Problems"/>): crates that
    /// overlap, stick out, don't fit the grid or have nothing below them, and crates lying on cargo of an earlier stop.
    /// Delivered crates count, they keep their cells. The planner and its moves never make the first kind; only a plan file
    /// edited by hand gets there (D24). The order comes where a rack has no layout in route order or the player put a
    /// crate there (D24 addendum). Hints for the player (shared racks, split stops) are in <see cref="LoadPlan.Warnings"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or uses a rack the ship doesn't have.</exception>
    public static IReadOnlyList<PlanWarning> Validate(LoadPlan plan, Ship ship)
    {
        plan.EnsureBelongsTo(ship);
        if (plan.Crates.FirstOrDefault(c => !ship.Racks.Any(r => r.Id == c.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Crate for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(plan));
        }

        var route = plan.Route;
        return [.. ship.Racks.OrderBy(r => r.Index)
            .SelectMany(rack => CrateLayout.Problems(rack, plan.CratesForRack(rack.Id), route).Select(problem => new PlanWarning(rack.Id, problem)))];
    }

    /// <summary>
    /// Takes the crates <paramref name="moving"/> picks out of the plan and lays them out in <paramref name="target"/>
    /// around the crates that stay there. Moved crates keep their index in the crate list, so placements keep their order.
    /// Nothing to move, or everything already in the target rack: the plan as it is.
    /// </summary>
    private static LoadPlan Relayout(LoadPlan plan, Ship ship, Rack target, Func<Crate, bool> moving, string what)
    {
        int[] moved = [.. Enumerable.Range(0, plan.Crates.Count).Where(i => moving(plan.Crates[i]))];
        if (moved.All(i => plan.Crates[i].RackId == target.Id))
        {
            return plan;
        }

        Crate[] staying = [.. plan.Crates.Where((c, i) => c.RackId == target.Id && !moved.Contains(i))];
        var positions = CrateLayout.Place(
                target, staying, [.. moved.Select(i => (plan.Crates[i].CrateSize, plan.Crates[i].Destination))], plan.Route, plan.FillOrder)
            ?? throw new PlanException(Invariant(
                $"{what} doesn't fit rack {target.Number}: {staying.Sum(c => (int)c.CrateSize)}/{target.CapacityScu} SCU taken."));

        List<Crate> crates = [.. plan.Crates];
        foreach (var (index, position) in moved.Zip(positions))
        {
            crates[index] = crates[index] with { RackId = target.Id, Position = position };
        }

        return WithHints(plan with { Crates = crates }, ship);
    }

    /// <summary>
    /// The plan's crates with every move applied at the crate's index; checks the caller's part: ship, racks, crates of
    /// the plan, none delivered or moved twice.
    /// </summary>
    private static List<Crate> Moved(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves)
    {
        plan.EnsureBelongsTo(ship);
        List<Crate> crates = [.. plan.Crates];
        HashSet<int> taken = [];
        foreach (var (crate, rackId, position) in moves)
        {
            RackOf(ship, rackId, nameof(moves));
            var index = IndexOnBoard(plan, crate, taken, "moves", nameof(moves));
            crates[index] = crate with { RackId = rackId, Position = position };
        }

        return crates;
    }

    /// <summary>
    /// The index of <paramref name="crate"/> in the plan, added to <paramref name="taken"/>; checks the caller's part: a crate
    /// of the plan, not delivered (D16), not named twice (<paramref name="verb"/> says how it was named).
    /// </summary>
    private static int IndexOnBoard(LoadPlan plan, Crate crate, HashSet<int> taken, string verb, string paramName)
    {
        var index = IndexOf(plan.Crates, crate);
        if (index < 0)
        {
            throw new ArgumentException(
                Invariant($"{(int)crate.CrateSize} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} in '{crate.RackId}' is not part of the plan."),
                paramName);
        }

        if (!taken.Add(index))
        {
            throw new ArgumentException(
                Invariant($"{(int)crate.CrateSize} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} {verb} more than once."),
                paramName);
        }

        EnsureOnBoard(crate.Delivered, crate.Commodity, crate.Destination, paramName);
        return index;
    }

    /// <summary>The first new problem in a rack a move leaves or enters, in rack order; null if there is none.</summary>
    private static string? Problem(LoadPlan plan, Ship ship, IReadOnlyList<CrateMove> moves, List<Crate> crates)
    {
        HashSet<string> touched = [.. moves.SelectMany(m => (string[])[m.Crate.RackId, m.RackId])];
        return ship.Racks.OrderBy(r => r.Index)
            .Where(r => touched.Contains(r.Id))
            .Select(r => CrateLayout.NewProblem(r, plan.CratesForRack(r.Id), [.. crates.Where(c => c.RackId == r.Id)]))
            .FirstOrDefault(problem => problem is not null);
    }

    private static Edit Split(SavedPlan saved, Ship ship, Crate crate, IReadOnlyList<CrateSize> parts)
    {
        var plan = saved.Plan;
        plan.EnsureBelongsTo(ship);
        var index = IndexOnBoard(plan, crate, [], "is named", nameof(crate));
        var size = (int)crate.CrateSize;
        var sum = string.Join(" + ", parts.Select(p => (int)p));

        if (crate.CrateSize == CrateSize.One)
        {
            return Edit.Refused("1 SCU crates can't be split.");
        }

        if (parts.Count < 2)
        {
            return Edit.Refused(Invariant($"A {size} SCU crate splits into two or more crates."));
        }

        if (parts.Where(p => !Enum.IsDefined(p)).Select(p => (int?)p).FirstOrDefault() is { } unknown)
        {
            return Edit.Refused(Invariant($"{unknown} SCU is not a crate size."));
        }

        if (parts.Sum(p => (int)p) != size)
        {
            return Edit.Refused(Invariant($"{sum} SCU don't add up to {size} SCU."));
        }

        var rack = RackOf(ship, crate.RackId, nameof(crate));
        if (CrateLayout.PlaceWithin(rack, crate, parts, plan.FillOrder) is not { } positions)
        {
            return Edit.Refused(Invariant($"{sum} SCU don't fit the footprint of the {size} SCU crate ({crate.CrateSize.Dimensions})."));
        }

        // Largest first, as the missions expand them (MissionItem.ToCargoItems). OrderByDescending is stable.
        Crate[] pieces = [.. parts.Zip(positions)
            .OrderByDescending(p => p.First)
            .Select(p => new Crate(crate.Destination, crate.Commodity, p.First, crate.RackId, p.Second))];
        List<Crate> crates = [.. plan.Crates];
        crates.RemoveAt(index);
        crates.InsertRange(index, pieces);

        // The planner made the crate from an item that holds it (SavedPlan checks that), so there is one.
        var item = ItemsHolding(saved.Missions, crate.Destination, crate.Commodity, [crate.CrateSize]).First();
        return new Edit(new SavedPlan(Rebooked(saved.Missions, item, [crate.CrateSize], parts), WithHints(plan with { Crates = crates }, ship)), null);
    }

    private static Edit Merge(SavedPlan saved, Ship ship, IReadOnlyList<Crate> crates)
    {
        var plan = saved.Plan;
        plan.EnsureBelongsTo(ship);
        if (crates.Count < 2)
        {
            throw new ArgumentException("A merge takes two or more crates.", nameof(crates));
        }

        HashSet<int> taken = [];
        int[] indices = [.. crates.Select(c => IndexOnBoard(plan, c, taken, "is named", nameof(crates)))];
        var first = crates[0];

        if (crates.Any(c => c.Destination != first.Destination))
        {
            return Edit.Refused("The crates go to different destinations.");
        }

        if (crates.Any(c => c.Commodity != first.Commodity))
        {
            return Edit.Refused("The crates hold different commodities.");
        }

        if (crates.Any(c => c.RackId != first.RackId))
        {
            return Edit.Refused("The crates lie in different racks.");
        }

        var merged = (CrateSize)crates.Sum(c => (int)c.CrateSize);
        if (!Enum.IsDefined(merged))
        {
            return Edit.Refused(Invariant($"{string.Join(" + ", crates.Select(c => (int)c.CrateSize).OrderDescending())} is not a crate size."));
        }

        // One item has to hold them all: its SCU is contract data and must not move to another item.
        CrateSize[] sizes = [.. crates.Select(c => c.CrateSize)];
        (int Stop, int Item)[] holding = [.. ItemsHolding(saved.Missions, first.Destination, first.Commodity, sizes)];
        if (holding.Length == 0)
        {
            return Edit.Refused("The crates belong to different contract items.");
        }

        // The kiosk never delivers a crate above the max crate size (D23).
        (int Stop, int Item)[] allowed = [.. holding.Where(at => saved.Missions[at.Stop].Items[at.Item].MaxCrateSize >= merged)];
        if (allowed.Length == 0)
        {
            var max = (int)saved.Missions[holding[0].Stop].Items[holding[0].Item].MaxCrateSize;
            return Edit.Refused(Invariant($"{(int)merged} SCU crates are larger than the max crate size of {max} SCU for {first.Commodity}."));
        }

        if (CrateLayout.BlockProblem(crates, merged) is { } problem)
        {
            return Edit.Refused(problem);
        }

        // The new crate takes the place of the first of them in the plan; the others go, from the back.
        GridPosition corner = new(crates.Min(c => c.Position.Length), crates.Min(c => c.Position.Width), crates.Min(c => c.Position.Stack));
        List<Crate> list = [.. plan.Crates];
        list[indices.Min()] = new Crate(first.Destination, first.Commodity, merged, first.RackId, corner);
        foreach (var index in indices.Order().Skip(1).Reverse())
        {
            list.RemoveAt(index);
        }

        return new Edit(new SavedPlan(Rebooked(saved.Missions, allowed[0], sizes, [merged]), WithHints(plan with { Crates = list }, ship)), null);
    }

    /// <summary>
    /// Stop and item index of every contract item for <paramref name="destination"/> and <paramref name="commodity"/>
    /// whose crates include all of <paramref name="crates"/>, in mission order.
    /// </summary>
    private static IEnumerable<(int Stop, int Item)> ItemsHolding(
        IReadOnlyList<MissionStop> missions, string destination, string commodity, IReadOnlyList<CrateSize> crates) =>
        from s in Enumerable.Range(0, missions.Count)
        where missions[s].Destination == destination
        from i in Enumerable.Range(0, missions[s].Items.Count)
        let item = missions[s].Items[i]
        where item.Commodity == commodity && Includes(item.CrateList, crates)
        select (s, i);

    /// <summary>True if <paramref name="list"/> holds every crate of <paramref name="wanted"/>, each as often as it is wanted.</summary>
    private static bool Includes(IReadOnlyList<CrateSize> list, IReadOnlyList<CrateSize> wanted)
    {
        List<CrateSize> rest = [.. list];
        return wanted.All(rest.Remove);
    }

    /// <summary>The missions with item <paramref name="at"/> holding <paramref name="added"/> instead of <paramref name="removed"/>.</summary>
    private static IReadOnlyList<MissionStop> Rebooked(
        IReadOnlyList<MissionStop> missions, (int Stop, int Item) at, IReadOnlyList<CrateSize> removed, IReadOnlyList<CrateSize> added)
    {
        var stop = missions[at.Stop];
        var item = stop.Items[at.Item];
        List<CrateSize> crates = [.. item.CrateList];
        foreach (var size in removed)
        {
            crates.Remove(size);
        }

        MissionItem[] items = [.. stop.Items];
        items[at.Item] = item.WithCrateList([.. crates, .. added]);
        MissionStop[] stops = [.. missions];
        stops[at.Stop] = stop with { Items = items };
        return stops;
    }

    /// <summary>A split or merge: the saved plan it gives, or why it is refused.</summary>
    private sealed record Edit(SavedPlan? Result, string? Problem)
    {
        public static Edit Refused(string problem) => new(null, problem);

        public SavedPlan Applied => Result ?? throw new PlanException(Problem ?? "");
    }

    private static int IndexOf(IReadOnlyList<Crate> crates, Crate crate)
    {
        for (var i = 0; i < crates.Count; i++)
        {
            if (crates[i] == crate)
            {
                return i;
            }
        }

        return -1;
    }

    private static LoadPlan WithHints(LoadPlan plan, Ship ship) =>
        plan with { Warnings = Hints([.. ship.Racks.OrderBy(r => r.Index)], plan.Placements) };

    private static Rack RackOf(Ship ship, string rackId, string paramName) =>
        ship.Racks.FirstOrDefault(r => r.Id == rackId)
        ?? throw new ArgumentException($"Ship '{ship.Id}' has no rack '{rackId}'.", paramName);

    private static void EnsureStop(LoadPlan plan, string destination)
    {
        if (!plan.Crates.Any(c => c.Destination == destination))
        {
            throw new ArgumentException($"The plan has no stop '{destination}'.", nameof(destination));
        }
    }

    /// <summary>Delivered cargo is off the ship (D16): it can't be moved.</summary>
    private static void EnsureOnBoard(bool delivered, string commodity, string destination, string paramName)
    {
        if (delivered)
        {
            throw new ArgumentException($"{commodity} for {destination} is delivered, its crates are off the ship.", paramName);
        }
    }

    /// <summary>
    /// What the player has to watch at the cargo lift, derived from the placements alone: every rack holding more
    /// than one destination, and every rack holding part of a destination that spans racks. Delivered cargo is off
    /// the ship and doesn't count (D16). Destinations keep placement order, which is route order for a planned stop.
    /// <paramref name="racks"/> must be in rack order.
    /// </summary>
    private static List<PlanWarning> Hints(Rack[] racks, IReadOnlyList<Placement> allPlacements)
    {
        Placement[] placements = [.. allPlacements.Where(p => !p.Delivered)];
        var racksOfDestination = placements
            .GroupBy(p => p.Destination)
            .ToDictionary(g => g.Key, g => racks.Where(r => g.Any(p => p.RackId == r.Id)).ToArray());

        List<PlanWarning> warnings = [];
        foreach (var rack in racks)
        {
            string[] inRack = [.. placements.Where(p => p.RackId == rack.Id).Select(p => p.Destination).Distinct()];
            if (inRack.Length > 1)
            {
                warnings.Add(new PlanWarning(rack.Id, Invariant(
                    $"Rack {rack.Number} shared by {string.Join(", ", inRack)} – identify crates by count")));
            }

            // Same message in every spanned rack, so the overview can show it once.
            foreach (var destination in inRack)
            {
                var spanned = racksOfDestination[destination];
                if (spanned.Length > 1)
                {
                    warnings.Add(new PlanWarning(rack.Id, Invariant(
                        $"{destination} spans racks {string.Join("+", spanned.Select(r => r.Number))}")));
                }
            }
        }

        return warnings;
    }

    private static void EnsureUnique(IReadOnlyList<Stop> stops)
    {
        if (stops.GroupBy(s => s.Order).FirstOrDefault(g => g.Count() > 1) is { } order)
        {
            throw new ArgumentException(Invariant($"Order {order.Key} is used by more than one stop."), nameof(stops));
        }

        // Case-insensitive like the mission file: "Riker" and "riker" are the same cargo lift.
        if (stops.GroupBy(s => s.Destination, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } destination)
        {
            throw new ArgumentException($"Destination '{destination.Key}' is used by more than one stop.", nameof(stops));
        }
    }

    /// <summary>
    /// Names the first crate in route order whose shape fits no rack. Checked per crate, not per mix:
    /// for Hull A every mix up to the rack capacity fits (D9).
    /// </summary>
    private static void EnsureCratesFitShip(Ship ship, Stop[] route)
    {
        foreach (var stop in route)
        {
            if (stop.Items.FirstOrDefault(i => !ship.Racks.Any(r => r.CanHold(i.CrateSize))) is { } item)
            {
                // MaxBy keeps the first rack on a tie.
                var largest = ship.Racks.MaxBy(r => r.Grid.Volume);
                throw new PlanException(Invariant(
                    $"{(int)item.CrateSize} SCU crate of {item.Commodity} for {stop.Destination} doesn't fit any rack of {ship.Id} (largest rack {largest?.Grid})."));
            }
        }
    }

    /// <summary>Names the first stop in route order that no longer fits into the ship.</summary>
    private static void EnsureFitsShip(Ship ship, Stop[] route)
    {
        var needed = route.Sum(s => s.TotalScu);
        var loaded = 0;
        foreach (var stop in route)
        {
            loaded += stop.TotalScu;
            if (loaded > ship.TotalScu)
            {
                throw new PlanException(Invariant(
                    $"Ship capacity exceeded: {needed - ship.TotalScu} SCU missing, stop '{stop.Destination}' doesn't fit (stops need {needed} SCU, ship '{ship.Id}' holds {ship.TotalScu} SCU)."));
            }
        }
    }

    private static Placement Place(Stop stop, int itemIndex, Rack rack, int scu)
    {
        var item = stop.Items[itemIndex];
        return new Placement(
            stop.Destination, rack.Id, scu == item.Scu ? item : new CargoItem(item.Commodity, scu, item.CrateSize));
    }

    /// <summary>
    /// Cuts the crate line into one contiguous segment per rack, in rack order. Best cut: fewest split stops,
    /// then fewest split items, then fewest stops in the fullest rack, then the smallest sum of squared stop
    /// counts (spreads sharing evenly), then fewest racks. Ties share in the lowest-numbered racks.
    /// </summary>
    private static Segment[] Partition(Rack[] racks, CrateLine line)
    {
        var (unbounded, _) = Solve(racks, line, line.Stops.Length);
        var rackCount = BestRackCount(unbounded, line.Length);
        if (rackCount < 0)
        {
            throw CratesDontFit(line, unbounded);
        }

        // "Fewest stops in the fullest rack" isn't additive, so it can't be part of the DP score. Instead:
        // the smallest cap on stops per rack that still reaches the fewest split stops and items.
        var target = unbounded[line.Length, rackCount].GetValueOrDefault();
        for (var cap = 1; ; cap++)
        {
            var (best, segmentStart) = Solve(racks, line, cap);
            rackCount = BestRackCount(best, line.Length);
            if (rackCount < 0 || best[line.Length, rackCount] is not { } score
                || (score.Splits, score.ItemCuts) != (target.Splits, target.ItemCuts))
            {
                continue;
            }

            var segments = new Segment[rackCount];
            for (int r = rackCount, end = line.Length; r > 0; r--)
            {
                var start = segmentStart[end, r];
                segments[r - 1] = new Segment(racks[r - 1], start, end);
                end = start;
            }

            return segments;
        }
    }

    /// <summary>
    /// Dynamic programming over (first `end` crates, first r racks), at most <paramref name="maxStopsPerRack"/>
    /// stops per rack. best[end, r]: score of crates [0, end) in racks [0, r), no rack empty; null = impossible.
    /// </summary>
    private static (Score?[,] Best, int[,] SegmentStart) Solve(Rack[] racks, CrateLine line, int maxStopsPerRack)
    {
        var best = new Score?[line.Length + 1, racks.Length + 1];
        var segmentStart = new int[line.Length + 1, racks.Length + 1];
        best[0, 0] = (0, 0, 0);

        for (var r = 1; r <= racks.Length; r++)
        {
            for (var end = r; end <= line.Length; end++)
            {
                var cutsStop = line.CutsStop(end);
                var cutsItem = line.CutsItem(end);

                // Smallest last segment first; ties keep it, so sharing ends up in the lowest-numbered racks.
                for (var start = end - 1; start >= r - 1; start--)
                {
                    var stopsInRack = line.StopsIn(start, end);
                    if (line.ScuBefore[end] - line.ScuBefore[start] > racks[r - 1].CapacityScu || stopsInRack > maxStopsPerRack)
                    {
                        break;
                    }

                    if (best[start, r - 1] is not { } previous)
                    {
                        continue;
                    }

                    // A stop counts as split once, at its first boundary: the one whose segment holds the stop's start.
                    var newSplit = cutsStop && start <= line.StopStart[line.Crates[end].Stop];
                    Score score = (
                        previous.Splits + (newSplit ? 1 : 0),
                        previous.ItemCuts + (cutsItem ? 1 : 0),
                        previous.Squares + (stopsInRack * stopsInRack));
                    if (best[end, r] is not { } current || score.CompareTo(current) < 0)
                    {
                        best[end, r] = score;
                        segmentStart[end, r] = start;
                    }
                }
            }
        }

        return (best, segmentStart);
    }

    /// <summary>Rack count with the best score for the whole line, fewest racks on a tie; -1 = impossible.</summary>
    private static int BestRackCount(Score?[,] best, int length) =>
        Enumerable.Range(0, best.GetLength(1))
            .Where(r => best[length, r] is not null)
            .OrderBy(r => best[length, r])
            .DefaultIfEmpty(-1)
            .First();

    /// <summary>Names the first stop whose crates can't be packed behind the stops before it.</summary>
    private static PlanException CratesDontFit(CrateLine line, Score?[,] best)
    {
        // Feasibility only gets lost while adding crates, so the first unreachable stop end pins the culprit.
        var stop = Enumerable.Range(0, line.Stops.Length)
            .First(s => Enumerable.Range(0, best.GetLength(1)).All(r => best[line.StopEnd(s), r] is null));

        return new PlanException(Invariant(
            $"Crates don't fit: stop '{line.Stops[stop].Destination}' can't be packed in whole crates into the racks behind the previous stops."));
    }

    /// <summary>One crate of item <see cref="Item"/> of stop <see cref="Stop"/> (indices into <see cref="CrateLine.Stops"/>).</summary>
    private readonly record struct LineCrate(int Stop, int Item, int Scu);

    /// <summary>Crates [Start, End) of the line go into <see cref="Rack"/>.</summary>
    private readonly record struct Segment(Rack Rack, int Start, int End);

    /// <summary>
    /// The route as one row of crates: stops in route order, items in input order. Rack boundaries fall
    /// between crates, so racks always hold whole crates and neighbouring parts of the route.
    /// </summary>
    private sealed record CrateLine(Stop[] Stops, LineCrate[] Crates, int[] ScuBefore, int[] StopStart)
    {
        public int Length => Crates.Length;

        public static CrateLine Of(IEnumerable<Stop> route)
        {
            // A stop without items has no crates and takes no rack.
            Stop[] stops = [.. route.Where(s => s.Items.Count > 0)];
            List<LineCrate> crates = [];
            var stopStart = new int[stops.Length];
            for (var s = 0; s < stops.Length; s++)
            {
                stopStart[s] = crates.Count;
                for (var i = 0; i < stops[s].Items.Count; i++)
                {
                    var crateScu = (int)stops[s].Items[i].CrateSize;
                    crates.AddRange(Enumerable.Repeat(new LineCrate(s, i, crateScu), stops[s].Items[i].Scu / crateScu));
                }
            }

            var scuBefore = new int[crates.Count + 1];
            for (var p = 0; p < crates.Count; p++)
            {
                scuBefore[p + 1] = scuBefore[p] + crates[p].Scu;
            }

            return new CrateLine(stops, [.. crates], scuBefore, stopStart);
        }

        public int StopEnd(int stop) => stop + 1 < Stops.Length ? StopStart[stop + 1] : Length;

        /// <summary>Number of stops with crates in [start, end).</summary>
        public int StopsIn(int start, int end) => Crates[end - 1].Stop - Crates[start].Stop + 1;

        /// <summary>A rack boundary before crate p splits a stop.</summary>
        public bool CutsStop(int p) => p > 0 && p < Length && Crates[p - 1].Stop == Crates[p].Stop;

        /// <summary>A rack boundary before crate p splits an item.</summary>
        public bool CutsItem(int p) => CutsStop(p) && Crates[p - 1].Item == Crates[p].Item;
    }
}
