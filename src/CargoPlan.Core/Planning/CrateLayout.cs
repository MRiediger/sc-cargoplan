using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Planning;

/// <summary>
/// Where crates lie in a rack's grid (D24). The planner assigns racks; this lays the crates of a rack into cells, the
/// same way every time, so planner, plan file migration and moves agree. Knows only crate sizes and grid positions.
/// </summary>
public static class CrateLayout
{
    /// <summary>
    /// Positions for <paramref name="crates"/> in the free cells of <paramref name="rack"/>, in their order; null if one
    /// doesn't fit. Fill order: layer by layer from the plate; in a layer row by row in grid order (the rack's stack axis
    /// makes port and starboard racks mirror images, D26); in a row along the length from the end <paramref name="order"/>
    /// names (D21). The largest crate goes first (ties in input order), each at the free place whose first cell comes first
    /// in fill order, on a tie whose last cell does, and only where it rests on the plate or on other crates.
    /// <paramref name="occupied"/> keep their cells; delivered ones are off the ship, their cells are free (D24 addendum).
    /// The stacking rule is tried, not forced (D24 addendum): if that layout lays a new crate on a crate of an earlier stop
    /// of <paramref name="route"/>, an exact search looks for one that doesn't. If there is none, the first layout stays and
    /// <see cref="Problems"/> names the crate: the cargo fits, so the order is a finding, never a refusal.
    /// </summary>
    public static IReadOnlyList<GridPosition>? Place(
        Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<(CrateSize Size, string Destination)> crates, IReadOnlyList<string> route, FillOrder order)
    {
        occupied = [.. occupied.Where(c => !c.Delivered)];
        if (Place(RackSpace.Around(rack, occupied, order), [.. crates.Select(c => c.Size)]) is not { } positions)
        {
            return null;
        }

        // Only what the new crates break counts; a plan file edited by hand may bring its own disorder.
        Crate[] placed = [.. crates.Zip(positions).Select(p => new Crate(p.First.Destination, "", p.First.Size, rack.Id, p.Second))];
        HashSet<Crate> fresh = new(placed, ReferenceEqualityComparer.Instance);
        return OutOfOrder([.. occupied, .. placed], route).Any(pair => fresh.Contains(pair.Upper) || fresh.Contains(pair.Lower))
            ? InOrder(rack, occupied, crates, route, order) ?? positions
            : positions;
    }

    /// <summary>
    /// <see cref="Place"/> for the parts of a split (D27): only the cells of <paramref name="box"/> are free, every other
    /// cell counts as taken. The parts fill the crate's footprint, and what lay below the crate carries them as it carried
    /// the crate, so nothing around it changes. Null if the parts don't fit the box.
    /// </summary>
    public static IReadOnlyList<GridPosition>? PlaceWithin(Rack rack, Crate box, IReadOnlyList<CrateSize> sizes, FillOrder order)
    {
        var grid = rack.Grid;
        var taken = new bool[grid.Length, grid.Width, grid.Stack];
        foreach (var cell in Cells(new GridPosition(0, 0, 0), grid))
        {
            taken[cell.Length, cell.Width, cell.Stack] = true;
        }

        foreach (var cell in box.Cells.Where(c => Inside(grid, c)))
        {
            taken[cell.Length, cell.Width, cell.Stack] = false;
        }

        return Place(new RackSpace(rack, taken, order), sizes);
    }

    /// <summary>
    /// Why <paramref name="crates"/> can't become one crate of <paramref name="size"/> where they lie (D27); null if their
    /// cells are exactly that crate's box, in its only orientation (D9). The new crate then takes the same cells, so it
    /// carries and rests as they did.
    /// </summary>
    public static string? BlockProblem(IReadOnlyList<Crate> crates, CrateSize size)
    {
        HashSet<GridPosition> cells = [.. crates.SelectMany(c => c.Cells)];
        if (cells.Count < crates.Sum(c => (int)c.CrateSize))
        {
            return "The crates overlap.";
        }

        if (!Connected(cells))
        {
            return "The crates are not adjacent.";
        }

        Dimensions block = new(
            cells.Max(c => c.Length) - cells.Min(c => c.Length) + 1,
            cells.Max(c => c.Width) - cells.Min(c => c.Width) + 1,
            cells.Max(c => c.Stack) - cells.Min(c => c.Stack) + 1);
        if (block.Volume != cells.Count)
        {
            return "The crates don't form a block.";
        }

        return block == size.Dimensions
            ? null
            : Invariant($"Together they would be a {block} crate, {(int)size} SCU crates are {size.Dimensions}.");
    }

