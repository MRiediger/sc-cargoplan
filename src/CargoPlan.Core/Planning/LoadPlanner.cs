using CargoPlan.Core.Models;
using static System.FormattableString;

using Score = (int Splits, int ItemCuts, int Squares);

namespace CargoPlan.Core.Planning;

/// <summary>Decides which rack each stop of a route goes into.</summary>
public static class LoadPlanner
{
    /// <summary>
    /// Plans the stops into the ship's racks. Racks fill in rack order (Rack 1 first) in route order, one rack
    /// per stop while racks last. With more stops than racks, only route-neighbours share a rack, spread evenly.
    /// A stop is split over neighbouring racks only if no plan without a split exists, preferably between
    /// its items. Every shared rack and every rack holding part of a split stop gets a warning.
    /// </summary>
    /// <exception cref="ArgumentException">Two stops share an order or a destination.</exception>
    /// <exception cref="PlanException">
    /// A crate's shape fits no rack, the stops exceed the ship, or their crates don't fit in route order.
    /// </exception>
    public static LoadPlan Plan(Ship ship, IReadOnlyList<Stop> stops)
    {
        EnsureUnique(stops);

        Stop[] route = [.. stops.OrderBy(s => s.Order)];
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        EnsureCratesFitShip(ship, route);
        EnsureFitsShip(ship, route);

        var line = CrateLine.Of(route);
        if (line.Length == 0)
        {
            return new LoadPlan(ship.Id, [], []);
        }

        var segments = Partition(racks, line);

        // Crates of one item lie next to each other, so each item becomes one placement per rack.
        List<Placement> placements = [.. segments.SelectMany(segment => line.Crates[segment.Start..segment.End]
            .GroupBy(c => (c.Stop, c.Item))
            .Select(g => Place(line.Stops[g.Key.Stop], g.Key.Item, segment.Rack, g.Sum(c => c.Scu))))];

        return new LoadPlan(ship.Id, placements, Hints(racks, placements));
    }

    /// <summary>
    /// Moves crates to another rack, as the player decides at the cargo lift (D22). Each move takes <c>Scu</c> of its
    /// source in whole crates; the rest stays. In the target rack the crates join cargo of the same destination,
    /// commodity and crate size; without such cargo all crates of a source keep its place in the plan, a part goes
    /// behind the target rack's cargo. Afterwards every rack holds at most one placement per destination, commodity,
    /// crate size and delivered state. Never refuses an overloaded rack or a crate that doesn't fit; <see cref="Validate"/>
    /// reports those. The hints are derived anew, so the plan never shows stale ones. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, the ship has no such rack, a source isn't part of the plan (or is named twice)
    /// or is delivered, or a move isn't whole crates of what its source holds.
    /// </exception>
    public static LoadPlan MoveCrates(LoadPlan plan, Ship ship, IReadOnlyList<(Placement Source, int Scu)> moves, string targetRackId)
    {
        plan.EnsureBelongsTo(ship);
        if (!ship.Racks.Any(r => r.Id == targetRackId))
        {
            throw new ArgumentException($"Ship '{ship.Id}' has no rack '{targetRackId}'.", nameof(targetRackId));
        }

        // Copy first, then change the copy: the original plan keeps its list. Null = all crates of a source joined others.
        List<Placement?> placements = [.. plan.Placements];
        foreach (var (index, scu) in Claim(plan.Placements, moves))
        {
            // Only the move's own source index changes, so the original placement is still the current one.
            var source = plan.Placements[index];
            if (source.RackId == targetRackId)
            {
                continue;
            }

            var rest = source.Item.Scu - scu;
            var partner = placements.FindIndex(p => p is { Delivered: false } && p.RackId == targetRackId && SameCargo(p, source));
            if (partner >= 0)
            {
                placements[partner] = Resized(placements[partner]!, placements[partner]!.Item.Scu + scu);
                placements[index] = rest > 0 ? Resized(source, rest) : null;
            }
            else if (rest == 0)
            {
                placements[index] = source with { RackId = targetRackId };
            }
            else
            {
                placements[index] = Resized(source, rest);
                placements.Add(Resized(source, scu) with { RackId = targetRackId });
            }
        }

        var merged = Merged(placements.OfType<Placement>());
        return plan with { Placements = merged, Warnings = Hints([.. ship.Racks.OrderBy(r => r.Index)], merged) };
    }

