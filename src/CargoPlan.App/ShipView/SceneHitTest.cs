using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// What lies on top at a point: the rack, its placement and crate there. <paramref name="Placement"/> and
/// <paramref name="Crate"/> are null on a free cell.
/// </summary>
public sealed record SceneHit(string RackId, Placement? Placement, Crate? Crate);

/// <summary>What lies under a point of the scene, front to back. Pure geometry, so it is testable without a window.</summary>
public static class SceneHitTest
{
    /// <summary>
    /// The tool tip at <paramref name="point"/> (projected units): the cargo's on top (<see cref="BoxAt"/>) with its rack's
    /// load below, over the rest of a rack the load alone, so an empty rack has one too (D67); null off the racks.
    /// </summary>
    public static string? ToolTipAt(ProjectedScene scene, Point point)
    {
        if (RackAt(scene, point) is not { } rack)
        {
            return null;
        }

        return CargoBoxOf(rack, point)?.Box.ToolTip is { } crate ? crate + "\n" + rack.Load.ToolTip : rack.Load.ToolTip;
    }

    /// <summary>The stop whose cargo lies on top at <paramref name="point"/>, delivered or not; null on free or empty space.</summary>
    public static string? DestinationAt(ProjectedScene scene, Point point) => PlacementAt(scene, point)?.Destination;

    /// <summary>The placement on top at <paramref name="point"/>, delivered or not; null on free or empty space.</summary>
    public static Placement? PlacementAt(ProjectedScene scene, Point point) => BoxAt(scene, point)?.Box.Placement;

    /// <summary>
    /// The topmost rack at <paramref name="point"/> with its placement and crate there; null off the racks. Any face of
    /// the rack counts, free cells and the gaps between boxes too: a drop onto them is a drop onto the rack (D20).
    /// </summary>
    public static SceneHit? HitAt(ProjectedScene scene, Point point)
    {
        if (RackAt(scene, point) is not { } rack)
        {
            return null;
        }

        var box = CargoBoxOf(rack, point)?.Box;
        return new SceneHit(rack.RackId, box?.Placement, box?.Crate);
    }

    /// <summary>
    /// The cargo box on top at <paramref name="point"/> (projected units), delivered or not. The first rack hit
    /// decides, even on its free cells: it covers whatever lies behind it.
    /// </summary>
    public static ProjectedBox? BoxAt(ProjectedScene scene, Point point) => RackAt(scene, point) is { } rack ? CargoBoxOf(rack, point) : null;

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
    /// The rack's frontmost box with cargo at the point. Free cells are drawn as outlines only, so the cargo of the
    /// same rack behind them shows and answers (D21).
    /// </summary>
    private static ProjectedBox? CargoBoxOf(ProjectedRack rack, Point point)
    {
        // Boxes are back to front, so walk them backwards.
        for (var i = rack.Boxes.Count - 1; i >= 0; i--)
        {
            var box = rack.Boxes[i];
            if (box.Box.Fill != CellFill.Free && box.Faces.Any(face => Contains(face.Polygon, point)))
            {
                return box;
            }
        }

        return null;
    }
}
