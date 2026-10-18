using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Isometric projection of ship coordinates (D14: x bow, y port, z up, in cells) onto the screen (y down), seen
/// from bow, port and above (D15): the bow points left-down, port right-down, up stays up. One cell is equally
/// long along all three axes; no perspective, so parallel edges stay parallel.
/// </summary>
public static class IsoProjection
{
    private static readonly double Cos30 = Math.Sqrt(3) / 2;
    private const double Sin30 = 0.5;

    public static Point Project(double x, double y, double z) => new((y - x) * Cos30, (x + y) * Sin30 - z);

    public static Point Project(ShipPoint point) => Project(point.X, point.Y, point.Z);

    /// <summary>Distance away from the viewer, up to a constant factor: larger lies further back.</summary>
    public static double Depth(double x, double y, double z) => -(x + y + z);
}