    /// <summary>Moves one whole placement to another rack: <see cref="MoveCrates"/> with all its crates.</summary>
    /// <exception cref="ArgumentException">See <see cref="MoveCrates"/>.</exception>
    public static LoadPlan MovePlacement(LoadPlan plan, Ship ship, Placement placement, string targetRackId) =>
        MoveCrates(plan, ship, [(placement, placement.Item.Scu)], targetRackId);

    /// <summary>
    /// Moves every crate of a stop still on board to another rack; split parts join there (D22). Delivered cargo is off
    /// the ship and stays, so a delivered stop moves nothing. The original plan stays unchanged.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The plan belongs to another ship, has no stop <paramref name="destination"/>, or the ship has no such rack.
    /// </exception>
    public static LoadPlan MoveStop(LoadPlan plan, Ship ship, string destination, string targetRackId)
    {
        plan.EnsureBelongsTo(ship);
        if (!plan.Placements.Any(p => p.Destination == destination))
        {
            throw new ArgumentException($"The plan has no stop '{destination}'.", nameof(destination));
        }

        return MoveCrates(plan, ship,
            [.. plan.Placements.Where(p => p.Destination == destination && !p.Delivered).Select(p => (Source: p, p.Item.Scu))],
            targetRackId);
    }

    /// <summary>
    /// Marks every placement of a stop as delivered, or back. A stop is unloaded in one visit, split parts included,
    /// so the unit is the stop, not the placement. The hints are derived anew without delivered cargo; capacity in
    /// <see cref="Validate"/> still counts it (D16). The original plan stays unchanged, placement order too.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or has no stop <paramref name="destination"/>.</exception>
    public static LoadPlan SetDelivered(LoadPlan plan, Ship ship, string destination, bool delivered)
    {
        plan.EnsureBelongsTo(ship);
        if (!plan.Placements.Any(p => p.Destination == destination))
        {
            throw new ArgumentException($"The plan has no stop '{destination}'.", nameof(destination));
        }

        List<Placement> placements = [.. plan.Placements
            .Select(p => p.Destination == destination ? p with { Delivered = delivered } : p)];
        return plan with { Placements = placements, Warnings = Hints([.. ship.Racks.OrderBy(r => r.Index)], placements) };
    }

