using System.Windows;
using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>A rack number in the overlay: where it stands on screen, and whether another rack covers it.</summary>
/// <param name="Anchor">In DIPs: the middle of the number, or with <paramref name="Above"/> the middle of its baseline's gap.</param>
/// <param name="Above">On a long ship the number stands above its rack (D61), its baseline a gap above the anchor.</param>
/// <param name="Hidden">Another rack lies between the camera and the number, or the number is behind the camera.</param>
public sealed record RackLabel(string RackId, int Number, Point Anchor, bool Above, bool Hidden);

/// <summary>
/// Where the rack numbers stand in the perspective view (D32, D61, D83), pure on <see cref="CameraMath"/>: on a compact
/// ship in the middle of the visible face that covers most of the screen (ties: top or bottom, sides, ends), on a long
/// ship above the top middle of the rack's outline. A number hides when the line of sight to it enters another rack
/// first; the hologram hides none, its box holds every rack (T8.2, R2).
/// </summary>
public static class RackLabels
{
    // Same rounding as the 2D view: faces of equal area stay equal, so ties fall to the order of BoxFaces.Order.
    private const int AreaDecimals = 9;

    // A rack entered this little before the number lies around it, not in front of it.
    private const double CoverMargin = 1e-6;

    public static IReadOnlyList<RackLabel> Place(ShipScene scene, CameraMath camera)
    {
        var isLong = IsoProjection.IsLong(scene.Bounds);
        return [.. scene.Racks.Select(rack =>
        {
            var faces = BoxFaces.Visible(rack.Bounds, camera);
            var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);
            Point3D top = new((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, max.Z);
            if (faces.Count == 0)
            {
                return new RackLabel(rack.RackId, rack.Number, camera.Project(top), isLong, Hidden: true);
            }

            Point anchor;
            Point3D target;
            if (isLong)
            {
                Point[] outline = [.. faces.SelectMany(f => f.Corners).Select(camera.Project)];
                (anchor, target) = (new Point((outline.Min(p => p.X) + outline.Max(p => p.X)) / 2, outline.Min(p => p.Y)), top);
            }
            else
            {
                // The largest free face, of those large enough to carry a number: a covered top moves the number to the
                // side next to it; a sliver seen almost edge-on doesn't take it.
                (BoxFaces.Face Face, double Area)[] ranked = [.. faces.Select(f => (f, Math.Round(Area(f, camera), AreaDecimals))).OrderByDescending(f => f.Item2)];
                var largest = ranked[0];
                var free = ranked.FirstOrDefault(f => f.Area >= MinFaceShare * largest.Area && !Covered(scene, rack, f.Face.Centre, camera));
                var face = free.Face ?? largest.Face;
                return new RackLabel(rack.RackId, rack.Number, camera.Project(face.Centre), Above: false, Hidden: free.Face is null);
            }

            return new RackLabel(rack.RackId, rack.Number, anchor, isLong, Covered(scene, rack, target, camera));
        })];
    }

    // A face smaller than this share of the largest visible one is too thin for a number (T8.2).
    private const double MinFaceShare = 0.25;

    /// <summary>Behind the camera, or another rack entered on the line of sight before it.</summary>
    private static bool Covered(ShipScene scene, SceneRack own, Point3D target, CameraMath camera)
    {
        var sight = target - camera.Position;
        if (Vector3D.DotProduct(sight, camera.LookDirection) <= camera.Near)
        {
            return true;
        }

        var distance = sight.Length;
        sight.Normalize();
        Ray ray = new(camera.Position, sight);
        return scene.Racks.Where(r => r != own).Any(r =>
            RayBox.Entry(ray, new Point3D(r.Bounds.Min.X, r.Bounds.Min.Y, r.Bounds.Min.Z), new Point3D(r.Bounds.Max.X, r.Bounds.Max.Y, r.Bounds.Max.Z))
                is { } entry && entry > 0 && entry < distance - CoverMargin);
    }

    /// <summary>The face's area on screen (shoelace formula over its projected corners).</summary>
    private static double Area(BoxFaces.Face face, CameraMath camera)
    {
        Point[] p = [.. face.Corners.Select(camera.Project)];
        var twice = 0.0;
        for (var i = 0; i < p.Length; i++)
        {
            var (a, b) = (p[i], p[(i + 1) % p.Length]);
            twice += a.X * b.Y - b.X * a.Y;
        }

        return Math.Abs(twice) / 2;
    }
}
