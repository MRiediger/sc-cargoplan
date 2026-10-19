using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>Which end of a rack's length the cells fill from (D21). A drawing choice, not where crates go.</summary>
public enum FillOrder
{
    BowFirst,
    SternFirst,
}

/// <summary>
/// One crate of a placement, counted from 1. A value: the same plan gives the same ids on every rebuild, so a selection
/// can hold them (T3.11c). Equal placements in one rack (D22) count on, so their crates never share an id.
/// </summary>
public sealed record CrateId(Placement Placement, int Number);

/// <summary>A box of whole cells in a rack's grid: its min corner, counted like <see cref="RackCell"/>, and its size.</summary>
public sealed record GridBox(int Length, int Width, int Stack, Dimensions Size);

/// <summary>
/// One SCU of a rack's grid, counted from the rack's <c>Origin</c> along length, width and stack (D14).
/// <see cref="Placement"/> is null for a free cell. <see cref="Block"/> is the box the cell is drawn in: its crate's
/// box, or a box of one cell for a cell drawn on its own (free, a share of an overloaded rack, no block fitted).
/// </summary>
public sealed record RackCell(int Length, int Width, int Stack, Placement? Placement, CrateId? Crate, GridBox Block)
{
    public bool IsFree => Placement is null;
}

/// <summary>
/// Fills a rack's grid with crates in fill order, then the free rest. The planner assigns racks, not cells, so the
/// order is schematic: it shows layers and crate shapes, not where a crate goes (D21).
/// </summary>
public static class RackCells
{
    private static readonly Dimensions OneCell = new(1, 1, 1);

    /// <summary>
    /// Every cell of the grid once, in fill order: layer by layer from the bottom; in a layer row by row from the
    /// centreline outwards, so port and starboard racks fill as mirror images; in a row along the length from the end
    /// <paramref name="order"/> names. Crates go in as blocks of their shape (D9), the largest first, each at the first
    /// place in fill order where it fits. A placement with a crate that fits nowhere goes cell by cell, crate after
    /// crate. A violated rack has no blocks; an overloaded one shares its cells by SCU and has no crates.
    /// </summary>
    /// <param name="isViolated">The rack is red (<c>LoadPlanner.Validate</c>).</param>
    public static IReadOnlyList<RackCell> Build(Rack rack, IReadOnlyList<Placement> placements, FillOrder order, bool isViolated)
    {
        // Only the fill order knows the axes; the block code below works on grid positions and their rank.
        (int Length, int Width, int Stack)[] positions = [.. Positions(rack, order)];
        return placements.Sum(p => p.Item.Scu) > rack.CapacityScu
            ? Shared(rack, positions, placements)
            : Crates(rack, positions, placements, isViolated);
    }

    private static IEnumerable<(int Length, int Width, int Stack)> Positions(Rack rack, FillOrder order)
    {
        var grid = rack.Grid;

        // y = 0 is the centreline (port is positive y, D14). OrderBy is stable: a rack centred on it starts at its min side.
        int[] rows = [.. Enumerable.Range(0, grid.Width).OrderBy(w => Math.Abs(rack.Origin.Y + w + 0.5))];

        for (var stack = 0; stack < grid.Stack; stack++)
        {
            foreach (var width in rows)
            {
                for (var step = 0; step < grid.Length; step++)
                {
                    // Length 0 is the aft end: x grows towards the bow.
                    yield return (order == FillOrder.BowFirst ? grid.Length - 1 - step : step, width, stack);
                }
            }
        }
    }

    private static RackCell[] Crates(Rack rack, (int Length, int Width, int Stack)[] positions, IReadOnlyList<Placement> placements, bool isViolated)
    {
        var grid = rack.Grid;
        var rank = new int[grid.Length, grid.Width, grid.Stack];
        for (var i = 0; i < positions.Length; i++)
        {
            rank[positions[i].Length, positions[i].Width, positions[i].Stack] = i;
        }

        var owner = new (CrateId Crate, GridBox Block)?[grid.Length, grid.Width, grid.Stack];
        List<CrateId[]> cellByCell = [];

        // OrderByDescending is stable: crates of one size keep plan order.
        foreach (var crates in Numbered(placements).OrderByDescending(c => c[0].Placement.Item.CrateSize))
        {
            if (isViolated || !TryPlaceBlocks(crates, rank, owner))
            {
                cellByCell.Add(crates);
            }
        }

        // The rest goes cell by cell into the free cells in fill order, each crate in as many cells as it has SCU.
        var free = positions.Where(p => owner[p.Length, p.Width, p.Stack] is null);
        foreach (var (crate, p) in cellByCell.SelectMany(c => c).SelectMany(c => Enumerable.Repeat(c, (int)c.Placement.Item.CrateSize)).Zip(free.ToArray()))
        {
            owner[p.Length, p.Width, p.Stack] = (crate, new GridBox(p.Length, p.Width, p.Stack, OneCell));
        }

        return [.. positions.Select(p => owner[p.Length, p.Width, p.Stack] is { } o
            ? new RackCell(p.Length, p.Width, p.Stack, o.Crate.Placement, o.Crate, o.Block)
            : FreeCell(p))];
    }