    /// <summary>The lay-out of <see cref="Place"/> into the free cells of <paramref name="space"/>, which it fills.</summary>
    private static GridPosition[]? Place(RackSpace space, IReadOnlyList<CrateSize> sizes)
    {
        var positions = new GridPosition[sizes.Count];

        // OrderByDescending is stable: crates of one size keep input order.
        foreach (var i in Enumerable.Range(0, sizes.Count).OrderByDescending(i => sizes[i]))
        {
            if (space.Add(sizes[i]) is not { } position)
            {
                return null;
            }

            positions[i] = position;
        }

        return positions;
    }

    /// <summary>
    /// The free cells of one rack, filled crate by crate as <see cref="Place"/> fills them: each crate at its first fit
    /// (<see cref="FirstFit"/>). The one code path for laying out and for probing a rack (D54): crates added largest
    /// first take exactly the cells <see cref="Place"/> gives the same sizes around the same cargo.
    /// </summary>
    internal sealed class RackSpace
    {
        private readonly bool[,,] _taken;
        private readonly int[,,] _rank;

        internal RackSpace(Rack rack, bool[,,] taken, FillOrder order) => (_taken, _rank) = (taken, Ranks(rack, order));

        /// <summary>The free cells of <paramref name="rack"/> around the cargo on board of <paramref name="occupied"/>.</summary>
        public static RackSpace Around(Rack rack, IEnumerable<Crate> occupied, FillOrder order)
        {
            var grid = rack.Grid;
            var taken = new bool[grid.Length, grid.Width, grid.Stack];
            foreach (var cell in occupied.Where(c => !c.Delivered).SelectMany(c => c.Cells).Where(c => Inside(grid, c)))
            {
                taken[cell.Length, cell.Width, cell.Stack] = true;
            }

            return new RackSpace(rack, taken, order);
        }

        /// <summary>Takes the first fit for a crate of <paramref name="size"/> and returns it; null, and nothing taken, if there is none.</summary>
        public GridPosition? Add(CrateSize size)
        {
            if (FirstFit(size.Dimensions, _rank, _taken) is not { } position)
            {
                return null;
            }

            foreach (var cell in Cells(position, size.Dimensions))
            {
                _taken[cell.Length, cell.Width, cell.Stack] = true;
            }

            return position;
        }
    }

