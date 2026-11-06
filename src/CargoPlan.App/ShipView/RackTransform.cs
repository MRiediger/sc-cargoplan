using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Where a rack lies in the picture (D91). Core and the scene's boxes keep computing where it lies before its rotation, in
/// whole cells ("straight"); this is the one step from there to the ship, about the middle of the rack's box. Rigid, so a
/// distance along a ray means the same on both sides and rays into different racks stay comparable (D21). A straight rack
/// has <see cref="Identity"/>, which hands everything back unchanged: such a rack runs without any arithmetic.
/// </summary>
public sealed class RackTransform
{
    // A corner this little past a whole cell is rounding: 1 + 2·cos 90° isn't quite 1, and a whole extra cell would reach
    // the long-ship rule and the bow wedge (D61, D52).
    private const double Noise = 1e-9;

    private readonly Matrix3D _toShip;
    private readonly Matrix3D _toStraight;

    private RackTransform(Matrix3D toShip, Matrix3D toStraight, Transform3D model) => (_toShip, _toStraight, Model) = (toShip, toStraight, model);

    public static RackTransform Identity { get; } = new(Matrix3D.Identity, Matrix3D.Identity, Transform3D.Identity);

    /// <summary>The rack's transform: <see cref="Identity"/> without a rotation.</summary>
    public static RackTransform Of(Rack rack)
    {
        if (rack.Rotation is not { } rotation)
        {
            return Identity;
        }

        var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);
        return Of(rotation, new Point3D((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0));
    }

    /// <summary>
    /// R = Rz(yaw) · Ry(pitch) · Rx(roll) about <paramref name="middle"/>. WPF multiplies row vectors, its matrix is the
    /// transpose of R, with the translation in the fourth row.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.matrix3d (Remarks: row-vector syntax).
    /// </summary>
    private static RackTransform Of(RackRotation rotation, Point3D middle)
    {
        var (sinYaw, cosYaw) = Math.SinCos(double.DegreesToRadians(rotation.Yaw));
        var (sinPitch, cosPitch) = Math.SinCos(double.DegreesToRadians(rotation.Pitch));
        var (sinRoll, cosRoll) = Math.SinCos(double.DegreesToRadians(rotation.Roll));

        double[,] r =
        {
            { cosYaw * cosPitch, cosYaw * sinPitch * sinRoll - sinYaw * cosRoll, cosYaw * sinPitch * cosRoll + sinYaw * sinRoll },
            { sinYaw * cosPitch, sinYaw * sinPitch * sinRoll + cosYaw * cosRoll, sinYaw * sinPitch * cosRoll - cosYaw * sinRoll },
            { -sinPitch, cosPitch * sinRoll, cosPitch * cosRoll },
        };

        var (x, y, z) = (middle.X, middle.Y, middle.Z);

        // The middle stays where it is: offset = middle − R · middle. The inverse of a rotation is its transpose.
        Matrix3D toShip = new(
            r[0, 0], r[1, 0], r[2, 0], 0,
            r[0, 1], r[1, 1], r[2, 1], 0,
            r[0, 2], r[1, 2], r[2, 2], 0,
            x - (r[0, 0] * x + r[0, 1] * y + r[0, 2] * z),
            y - (r[1, 0] * x + r[1, 1] * y + r[1, 2] * z),
            z - (r[2, 0] * x + r[2, 1] * y + r[2, 2] * z),
            1);
        Matrix3D toStraight = new(
            r[0, 0], r[0, 1], r[0, 2], 0,
            r[1, 0], r[1, 1], r[1, 2], 0,
            r[2, 0], r[2, 1], r[2, 2], 0,
            x - (r[0, 0] * x + r[1, 0] * y + r[2, 0] * z),
            y - (r[0, 1] * x + r[1, 1] * y + r[2, 1] * z),
            z - (r[0, 2] * x + r[1, 2] * y + r[2, 2] * z),
            1);

        MatrixTransform3D model = new(toShip);
        model.Freeze();
        return new RackTransform(toShip, toStraight, model);
    }

    /// <summary>True for a straight rack.</summary>
    public bool IsIdentity => ReferenceEquals(this, Identity);

    /// <summary>What a 3D model built straight wears, so its mesh, normals and hatch stay as they are (D83).</summary>
    public Transform3D Model { get; }

    public Point3D ToShip(Point3D point) => IsIdentity ? point : _toShip.Transform(point);

    public Vector3D ToShip(Vector3D vector) => IsIdentity ? vector : _toShip.Transform(vector);

    public IReadOnlyList<BoxEdges.Edge> ToShip(IReadOnlyList<BoxEdges.Edge> edges) =>
        IsIdentity ? edges : [.. edges.Select(e => new BoxEdges.Edge(_toShip.Transform(e.From), _toShip.Transform(e.To)))];

    public Point3D ToStraight(Point3D point) => IsIdentity ? point : _toStraight.Transform(point);

    /// <summary>A line of sight where the rack's boxes are axis-aligned, for <see cref="RayBox"/>; its length stays, so distances along it do too.</summary>
    public Ray ToStraight(Ray ray) => IsIdentity ? ray : new Ray(_toStraight.Transform(ray.Origin), _toStraight.Transform(ray.Direction));

    /// <summary>The whole cells around <paramref name="box"/> turned: what the camera's fit, the long-ship rule and the bow wedge go by.</summary>
    public ShipBox Around(ShipBox box)
    {
        if (IsIdentity)
        {
            return box;
        }

        Point3D[] corners =
        [
            .. from x in new[] { box.Min.X, box.Max.X }
               from y in new[] { box.Min.Y, box.Max.Y }
               from z in new[] { box.Min.Z, box.Max.Z }
               select _toShip.Transform(new Point3D(x, y, z)),
        ];

        return new ShipBox(
            new ShipPoint(Down(corners.Min(c => c.X)), Down(corners.Min(c => c.Y)), Down(corners.Min(c => c.Z))),
            new ShipPoint(Up(corners.Max(c => c.X)), Up(corners.Max(c => c.Y)), Up(corners.Max(c => c.Z))));
    }

    private static int Down(double value) => (int)Math.Floor(value + Noise);

    private static int Up(double value) => (int)Math.Ceiling(value - Noise);
}
