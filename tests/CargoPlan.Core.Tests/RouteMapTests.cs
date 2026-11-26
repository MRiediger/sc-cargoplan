using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.MapNodesTests;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The map's graph (T13.7, D128): an edge is the port matrix's cell, a surface's edge the sum of its places' cells (K1);
/// the focus keeps the edges from the start and their way back; the route's legs in its order. Built here (A10).
/// </summary>
public sealed class RouteMapTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L5 = "HUR-L5 High Course Station";
    private const string Baijini = "Baijini Point";
    private const string Surface = "Hurston (surface)";

    private static readonly RouteFilter All = RouteFilter.Everything(StarSystem.Stanton);

    private static readonly Positions Places = new(
    [
        P("Hurston", "Planet", 12.85, 0), P(Everus, "Manmade", 12.849, 0.001), P(L1, "Manmade_VisibleOnInteraction", 11.56, 0),
        P(L5, "Manmade_VisibleOnInteraction", 6.43, -11.13), P("Lorville", "LandingZone", 12.85, 0), P("Ita", "Moon", 12.83, 0.11),
        P("ArcCorp", "Planet", 18.59, -22.15), P(Baijini, "Manmade", 18.59, -22.15),
    ]);

    [Fact]
    public void A_station_to_station_edge_is_its_matrix_cell()
    {
        var matrix = PortMatrix.Build([T([Everus], [L1]), T([Everus], [L1, L5])], All);

        var map = Map(matrix);

        map.Edge(Everus, L1)!.Parts.Should().ContainSingle().Which.Cell.Should().BeSameAs(matrix.Cell(Everus, L1));
        map.Edge(Everus, L5)!.Count.Should().Be(1);
        map.Edges.Should().HaveCount(2);
    }

    [Fact]
    public void A_surface_edge_is_the_sum_of_its_places_cells_and_keeps_each_cell()
    {
        var matrix = PortMatrix.Build([T([Everus], ["Lorville", "Ita"], reward: 100_000, maxScu: 64), T([Everus], ["Lorville"], reward: 50_000, maxScu: 200)], All);

        var edge = Map(matrix).Edge(Everus, Surface)!;

        edge.Parts.Select(p => p.Dropoff).Should().Equal("Ita", "Lorville");
        edge.Parts.Select(p => p.Cell).Should().Equal(matrix.Cell(Everus, "Ita")!, matrix.Cell(Everus, "Lorville")!);
        (edge.Count, edge.RewardSum, edge.MaxScu).Should().Be((3, 250_000L, 200), "the sum of the cells: a template naming two places counts twice, as in the matrix");
    }

    [Fact]
    public void A_pair_inside_one_node_is_not_drawn_and_counted()
    {
        var map = Map(PortMatrix.Build([T(["Lorville"], ["Ita"]), T(["Lorville"], ["Ita", L1])], All));

        map.Edges.Select(e => (e.From, e.To)).Should().Equal((Surface, L1));
        map.InsideOneNode.Should().Be(2);
    }

    [Fact]
    public void The_nodes_are_those_of_an_edge_with_their_counts_out_and_in()
    {
        var map = Map(PortMatrix.Build([T([Everus], [L1], reward: 100_000), T([Everus], [L1], reward: null), T([L1], [Everus], reward: 20_000)], All));

        map.Nodes.Select(n => n.Key).Should().Equal(Everus, L1);
        map.Figures(Everus).Should().Be(new MapNodeFigures(2, 1, 100_000));
        map.Figures(L1).Should().Be(new MapNodeFigures(1, 2, 20_000));
    }

    [Fact]
    public void The_focus_lights_the_edges_from_the_start_and_their_ways_back_only()
    {
        var matrix = PortMatrix.Build([T([Everus], [L1]), T([L1], [Everus]), T([L1], [L5]), T([L5], [Everus]), T([Baijini], [Everus])], All);

        var map = Map(matrix, focus: Everus);

        map.Edges.Where(map.IsLit).Select(e => (e.From, e.To)).Should().BeEquivalentTo([(Everus, L1), (L1, Everus)]);
        Map(matrix).Edges.Should().OnlyContain(e => Map(matrix).IsLit(e), "no focus lights every edge");
    }

    [Fact]
    public void A_focus_on_a_node_the_filter_hides_lights_everything()
    {
        var map = Map(PortMatrix.Build([T([Everus], [L1])], All), focus: Baijini);

        map.Focus.Should().BeNull();
        map.Edges.Should().OnlyContain(e => map.IsLit(e));
    }

    [Fact]
    public void The_routes_legs_are_its_stations_in_order_there_and_back_and_its_nodes_show_numbered()
    {
        var route = new Route("Everus Harbor ring", DateTime.UtcNow, Everus, LoopKind.LocalRing, StarSystem.Stanton, [L1, L5, "Lorville"], [], []);

        var map = Map(PortMatrix.Build([T([Baijini], [Everus])], All), route: route);

        map.Legs.Should().Equal((Everus, L1), (L1, L5), (L5, Surface), (Surface, Everus));
        map.Numbers.Should().BeEquivalentTo(new Dictionary<string, string> { [L1] = "1", [L5] = "2", [Surface] = "3" });
        map.Nodes.Select(n => n.Key).Should().Contain([Everus, L1, L5, Surface, Baijini]);
    }

    [Fact]
    public void A_route_of_another_system_draws_nothing()
    {
        var route = new Route("Ruin Station ring", DateTime.UtcNow, "Ruin Station", LoopKind.LocalRing, StarSystem.Pyro, ["Checkmate"], [], []);

        var map = Map(PortMatrix.Build([T([Everus], [L1])], All), route: route);

        map.Legs.Should().BeEmpty();
        map.Numbers.Should().BeEmpty();
    }

    [Theory]
    [InlineData(RouteMap.StrongerFrom - 1, 1)]
    [InlineData(RouteMap.StrongestFrom - 1, 2)]
    [InlineData(RouteMap.StrongerFrom, 2)]
    [InlineData(RouteMap.StrongestFrom, 3)]
    [InlineData(null, 1)]
    public void The_edges_strength_has_three_steps_of_its_aUEC_and_none_known_is_the_first(long? reward, int strength) =>
        Map(PortMatrix.Build([T([Everus], [L1], reward: reward, contractor: reward is null ? HaulContractor.RedWind : HaulContractor.Covalex, faction: reward is null ? HaulFaction.Hh : null)], All))
            .Edge(Everus, L1)!.Strength.Should().Be(strength);

    [Fact]
    public void An_edges_colour_is_its_highest_rank_and_a_pair_both_ways_is_two_way()
    {
        var map = Map(PortMatrix.Build([T([Everus], [L1], rank: HaulRank.Junior), T([Everus], [L1], rank: HaulRank.Master), T([L1], [Everus]), T([Everus], [L5])], All));

        map.Edge(Everus, L1)!.TopRank.Should().Be(HaulRank.Master);
        (map.Edge(Everus, L1)!.TwoWay, map.Edge(L1, Everus)!.TwoWay, map.Edge(Everus, L5)!.TwoWay).Should().Be((true, true, false));
    }

    [Fact]
    public void An_edges_distance_is_its_nodes_in_Gm()
    {
        var edge = Map(PortMatrix.Build([T([Everus], [L1])], All)).Edge(Everus, L1)!;

        edge.Gm.Should().BeApproximately(1.289, 0.001);
    }

    [Fact]
    public void The_nearest_edge_within_six_pixels_is_hit_and_none_at_seven()
    {
        (MapPoint, MapPoint)[] segments = [(new(0, 0), new(100, 0)), (new(0, 20), new(100, 20))];

        MapGeometry.Nearest(new MapPoint(50, 6), segments, MapGeometry.HitPx).Should().Be(0);
        MapGeometry.Nearest(new MapPoint(50, 14), segments, MapGeometry.HitPx).Should().Be(1, "6 px from the second, 14 from the first");
        MapGeometry.Nearest(new MapPoint(50, 7), segments.Take(1).ToList(), MapGeometry.HitPx).Should().BeNull();
        MapGeometry.Nearest(new MapPoint(-5, 0), segments, MapGeometry.HitPx).Should().Be(0, "beyond an end the distance is to that end");
    }

    [Fact]
    public void An_edges_line_ends_at_its_nodes_rims_and_a_two_way_one_moves_to_the_right_of_its_way()
    {
        var (a, b) = MapGeometry.Line(new MapPoint(0, 0), new MapPoint(100, 0), 6, 4, twoWay: false);
        (a, b).Should().Be((new MapPoint(6, 0), new MapPoint(96, 0)));

        var (c, d) = MapGeometry.Line(new MapPoint(0, 0), new MapPoint(100, 0), 6, 4, twoWay: true);
        (c.Y, d.Y).Should().Be((MapGeometry.TwoWayOffsetPx, MapGeometry.TwoWayOffsetPx), "right of the way along +x with y down");
    }

    private static RouteMap Map(PortMatrix matrix, string? focus = null, Route? route = null)
    {
        var nodeOf = MapNodes.For(Places.All.Select(p => p.Name), Places, StarSystem.Stanton);
        return RouteMap.Build(matrix, nodeOf, StarSystem.Stanton, focus, route);
    }
}
