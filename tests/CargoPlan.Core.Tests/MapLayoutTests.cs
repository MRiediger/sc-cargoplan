using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The map's layout (T13.7, D128): polar about the star, angle and radius to scale; what lies close
/// spreads around the first node in place order at its real angle; labels where they are free. Nodes built here (A10).
/// </summary>
public sealed class MapLayoutTests
{
    private static readonly MapSize View = new(1100, 700);

    [Fact]
    public void The_angle_about_the_star_is_kept()
    {
        MapNode[] nodes = [N("A", 10, 0), N("B", 20, 50), N("C", 40, -120), N("D", 60, 170)];

        var layout = MapLayout.For(nodes, View, TestLabels.Size);

        foreach (var node in nodes)
        {
            Polar(layout, node.Key).Should().BeApproximately(Math.Atan2(node.Y, node.X) * 180 / Math.PI, 1e-6, node.Key);
        }
    }

    [Fact]
    public void The_radius_is_the_distance_to_scale_and_the_farthest_fits_the_view_with_room_for_a_label()
    {
        var layout = MapLayout.For([N("A", 16, 0), N("B", 64, 90)], View, TestLabels.Size);

        (Radius(layout, "B") / Radius(layout, "A")).Should().BeApproximately(4, 1e-9, "64 Gm / 16 Gm, no compression");
        Radius(layout, "B").Should().BeApproximately((View.Height / 2) - MapLayout.MarginPx, 1e-9);
    }

    [Fact]
    public void Close_nodes_spread_around_the_first_in_place_order_at_their_real_angle()
    {
        // Everus with L1 inside and L2 outside, 1.3 Gm each: a few pixels apart without the spread, with a far node out
        // where MIC-L2 lies to set Stanton's scale.
        var layout = MapLayout.For(
            [N("HUR-L2 Faithful Dream Station", 14.14, 0), N("Everus Harbor", 12.85, 0, MapNodeKind.Hub), N("HUR-L1 Green Glade Station", 11.56, 0), N("Far", 47.8, 58.9)],
            View, TestLabels.Size);

        var everus = layout.Points["Everus Harbor"];
        Polar(layout, "Everus Harbor").Should().BeApproximately(0, 1e-9, "the anchor stays where the star puts it");
        var l1 = Relative(layout, "HUR-L1 Green Glade Station", everus);
        var l2 = Relative(layout, "HUR-L2 Faithful Dream Station", everus);
        (l1.Angle, l2.Angle).Should().Be((180, 0), "L1 lies towards the star, L2 away from it");
        l1.Distance.Should().BeApproximately(MapLayout.SpreadPx, 1e-6);
        l2.Distance.Should().BeApproximately(MapLayout.SpreadPx, 1e-6);
    }

    [Fact]
    public void Members_in_one_direction_keep_a_minimum_angle_apart()
    {
        var layout = MapLayout.For([N("Hub", 30, 0, MapNodeKind.Hub), N("X1", 30.2, 0), N("X2", 30.3, 0)], View, TestLabels.Size);

        var hub = layout.Points["Hub"];
        var (a, b) = (Relative(layout, "X1", hub).Angle, Relative(layout, "X2", hub).Angle);
        Math.Abs(a - b).Should().BeGreaterThanOrEqualTo(MapLayout.MinSpreadDegrees - 1e-9);
    }

    [Fact]
    public void Far_nodes_do_not_spread()
    {
        var layout = MapLayout.For([N("A", 20, 0), N("B", 20, 60)], View, TestLabels.Size);

        Polar(layout, "B").Should().BeApproximately(60, 1e-9);
    }

    [Fact]
    public void The_same_nodes_in_another_order_give_the_same_layout()
    {
        MapNode[] nodes = [N("Hub", 30, 0, MapNodeKind.Hub), N("X1", 30.2, 0), N("X2", 30.3, 0), N("Far", 50, 120)];

        var one = MapLayout.For(nodes, View, TestLabels.Size);
        var other = MapLayout.For([.. nodes.Reverse()], View, TestLabels.Size);

        other.Points.Should().BeEquivalentTo(one.Points);
        other.Labels.Should().BeEquivalentTo(one.Labels);
    }

