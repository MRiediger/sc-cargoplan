using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The hidden-line look's edges (D63). A <c>Viewport3D</c> draws no lines, so every edge becomes a thin strip of two
/// triangles turned to the camera, as wide on screen at every zoom as a pen of the ship view (D32), and lifted a little
/// towards the camera so the opaque hull under it doesn't hide it. Built anew when camera or zoom change.
/// </summary>
public static class HoloLines
{
    public const double WidthPixels = 1;

    /// <summary>In cells along the view: far less than the hull between a visible and a hidden edge.</summary>
    public const double LiftCells = 0.05;

    /// <summary>Half of <see cref="WidthPixels"/> in cells at the target's plane (<see cref="CameraMath.PxPerCellAtTarget"/>).</summary>
    public static double HalfWidthCells(double pxPerCell) => WidthPixels / 2 / pxPerCell;

    /// <param name="points">The mesh's vertices in cells.</param>
    /// <param name="segments">Index pairs into <paramref name="points"/>.</param>
    /// <param name="look">The camera's view direction, unit length.</param>
    /// <returns>Four positions and six indices per edge; an edge seen end-on has no width and is left out.</returns>
    public static (Point3D[] Positions, int[] Indices) Strips(IReadOnlyList<Point3D> points, ReadOnlySpan<int> segments, Vector3D look, double halfWidth)
    {
        var lift = -LiftCells * look;
        var positions = new List<Point3D>(2 * segments.Length);
        var indices = new List<int>(3 * segments.Length);
        for (var i = 0; i + 1 < segments.Length; i += 2)
        {
            var (a, b) = (points[segments[i]] + lift, points[segments[i + 1]] + lift);
            var side = Vector3D.CrossProduct(b - a, look);
            if (side.LengthSquared < 1e-18)
            {
                continue;
            }

            side *= halfWidth / side.Length;
            var first = positions.Count;
            positions.AddRange([a - side, a + side, b + side, b - side]);

            // Wound to show the front to the camera: a model without BackMaterial shows nothing from behind (T8.5).
            indices.AddRange([first, first + 2, first + 1, first, first + 3, first + 2]);
        }

        return ([.. positions], [.. indices]);
    }
}
