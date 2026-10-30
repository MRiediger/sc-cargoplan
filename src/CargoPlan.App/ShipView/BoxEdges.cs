using System.Windows.Media.Media3D;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The edges of axis-aligned boxes in ship coordinates (cells), as the 3D view draws them: crate edges, the free grid,
/// rack edges and the dashed frame around <c>Ship.Bounds</c> (T8.2). Pure; the strips are built from these
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

    /// <summary>The edge <paramref name="from"/> → <paramref name="to"/> as dashes of <paramref name="dash"/> with <paramref name="gap"/> between, from its start; the last dash ends at <paramref name="to"/>.</summary>
    public static IReadOnlyList<Edge> Dashed(Point3D from, Point3D to, double dash, double gap)
    {
        var along = to - from;
        var length = along.Length;
        along /= length;
        List<Edge> dashes = [];
        for (var start = 0.0; start < length; start += dash + gap)
        {
            dashes.Add(new Edge(from + start * along, start + dash >= length ? to : from + (start + dash) * along));
        }

        return dashes;
    }
}
