using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>Where a dragged crate would go: its min corner at this cell of this rack (D25).</summary>
public sealed record Drop(string RackId, GridPosition Position);

/// <summary>
/// The cell under the cursor as a drop target (D25). The two moves at the grid: put a crate on top of another, or into
/// a gap. Pure geometry on the rack's cells in ship coordinates, so it is testable without a window.
/// </summary>
public static class DropTarget
{
    // A line that only grazes an edge or a corner of a cell doesn't enter it.
    private const double Graze = 1e-9;

    /// <summary>
    /// The cell under <paramref name="projected"/> (projected units) in <paramref name="rack"/>, seen as the hit test
    /// sees it (D21): a crate on the line of sight answers even behind free outlines, and the drop goes on top of it
    /// (one step further along the stack), over the cell the line meets; no target if the crate reaches the end of the
    /// stack. Without a crate on the line, the frontmost free cell answers and the drop falls down its stack column onto
    /// the next crate or the plate, so every cell on the plate can be reached from outside. "Down" is the grid's stack
    /// axis, wherever the rack points it (D26). Nothing on screen slides later: whether the crate fits is Core's call.
    /// </summary>
    /// <param name="crates">The rack's crates without the dragged ones: those are air, and so is delivered cargo, which is off
    /// the ship and carries nothing (D24 addendum).</param>
    /// <returns>Null if the line of sight misses the rack.</returns>
    public static Drop? Resolve(Rack rack, IReadOnlyList<Crate> crates, IsoProjection camera, Point projected)
    {
        var origin = camera.Unproject(projected);
        var direction = camera.ViewDirection;

        Dictionary<GridPosition, Crate> taken = [];
        foreach (var crate in crates.Where(c => !c.Delivered))
        {
            foreach (var cell in crate.Cells)
            {
                // Overlapping crates of a plan file edited by hand: the first one answers.
                taken.TryAdd(cell, crate);
            }
        }

        var grid = rack.Grid;
        GridPosition[] seen =
        [
            .. from l in Enumerable.Range(0, grid.Length)
               from w in Enumerable.Range(0, grid.Width)
               from s in Enumerable.Range(0, grid.Stack)
               let cell = new GridPosition(l, w, s)
               let entry = Entry(rack.CellBox(cell, CrateSize.One), origin, direction)
               where entry is not null
               orderby entry
               select cell,
        ];

        if (seen.FirstOrDefault(taken.ContainsKey) is { } hit)
        {
            var below = taken[hit];
            var top = below.Position.Stack + below.Shape.Stack;
            return top < grid.Stack ? new Drop(rack.Id, new GridPosition(hit.Length, hit.Width, top)) : null;
        }

        if (seen.FirstOrDefault() is not { } front)
        {
            return null;
        }

        var stack = front.Stack;
        while (stack > 0 && !taken.ContainsKey(new GridPosition(front.Length, front.Width, stack - 1)))
        {
            stack--;
        }

        return new Drop(rack.Id, new GridPosition(front.Length, front.Width, stack));
    }

    /// <summary>
    /// Where the line <paramref name="origin"/> + t·<paramref name="direction"/> enters the cell, as t (smaller lies
    /// nearer the camera); null if it misses. The slab method: the line is inside the box where it is inside all three
    /// pairs of planes. Source: Kay &amp; Kajiya (1986), Ray tracing complex scenes, SIGGRAPH '86.
    /// </summary>
    private static double? Entry(ShipBox cell, (double X, double Y, double Z) origin, (double X, double Y, double Z) direction)
    {
        var (enter, leave) = (double.NegativeInfinity, double.PositiveInfinity);
        foreach (var (min, start, step) in (ReadOnlySpan<(double, double, double)>)
            [(cell.Min.X, origin.X, direction.X), (cell.Min.Y, origin.Y, direction.Y), (cell.Min.Z, origin.Z, direction.Z)])
        {
            if (Math.Abs(step) < Graze)
            {
                // Parallel to this pair of planes: inside between them or nowhere.
                if (start <= min || start >= min + 1)
                {
                    return null;
                }

                continue;
            }

            var (a, b) = ((min - start) / step, (min + 1 - start) / step);
            enter = Math.Max(enter, Math.Min(a, b));
            leave = Math.Min(leave, Math.Max(a, b));
        }

        return leave - enter > Graze ? enter : null;
    }
}