    /// <summary>The crates of each placement, in plan order; equal placements count on (D22).</summary>
    private static List<CrateId[]> Numbered(IReadOnlyList<Placement> placements)
    {
        Dictionary<Placement, int> counted = [];
        List<CrateId[]> crates = [];
        foreach (var placement in placements)
        {
            var first = counted.GetValueOrDefault(placement) + 1;
            var count = placement.Item.Scu / (int)placement.Item.CrateSize;
            crates.Add([.. Enumerable.Range(first, count).Select(n => new CrateId(placement, n))]);
            counted[placement] = first + count - 1;
        }

        return crates;
    }

    /// <summary>Places all crates of one placement as blocks, or none: a crate that fits nowhere takes the others back.</summary>
    private static bool TryPlaceBlocks(CrateId[] crates, int[,,] rank, (CrateId Crate, GridBox Block)?[,,] owner)
    {
        List<GridBox> placed = [];
        foreach (var crate in crates)
        {
            if (FirstFit(crate.Placement.Item.CrateSize.Dimensions, rank, owner) is not { } block)
            {
                foreach (var cell in placed.SelectMany(CellsOf))
                {
                    owner[cell.Length, cell.Width, cell.Stack] = null;
                }

                return false;
            }

            foreach (var cell in CellsOf(block))
            {
                owner[cell.Length, cell.Width, cell.Stack] = (crate, block);
            }

            placed.Add(block);
        }

        return true;
    }

    /// <summary>
    /// Of all free places for a block of <paramref name="size"/>, the one whose first cell comes first in fill order,
    /// on a tie the one whose last cell does; null if the block fits nowhere. Only the ranks decide, not the axes.
    /// </summary>
    private static GridBox? FirstFit(Dimensions size, int[,,] rank, (CrateId Crate, GridBox Block)?[,,] owner)
    {
        GridBox? best = null;
        (int First, int Last) bestRanks = (int.MaxValue, int.MaxValue);
        for (var l = 0; l + size.Length <= rank.GetLength(0); l++)
        {
            for (var w = 0; w + size.Width <= rank.GetLength(1); w++)
            {
                for (var s = 0; s + size.Stack <= rank.GetLength(2); s++)
                {
                    GridBox block = new(l, w, s, size);
                    var cells = CellsOf(block).ToArray();
                    if (cells.Any(c => owner[c.Length, c.Width, c.Stack] is not null))
                    {
                        continue;
                    }

                    var ranks = (First: cells.Min(c => rank[c.Length, c.Width, c.Stack]), Last: cells.Max(c => rank[c.Length, c.Width, c.Stack]));
                    if (ranks.First < bestRanks.First || (ranks.First == bestRanks.First && ranks.Last < bestRanks.Last))
                    {
                        (best, bestRanks) = (block, ranks);
                    }
                }
            }
        }

        return best;
    }

    private static IEnumerable<(int Length, int Width, int Stack)> CellsOf(GridBox box) =>
        from l in Enumerable.Range(box.Length, box.Size.Length)
        from w in Enumerable.Range(box.Width, box.Size.Width)
        from s in Enumerable.Range(box.Stack, box.Size.Stack)
        select (l, w, s);

    /// <summary>An overloaded rack (only after a manual move, D10): cells by SCU share, in plan order, no crates.</summary>
    private static RackCell[] Shared(Rack rack, (int Length, int Width, int Stack)[] positions, IReadOnlyList<Placement> placements)
    {
        var counts = CellCounts(rack.CapacityScu, placements);
        var owners = placements.SelectMany((placement, i) => Enumerable.Repeat(placement, counts[i]));

        return [.. positions.Zip(owners).Select(pair =>
            new RackCell(pair.First.Length, pair.First.Width, pair.First.Stack, pair.Second, null, new GridBox(pair.First.Length, pair.First.Width, pair.First.Stack, OneCell)))];
    }

    private static RackCell FreeCell((int Length, int Width, int Stack) p) =>
        new(p.Length, p.Width, p.Stack, null, null, new GridBox(p.Length, p.Width, p.Stack, OneCell));

    /// <summary>
    /// Cells per placement of an overloaded rack: shares of the cells by largest remainder (ties in plan order), and
    /// every placement keeps at least one cell, taken from the first of the largest, so it can still be dragged out
    /// (D20). One cell is then no longer one SCU; the rack is red anyway.
    /// </summary>
    private static int[] CellCounts(int cells, IReadOnlyList<Placement> placements)
    {
        int[] scu = [.. placements.Select(p => p.Item.Scu)];
        var used = scu.Sum();
        int[] counts = [.. scu.Select(s => s * cells / used)];
        var missing = cells - counts.Sum();
        foreach (var i in Enumerable.Range(0, scu.Length).OrderByDescending(i => scu[i] * cells % used).Take(missing))
        {
            counts[i]++;
        }

        for (var i = 0; i < counts.Length; i++)
        {
            var largest = Array.IndexOf(counts, counts.Max());
            if (counts[i] == 0 && counts[largest] > 1)
            {
                counts[largest]--;
                counts[i] = 1;
            }
        }

        return counts;
    }
}
