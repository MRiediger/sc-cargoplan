using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The camera's orientation on ship coordinates (D14: x bow, y port, z up, in cells); <see cref="CameraMath"/> projects
/// with it (D83). <see cref="Yaw"/> is the camera's bearing around z, from the bow (0°) towards port (90°);
/// <see cref="Pitch"/> its height, positive from above, clamped to ±85° so up stays up on the screen (D15).
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

    /// <summary>A ship longer than this many times its width is long (D61).</summary>
    public const double LongShipRatio = 2;

    /// <summary>
    /// The default camera's yaw for a long ship: from port, the length nearly level and the bow pointing left; the bow
    /// faces still show, so crates behind each other stay countable (D61, 90° hid them).
    /// </summary>
    public const double LongShipYaw = 70;

    /// <summary>Longer along x than <see cref="LongShipRatio"/> times its width along y: one row of racks, seen best from the side (D61).</summary>
    public static bool IsLong(ShipBox bounds) => bounds.Max.X - bounds.Min.X > LongShipRatio * (bounds.Max.Y - bounds.Min.Y);

    /// <summary>The camera a ship starts with: <see cref="Default"/>, or from the side for a long ship (D61).</summary>
    public static IsoProjection DefaultFor(ShipBox bounds) => IsLong(bounds) ? new(LongShipYaw, DefaultPitch) : Default;

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

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double Degrees(double radians) => radians * 180 / Math.PI;
}
