using System.Windows.Media.Media3D;
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
    /// <summary>
    /// The cell on the line of sight <paramref name="ray"/> (<see cref="CameraMath.Unproject"/>) in <paramref name="rack"/>,
    /// seen as the hit test sees it (D21): a crate on the line answers even behind free outlines, and the drop goes on top
    /// of it (one step further along the stack), over the cell the line meets; no target if the crate reaches the end of
    /// the stack. Without a crate on the line, the frontmost free cell answers and the drop falls down its stack column
    /// onto the next crate or the plate, so every cell on the plate can be reached from outside. "Down" is the grid's
    /// stack axis, wherever the rack points it (D26), whatever way the line runs: the line picks the cell the cursor shows,
    /// the grid does the fall (T8.4). Cells behind the camera aren't seen, the one around it from the camera on
    /// (<see cref="RayBox.Ahead"/>). Nothing on screen slides later: whether the crate fits is Core's call.
    /// </summary>
    /// <param name="crates">The rack's crates without the dragged ones: those are air, and so is delivered cargo, which is off
    /// the ship and carries nothing (D24 addendum).</param>
    /// <returns>Null if the line of sight misses the rack.</returns>
    public static Drop? Resolve(Rack rack, IReadOnlyList<Crate> crates, Ray ray)
    {
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
               let entry = Ahead(rack.CellBox(cell, CrateSize.One), ray)
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

    /// <summary>How far along <paramref name="ray"/> the cell begins in front of the camera; null if it misses or lies behind.</summary>
    private static double? Ahead(ShipBox cell, Ray ray) =>
        RayBox.Ahead(ray, new Point3D(cell.Min.X, cell.Min.Y, cell.Min.Z), new Point3D(cell.Max.X, cell.Max.Y, cell.Max.Z));
}
