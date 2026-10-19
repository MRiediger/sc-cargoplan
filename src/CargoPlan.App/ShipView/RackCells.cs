using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>Which end of a rack's length the cells fill from (D21). A drawing choice, not where crates go.</summary>
public enum FillOrder
{
    BowFirst,
    SternFirst,
}

/// <summary>
/// One SCU of a rack's grid, counted from the rack's <c>Origin</c> along length, width and stack (D14).
/// <see cref="Placement"/> is null for a free cell.
/// </summary>
public sealed record RackCell(int Length, int Width, int Stack, Placement? Placement)
{
    public bool IsFree => Placement is null;
}

/// <summary>
/// Fills a rack's grid cell by cell, one cell per SCU, placements in plan order, then the free rest. The planner
/// assigns racks, not cells, so the order is schematic: it shows layers, not where a crate goes (D21).
/// </summary>
public static class RackCells
{
    /// <summary>
    /// Every cell of the grid once, in fill order: layer by layer from the bottom; in a layer row by row from the
    /// centreline outwards, so port and starboard racks fill as mirror images; in a row along the length from the end
    /// <paramref name="order"/> names. An overloaded rack (only after a manual move, D10) shares its cells by SCU.
    /// </summary>
    public static IReadOnlyList<RackCell> Build(Rack rack, IReadOnlyList<Placement> placements, FillOrder order)
    {
        var counts = CellCounts(rack.CapacityScu, placements);
        var owners = placements.SelectMany((placement, i) => Enumerable.Repeat<Placement?>(placement, counts[i]));

        return [.. Positions(rack, order)
            .Zip(owners.Concat(Enumerable.Repeat<Placement?>(null, rack.CapacityScu)))
            .Select(pair => new RackCell(pair.First.Length, pair.First.Width, pair.First.Stack, pair.Second))];
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

    /// <summary>
    /// Cells per placement: its SCU while they fit. Overloaded: shares of the cells by largest remainder (ties in plan
    /// order), and every placement keeps at least one cell, taken from the first of the largest, so it can still be
    /// dragged out (D20). One cell is then no longer one SCU; the rack is red anyway.
    /// </summary>
    private static int[] CellCounts(int cells, IReadOnlyList<Placement> placements)
    {
        int[] scu = [.. placements.Select(p => p.Item.Scu)];
        var used = scu.Sum();
        if (used <= cells)
        {
            return scu;
        }

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