    [Fact]
    public void Labels_lie_free_of_each_other_and_of_the_nodes()
    {
        MapNode[] nodes = [N("Hub", 30, 0, MapNodeKind.Hub), N("X1", 30.2, 0), N("X2", 29.8, 0), N("Far", 50, 120), N("Near", 31, 10)];

        var layout = MapLayout.For(nodes, View, TestLabels.Size);

        var boxes = nodes.Select(n => layout.Labels[n.Key].Box(layout.Points[n.Key])).ToList();
        foreach (var (box, i) in boxes.Select((b, i) => (b, i)))
        {
            boxes.Where((_, j) => j != i).Should().NotContain(other => other.Intersects(box));
            nodes.Where((_, j) => j != i).Select(n => MapLayout.Disc(layout.Points[n.Key], n.Kind)).Should().NotContain(d => d.Intersects(box));
        }
    }

    [Fact]
    public void A_station_label_with_no_free_place_shows_from_one_and_a_half_hubs_and_surface_always()
    {
        // Eight stations around one hub, spread into one ring: not all eight labels find room at zoom 1.
        MapNode[] nodes =
        [
            N("Hub", 30, 0, MapNodeKind.Hub), N("Hurston (surface)", 30.05, 0, MapNodeKind.Surface),
            .. Enumerable.Range(1, 8).Select(i => N($"Station Number {i}", 30 + (0.01 * i), 0.01 * i)),
        ];

        var layout = MapLayout.For(nodes, View, TestLabels.Size);

        layout.Labels.Values.Should().Contain(l => !l.Free, "the case needs a label without room");
        layout.ShownLabels(1).Should().Contain(["Hub", "Hurston (surface)"]).And.HaveCount(layout.Labels.Count(l => l.Value.Free || l.Value.Always));
        layout.ShownLabels(1.49).Should().BeEquivalentTo(layout.ShownLabels(1));
        layout.ShownLabels(MapLayout.AllLabelsZoom).Should().HaveCount(nodes.Length);
    }

    [Fact]
    public void A_projection_zooms_about_the_view_centre_and_pans()
    {
        var layout = MapLayout.For([N("A", 16, 0)], View, TestLabels.Size);

        var p = layout.Points["A"];
        layout.Project(p, 2, new MapPoint(10, -5)).Should().Be(new MapPoint(550 + (2 * (p.X - 550)) + 10, 350 + (2 * (p.Y - 350)) - 5));
    }

    private static MapNode N(string key, double radiusGm, double angleDegrees, MapNodeKind kind = MapNodeKind.Station)
    {
        var a = angleDegrees * Math.PI / 180;
        return new MapNode(key, PlaceNames.Short(key), kind, radiusGm * 1e9 * Math.Cos(a), radiusGm * 1e9 * Math.Sin(a), 0, [key]);
    }

    // The angle about the view's centre (the star) in the star's orientation: y up.
    private static double Polar(MapLayout layout, string key)
    {
        var p = layout.Points[key];
        return Math.Atan2(layout.Centre.Y - p.Y, p.X - layout.Centre.X) * 180 / Math.PI;
    }

    private static double Radius(MapLayout layout, string key)
    {
        var p = layout.Points[key];
        return Math.Sqrt(Math.Pow(p.X - layout.Centre.X, 2) + Math.Pow(p.Y - layout.Centre.Y, 2));
    }

    private static (double Angle, double Distance) Relative(MapLayout layout, string key, MapPoint from)
    {
        var p = layout.Points[key];
        var angle = Math.Round(Math.Atan2(from.Y - p.Y, p.X - from.X) * 180 / Math.PI, 6);
        return (angle == -180 ? 180 : angle, Math.Sqrt(Math.Pow(p.X - from.X, 2) + Math.Pow(p.Y - from.Y, 2)));
    }
}

/// <summary>A label's size without WPF: a little wider than Segoe UI 12 px measures, so a test that passes here passes rendered.</summary>
internal static class TestLabels
{
    public const double CharWidth = 7.5;
    public const double Height = 16;

    public static MapSize Size(string text) => new((text.Length * CharWidth) + 4, Height);
}
