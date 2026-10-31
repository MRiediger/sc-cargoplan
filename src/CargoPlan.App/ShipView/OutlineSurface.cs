using System.Windows;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The outline's surface (D62) as triangles for the 3D scene (T8.5): WPF 3D draws triangles only, and a traced outline is
/// concave. Ear clipping: cut off a corner whose triangle holds no other point, until three are left.
/// </summary>
public static class OutlineSurface
{
    // A corner this flat (cross product against the outline's size squared) lies on a straight edge.
    private const double Flat = 1e-12;

    /// <summary>
    /// Indices into <paramref name="polygon"/>, three per triangle, each counter-clockwise seen from +z whatever the
    /// polygon's own direction, so its front faces up. A point on a straight edge gets no triangle of its own. Should no
    /// ear be left (a trace that touches itself), the rest stays unfilled; every shipped outline fills whole (tested).
    /// </summary>
    public static int[] Triangles(IReadOnlyList<Point> polygon)
    {
        List<int> left = [.. Enumerable.Range(0, polygon.Count)];
        if (SignedArea(polygon) < 0)
        {
            left.Reverse();
        }

        var size = Math.Max(polygon.Max(p => p.X) - polygon.Min(p => p.X), polygon.Max(p => p.Y) - polygon.Min(p => p.Y));
        var flat = Flat * size * size;
        List<int> triangles = [];
        var (i, misses) = (0, 0);
        while (left.Count >= 3 && misses < left.Count)
        {
            i %= left.Count;
            var (a, b, c) = (left[(i + left.Count - 1) % left.Count], left[i], left[(i + 1) % left.Count]);
            var turn = Cross(polygon[a], polygon[b], polygon[c]);
            if (Math.Abs(turn) <= flat)
            {
                left.RemoveAt(i);
                misses = 0;
            }
            else if (turn > 0 && !left.Any(k => k != a && k != b && k != c && Within(polygon[k], polygon[a], polygon[b], polygon[c], flat)))
            {
                triangles.AddRange([a, b, c]);
                left.RemoveAt(i);
                misses = 0;
            }
            else
            {
                i++;
                misses++;
            }
        }

        return [.. triangles];
    }

    private static double SignedArea(IReadOnlyList<Point> polygon) =>
        polygon.Zip(polygon.Skip(1).Append(polygon[0])).Sum(e => e.First.X * e.Second.Y - e.Second.X * e.First.Y) / 2;

    // > 0 where a → b → c turns left (counter-clockwise).
    private static double Cross(Point a, Point b, Point c) => (b.X - a.X) * (c.Y - b.Y) - (b.Y - a.Y) * (c.X - b.X);

    // Inside the counter-clockwise triangle or on its edges: a point on an edge would be cut off with the ear.
    private static bool Within(Point p, Point a, Point b, Point c, double flat) =>
        Cross(a, b, p) >= -flat && Cross(b, c, p) >= -flat && Cross(c, a, p) >= -flat;
}
