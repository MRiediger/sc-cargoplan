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
        tip.Skip(1).SkipLast(1).Should().Equal(cell.Skip(1), "the template lines are the matrix cell's");
        tip[^1].Should().Be("0/2 in plan", "T13.7c: how many of the edge's missions the plan holds");
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

        model.MapLegend.Should().NotContain("violet");
        model.ShowRoute(route);
        model.Map!.Graph.Active.Legs.Should().Equal((Everus, L1), (L1, L5), (L5, Everus));
        model.MapLegend.Should().EndWith(" · violet: your plan", "gate finding 7a/7b: the help line names the route's colour while it runs");

        var map = model.Map;
        model.ShowRoute(route, [new MapExtra(L5, L1, 3, null)]);
        model.Map.Should().NotBeSameAs(map, "new extras draw anew");
        model.Map!.Graph.Extras.Should().ContainSingle().Which.Should().BeEquivalentTo(new { From = L5, To = L1 });
        map = model.Map;
        model.ShowRoute(route, [new MapExtra(L5, L1, 3, null)]);
        model.Map.Should().BeSameAs(map, "the same route and extras draw nothing anew");

        model.ShowRoute(null);
        model.Map!.Graph.Active.Legs.Should().BeEmpty();
        model.MapLegend.Should().NotContain("violet");
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
    public void A_click_on_an_edge_opens_its_missions_without_a_focus_and_plus_builds_a_plan_that_Start_plan_starts()
    {
        // T13.7c (D130): missions one by one from the edge's panel; out to L1, back to Everus; the plan starts as a custom route.
        var model = Map([T([Everus], [L1], reward: 100_000), T([L1], [Everus], reward: 40_000, title: "Junior Rank - Small Cargo Haul")]);
        model.ShowShip(HullB);
        PlanStart? started = null;
        model.StartRequested += (_, start) => started = start;

        model.OpenEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));

        model.EdgePanel!.Header.Should().Be("Everus Harbor → HUR-L1 Green Glade Station · 0/1 in plan");
        model.EdgePanel.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Text = "Senior Rank - Medium Cargo Haul · 100,000 aUEC · 10–64 SCU in 16s · Everus Harbor → HUR-L1",
            State = " · fits",
            CanAdd = true,
            CanRemove = false,
        });
        model.AddMissionCommand.Execute(model.EdgePanel.Rows[0]);
        model.EdgePanel!.Header.Should().EndWith("1/1 in plan");
        model.EdgePanel.Rows[0].Should().BeEquivalentTo(new { State = " · in plan", CanAdd = false, CanRemove = true });
        model.Map!.EdgeToolTip(model.Map.Graph.Edge(Everus, L1)!).Should().EndWith("\n1/1 in plan\nclick: its missions · right click: take its last out of your plan");

        model.OpenEdgeCommand.Execute(model.Map.Graph.Edge(L1, Everus));
        model.AddMissionCommand.Execute(model.EdgePanel!.Rows[0]);

        (model.HasCustom, model.OffersStart, model.CanStartPlan).Should().Be((true, true, true));
        model.CustomSummary.Should().Be(
            $"Own plan from Everus Harbor: Everus Harbor → HUR-L1 → Everus Harbor · 2 contracts · Σ 140,000 aUEC · 2 landings · 8 crates · 0 long jumps · peak 64/{HullB.TotalScu} SCU at Everus Harbor · fits MISC Hull B");
        model.CustomRows.Select(r => (r.Text, r.State, r.CanRemove)).Should().Equal(
            ("Bulk · Senior Rank - Medium Cargo Haul · 100,000 aUEC · 10–64 SCU in 16s · HUR-L1", " · fits", true),
            ("Back · Junior Rank - Small Cargo Haul · 40,000 aUEC · 10–64 SCU in 16s · accept at HUR-L1", " · fits", true));
        model.Map!.Graph.Draft.Legs.Should().Equal((Everus, L1), (L1, Everus));
        model.Map.Graph.Draft.Numbers.Should().BeEquivalentTo(new Dictionary<string, string> { [L1] = "1" });
        model.StartPlanToolTip.Should().Be("Start plan: Own plan from Everus Harbor – its contracts as placeholders in the Cargo tab");

        model.StartPlanCommand.Execute(null);

        started!.Loop.Kind.Should().Be(LoopKind.Custom);
        (started.Name, started.Loop.Stations.Single(), started.Loop.Contracts.Count).Should().Be(("Own plan from Everus Harbor", L1, 2));
        (model.HasCustom, model.OffersStart).Should().Be((false, false), "the plan is the route now");
    }

    [Fact]
    public void The_same_edge_again_or_the_cross_closes_its_panel()
    {
        var model = Map([T([Everus], [L1]), T([L1], [L5])]);
        var edge = model.Map!.Graph.Edge(Everus, L1);

        model.OpenEdgeCommand.Execute(edge);
        model.HasEdgePanel.Should().BeTrue();
        model.OpenEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.HasEdgePanel.Should().BeFalse("the same edge again");

        model.OpenEdgeCommand.Execute(edge);
        model.OpenEdgeCommand.Execute(model.Map!.Graph.Edge(L1, L5));
        model.EdgePanel!.From.Should().Be(L1, "another edge, its panel");
        model.ClosePanelCommand.Execute(null);
        model.HasEdgePanel.Should().BeFalse();
    }

    [Fact]
    public void Minus_and_a_right_click_take_missions_out_the_focus_keeps_the_plan_and_Clear_drops_it()
    {
        var model = Map([T([Everus], [L1], reward: 100_000), T([Everus], [L1], reward: 50_000), T([L1], [L5])]);
        model.ShowShip(HullB);
        model.OpenEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.AddMissionCommand.Execute(model.EdgePanel!.Rows[0]);
        model.AddMissionCommand.Execute(model.EdgePanel!.Rows[1]);
        model.CustomRows.Should().HaveCount(2);

        model.RemoveEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.CustomRows.Should().ContainSingle().Which.Text.Should().Contain("100,000 aUEC");
        model.AddMissionCommand.Execute(model.EdgePanel!.Rows[1]);
        model.RemoveMissionCommand.Execute(model.CustomRows[0]);
        model.CustomRows.Should().ContainSingle().Which.Text.Should().Contain("50,000 aUEC");

        model.FocusCommand.Execute(L1);
        model.HasCustom.Should().BeTrue("the focus is a filter now (T13.7c)");
        model.ClearCustomCommand.Execute(null);
        (model.HasCustom, model.Map!.Graph.Focus).Should().Be((false, L1), "Clear keeps the focus");
    }

    [Fact]
    public void The_focus_lists_its_missions_with_plus_and_one_of_another_board_says_so()
    {
        // Martin's R3: the station's missions under the plan; the board is routes.hub's (Everus: Stanton1).
        var model = Map([T([Everus], [L1], reward: 100_000), T([L1], [L5], reward: 90_000), T([Everus], [L1], reward: 80_000, areaKey: "Stanton3"), T([Everus], [L5])]);
        model.ShowShip(HullB);
        model.ShowsCustomBar.Should().BeFalse();

        model.FocusCommand.Execute(L1);

        model.FocusMissionsHeader.Should().Be("Missions at HUR-L1");
        model.FocusMissions.Select(r => (r.Text.Split(" · ")[1], r.State, r.CanAdd)).Should().BeEquivalentTo(
            [("100,000 aUEC", (string?)null, true), ("80,000 aUEC", " · different board", false), ("90,000 aUEC", (string?)null, true)]);
        model.ShowsCustomBar.Should().BeTrue();
        model.AddMissionCommand.Execute(model.FocusMissions.Single(r => r.Text.Contains("90,000")));
        model.CustomRows.Should().ContainSingle().Which.Text.Should().Contain("accept at HUR-L1", "from L1, not from the hub");

        model.FocusCommand.Execute(L1);
        model.FocusMissions.Should().BeEmpty();
        model.FocusMissionsHeader.Should().BeNull();
    }

    [Fact]
    public void A_mission_that_likely_doesnt_fit_goes_in_marked_and_without_a_ship_there_is_no_plus()
    {
        // Martin's R2: the SCU and the planner's first fit along the order; marked, not refused.
        var model = Map([T([Everus], [L1], reward: 100_000), T([Everus], [L5], reward: 90_000)]);
        model.OpenEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L1));
        model.EdgePanel!.Rows[0].Should().BeEquivalentTo(new { State = " · no ship", CanAdd = false });

        model.ShowShip(HullA);
        model.AddMissionCommand.Execute(model.EdgePanel!.Rows[0]);
        model.OpenEdgeCommand.Execute(model.Map!.Graph.Edge(Everus, L5));
        model.EdgePanel!.Rows[0].Should().BeEquivalentTo(new { State = " · likely doesn't fit at Everus Harbor", CanAdd = true });
        model.AddMissionCommand.Execute(model.EdgePanel.Rows[0]);

        model.CustomRows.Select(r => r.State).Should().Equal(" · fits", " · likely doesn't fit at Everus Harbor");
        model.CustomSummary.Should().EndWith($"peak 128/64 SCU at Everus Harbor · 1 likely don't fit {HullA.Name}");
    }
    private static readonly Ship HullA = CargoPlan.Core.Tests.FrozenShips.Load("hull_a");

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
