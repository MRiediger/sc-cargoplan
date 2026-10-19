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
    /// Positions for crates of <paramref name="sizes"/> in the free cells of <paramref name="rack"/>, in the order of
    /// <paramref name="sizes"/>; null if one doesn't fit. Fill order: layer by layer from the plate; in a layer row by row
    /// in grid order (the rack's stack axis makes port and starboard racks mirror images, D26); in a row along the length
    /// from the end <paramref name="order"/> names (D21). The largest crate goes first (ties in input order), each at the free place
    /// whose first cell comes first in fill order, on a tie whose last cell does, and only where it rests on the plate
    /// or on other crates. <paramref name="occupied"/> keep their cells, delivered ones too (D24).
    /// </summary>
    public static IReadOnlyList<GridPosition>? Place(Rack rack, IReadOnlyList<Crate> occupied, IReadOnlyList<CrateSize> sizes, FillOrder order)
    {
        var grid = rack.Grid;
        var taken = new bool[grid.Length, grid.Width, grid.Stack];
        foreach (var cell in occupied.SelectMany(c => c.Cells).Where(c => Inside(grid, c)))
        {
            taken[cell.Length, cell.Width, cell.Stack] = true;
        }

        return Place(rack, taken, sizes, order);
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

        return Place(rack, taken, sizes, order);
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

    /// <summary>The lay-out of <see cref="Place"/> into the free cells of <paramref name="taken"/>, which it fills.</summary>
    private static GridPosition[]? Place(Rack rack, bool[,,] taken, IReadOnlyList<CrateSize> sizes, FillOrder order)
    {
        var rank = Ranks(rack, order);
        var positions = new GridPosition[sizes.Count];

        // OrderByDescending is stable: crates of one size keep input order.
        foreach (var i in Enumerable.Range(0, sizes.Count).OrderByDescending(i => sizes[i]))
        {
            if (FirstFit(sizes[i].Dimensions, rank, taken) is not { } position)
            {
                return null;
            }

            foreach (var cell in Cells(position, sizes[i].Dimensions))
            {
                taken[cell.Length, cell.Width, cell.Stack] = true;
            }

            positions[i] = position;
        }

        return positions;
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

        // One entry per crate, in plan order.
        Placement[] crates = [.. placements.SelectMany(p => Enumerable.Repeat(p, p.Item.Scu / (int)p.Item.CrateSize))];
        var positions = new GridPosition[crates.Length];
        foreach (var rack in ship.Racks)
        {
            int[] inRack = [.. Enumerable.Range(0, crates.Length).Where(i => crates[i].RackId == rack.Id)];
            var laid = Place(rack, [], [.. inRack.Select(i => crates[i].Item.CrateSize)], order)
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
    /// one cell, a crate with an empty cell under its bottom (D24). Delivered crates count, they keep their cells.
    /// Empty for every plan the planner makes.
    /// </summary>
    public static IReadOnlyList<string> Problems(Rack rack, IReadOnlyList<Crate> crates) =>
        // An 8 across two crates of one stop overlaps both: one line is enough.
        [.. Findings(rack, crates).Select(f => f.Message).Distinct()];

    /// <summary>
    /// The crates <see cref="Problems"/> names, so the ship view can mark the crate, not only its rack. Empty for every
    /// plan the planner makes. Equal crates (a file edited by hand) count once.
    /// </summary>
    public static IReadOnlySet<Crate> Faulty(Rack rack, IReadOnlyList<Crate> crates) =>
        Findings(rack, crates).SelectMany(f => f.Crates).ToHashSet();

    /// <summary>
    /// The first problem the crates of one rack have <paramref name="after"/> a move and didn't have
    /// <paramref name="before"/>; null if the move breaks nothing (D25). A moved crate is a new record, so everything
    /// it causes is new; a problem of a plan file edited by hand that the move doesn't touch stays out.
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

    /// <summary>Every problem with the crates it names; the one place for the rules of <see cref="Problems"/> and <see cref="Faulty"/>.</summary>
    private static IEnumerable<(string Message, Crate[] Crates)> Findings(Rack rack, IReadOnlyList<Crate> crates)
    {
        foreach (var crate in crates)
        {
            var size = (int)crate.CrateSize;
            if (!rack.CanHold(crate.CrateSize))
            {
                yield return (Invariant($"{size} SCU crate of {crate.Commodity} for {crate.Destination} doesn't fit rack {rack.Number} ({rack.Grid})"), [crate]);
            }
            else if (!Fits(rack, crate))
            {
                yield return (Invariant(
                    $"{size} SCU crate of {crate.Commodity} for {crate.Destination} at {crate.Position} lies outside rack {rack.Number} ({rack.Grid})"), [crate]);
            }
        }

        for (var a = 0; a < crates.Count; a++)
        {
            for (var b = a + 1; b < crates.Count; b++)
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
