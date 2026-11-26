using CargoPlan.App.Routing;
using CargoPlan.App.Settings;
using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;

namespace CargoPlan.App.Tests;

/// <summary>
/// The Plan tab's map view (T13.7, D128): the matrix's cells under the matrix's filters as a graph, the focus for the
/// session, the active route, the tool tips in the matrix cell's format. Templates and positions built here (A10).
/// </summary>
public sealed class RoutesMapViewModelTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L5 = "HUR-L5 High Course Station";

    private static readonly Positions Places = new(
    [
        P("Hurston", "Planet", 12.85, 0), P(Everus, "Manmade", 12.849, 0.001), P(L1, "Manmade_VisibleOnInteraction", 11.56, 0),
        P(L5, "Manmade_VisibleOnInteraction", 6.43, -11.13), P("Lorville", "LandingZone", 12.85, 0), P("Ita", "Moon", 12.83, 0.11),
    ]);

    private static int _uuid;

    [Fact]
    public void The_map_view_shows_the_matrixs_filters_and_lines_and_no_start()
    {
        var model = Map([T([Everus], [L1]), T([], [], areaKey: "Stanton")]);

        (model.ShowsMap, model.ShowsMatrixFilters, model.OffersStart, model.ShowsMatrix).Should().Be((true, true, false, false));
        model.Map.Should().NotBeNull();
        model.WithoutPlaceText.Should().Be("1 template not shown: the game data name no pickup or destination for it.", "the map draws the matrix, its lines hold");
        model.MapLegend.Should().Contain("dashed: game data only");
    }

    [Fact]
    public void Outside_the_map_view_there_is_no_map_and_interstellar_says_why()
    {
        var model = Map([T([Everus], [L1])]);

        model.NextCommand.Execute("view");
        model.Map.Should().BeNull();

        model.PreviousCommand.Execute("view");
        model.NextCommand.Execute("system");
        model.NextCommand.Execute("system");
        model.SystemLabel.Should().Be("Interstellar");
        model.Map.Should().BeNull();
        model.MapNote.Should().Be("No map for this system.");
    }

    [Fact]
    public void An_edges_tool_tip_heads_with_places_distance_count_and_sum_and_lists_the_templates_as_its_matrix_cell()
    {
        var model = Map([T([Everus], [L1], reward: 100_000), T([Everus], [L1], reward: 50_250, title: "Junior Rank - Small Cargo Haul")]);

        var tip = model.Map!.EdgeToolTip(model.Map.Graph.Edge(Everus, L1)!).Split('\n');

        tip[0].Should().Be("Everus Harbor → HUR-L1 Green Glade Station · 1.3 Gm · 2 contracts · Σ 150,250 aUEC · most SCU in one contract: 64");
        var cell = MatrixCellToolTip(model, Everus, L1).Split('\n');
        tip.Skip(1).Should().Equal(cell.Skip(1), "the template lines are the matrix cell's");
    }

    [Fact]
    public void A_surface_edges_tool_tip_has_a_section_per_place_and_one_without_rewards_says_so()
    {
        var model = Map([T([Everus], ["Lorville", "Ita"], reward: 100_000), T([L5], [L1], reward: null, contractor: HaulContractor.RedWind, faction: HaulFaction.Hh)]);

        var tip = model.Map!.EdgeToolTip(model.Map.Graph.Edge(Everus, "Hurston (surface)")!).Split('\n');
        tip[0].Should().StartWith("Everus Harbor → Hurston (surface) · ").And.Contain("2 contracts · Σ 200,000 aUEC");
        tip.Should().Contain(["Everus Harbor → Ita:", "Everus Harbor → Lorville:"]);

        model.Map.EdgeToolTip(model.Map.Graph.Edge(L5, L1)!).Should().Contain("– aUEC: no reward in the game data, a scan shows it");
    }

    [Fact]
    public void A_nodes_tool_tip_counts_out_and_in_and_names_a_surfaces_places()
    {
        var model = Map([T([Everus], [L1], reward: 100_000), T([L1], [Everus], reward: 20_000), T([Everus], ["Lorville"], reward: 30_000)]);
        var graph = model.Map!.Graph;

        model.Map.NodeToolTip(graph.Nodes.Single(n => n.Key == Everus)).Should().Be("Everus Harbor · 2 out · 1 in · Σ out 130,000 aUEC\nclick: only what leaves here and comes back");
        model.Map.NodeToolTip(graph.Nodes.Single(n => n.Key == "Hurston (surface)")).Should().Contain("\nsurface and moons: Lorville", "the places the system's templates name");
    }

    [Fact]
    public void A_click_on_a_node_focuses_it_for_the_session_another_click_shows_all_and_a_filter_keeps_it()
    {
        var model = Map([T([Everus], [L1]), T([L1], [L5])]);

        model.FocusCommand.Execute(Everus);
        model.Map!.Graph.Focus.Should().Be(Everus);
        model.Map.NodeToolTip(model.Map.Graph.Nodes[0]).Should().EndWith("click: show every contract again");

        model.NextCommand.Execute("rank");
        model.PreviousCommand.Execute("rank");
        model.Map!.Graph.Focus.Should().Be(Everus, "the focus lives for the session, not for one filter");

        model.FocusCommand.Execute(Everus);
        model.Map!.Graph.Focus.Should().BeNull();
    }

    [Fact]
    public void The_active_route_is_drawn_until_it_ends()
    {
        // A route's stations are places of its templates' pools (it comes from a loop), so the map knows them.
        var model = Map([T([Everus], [L1]), T([L5], [Everus])]);
        var route = new Route("Everus Harbor ring", DateTime.UtcNow, Everus, LoopKind.LocalRing, StarSystem.Stanton, [L1, L5], [], []);

        model.MapLegend.Should().NotContain("orange");
        model.ShowRoute(route);
        model.Map!.Graph.Active.Legs.Should().Equal((Everus, L1), (L1, L5), (L5, Everus));
        model.MapLegend.Should().EndWith(" · orange: your plan", "gate finding 7a/7b: the help line names the route's colour while it runs");

        var map = model.Map;
        model.ShowRoute(route, [new MapExtra(L5, L1, 3, null)]);
        model.Map.Should().NotBeSameAs(map, "new extras draw anew");
        model.Map!.Graph.Extras.Should().ContainSingle().Which.Should().BeEquivalentTo(new { From = L5, To = L1 });
        map = model.Map;
        model.ShowRoute(route, [new MapExtra(L5, L1, 3, null)]);
        model.Map.Should().BeSameAs(map, "the same route and extras draw nothing anew");

        model.ShowRoute(null);
        model.Map!.Graph.Active.Legs.Should().BeEmpty();
        model.MapLegend.Should().NotContain("orange");
    }

    [Fact]
    public void A_filter_makes_a_new_graph_over_the_same_nodes_and_the_recipes_hub_is_ringed()
    {
        var model = Map([T([Everus], [L1]), T([L1], [L5], rank: HaulRank.Master)]);
        var before = model.Map!;

        model.NextCommand.Execute("rank");

        model.Map.Should().NotBeSameAs(before);
        model.Map!.Universe.Should().BeSameAs(before.Universe, "the layout is made of every node of the system: a filter moves none");
        model.Map.Hub.Should().Be(Everus);
    }

    [Fact]
    public void With_a_station_in_focus_a_click_on_an_edge_builds_an_own_plan_that_Start_plan_starts()
    {
        // T13.7b (D129): out to L1, back to Everus; the plan starts as a custom route and the draft goes.
        var model = Map([T([Everus], [L1], reward: 100_000), T([L1], [Everus], reward: 40_000, title: "Junior Rank - Small Cargo Haul")]);
        model.ShowShip(HullB);
        PlanStart? started = null;
        model.StartRequested += (_, start) => started = start;

        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.HasCustom.Should().BeFalse("no station in focus: a click adds nothing");

        model.FocusCommand.Execute(Everus);
        model.Map!.CanPick.Should().BeTrue();
        model.Map.EdgeToolTip(model.Map.Graph.Edge(Everus, L1)!).Should().EndWith("click: add its next contract to your plan · right click: take its last back");
        model.AddEdgeCommand.Execute(model.Map.Graph.Edge(Everus, L1));
        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(L1, Everus));

        (model.HasCustom, model.OffersStart, model.CanStartPlan).Should().Be((true, true, true));
        model.CustomSummary.Should().Be("Own plan from Everus Harbor: Everus Harbor → HUR-L1 → Everus Harbor · 2 contracts · Σ 140,000 aUEC · 2 landings · 8 crates · 0 long jumps · fits MISC Hull B");
        model.CustomLines.Should().Equal(
            "Bulk · Senior Rank - Medium Cargo Haul · 100,000 aUEC · 10–64 SCU in 16s · HUR-L1 · fits",
            "Back · Junior Rank - Small Cargo Haul · 40,000 aUEC · 10–64 SCU in 16s · accept at HUR-L1 · fits");
        model.Map!.Graph.Draft.Legs.Should().Equal((Everus, L1), (L1, Everus));
        model.Map.Graph.Draft.Numbers.Should().BeEquivalentTo(new Dictionary<string, string> { [L1] = "1" });
        model.StartPlanToolTip.Should().Be("Start plan: Own plan from Everus Harbor – its contracts as placeholders in the Cargo tab");

        model.StartPlanCommand.Execute(null);

        started!.Loop.Kind.Should().Be(LoopKind.Custom);
        (started.Name, started.Loop.Stations.Single(), started.Loop.Contracts.Count).Should().Be(("Own plan from Everus Harbor", L1, 2));
        (model.HasCustom, model.OffersStart).Should().Be((false, false), "the plan is the route now");
    }

    [Fact]
    public void A_right_click_takes_the_edges_last_contract_back_Clear_and_another_focus_drop_the_plan()
    {
        var model = Map([T([Everus], [L1], reward: 100_000), T([Everus], [L1], reward: 50_000), T([L1], [L5])]);
        model.ShowShip(HullB);
        model.FocusCommand.Execute(Everus);
        var edge = model.Map!.Graph.Edge(Everus, L1)!;

        model.AddEdgeCommand.Execute(edge);
        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.CustomLines.Should().HaveCount(2);
        model.RemoveEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.CustomLines.Should().ContainSingle().Which.Should().Contain("100,000 aUEC");

        model.ClearCustomCommand.Execute(null);
        (model.HasCustom, model.Map!.Graph.Focus).Should().Be((false, Everus), "Clear keeps the focus");

        model.AddEdgeCommand.Execute(model.Map.Graph.Edge(Everus, L1));
        model.FocusCommand.Execute(L1);
        model.HasCustom.Should().BeFalse("another start, another plan");
    }

    [Fact]
    public void An_edge_the_focus_dims_is_not_picked()
    {
        var model = Map([T([Everus], [L1]), T([L1], [L5])]);
        model.ShowShip(HullB);
        model.FocusCommand.Execute(Everus);

        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(L1, L5));

        model.HasCustom.Should().BeFalse("L1 → L5 is dimmed until L1 is a station of the plan");
        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.AddEdgeCommand.Execute(model.Map!.Graph.Edge(L1, L5));
        model.CustomLines.Should().HaveCount(2, "now along the plan");
    }

    private static readonly Ship HullB = CargoPlan.Core.Tests.FrozenShips.Load("hull_b");

    private static RoutesViewModel Map(IReadOnlyList<HaulTemplate> templates)
    {
        var model = new RoutesViewModel(new HaulingLoad.Loaded(new HaulingData(templates, Places), []), new ShipDataVersion(2026, 11, 2), RoutesSettings.Default, null);
        model.PreviousCommand.Execute("view");
        return model;
    }

    // The matrix view's tool tip of the cell, from the same model.
    private static string MatrixCellToolTip(RoutesViewModel model, string pickup, string dropoff)
    {
        model.NextCommand.Execute("view");
        var tip = model.Rows.Single(r => r.Pickup == pickup).Cells[model.Columns.ToList().IndexOf(dropoff)]!.ToolTip;
        model.PreviousCommand.Execute("view");
        return tip;
    }

    private static HaulTemplate T(
        string[] pickups,
        string[] dropoffs,
        long? reward = 100_000,
        HaulRank rank = HaulRank.Senior,
        HaulContractor contractor = HaulContractor.Covalex,
        HaulFaction? faction = null,
        string areaKey = "Stanton1",
        string title = "Senior Rank - Medium Cargo Haul") => new(
        $"m{Interlocked.Increment(ref _uuid):D4}", "d", contractor, title, rank, HaulSize.Medium, false, HaulShape.AToB, 1, StarSystem.Stanton, areaKey,
        HaulMissionType.Stellar, faction, reward, [new HaulLeg("Gold", 10, 64, CrateSize.Sixteen)], pickups, dropoffs);

    private static StarPosition P(string name, string type, double xGm, double yGm) =>
        new(name, name, StarSystem.Stanton, type, null, xGm * 1e9, yGm * 1e9, 0, []);
}
