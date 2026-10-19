using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>What lies on top at a point: the rack, and its placement there; <paramref name="Placement"/> is null on free space.</summary>
public sealed record SceneHit(string RackId, Placement? Placement);

/// <summary>What lies under a point of the scene, front to back. Pure geometry, so it is testable without a window.</summary>
public static class SceneHitTest
{
    /// <summary>The tool tip of the topmost segment at <paramref name="point"/> (projected units); see <see cref="StripAt"/>.</summary>
    public static string? ToolTipAt(ProjectedScene scene, Point point) => StripAt(scene, point)?.ToolTip;

    /// <summary>The stop whose cargo lies on top at <paramref name="point"/>, delivered or not; null on free or empty space.</summary>
    public static string? DestinationAt(ProjectedScene scene, Point point) => PlacementAt(scene, point)?.Destination;

    /// <summary>The placement on top at <paramref name="point"/>, delivered or not; null on free or empty space.</summary>
    public static Placement? PlacementAt(ProjectedScene scene, Point point) => StripAt(scene, point)?.Placement;

    /// <summary>
    /// The topmost rack at <paramref name="point"/> with its placement there; null off the racks. Any face of the rack
    /// counts, free space too: a drop onto it is a drop onto the rack (D20).
    /// </summary>
    public static SceneHit? HitAt(ProjectedScene scene, Point point) =>
        RackAt(scene, point) is { } rack ? new SceneHit(rack.RackId, StripOf(rack, point)?.Placement) : null;

    /// <summary>
    /// The topmost segment at <paramref name="point"/> (projected units). The first rack hit decides, even on its
    /// free space: it covers whatever lies behind it.
    /// </summary>
    public static SceneStrip? StripAt(ProjectedScene scene, Point point) => RackAt(scene, point) is { } rack ? StripOf(rack, point) : null;

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

    private static SceneStrip? StripOf(ProjectedRack rack, Point point) => rack.Strips.FirstOrDefault(s => Contains(s.Polygon, point));
}
