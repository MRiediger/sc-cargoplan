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
    /// names (D21). The largest crate goes first (ties in input order: the planner passes them in its canonical order,
    /// D64), each at the free place whose first cell comes first in fill order, on a tie whose last cell does, and only
    /// where it rests on the plate or on other crates. <paramref name="occupied"/> keep their cells; delivered ones are off
    /// the ship, their cells are free (D24 addendum). No crate on another destination's cargo is tried, not forced (D64):
    /// if that layout puts a new crate on one, an exact search looks for one that doesn't. If there is none, the first
    /// layout stays and the plan's hints name the pair: the cargo fits, so stacking is a hint, never a refusal. The search
    /// gives up after <see cref="MaxStackingSearchSteps"/> steps and keeps the first layout too (<see cref="Lay"/> says so, D54).
    /// </summary>
    public static IReadOnlyList<GridPosition>? Place(
        Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<(CrateSize Size, string Destination)> crates, FillOrder order) =>
        Lay(rack, occupied, crates, order)?.Positions;

    /// <summary>
    /// Steps × cells of the exact search for a layout without foreign stacking before it gives up (D58, D64): a step fills
    /// or leaves empty one cell, and costs more the more cells the rack has (T7.1d, Release: about 5.7 µs on 1,008 cells,
    /// 0.3 µs on 81). 150,000 steps on the 1,008 cells of the T7.0 stress rack plan in 1.3 s, the largest budget measured
    /// under 1.5 s. A count, not a time: a plan comes out the same on every machine. The planner spends one such budget per
    /// plan over all racks and every choice it weighs (D64).
    /// </summary>
    internal const long StackingSearchCellSteps = 150_000L * 1_008;

    /// <summary>The steps the search may take in a rack of <paramref name="cells"/> cells (D58).</summary>
    internal static int MaxStackingSearchSteps(int cells) => (int)Math.Min(int.MaxValue, StackingSearchCellSteps / cells);

    /// <summary>
    /// What one plan, or one placement into a plan, may spend on the search over all racks and choices it weighs (D64):
    /// <see cref="StackingSearchCellSteps"/> once. Each search gets what is left, scaled by its rack's cells (D58).
    /// </summary>
    internal sealed class StackingBudget
    {
        private long _cellSteps = StackingSearchCellSteps;

        public int For(Rack rack) => (int)Math.Min(int.MaxValue, _cellSteps / rack.Grid.Volume);

        public void Spend(Rack rack, Laying laying) => _cellSteps -= laying.Steps * rack.Grid.Volume;
    }

    /// <summary>
    /// <see cref="Place"/> with what the caller weighs and tells the player (<see cref="Laying"/>): whether no new crate
    /// sits on another destination's cargo, whether the search for such a layout gave up at
    /// <paramref name="maxStackingSearchSteps"/> (default: <see cref="MaxStackingSearchSteps"/> of the rack), and the steps
    /// it took. Null if a crate doesn't fit.
    /// </summary>
    internal static Laying? Lay(
        Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<(CrateSize Size, string Destination)> crates, FillOrder order,
        int? maxStackingSearchSteps = null)
    {
        occupied = [.. occupied.Where(c => !c.Delivered)];
        if (Place(RackSpace.Around(rack, occupied, order), [.. crates.Select(c => c.Size)]) is not { } positions)
        {
            return null;
        }

        // Only what the new crates do counts; a plan file edited by hand, or the player, may have stacked before.
        Crate[] placed = [.. crates.Zip(positions).Select(p => new Crate(p.First.Destination, "", p.First.Size, rack.Id, p.Second))];
        var byCell = ByCell(rack, [.. occupied, .. placed]);
        if (placed.All(c => Chain(c, byCell).All(lower => lower.Destination == c.Destination)))
        {
            return new Laying(positions, true, false, 0);
        }

        if (MustStack(rack, occupied, crates))
        {
            return new Laying(positions, false, false, 0);
        }

        var (apart, gaveUp, steps) = Apart(rack, occupied, crates, order, maxStackingSearchSteps ?? MaxStackingSearchSteps(rack.Grid.Volume));
        return new Laying(apart ?? positions, apart is not null, gaveUp, steps);
    }

    /// <summary>
    /// Positions of a <see cref="Lay"/>: <paramref name="StackingFree"/> if no new crate sits on another destination's
    /// cargo; <paramref name="GaveUp"/> if the search for such a layout ran out of steps (none is known, one may exist);
    /// neither means one was proven impossible. <paramref name="Steps"/> the search took.
    /// </summary>
    internal sealed record Laying(IReadOnlyList<GridPosition> Positions, bool StackingFree, bool GaveUp, long Steps);

    /// <summary>
    /// True if no layout keeps every new destination off other cargo, proven without a search (D64): such a layout gives
    /// every column of the plate (length × width) to one destination, since a cell above another rests on it. A new
    /// destination not yet in the rack needs at least ⌈SCU / stack⌉ columns and at least its largest crate's footprint,
    /// and only columns with a free plate cell. Destinations with cargo in the rack may use their own columns and are left
    /// out, so the bound never says "must" where a layout exists.
    /// </summary>
    internal static bool MustStack(Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<(CrateSize Size, string Destination)> crates)
    {
        var grid = rack.Grid;
        HashSet<string> inRack = [.. occupied.Where(c => !c.Delivered).Select(c => c.Destination)];
        HashSet<(int Length, int Width)> taken = [.. occupied.Where(c => !c.Delivered).SelectMany(c => c.Cells).Where(c => c.Stack == 0).Select(c => (c.Length, c.Width))];
        var needed = crates
            .Where(c => !inRack.Contains(c.Destination))
            .GroupBy(c => c.Destination)
            .Sum(g => Math.Max(
                (g.Sum(c => (int)c.Size) + grid.Stack - 1) / grid.Stack,
                g.Max(c => c.Size.Dimensions.Length * c.Size.Dimensions.Width)));
        return needed > grid.Length * grid.Width - taken.Count;
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
    /// first take exactly the cells <see cref="Place"/> gives the same sizes around the same cargo. A size the rack
    /// doesn't take (<see cref="Rack.CanHold"/>, its max crate size, D56) finds no fit, wherever there is room.
    /// </summary>
    internal sealed class RackSpace
    {
        private readonly Rack _rack;
        private readonly bool[,,] _taken;
        private readonly int[,,] _rank;

        internal RackSpace(Rack rack, bool[,,] taken, FillOrder order) => (_rack, _taken, _rank) = (rack, taken, Ranks(rack, order));

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
            if (!_rack.CanHold(size) || FirstFit(size.Dimensions, _rank, _taken) is not { } position)
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
    public static IReadOnlyList<Crate> LayOut(Ship ship, IReadOnlyList<Placement> placements, FillOrder order) =>
        LayOutShip(ship, placements, order).Crates;

    /// <summary><see cref="LayOut"/> with the racks whose search for a layout without foreign stacking gave up (<see cref="Laying.GaveUp"/>).</summary>
    /// <exception cref="ArgumentException">A placement is in a rack the ship doesn't have.</exception>
    /// <exception cref="PlanException">The crates of a rack don't fit its grid.</exception>
    internal static ShipLayout LayOutShip(Ship ship, IReadOnlyList<Placement> placements, FillOrder order)
    {
        var (layout, problem) = LaidOut(ship, placements, order);
        return layout ?? throw new PlanException(problem!);
    }

    /// <summary>The layout of <see cref="LayOutShip"/>, or why the first rack that doesn't take its crates doesn't.</summary>
    private static (ShipLayout? Layout, string? Problem) LaidOut(Ship ship, IReadOnlyList<Placement> placements, FillOrder order)
    {
        if (placements.FirstOrDefault(p => !ship.Racks.Any(r => r.Id == p.RackId)) is { } stray)
        {
            throw new ArgumentException(
                $"Placement for {stray.Destination} is in rack '{stray.RackId}', which ship '{ship.Id}' doesn't have.", nameof(placements));
        }

        // One entry per crate, in plan order.
        Placement[] crates = [.. placements.SelectMany(p => Enumerable.Repeat(p, p.Item.Scu / (int)p.Item.CrateSize))];
        var positions = new GridPosition[crates.Length];
        List<Rack> stackingUnchecked = [];
        foreach (var rack in ship.Racks)
        {
            int[] inRack = [.. Enumerable.Range(0, crates.Length).Where(i => crates[i].RackId == rack.Id)];
            if (Lay(rack, [], [.. inRack.Select(i => (crates[i].Item.CrateSize, crates[i].Destination))], order) is not { } laid)
            {
                return (null, Invariant(
                    $"Crates for rack {rack.Number} don't fit its grid ({rack.Grid}): {inRack.Sum(i => (int)crates[i].Item.CrateSize)}/{rack.CapacityScu} SCU."));
            }

            foreach (var (index, position) in inRack.Zip(laid.Positions))
            {
                positions[index] = position;
            }

            if (laid.GaveUp)
            {
                stackingUnchecked.Add(rack);
            }
        }

        return (new ShipLayout(
            [.. crates.Zip(positions).Select(c =>
                new Crate(c.First.Destination, c.First.Item.Commodity, c.First.Item.CrateSize, c.First.RackId, c.Second, c.First.Delivered))],
            stackingUnchecked), null);
    }

    /// <summary>The crates of <see cref="LayOutShip"/>, and the racks whose search for a layout without foreign stacking gave up.</summary>
    internal sealed record ShipLayout(IReadOnlyList<Crate> Crates, IReadOnlyList<Rack> StackingUnchecked);

    /// <summary>True if every cell of <paramref name="crate"/> lies inside the grid of <paramref name="rack"/>.</summary>
    public static bool Fits(Rack rack, Crate crate) => crate.Cells.All(c => Inside(rack.Grid, c));

    /// <summary>
    /// Why the crates of one rack can't lie as they are: a crate the grid can't hold or that sticks out, two crates in
    /// one cell, a crate with an empty cell under its bottom (D24). Delivered crates are off the ship: they take no cell,
    /// carry nothing and are not checked (D24 addendum). A crate on another destination's cargo is no problem but a hint
    /// of the plan (D64). The planner and its moves never make these; only a plan file edited by hand gets there.
    /// </summary>
    public static IReadOnlyList<string> Problems(Rack rack, IReadOnlyList<Crate> crates) =>
        // An 8 across two crates of one stop overlaps both: one line is enough.
        [.. Findings(rack, crates).Select(f => f.Message).Distinct()];

    /// <summary>
    /// The crates <see cref="Problems"/> names, so the ship view can mark the crate, not only its rack. Equal crates (a file
    /// edited by hand) count once.
    /// </summary>
    public static IReadOnlySet<Crate> Faulty(Rack rack, IReadOnlyList<Crate> crates) =>
        Findings(rack, crates).SelectMany(f => f.Crates).ToHashSet();

    /// <summary>
    /// The first problem the crates of one rack have <paramref name="after"/> a move and didn't have
    /// <paramref name="before"/>; null if the move breaks nothing (D25). A moved crate is a new record, so everything
    /// it causes is new; a problem of a plan file edited by hand that the move doesn't touch stays out. Stacking on another
    /// destination's cargo never refuses a move: the player picks the cell, the plan's hints say it (D64).
    /// </summary>
    public static string? NewProblem(Rack rack, IReadOnlyList<Crate> before, IReadOnlyList<Crate> after)
    {
        var old = Findings(rack, before).ToArray();
        foreach (var finding in Findings(rack, after))
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
    /// and <see cref="NewProblem"/>: the crates can't lie like this.
    /// </summary>
    private static IEnumerable<(string Message, Crate[] Crates)> Findings(Rack rack, IReadOnlyList<Crate> all)
    {
        Crate[] crates = [.. all.Where(c => !c.Delivered)];
        foreach (var crate in crates)
        {
            var size = (int)crate.CrateSize;
            if (!rack.CanHold(crate.CrateSize))
            {
                yield return (Invariant(
                    $"{size} SCU crate of {crate.Commodity} for {crate.Destination} doesn't fit rack {rack.Number} ({Refusal(rack, crate.CrateSize)})"), [crate]);
            }
            else if (!Fits(rack, crate))
            {
                yield return (Invariant(
                    $"{size} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} lies outside rack {rack.Number} ({rack.Grid})"), [crate]);
            }
        }

        for (var a = 0; a < crates.Length; a++)
        {
            for (var b = a + 1; b < crates.Length; b++)
            {
                if (Overlap(crates[a], crates[b]))
                {
                    yield return (Invariant($"Crates of {crates[a].Destination} and {crates[b].Destination} overlap in rack {rack.Number}"), [crates[a], crates[b]]);
                }
            }
        }

        foreach (var crate in crates.Where((crate, i) => !Rests(crate, crates.Where((_, j) => j != i))))
        {
            yield return (Invariant($"{(int)crate.CrateSize} SCU crate of {crate.Commodity} for {crate.Destination} in rack {rack.Number} has nothing below it"), [crate]);
        }
    }

    /// <summary>
    /// Why <paramref name="rack"/> doesn't take a crate of <paramref name="size"/> (<see cref="Rack.CanHold"/> is false):
    /// its max crate size if that is the cause (D56), else its grid.
    /// </summary>
    internal static string Refusal(Rack rack, CrateSize size) =>
        rack.FitsGrid(size) && rack.MaxCrateSize is { } max ? Invariant($"takes crates up to {(int)max} SCU") : Invariant($"{rack.Grid}");

    /// <summary>
    /// A layout of <paramref name="crates"/> in the free cells where no new crate sits on another destination's cargo
    /// (D64); null if there is none. A crate may rest only on its own destination's cargo whose support chain holds no
    /// other destination. Exact search within one rack: the cells in fill order, at each free cell either one of the
    /// crates left whose first cell it is, or nothing; crates of one size and destination are tried once, not in every
    /// order. Deterministic: largest first, then destinations in input order, leaving the cell empty last. After
    /// <paramref name="maxSteps"/> steps it gives up with null and <c>GaveUp</c>: on a large grid the search runs for
    /// minutes (T7.0, 1,008 cells, four stops).
    /// </summary>
    private static (GridPosition[]? Positions, bool GaveUp, long Steps) Apart(
        Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<(CrateSize Size, string Destination)> crates, FillOrder order, int maxSteps)
    {
        const int free = -1;
        const int empty = -2;
        const int mixed = -3;
        var grid = rack.Grid;
        var rank = Ranks(rack, order);
        GridPosition[] cells = [.. Cells(new GridPosition(0, 0, 0), grid).OrderBy(c => rank[c.Length, c.Width, c.Stack])];
        List<string> destinations = [.. crates.Select(c => c.Destination).Distinct()];

        // Who holds a cell: free, empty (left out by the search, carries nothing), mixed (cargo with another destination
        // below it, or of a destination not laid now: nothing new may rest on it) or the index of a destination laid now.
        // Lay passes only cargo on board (D24 addendum).
        var owner = new int[grid.Length, grid.Width, grid.Stack];
        foreach (var cell in cells)
        {
            owner[cell.Length, cell.Width, cell.Stack] = free;
        }

        var byCell = ByCell(rack, occupied);
        foreach (var crate in occupied)
        {
            var own = destinations.IndexOf(crate.Destination);
            var holds = own >= 0 && Chain(crate, byCell).All(lower => lower.Destination == crate.Destination) ? own : mixed;
            foreach (var cell in crate.Cells.Where(c => Inside(grid, c)))
            {
                owner[cell.Length, cell.Width, cell.Stack] = holds;
            }
        }

        // GroupBy keeps first appearance and OrderByDescending is stable: ties of size go in input order.
        var kinds = Enumerable.Range(0, crates.Count)
            .GroupBy(i => (crates[i].Size, Destination: destinations.IndexOf(crates[i].Destination)))
            .OrderByDescending(g => g.Key.Size)
            .Select(g => (g.Key.Size, g.Key.Destination, Indices: g.ToArray()))
            .ToArray();
        var left = kinds.Select(k => k.Indices.Length).ToArray();
        var scuLeft = crates.Sum(c => (int)c.Size);
        var destinationLeft = destinations.Select(d => crates.Where(c => c.Destination == d).Sum(c => (int)c.Size)).ToArray();
        var ownRoom = new int[destinations.Count];
        var positions = new GridPosition[crates.Count];
        var steps = 0L;

        return Fill(0) ? (positions, false, steps) : (null, steps > maxSteps, Math.Min(steps, maxSteps));

        bool Fill(int next)
        {
            if (scuLeft == 0)
            {
                return true;
            }

            // Once over the limit every level returns at once, so giving up costs no more than the depth.
            if (++steps > maxSteps)
            {
                return false;
            }

            while (next < cells.Length && owner[cells[next].Length, cells[next].Width, cells[next].Stack] != free)
            {
                next++;
            }

            if (cells.Skip(next).Count(c => owner[c.Length, c.Width, c.Stack] == free) < scuLeft || OutOfColumns())
            {
                return false;
            }

            var cell = cells[next];
            for (var k = 0; k < kinds.Length; k++)
            {
                var (size, destination, indices) = kinds[k];
                if (left[k] == 0 || StartingAt(cell, size.Dimensions) is not { } position || !CanTake(position, size.Dimensions, destination))
                {
                    continue;
                }

                Mark(position, size.Dimensions, destination);
                positions[indices[indices.Length - left[k]]] = position;
                (left[k], scuLeft) = (left[k] - 1, scuLeft - (int)size);
                destinationLeft[destination] -= (int)size;
                if (Fill(next + 1))
                {
                    return true;
                }

                (left[k], scuLeft) = (left[k] + 1, scuLeft + (int)size);
                destinationLeft[destination] += (int)size;
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

        // The column bound of MustStack on what is left: every column belongs to one destination, so a destination's rest
        // goes into the free cells above its own cargo or into columns whose plate cell is still free.
        bool OutOfColumns()
        {
            Array.Clear(ownRoom);
            var open = 0;
            for (var l = 0; l < grid.Length; l++)
            {
                for (var w = 0; w < grid.Width; w++)
                {
                    var s = 0;
                    while (s < grid.Stack && owner[l, w, s] >= 0)
                    {
                        s++;
                    }

                    if (s == grid.Stack || owner[l, w, s] != free)
                    {
                        continue;
                    }

                    if (s == 0)
                    {
                        open++;
                        continue;
                    }

                    var room = 0;
                    while (s + room < grid.Stack && owner[l, w, s + room] == free)
                    {
                        room++;
                    }

                    ownRoom[owner[l, w, s - 1]] += room;
                }
            }

            var columns = 0;
            for (var d = 0; d < destinations.Count; d++)
            {
                columns += Math.Max(0, destinationLeft[d] - ownRoom[d] + grid.Stack - 1) / grid.Stack;
            }

            return columns > open;
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

        // Free cells, and every bottom cell on the plate or on the crate's own destination.
        bool CanTake(GridPosition position, Dimensions size, int destination) =>
            Cells(position, size).All(c => owner[c.Length, c.Width, c.Stack] == free)
            && (position.Stack == 0 || Cells(position, size).Where(c => c.Stack == position.Stack)
                .All(c => owner[c.Length, c.Width, c.Stack - 1] == destination));

        void Mark(GridPosition position, Dimensions size, int value)
        {
            foreach (var c in Cells(position, size))
            {
                owner[c.Length, c.Width, c.Stack] = value;
            }
        }
    }

    /// <summary>
    /// Who sits on whom in a rack (D64): for every crate on board, each other destination in its support chain along the
    /// stack axis (the crates right below it, theirs, down to the plate), as (destination on top, destination below),
    /// each pair once, in crate order. That cargo has to come off before what lies below can go at the elevator; which
    /// stop comes first is the player's to know. Delivered crates are off the ship and carry nothing (D24 addendum).
    /// </summary>
    public static IReadOnlyList<(string Upper, string Lower)> StackedOn(Rack rack, IReadOnlyList<Crate> crates)
    {
        Crate[] onBoard = [.. crates.Where(c => !c.Delivered)];
        var byCell = ByCell(rack, onBoard);
        List<(string Upper, string Lower)> pairs = [];
        foreach (var crate in onBoard)
        {
            foreach (var lower in Chain(crate, byCell).Select(c => c.Destination).Where(d => d != crate.Destination))
            {
                if (!pairs.Contains((crate.Destination, lower)))
                {
                    pairs.Add((crate.Destination, lower));
                }
            }
        }

        return pairs;
    }

    /// <summary>The crate in every cell; of overlapping crates (a file edited by hand) the first keeps the cell.</summary>
    private static Dictionary<GridPosition, Crate> ByCell(Rack rack, IEnumerable<Crate> crates)
    {
        Dictionary<GridPosition, Crate> byCell = [];
        foreach (var cell in crates.SelectMany(c => c.Cells.Where(cell => Inside(rack.Grid, cell)).Select(cell => (cell, c))))
        {
            byCell.TryAdd(cell.cell, cell.c);
        }

        return byCell;
    }

    /// <summary>The crates right below <paramref name="upper"/>'s bottom; one it overlaps (a file edited by hand) doesn't carry it.</summary>
    private static IEnumerable<Crate> Below(Crate upper, Dictionary<GridPosition, Crate> byCell) =>
        upper.Position.Stack == 0
            ? []
            : upper.Bottom
                .Select(b => byCell.GetValueOrDefault(new GridPosition(b.Length, b.Width, b.Stack - 1)))
                .OfType<Crate>()
                .Distinct()
                .Where(lower => !Overlap(upper, lower));

    /// <summary>Every crate <paramref name="crate"/> rests on, directly or through others, down to the plate.</summary>
    private static IEnumerable<Crate> Chain(Crate crate, Dictionary<GridPosition, Crate> byCell)
    {
        HashSet<Crate> seen = new(ReferenceEqualityComparer.Instance);
        Stack<Crate> next = new(Below(crate, byCell));
        while (next.TryPop(out var lower))
        {
            if (seen.Add(lower))
            {
                yield return lower;
                foreach (var further in Below(lower, byCell))
                {
                    next.Push(further);
                }
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
