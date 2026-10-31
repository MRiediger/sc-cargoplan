using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public sealed class OutlineSurfaceTests
{
    private static double Area(IReadOnlyList<Point> polygon) =>
        polygon.Zip(polygon.Skip(1).Append(polygon[0])).Sum(e => e.First.X * e.Second.Y - e.Second.X * e.First.Y) / 2;

    private static double Area(Point a, Point b, Point c) => ((b.X - a.X) * (c.Y - a.Y) - (c.X - a.X) * (b.Y - a.Y)) / 2;

    // Even-odd ray to +x; a point exactly on an edge doesn't occur for centroids of a valid triangulation.
    private static bool Inside(IReadOnlyList<Point> polygon, Point p)
    {
        var inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            var (a, b) = (polygon[i], polygon[j]);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    /// <summary>Every triangle faces up (counter-clockwise from above), lies inside, and together they cover the polygon.</summary>
    private static void ShouldCover(IReadOnlyList<Point> polygon, string because = "")
    {
        var indices = OutlineSurface.Triangles(polygon);

        indices.Length.Should().Be(3 * (indices.Length / 3));
        var total = 0.0;
        for (var t = 0; t < indices.Length; t += 3)
        {
            var (a, b, c) = (polygon[indices[t]], polygon[indices[t + 1]], polygon[indices[t + 2]]);
            var area = Area(a, b, c);
            area.Should().BePositive(because);
            Inside(polygon, new Point((a.X + b.X + c.X) / 3, (a.Y + b.Y + c.Y) / 3)).Should().BeTrue(because);
            total += area;
        }

        total.Should().BeApproximately(Math.Abs(Area(polygon)), 1e-9 * Math.Max(1, Math.Abs(Area(polygon))), because);
    }

    [Fact]
    public void A_square_is_two_triangles()
    {
        Point[] square = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];

        OutlineSurface.Triangles(square).Should().HaveCount(6);
        ShouldCover(square);
    }

    [Fact]
    public void A_concave_outline_is_covered_without_a_triangle_outside()
    {
        // An arrow pointing to +x with a notch at the back: the notch's tip is a reflex corner.
        ShouldCover([new(10, 0), new(-10, 5), new(-4, 0), new(-10, -5)]);
    }

    [Fact]
    public void A_clockwise_outline_gives_triangles_facing_up_too()
    {
        ShouldCover([new(0, 0), new(0, 1), new(1, 1), new(1, 0)]);
    }

    [Fact]
    public void Points_on_a_straight_edge_add_no_triangle_of_their_own()
    {
        // The traced bow tips have such points (Avenger: [12.31, -0.169], [12.31, 0.0], …).
        Point[] withCollinear = [new(0, 0), new(1, 0), new(2, 0), new(2, 1), new(0, 1)];

        ShouldCover(withCollinear);
        OutlineSurface.Triangles(withCollinear).Length.Should().BeLessThanOrEqualTo(3 * (withCollinear.Length - 2));
    }

    [Fact]
    public void Every_shipped_outline_is_covered()
    {
        foreach (var outline in ShipOutlineLoader.Load().Outlines.Values)
        {
            ShouldCover([.. outline.Points.Select(p => new Point(p.X, p.Y))], outline.Slug);
        }
    }
}
