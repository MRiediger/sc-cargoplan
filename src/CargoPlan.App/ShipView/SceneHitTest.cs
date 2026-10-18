using System.Windows;

namespace CargoPlan.App.ShipView;

/// <summary>What lies under a point of the scene, front to back. Pure geometry, so it is testable without a window.</summary>
public static class SceneHitTest
{
    /// <summary>
    /// The tool tip of the topmost segment at <paramref name="point"/> (projected units). The first rack hit
    /// decides, even on its free space: it covers whatever lies behind it.
    /// </summary>
    public static string? ToolTipAt(ProjectedScene scene, Point point)
    {
        // Racks are back to front, so walk them backwards.
        for (var i = scene.Racks.Count - 1; i >= 0; i--)
        {
            var rack = scene.Racks[i];
            if (rack.Faces.Any(face => Contains(face.Polygon, point)))
            {
                return rack.Strips.FirstOrDefault(s => Contains(s.Polygon, point))?.ToolTip;
            }
        }

        return null;
    }

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
}