    /// <summary>
    /// Violations that keep the plan from being loaded, per rack in rack order: more SCU than the rack holds,
    /// and crates whose shape doesn't fit the rack (per crate, not per mix, D9). Empty for every plan from
    /// <see cref="Plan"/>; hints for the player (shared racks, split stops) are in <see cref="LoadPlan.Warnings"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship or uses a rack the ship doesn't have.</exception>
    public static IReadOnlyList<PlanWarning> Validate(LoadPlan plan, Ship ship)
    {
        plan.EnsureBelongsTo(ship);
        if (plan.Placements.FirstOrDefault(p => !ship.Racks.Any(r => r.Id == p.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Placement for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(plan));
        }

        List<PlanWarning> violations = [];
        foreach (var rack in ship.Racks.OrderBy(r => r.Index))
        {
            var placements = plan.PlacementsForRack(rack.Id);
            var used = placements.Sum(p => p.Item.Scu);
            if (used > rack.CapacityScu)
            {
                violations.Add(new PlanWarning(rack.Id, Invariant($"Rack {rack.Number} overloaded: {used}/{rack.CapacityScu} SCU")));
            }

            violations.AddRange(placements
                .Where(p => !rack.CanHold(p.Item.CrateSize))
                .Select(p => new PlanWarning(rack.Id, Invariant(
                    $"{(int)p.Item.CrateSize} SCU crate of {p.Item.Commodity} for {p.Destination} doesn't fit rack {rack.Number} ({rack.Grid})"))));
        }

        return violations;
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

    /// <summary>
    /// Pairs each move with the index of its source in <paramref name="placements"/>. Equal placements are
    /// interchangeable, so each move takes the first one no earlier move took.
    /// </summary>
    private static List<(int Index, int Scu)> Claim(IReadOnlyList<Placement> placements, IReadOnlyList<(Placement Source, int Scu)> moves)
    {
        List<(int Index, int Scu)> claimed = [];
        foreach (var (source, scu) in moves)
        {
            var (item, crate) = (source.Item, (int)source.Item.CrateSize);
            var index = Enumerable.Range(0, placements.Count)
                .FirstOrDefault(i => placements[i] == source && !claimed.Exists(c => c.Index == i), -1);
            if (index < 0)
            {
                throw new ArgumentException(Invariant(
                    $"Placement of {item.Scu} SCU {item.Commodity} for {source.Destination} in '{source.RackId}' is not part of the plan or is moved twice."),
                    nameof(moves));
            }

            if (source.Delivered)
            {
                throw new ArgumentException($"{item.Commodity} for {source.Destination} is delivered, its crates are off the ship.", nameof(moves));
            }

            if (scu <= 0 || scu % crate != 0)
            {
                throw new ArgumentException(Invariant(
                    $"{scu} SCU of {item.Commodity} for {source.Destination} is not a whole number of its {crate} SCU crates."),
                    nameof(moves));
            }

            if (scu > item.Scu)
            {
                throw new ArgumentException(Invariant(
                    $"{scu} SCU of {item.Commodity} for {source.Destination} to move, but only {item.Scu} SCU are placed in '{source.RackId}'."),
                    nameof(moves));
            }

            claimed.Add((index, scu));
        }

        return claimed;
    }

    /// <summary>
    /// Joins placements of the same cargo and state in the same rack into the first one. GroupBy yields the groups in
    /// the order of their first element, so plan order stays (learn.microsoft.com, Enumerable.GroupBy, Remarks).
    /// </summary>
    private static List<Placement> Merged(IEnumerable<Placement> placements) =>
        [.. placements
            .GroupBy(p => (p.RackId, p.Destination, p.Item.Commodity, p.Item.CrateSize, p.Delivered))
            .Select(g => g.Skip(1).Any() ? Resized(g.First(), g.Sum(p => p.Item.Scu)) : g.First())];

    private static bool SameCargo(Placement a, Placement b) =>
        a.Destination == b.Destination && a.Item.Commodity == b.Item.Commodity && a.Item.CrateSize == b.Item.CrateSize;

    /// <summary>The same cargo with another amount; <c>with</c> can't change <see cref="CargoItem.Scu"/> (get-only).</summary>
    private static Placement Resized(Placement placement, int scu) =>
        placement with { Item = new CargoItem(placement.Item.Commodity, scu, placement.Item.CrateSize) };

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
    private readonly record struct Crate(int Stop, int Item, int Scu);

    /// <summary>Crates [Start, End) of the line go into <see cref="Rack"/>.</summary>
    private readonly record struct Segment(Rack Rack, int Start, int End);

    /// <summary>
    /// The route as one row of crates: stops in route order, items in input order. Rack boundaries fall
    /// between crates, so racks always hold whole crates and neighbouring parts of the route.
    /// </summary>
    private sealed record CrateLine(Stop[] Stops, Crate[] Crates, int[] ScuBefore, int[] StopStart)
    {
        public int Length => Crates.Length;

        public static CrateLine Of(IEnumerable<Stop> route)
        {
            // A stop without items has no crates and takes no rack.
            Stop[] stops = [.. route.Where(s => s.Items.Count > 0)];
            List<Crate> crates = [];
            var stopStart = new int[stops.Length];
            for (var s = 0; s < stops.Length; s++)
            {
                stopStart[s] = crates.Count;
                for (var i = 0; i < stops[s].Items.Count; i++)
                {
                    var crateScu = (int)stops[s].Items[i].CrateSize;
                    crates.AddRange(Enumerable.Repeat(new Crate(s, i, crateScu), stops[s].Items[i].Scu / crateScu));
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
