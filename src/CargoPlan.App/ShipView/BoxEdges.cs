using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The edges of axis-aligned boxes in ship coordinates (cells), as the 3D view draws them: crate edges, the free grid,
/// rack edges (T8.2; the frame around <c>Ship.Bounds</c> went with D84). Pure; the strips are built from these
/// (<see cref="HoloLines.Strips"/>).
/// </summary>
public static class BoxEdges
{
    public readonly record struct Edge(Point3D From, Point3D To);

    /// <summary>The 12 edges: every pair of corners that differ on exactly one axis.</summary>
    public static IReadOnlyList<Edge> Of(Point3D min, Point3D max)
    {
        Point3D Corner(int i) => new((i & 1) == 0 ? min.X : max.X, (i & 2) == 0 ? min.Y : max.Y, (i & 4) == 0 ? min.Z : max.Z);
        return [.. from i in Enumerable.Range(0, 8)
                   from axis in (int[])[1, 2, 4]
                   where (i & axis) == 0
                   select new Edge(Corner(i), Corner(i | axis))];
    }
}
