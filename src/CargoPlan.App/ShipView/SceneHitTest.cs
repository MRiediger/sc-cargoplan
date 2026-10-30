using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// What lies first on a line of sight: the rack, its placement and crate there. <paramref name="Placement"/> and
/// <paramref name="Crate"/> are null on a free cell.
/// </summary>
public sealed record SceneHit(string RackId, Placement? Placement, Crate? Crate);

/// <summary>
/// What a line of sight meets in the scene (D21, D83): a ray against the boxes of the rack and crate data, from
/// <see cref="CameraMath.Unproject"/>, the view's only projection. Pure, so it is testable without a window. A hit is
/// geometry, not what shows: a half-transparent or dimmed crate covers what lies behind it, and the hologram, the outline
/// and the bow wedge are no scene data, so they neither answer nor cover (T8.3).
/// </summary>
public static class SceneHitTest
{
    /// <summary>
    /// The tool tip on <paramref name="ray"/>: the cargo's first (<see cref="BoxAt"/>) with its rack's load below, over
    /// the rest of a rack the load alone, so an empty rack has one too (D67); null off the racks.
    /// </summary>
    public static string? ToolTipAt(ShipScene scene, Ray ray)
    {
        if (RackAt(scene, ray) is not { } rack)
        {
            return null;
        }

        return CargoBoxOf(rack, ray)?.ToolTip is { } crate ? crate + "\n" + rack.Load.ToolTip : rack.Load.ToolTip;
    }

    /// <summary>The stop whose cargo comes first on <paramref name="ray"/>, delivered or not; null on free or empty space.</summary>
    public static string? DestinationAt(ShipScene scene, Ray ray) => PlacementAt(scene, ray)?.Destination;

    /// <summary>The placement first on <paramref name="ray"/>, delivered or not; null on free or empty space.</summary>
    public static Placement? PlacementAt(ShipScene scene, Ray ray) => BoxAt(scene, ray)?.Placement;

    /// <summary>
    /// The first rack on <paramref name="ray"/> with its placement and crate there; null off the racks. Any part of the
    /// rack counts, free cells too: a drop onto them is a drop onto the rack (D20).
    /// </summary>
    public static SceneHit? HitAt(ShipScene scene, Ray ray)
    {
        if (RackAt(scene, ray) is not { } rack)
        {
            return null;
        }

        var box = CargoBoxOf(rack, ray);
        return new SceneHit(rack.RackId, box?.Placement, box?.Crate);
    }

    /// <summary>
    /// <see cref="HitAt(ShipScene, Ray)"/> at a point of the view (<paramref name="screen"/>, DIPs) seen by
    /// <paramref name="camera"/>: what the mouse is over, and nothing outside the camera's viewport (K1.4, D33). The view
    /// clips its drawing, so a rack zoomed beyond its edge can't be seen; with the mouse captured, points over the legend
    /// or the header come here too, and must hit nothing.
    /// </summary>
    public static SceneHit? HitAt(ShipScene scene, CameraMath camera, Point screen) =>
        InView(camera.Viewport, screen) ? HitAt(scene, camera.Unproject(screen)) : null;

    /// <summary><see cref="DestinationAt(ShipScene, Ray)"/> at a point of the view, like <see cref="HitAt(ShipScene, CameraMath, Point)"/>.</summary>
    public static string? DestinationAt(ShipScene scene, CameraMath camera, Point screen) =>
        InView(camera.Viewport, screen) ? DestinationAt(scene, camera.Unproject(screen)) : null;

    /// <summary><see cref="ToolTipAt(ShipScene, Ray)"/> at a point of the view, like <see cref="HitAt(ShipScene, CameraMath, Point)"/>.</summary>
    public static string? ToolTipAt(ShipScene scene, CameraMath camera, Point screen) =>
        InView(camera.Viewport, screen) ? ToolTipAt(scene, camera.Unproject(screen)) : null;

    // Edges included: the view's last pixel row is still the view.
    private static bool InView(Size viewport, Point screen) => new Rect(viewport).Contains(screen);

    /// <summary>
    /// The cargo box first on <paramref name="ray"/>, delivered or not. The first rack entered decides, even on its free
    /// cells: it covers whatever lies behind it (D21).
    /// </summary>
    public static SceneBox? BoxAt(ShipScene scene, Ray ray) => RackAt(scene, ray) is { } rack ? CargoBoxOf(rack, ray) : null;

    /// <summary>Even-odd rule: a ray from the point crosses the outline an odd number of times if the point is inside.</summary>
    /// <remarks>For the 2D painter's tests alone since T8.3; goes with it in T8.6.</remarks>
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

    private static SceneRack? RackAt(ShipScene scene, Ray ray) => First(scene.Racks, r => r.Bounds, ray);

    /// <summary>
    /// The rack's first box with cargo on the ray. Free cells are drawn as outlines only, so the cargo of the same rack
    /// behind them answers (D21). The boxes are the cells, not the inset boxes drawn: a joint belongs to its crates (R3).
    /// </summary>
    private static SceneBox? CargoBoxOf(SceneRack rack, Ray ray) => First(rack.Boxes.Where(b => b.Fill != CellFill.Free), b => b.Bounds, ray);

    /// <summary>The item whose box the ray reaches first in front of its origin; on a tie the earlier one.</summary>
    private static T? First<T>(IEnumerable<T> items, Func<T, ShipBox> boxOf, Ray ray)
        where T : class
    {
        T? first = null;
        var nearest = double.PositiveInfinity;
        foreach (var item in items)
        {
            var box = boxOf(item);
            if (RayBox.Ahead(ray, new Point3D(box.Min.X, box.Min.Y, box.Min.Z), new Point3D(box.Max.X, box.Max.Y, box.Max.Z)) is { } t && t < nearest)
            {
                (first, nearest) = (item, t);
            }
        }

        return first;
    }
}
