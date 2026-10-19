using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Orthographic camera on ship coordinates (D14: x bow, y port, z up, in cells), projecting onto the screen (y down).
/// <see cref="Yaw"/> is the camera's bearing around z, from the bow (0°) towards port (90°); <see cref="Pitch"/> its
/// height, positive from above, clamped to ±85° so up stays up on the screen (D15). No perspective: parallel edges
/// stay parallel.
/// </summary>
public sealed record IsoProjection
{
    public const double MinPitch = -85;
    public const double MaxPitch = 85;
    public const double DefaultYaw = 45;

    /// <summary>arctan(1/√2) ≈ 35.26°: with <see cref="DefaultYaw"/> the camera looks along −(1, 1, 1), the classic isometric view.</summary>
    public static readonly double DefaultPitch = Degrees(Math.Atan(1 / Math.Sqrt(2)));

    /// <summary>Bow, port and above: the bow points left-down, port right-down, up stays up (D15).</summary>
    public static IsoProjection Default { get; } = new(DefaultYaw, DefaultPitch);

    // √(3/2) stretches the true projection so that in the default view one cell is 1 unit long on all three axes,
    // exactly the formula before T3.5c: u = (y − x)·cos 30°, v = (x + y)·sin 30° − z.
    private static readonly double Stretch = Math.Sqrt(1.5);

    private readonly double _sinYaw;
    private readonly double _cosYaw;
    private readonly double _sinPitch;
    private readonly double _cosPitch;

    /// <param name="yaw">Degrees; wrapped into [0, 360).</param>
    /// <param name="pitch">Degrees; clamped to [<see cref="MinPitch"/>, <see cref="MaxPitch"/>].</param>
    public IsoProjection(double yaw, double pitch)
    {
        Yaw = (yaw % 360 + 360) % 360;
        Pitch = Math.Clamp(pitch, MinPitch, MaxPitch);
        (_sinYaw, _cosYaw) = (Math.Sin(Radians(Yaw)), Math.Cos(Radians(Yaw)));
        (_sinPitch, _cosPitch) = (Math.Sin(Radians(Pitch)), Math.Cos(Radians(Pitch)));
    }

    // Get-only (no init): a `with` would copy the sines above unchanged. Orbit via OrbitBy.
    public double Yaw { get; }

    public double Pitch { get; }

    /// <summary>From the camera into the scene, unit length.</summary>
    public (double X, double Y, double Z) ViewDirection => (-_cosPitch * _cosYaw, -_cosPitch * _sinYaw, -_sinPitch);

    public IsoProjection OrbitBy(double yaw, double pitch) => new(Yaw + yaw, Pitch + pitch);

    public Point Project(double x, double y, double z) => new(
        Stretch * (y * _cosYaw - x * _sinYaw),
        Stretch * ((x * _cosYaw + y * _sinYaw) * _sinPitch - z * _cosPitch));

    public Point Project(ShipPoint point) => Project(point.X, point.Y, point.Z);

    /// <summary>
    /// A point in ship coordinates that projects to <paramref name="projected"/>; every point of the line through it
    /// along <see cref="ViewDirection"/> does too. The inverse of <see cref="Project(double, double, double)"/> on the
    /// two screen axes, which are unit vectors in the ship: right (−sin yaw, cos yaw, 0) and down
    /// (cos yaw·sin pitch, sin yaw·sin pitch, −cos pitch), both at right angles to the view direction.
    /// </summary>
    public (double X, double Y, double Z) Unproject(Point projected)
    {
        var (right, down) = (projected.X / Stretch, projected.Y / Stretch);
        return (
            -right * _sinYaw + down * _cosYaw * _sinPitch,
            right * _cosYaw + down * _sinYaw * _sinPitch,
            -down * _cosPitch);
    }

    /// <summary>Distance along <see cref="ViewDirection"/>, up to a constant: larger lies further back.</summary>
    public double Depth(double x, double y, double z)
    {
        var d = ViewDirection;
        return x * d.X + y * d.Y + z * d.Z;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double Degrees(double radians) => radians * 180 / Math.PI;
}