    /// <summary>
    /// The crates of <paramref name="placements"/>, one per crate in plan order, each rack laid out with
    /// <see cref="Place"/>. The one way from placements to crates: the planner uses it, and so does reading a schema 1
    /// plan file, so a migrated plan lies exactly as a fresh one.
    /// </summary>
    /// <exception cref="ArgumentException">A placement is in a rack the ship doesn't have.</exception>
    /// <exception cref="PlanException">The crates of a rack don't fit its grid.</exception>
    public static IReadOnlyList<Crate> LayOut(Ship ship, IReadOnlyList<Placement> placements, FillOrder order)
    {
        if (placements.FirstOrDefault(p => !ship.Racks.Any(r => r.Id == p.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Placement for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(placements));
        }

        // One entry per crate, in plan order; the route is the order of first appearance, as LoadPlan.Route derives it.
        Placement[] crates = [.. placements.SelectMany(p => Enumerable.Repeat(p, p.Item.Scu / (int)p.Item.CrateSize))];
        string[] route = [.. placements.GroupBy(p => p.Destination).Select(g => g.Key)];
        var positions = new GridPosition[crates.Length];
        foreach (var rack in ship.Racks)
        {
            int[] inRack = [.. Enumerable.Range(0, crates.Length).Where(i => crates[i].RackId == rack.Id)];
            var laid = Place(rack, [], [.. inRack.Select(i => (crates[i].Item.CrateSize, crates[i].Destination))], route, order)
                ?? throw new PlanException(Invariant(
                    $"Crates for rack {rack.Number} don't fit its grid ({rack.Grid}): {inRack.Sum(i => (int)crates[i].Item.CrateSize)}/{rack.CapacityScu} SCU."));
            foreach (var (index, position) in inRack.Zip(laid))
            {
                positions[index] = position;
            }
        }

        return [.. crates.Zip(positions).Select(c =>
            new Crate(c.First.Destination, c.First.Item.Commodity, c.First.Item.CrateSize, c.First.RackId, c.Second, c.First.Delivered))];
    }

    /// <summary>True if every cell of <paramref name="crate"/> lies inside the grid of <paramref name="rack"/>.</summary>
    public static bool Fits(Rack rack, Crate crate) => crate.Cells.All(c => Inside(rack.Grid, c));

    /// <summary>
    /// Why the crates of one rack can't lie as they are: a crate the grid can't hold or that sticks out, two crates in
    /// one cell, a crate with an empty cell under its bottom (D24). Delivered crates are off the ship: they take no cell,
    /// carry nothing and are not checked (D24 addendum).
    /// Also every crate lying on cargo of an earlier stop of <paramref name="route"/> (D24 addendum): possible, only tedious
    /// at the elevator. The planner never makes the others; this one only where a rack has no layout in route order.
    /// </summary>
    public static IReadOnlyList<string> Problems(Rack rack, IReadOnlyList<Crate> crates, IReadOnlyList<string> route) =>
        // An 8 across two crates of one stop overlaps both: one line is enough.
        [.. Findings(rack, crates, route).Select(f => f.Message).Distinct()];

    /// <summary>
    /// The crates <see cref="Problems"/> names, so the ship view can mark the crate, not only its rack; of a crate on cargo
    /// of an earlier stop, the upper one. Equal crates (a file edited by hand) count once.
    /// </summary>
    public static IReadOnlySet<Crate> Faulty(Rack rack, IReadOnlyList<Crate> crates, IReadOnlyList<string> route) =>
        Findings(rack, crates, route).SelectMany(f => f.Crates).ToHashSet();

    /// <summary>
    /// The first blocking problem the crates of one rack have <paramref name="after"/> a move and didn't have
    /// <paramref name="before"/>; null if the move breaks nothing (D25). A moved crate is a new record, so everything
    /// it causes is new; a problem of a plan file edited by hand that the move doesn't touch stays out. The stacking order
    /// never refuses a move: the player picks the cell, the plan marks it (D24 addendum).
    /// </summary>
    public static string? NewProblem(Rack rack, IReadOnlyList<Crate> before, IReadOnlyList<Crate> after)
    {
        var old = Findings(rack, before, []).Where(f => f.Blocking).ToArray();
        foreach (var finding in Findings(rack, after, []).Where(f => f.Blocking))
        {
            if (!old.Any(o => o.Message == finding.Message && o.Crates.SequenceEqual(finding.Crates)))
            {
                return finding.Message;
            }
        }

        return null;
    }

    /// <summary>
    /// Every problem with the crates it names; the one place for the rules of <see cref="Problems"/>, <see cref="Faulty"/>
    /// and <see cref="NewProblem"/>. Blocking: the crates can't lie like this. Not blocking: the stacking order.
    /// </summary>
    private static IEnumerable<(string Message, Crate[] Crates, bool Blocking)> Findings(Rack rack, IReadOnlyList<Crate> all, IReadOnlyList<string> route)
    {
        Crate[] crates = [.. all.Where(c => !c.Delivered)];
        foreach (var crate in crates)
        {
            var size = (int)crate.CrateSize;
            if (!rack.CanHold(crate.CrateSize))
            {
                yield return (Invariant($"{size} SCU crate of {crate.Commodity} for {crate.Destination} doesn't fit rack {rack.Number} ({rack.Grid})"), [crate], true);
            }
            else if (!Fits(rack, crate))
            {
                yield return (Invariant(
                    $"{size} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} lies outside rack {rack.Number} ({rack.Grid})"), [crate], true);
            }
        }

        for (var a = 0; a < crates.Length; a++)
        {
            for (var b = a + 1; b < crates.Length; b++)
            {
                if (Overlap(crates[a], crates[b]))
                {
                    yield return (Invariant($"Crates of {crates[a].Destination} and {crates[b].Destination} overlap in rack {rack.Number}"), [crates[a], crates[b]], true);
                }
            }
        }

        foreach (var crate in crates.Where((crate, i) => !Rests(crate, crates.Where((_, j) => j != i))))
        {
            yield return (Invariant($"{(int)crate.CrateSize} SCU crate of {crate.Commodity} for {crate.Destination} in rack {rack.Number} has nothing below it"), [crate], true);
        }

        foreach (var (upper, lower) in OutOfOrder(crates, route).DistinctBy(p => (p.Upper, p.Lower.Destination)))
        {
            yield return (Invariant(
                $"{(int)upper.CrateSize} SCU crate of {upper.Commodity} for {upper.Destination} in rack {rack.Number} lies on cargo for {lower.Destination}, which is unloaded first"),
                [upper], false);
        }
    }

    /// <summary>
    /// The stacking rule (D24 addendum): a crate may rest only on crates unloaded no earlier than itself, its own stop or a
    /// later one. Stop indices from <see cref="StopIndex"/>.
    /// </summary>
    private static bool MayRestOn(int upperStop, int lowerStop) => lowerStop >= upperStop;

    /// <summary>
    /// The place of <paramref name="destination"/> in <paramref name="route"/>; a destination not in it comes after all of
    /// it, in no order among its kind. So an empty route knows no order.
    /// </summary>
    private static int StopIndex(IReadOnlyList<string> route, string destination)
    {
        for (var i = 0; i < route.Count; i++)
        {
            if (route[i] == destination)
            {
                return i;
            }
        }

        return route.Count;
    }

    /// <summary>
    /// Every crate lying right on a crate that <see cref="MayRestOn"/> forbids, with that crate. Delivered cargo is off the
    /// ship and has no order (D16); an 8 resting on its own lower half is no pair, and a crate inside another (overlap, a
    /// file edited by hand) doesn't lie on it.
    /// </summary>
    private static IEnumerable<(Crate Upper, Crate Lower)> OutOfOrder(IReadOnlyList<Crate> crates, IReadOnlyList<string> route)
    {
        // Overlapping crates of a file edited by hand: the first keeps the cell, Problems names the overlap anyway.
        Dictionary<GridPosition, Crate> byCell = [];
        foreach (var crate in crates)
        {
            foreach (var cell in crate.Cells)
            {
                byCell.TryAdd(cell, crate);
            }
        }

        foreach (var upper in crates.Where(c => !c.Delivered && c.Position.Stack > 0))
        {
            var lowers = upper.Bottom
                .Select(b => byCell.GetValueOrDefault(new GridPosition(b.Length, b.Width, b.Stack - 1)))
                .OfType<Crate>()
                .Distinct();
            foreach (var lower in lowers.Where(l =>
                         !l.Delivered && !Overlap(upper, l) && !MayRestOn(StopIndex(route, upper.Destination), StopIndex(route, l.Destination))))
            {
                yield return (upper, lower);
            }
        }
    }

    /// <summary>
    /// A layout of <paramref name="crates"/> in the free cells that keeps the stacking rule; null if there is none. Exact
    /// search within one rack: the cells in fill order, at each free cell either one of the crates left whose first cell it
    /// is, or nothing; crates of one size and stop are tried once, not in every order. Deterministic: largest first, then
    /// later stops first, leaving the cell empty last.
    /// </summary>
    private static GridPosition[]? InOrder(
        Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<(CrateSize Size, string Destination)> crates, IReadOnlyList<string> route, FillOrder order)
    {
        const int free = -1;
        const int empty = -2;
        var grid = rack.Grid;
        var rank = Ranks(rack, order);
        GridPosition[] cells = [.. Cells(new GridPosition(0, 0, 0), grid).OrderBy(c => rank[c.Length, c.Width, c.Stack])];

        // Who holds a cell: free, empty (left out by the search, carries nothing) or the stop index of the crate there.
        // Place passes only cargo on board: delivered cells are free (D24 addendum).
        var owner = new int[grid.Length, grid.Width, grid.Stack];
        foreach (var cell in cells)
        {
            owner[cell.Length, cell.Width, cell.Stack] = free;
        }

        foreach (var crate in occupied)
        {
            foreach (var cell in crate.Cells.Where(c => Inside(grid, c)))
            {
                owner[cell.Length, cell.Width, cell.Stack] = StopIndex(route, crate.Destination);
            }
        }

        // OrderByDescending is stable; the indices of a kind stay in input order.
        var kinds = Enumerable.Range(0, crates.Count)
            .GroupBy(i => (crates[i].Size, Stop: StopIndex(route, crates[i].Destination)))
            .OrderByDescending(g => g.Key.Size)
            .ThenByDescending(g => g.Key.Stop)
            .Select(g => (g.Key.Size, g.Key.Stop, Indices: g.ToArray()))
            .ToArray();
        var left = kinds.Select(k => k.Indices.Length).ToArray();
        var scuLeft = crates.Sum(c => (int)c.Size);
        var positions = new GridPosition[crates.Count];

        return Fill(0) ? positions : null;

        bool Fill(int next)
        {
            if (scuLeft == 0)
            {
                return true;
            }

            while (next < cells.Length && owner[cells[next].Length, cells[next].Width, cells[next].Stack] != free)
            {
                next++;
            }

            if (cells.Skip(next).Count(c => owner[c.Length, c.Width, c.Stack] == free) < scuLeft)
            {
                return false;
            }

            var cell = cells[next];
            for (var k = 0; k < kinds.Length; k++)
            {
                var (size, stop, indices) = kinds[k];
                if (left[k] == 0 || StartingAt(cell, size.Dimensions) is not { } position || !CanTake(position, size.Dimensions, stop))
                {
                    continue;
                }

                Mark(position, size.Dimensions, stop);
                positions[indices[indices.Length - left[k]]] = position;
                (left[k], scuLeft) = (left[k] - 1, scuLeft - (int)size);
                if (Fill(next + 1))
                {
                    return true;
                }

                (left[k], scuLeft) = (left[k] + 1, scuLeft + (int)size);
                Mark(position, size.Dimensions, free);
            }

            owner[cell.Length, cell.Width, cell.Stack] = empty;
            if (Fill(next + 1))
            {
                return true;
            }

            owner[cell.Length, cell.Width, cell.Stack] = free;
            return false;
        }

        // The place of a crate whose first cell in fill order is this one; null if it would stick out.
        GridPosition? StartingAt(GridPosition cell, Dimensions size)
        {
            foreach (var corner in Cells(new GridPosition(0, 0, 0), size))
            {
                if (cell.Length < corner.Length || cell.Width < corner.Width || cell.Stack < corner.Stack)
                {
                    continue;
                }

                GridPosition position = new(cell.Length - corner.Length, cell.Width - corner.Width, cell.Stack - corner.Stack);
                if (Inside(grid, new GridPosition(position.Length + size.Length - 1, position.Width + size.Width - 1, position.Stack + size.Stack - 1))
                    && Cells(position, size).Min(c => rank[c.Length, c.Width, c.Stack]) == rank[cell.Length, cell.Width, cell.Stack])
                {
                    return position;
                }
            }

            return null;
        }

        // Free cells, and every bottom cell on the plate or on cargo it may rest on.
        bool CanTake(GridPosition position, Dimensions size, int stop) =>
            Cells(position, size).All(c => owner[c.Length, c.Width, c.Stack] == free)
            && (position.Stack == 0 || Cells(position, size).Where(c => c.Stack == position.Stack)
                .All(c => owner[c.Length, c.Width, c.Stack - 1] >= 0 && MayRestOn(stop, owner[c.Length, c.Width, c.Stack - 1])));

        void Mark(GridPosition position, Dimensions size, int value)
        {
            foreach (var c in Cells(position, size))
            {
                owner[c.Length, c.Width, c.Stack] = value;
            }
        }
    }

    /// <summary>True if two crates share a cell.</summary>
    public static bool Overlap(Crate a, Crate b) => a.Cells.Intersect(b.Cells).Any();

    /// <summary>
    /// The support rule (D24): a crate lies on the plate (stack 0), or every cell of its bottom has a cell of one of
    /// <paramref name="others"/> right below it. Its own upper cells rest on its own lower ones.
    /// </summary>
    public static bool Rests(Crate crate, IEnumerable<Crate> others)
    {
        if (crate.Position.Stack == 0)
        {
            return true;
        }

        HashSet<GridPosition> below = [.. others.SelectMany(c => c.Cells)];
        return crate.Bottom.All(c => below.Contains(new GridPosition(c.Length, c.Width, c.Stack - 1)));
    }

    /// <summary>Rank of every cell in fill order. Only this knows the axes; the rest works on ranks.</summary>
    private static int[,,] Ranks(Rack rack, FillOrder order)
    {
        var grid = rack.Grid;
        var rank = new int[grid.Length, grid.Width, grid.Stack];

        // Grid order only (D26): the stack axis puts the grid into the ship, so port and starboard mirror without help here.
        var next = 0;
        for (var stack = 0; stack < grid.Stack; stack++)
        {
            for (var width = 0; width < grid.Width; width++)
            {
                for (var step = 0; step < grid.Length; step++)
                {
                    // Length 0 is the aft end: x grows towards the bow (on racks stacking along x, length runs up, D26).
                    rank[order == FillOrder.BowFirst ? grid.Length - 1 - step : step, width, stack] = next++;
                }
            }
        }

        return rank;
    }

    /// <summary>
    /// Of all free, supported places for a crate of <paramref name="size"/>, the one whose first cell comes first in fill
    /// order, on a tie the one whose last cell does; null if there is none.
    /// </summary>
    private static GridPosition? FirstFit(Dimensions size, int[,,] rank, bool[,,] taken)
    {
        GridPosition? best = null;
        (int First, int Last) bestRanks = (int.MaxValue, int.MaxValue);
        for (var l = 0; l + size.Length <= rank.GetLength(0); l++)
        {
            for (var w = 0; w + size.Width <= rank.GetLength(1); w++)
            {
                for (var s = 0; s + size.Stack <= rank.GetLength(2); s++)
                {
                    GridPosition position = new(l, w, s);
                    var cells = Cells(position, size).ToArray();
                    if (cells.Any(c => taken[c.Length, c.Width, c.Stack])
                        || (s > 0 && cells.Any(c => c.Stack == s && !taken[c.Length, c.Width, s - 1])))
                    {
                        continue;
                    }

                    var ranks = (First: cells.Min(c => rank[c.Length, c.Width, c.Stack]), Last: cells.Max(c => rank[c.Length, c.Width, c.Stack]));
                    if (ranks.First < bestRanks.First || (ranks.First == bestRanks.First && ranks.Last < bestRanks.Last))
                    {
                        (best, bestRanks) = (position, ranks);
                    }
                }
            }
        }

        return best;
    }

    private static IEnumerable<GridPosition> Cells(GridPosition position, Dimensions size) =>
        from l in Enumerable.Range(position.Length, size.Length)
        from w in Enumerable.Range(position.Width, size.Width)
        from s in Enumerable.Range(position.Stack, size.Stack)
        select new GridPosition(l, w, s);

    private static readonly (int Length, int Width, int Stack)[] FaceSteps = [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)];

    /// <summary>True if every cell reaches every other through cells that share a face.</summary>
    private static bool Connected(HashSet<GridPosition> cells)
    {
        HashSet<GridPosition> reached = [cells.First()];
        Queue<GridPosition> next = new(reached);
        while (next.TryDequeue(out var cell))
        {
            foreach (var (l, w, s) in FaceSteps)
            {
                // Below 0 is outside every grid, and GridPosition rejects negative values.
                if (cell.Length + l >= 0 && cell.Width + w >= 0 && cell.Stack + s >= 0
                    && new GridPosition(cell.Length + l, cell.Width + w, cell.Stack + s) is var neighbour
                    && cells.Contains(neighbour) && reached.Add(neighbour))
                {
                    next.Enqueue(neighbour);
                }
            }
        }

        return reached.Count == cells.Count;
    }

    private static bool Inside(Dimensions grid, GridPosition cell) =>
        cell.Length < grid.Length && cell.Width < grid.Width && cell.Stack < grid.Stack;
}
