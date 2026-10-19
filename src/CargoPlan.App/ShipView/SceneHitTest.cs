using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>What lies on top at a point: the rack, and its placement there; <paramref name="Placement"/> is null on a free cell.</summary>
public sealed record SceneHit(string RackId, Placement? Placement);

/// <summary>What lies under a point of the scene, front to back. Pure geometry, so it is testable without a window.</summary>
public static class SceneHitTest
{
    /// <summary>The tool tip of the cargo on top at <paramref name="point"/> (projected units); see <see cref="CellAt"/>.</summary>
    public static string? ToolTipAt(ProjectedScene scene, Point point) => CellAt(scene, point)?.Cell.ToolTip;

    /// <summary>The stop whose cargo lies on top at <paramref name="point"/>, delivered or not; null on free or empty space.</summary>
    public static string? DestinationAt(ProjectedScene scene, Point point) => PlacementAt(scene, point)?.Destination;

    /// <summary>The placement on top at <paramref name="point"/>, delivered or not; null on free or empty space.</summary>
    public static Placement? PlacementAt(ProjectedScene scene, Point point) => CellAt(scene, point)?.Cell.Placement;

    /// <summary>
    /// The topmost rack at <paramref name="point"/> with its placement there; null off the racks. Any face of the rack
    /// counts, free cells and the gaps between cells too: a drop onto them is a drop onto the rack (D20).
    /// </summary>
    public static SceneHit? HitAt(ProjectedScene scene, Point point) =>
        RackAt(scene, point) is { } rack ? new SceneHit(rack.RackId, CargoCellOf(rack, point)?.Cell.Placement) : null;

    /// <summary>
    /// The cargo cell on top at <paramref name="point"/> (projected units), delivered or not. The first rack hit
    /// decides, even on its free cells: it covers whatever lies behind it.
    /// </summary>
    public static ProjectedCell? CellAt(ProjectedScene scene, Point point) => RackAt(scene, point) is { } rack ? CargoCellOf(rack, point) : null;

    /// <summary>Even-odd rule: a ray from the point crosses the outline an odd number of times if the point is inside.</summary>
    public static bool Contains(IReadOnlyList<Point> polygon, Point point)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > point.Y) != (b.Y > point.Y)
                && point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static ProjectedRack? RackAt(ProjectedScene scene, Point point)
    {
        // Racks are back to front, so walk them backwards.
        for (var i = scene.Racks.Count - 1; i >= 0; i--)
        {
            if (scene.Racks[i].Faces.Any(face => Contains(face.Polygon, point)))
            {
                return scene.Racks[i];
            }
        }

        return null;
    }

    /// <summary>
    /// The rack's frontmost cell with cargo at the point. Free cells are drawn as outlines only, so the cargo of the
    /// same rack behind them shows and answers (D21).
    /// </summary>
    private static ProjectedCell? CargoCellOf(ProjectedRack rack, Point point)
    {
        // Cells are back to front, so walk them backwards.
        for (var i = rack.Cells.Count - 1; i >= 0; i--)
        {
            var cell = rack.Cells[i];
            if (cell.Cell.Fill != CellFill.Free && cell.Faces.Any(face => Contains(face.Polygon, point)))
            {
                return cell;
            }
        }

        return null;
    }
}
