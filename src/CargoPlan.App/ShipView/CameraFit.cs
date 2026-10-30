using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// What fit-to-view shows in the perspective view (D83), as points in ship coordinates, and the distance at which they
/// fill the view. The rules of the orthographic fit (<c>SceneProjector.FitExtent</c>, D61, D62, D63): a hologram is fitted
/// whole; a long ship with cargo fits its loaded racks and the bow; with an outline a ship fits its racks and the wedge's
/// bow zone; otherwise the racks and the marker.
/// </summary>
public static class CameraFit
{
    /// <summary>What the fit shows at the current cargo.</summary>
    public static IEnumerable<Point3D> FitPoints(ShipScene scene) => FitPoints(scene, IsoProjection.IsLong(scene.Bounds));

    /// <summary>The fit with every rack, loaded or not: fixed to the ship and its marker, for the zoom limits (D76).</summary>
    public static IEnumerable<Point3D> ShipPoints(ShipScene scene) => FitPoints(scene, isLong: false);

    /// <summary>The whole scene: the racks and the marker, a true-size outline included (K5.2d).</summary>
    public static IEnumerable<Point3D> WholePoints(ShipScene scene) =>
        Corners(scene.Bounds).Concat(scene.Marker is BowMarker.Outline o ? o.Points.Select(p => new Point3D(p.X, p.Y, p.Z)) : Bow(scene));

    /// <summary>The middle of the points' box: where a fit looks.</summary>
    public static Point3D Centre(IReadOnlyCollection<Point3D> points) => new(
        (points.Min(p => p.X) + points.Max(p => p.X)) / 2,
        (points.Min(p => p.Y) + points.Max(p => p.Y)) / 2,
        (points.Min(p => p.Z) + points.Max(p => p.Z)) / 2);

    /// <summary>
    /// The nearest distance at which every point lies within <paramref name="margin"/> of the view's edges, looking at
    /// <paramref name="target"/>; a viewport smaller than four margins shrinks the margin, as the orthographic fit did.
    /// Exact, per point: a point <c>r</c> to the right, <c>u</c> up and <c>f</c> beyond the target lands
    /// <c>focal · r / (d + f)</c> from the middle, so <c>d ≥ focal · |r| / room − f</c>.
    /// </summary>
    public static double Distance(IsoProjection orientation, Point3D target, IEnumerable<Point3D> points, Size viewport, double margin)
    {
        var edge = Math.Min(margin, Math.Min(viewport.Width, viewport.Height) / 4);
        var (across, upwards) = (viewport.Width / 2 - edge, viewport.Height / 2 - edge);
        var axes = new CameraMath(orientation, 0, target, viewport);
        var (right, up, look, focal) = (axes.RightDirection, axes.UpDirection, axes.LookDirection, axes.Focal);

        var distance = 0.0;
        foreach (var point in points)
        {
            var v = point - target;
            var f = Vector3D.DotProduct(v, look);
            distance = Math.Max(distance, Math.Max(
                Math.Max(focal * Math.Abs(Vector3D.DotProduct(v, right)) / across, focal * Math.Abs(Vector3D.DotProduct(v, up)) / upwards) - f,
                2 * CameraMath.NearPlane - f));
        }

        return distance;
    }

    private static IEnumerable<Point3D> FitPoints(ShipScene scene, bool isLong)
    {
        ShipBox[] loaded = [.. scene.Racks.Where(r => r.IsLoaded).Select(r => r.Bounds)];
        return scene.Marker is BowMarker.Holo ? WholePoints(scene)
            : isLong && loaded.Length > 0 ? Corners(loaded.Aggregate((a, b) => a.Union(b))).Concat(Bow(scene))
            : scene.Marker is BowMarker.Outline ? Corners(scene.Bounds).Concat(Bow(scene))
            : WholePoints(scene);
    }

    /// <summary>What marks the bow: the wedge's corners, with an outline too (its bow zone), or the hologram's box.</summary>
    private static IEnumerable<Point3D> Bow(ShipScene scene) => scene.Marker switch
    {
        BowMarker.Wedge or BowMarker.Outline => SceneProjector.BowWedge(scene.Bounds).SelectMany(f => f.Corners).Select(c => new Point3D(c.X, c.Y, c.Z)),
        BowMarker.Holo h => Corners(h.Min.X, h.Min.Y, h.Min.Z, h.Max.X, h.Max.Y, h.Max.Z),
        _ => [],
    };

    private static IEnumerable<Point3D> Corners(ShipBox box) => Corners(box.Min.X, box.Min.Y, box.Min.Z, box.Max.X, box.Max.Y, box.Max.Z);

    private static IEnumerable<Point3D> Corners(double minX, double minY, double minZ, double maxX, double maxY, double maxZ) =>
        from x in new[] { minX, maxX }
        from y in new[] { minY, maxY }
        from z in new[] { minZ, maxZ }
        select new Point3D(x, y, z);
}
