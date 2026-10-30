using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The faces of a box the perspective camera sees (D83): those whose outward normal points against the line from the
/// camera to the face. Pure; the rack numbers' anchors and the overlay's edges on top come from them (T8.2).
/// </summary>
public static class BoxFaces
{
    // A face seen exactly edge-on has no area; it is left out.
    private const double EdgeOn = 1e-9;

    /// <summary>Also the preference on equal area: top or bottom first, then the sides, then the ends (as the 2D view).</summary>
    public static IReadOnlyList<BoxFace> Order { get; } = [BoxFace.Top, BoxFace.Bottom, BoxFace.Port, BoxFace.Starboard, BoxFace.Bow, BoxFace.Aft];

    /// <param name="Corners">Round the face, four corners in ship coordinates.</param>
    public sealed record Face(BoxFace Side, IReadOnlyList<Point3D> Corners, Point3D Centre, Vector3D Normal);

    public static IReadOnlyList<Face> Visible(ShipBox box, CameraMath camera) =>
        Visible(new Point3D(box.Min.X, box.Min.Y, box.Min.Z), new Point3D(box.Max.X, box.Max.Y, box.Max.Z), camera);

    public static IReadOnlyList<Face> Visible(Point3D min, Point3D max, CameraMath camera) =>
        [.. All(min, max).Where(f => Vector3D.DotProduct(f.Centre - camera.Position, f.Normal) < -EdgeOn)];

    public static IReadOnlyList<Face> All(Point3D min, Point3D max)
    {
        var (aft, bow) = (min.X, max.X);
        return [.. Order.Select(side => side switch
        {
            BoxFace.Top => Make(side, Level(max.Z), new Vector3D(0, 0, 1)),
            BoxFace.Bottom => Make(side, Level(min.Z), new Vector3D(0, 0, -1)),
            BoxFace.Port => Make(side, Side(max.Y), new Vector3D(0, 1, 0)),
            BoxFace.Starboard => Make(side, Side(min.Y), new Vector3D(0, -1, 0)),
            BoxFace.Bow => Make(side, End(max.X), new Vector3D(1, 0, 0)),
            _ => Make(side, End(min.X), new Vector3D(-1, 0, 0)),
        })];

        Point3D[] Level(double z) => [new(aft, min.Y, z), new(bow, min.Y, z), new(bow, max.Y, z), new(aft, max.Y, z)];
        Point3D[] Side(double y) => [new(aft, y, min.Z), new(bow, y, min.Z), new(bow, y, max.Z), new(aft, y, max.Z)];
        Point3D[] End(double x) => [new(x, min.Y, min.Z), new(x, max.Y, min.Z), new(x, max.Y, max.Z), new(x, min.Y, max.Z)];
    }

    private static Face Make(BoxFace side, Point3D[] corners, Vector3D normal) => new(
        side, corners, new Point3D(corners.Average(c => c.X), corners.Average(c => c.Y), corners.Average(c => c.Z)), normal);
}
