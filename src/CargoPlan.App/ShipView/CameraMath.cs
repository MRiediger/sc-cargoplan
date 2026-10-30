using System.Windows;
using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>A line of sight: from the camera through a point on screen, <see cref="Direction"/> of unit length.</summary>
public readonly record struct Ray(Point3D Origin, Vector3D Direction);

/// <summary>
/// The ship view's one projection (D83): a perspective camera on ship coordinates (x bow, y port, z up, in cells),
/// looking at <paramref name="Target"/> from <paramref name="Distance"/> cells along the orientation's view direction.
/// The scene's <see cref="PerspectiveCamera"/> is set from it (<see cref="ApplyTo"/>), never the other way round, and
/// the overlay's anchors, the hit test and the tests' click points use <see cref="Project"/> and
/// <see cref="Unproject"/> alone; a conformity test holds both against WPF's own mapping. No visuals: value types only.
/// </summary>
/// <param name="Orientation">Yaw and pitch, wrapped and clamped as in the orthographic view, so a reset shows the same angle.</param>
/// <param name="Viewport">The view's size in DIPs; screen y points down.</param>
public sealed record CameraMath(IsoProjection Orientation, double Distance, Point3D Target, Size Viewport)
{
    // Degrees, horizontal like WPF's; a constant, not a setting (D83).
    // Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.perspectivecamera.fieldofview
    public const double FieldOfView = 40;

    /// <summary>In cells: small enough that the closest zoom never clips, large enough for the depth buffer.</summary>
    public const double NearPlane = 0.05;

    /// <summary>Beyond the target in cells: the largest hull with its hologram lies well inside.</summary>
    public const double FarReach = 2000;

    /// <summary>From the camera into the scene, unit length.</summary>
    public Vector3D LookDirection
    {
        get
        {
            var (x, y, z) = Orientation.ViewDirection;
            return new Vector3D(x, y, z);
        }
    }

    /// <summary>The screen's right axis in the ship: level, at right angles to the view.</summary>
    public Vector3D RightDirection => new(-Math.Sin(Radians(Orientation.Yaw)), Math.Cos(Radians(Orientation.Yaw)), 0);

    /// <summary>The screen's up axis in the ship, at right angles to look and right; WPF's look × up is then right.</summary>
    public Vector3D UpDirection => Vector3D.CrossProduct(RightDirection, LookDirection);

    public Point3D Position => Target - Distance * LookDirection;

    public double Near => NearPlane;

    public double Far => Distance + FarReach;

    /// <summary>DIPs per cell at one cell in front of the camera; the horizontal field of view spans the viewport's width.</summary>
    public double Focal => Viewport.Width / 2 / Math.Tan(Radians(FieldOfView / 2));

    /// <summary>A cell at right angles to the view, in the target's plane, in DIPs: what the zoom limit reads (D76, D83).</summary>
    public double PxPerCellAtTarget => Focal / Distance;

    public Point Project(Point3D point)
    {
        var v = point - Position;
        var depth = Vector3D.DotProduct(v, LookDirection);
        return new Point(
            Viewport.Width / 2 + Focal * Vector3D.DotProduct(v, RightDirection) / depth,
            Viewport.Height / 2 - Focal * Vector3D.DotProduct(v, UpDirection) / depth);
    }

    /// <summary>The line of sight through <paramref name="screen"/>: every point on it projects there.</summary>
    public Ray Unproject(Point screen)
    {
        var direction = LookDirection
            + (screen.X - Viewport.Width / 2) / Focal * RightDirection
            - (screen.Y - Viewport.Height / 2) / Focal * UpDirection;
        direction.Normalize();
        return new Ray(Position, direction);
    }

    /// <summary>The scene's camera, from this one; the only place a <see cref="PerspectiveCamera"/> is set (D83).</summary>
    public void ApplyTo(PerspectiveCamera camera)
    {
        camera.Position = Position;
        camera.LookDirection = LookDirection;
        camera.UpDirection = UpDirection;
        camera.FieldOfView = FieldOfView;
        camera.NearPlaneDistance = Near;
        camera.FarPlaneDistance = Far;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;
}
