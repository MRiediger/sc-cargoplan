using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using Xunit.Abstractions;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The player's own route from the map (T13.7c, D130): missions taken one by one on one board, the start its hub, the
/// stops their pickups and destinations in the shortest order with each pickup before its stops, along it the SCU aboard
/// and the planner's first fit; a mission that likely doesn't fit is taken and marked, one of another board refused.
/// Built here, the ships from FrozenShips (A10).
/// </summary>
public class CustomPlanTests(ITestOutputHelper output)
{
    private const string Everus = "Everus Harbor";
    private const string Seraphim = "Seraphim Station";
    private const string Baijini = "Baijini Point";
    private const string Tressler = "Port Tressler";

    private static readonly Ship HullA = FrozenShips.Load("hull_a");
    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    // In Gm, as LoopsTests: every leg between the hubs is long.
    private static readonly Positions Places = new(
    [
        At(Everus, 0, 0), At(Baijini, -23, -5), At(Tressler, -10, -40), At(Seraphim, 30, -10),
    ]);

    // Design §8.6 (Hull B, Senior), the hub-to-hub contracts system-wide as in the data: at Everus a Senior Medium of tin
    // to three hubs, a Junior Medium and a Junior Direct Medium to Baijini; at Baijini a Junior Direct Medium of stims to
    // Port Tressler; at Port Tressler an Experienced Direct Medium of ice home; at Seraphim a Junior Small and a Rookie
    // Direct Small home. Bands at the SCU §8.6 saw.
    private static readonly HaulTemplate Senior = T([Everus], [Baijini, Tressler, Seraphim], 289_000, shape: HaulShape.SingleToMulti, stops: 3,
        legs: [Leg("Tin", 41, 113), Leg("Tin", 41, 129), Leg("Tin", 41, 91)], title: "Senior Rank - Medium Cargo Haul", areaKey: Board.StantonWide);

    private static readonly HaulTemplate JuniorMedium = T([Everus], [Baijini], 69_250, rank: HaulRank.Junior, legs: [Leg("Tin", 20, 41)], areaKey: Board.StantonWide);
    private static readonly HaulTemplate JuniorDirect = T([Everus], [Baijini], 57_750, rank: HaulRank.Junior, direct: true, legs: [Leg("Tin", 60, 89)], areaKey: Board.StantonWide);
    private static readonly HaulTemplate Stims = T([Baijini], [Tressler], 87_250, rank: HaulRank.Junior, direct: true, legs: [Leg("Stims", 60, 91, CrateSize.Eight)], areaKey: Board.StantonWide);
    private static readonly HaulTemplate Ice = T([Tressler], [Everus], 165_250, rank: HaulRank.Experienced, direct: true, legs: [Leg("Pressurized Ice", 280, 339)], areaKey: Board.StantonWide);
    private static readonly HaulTemplate SeraphimSmall = T([Seraphim], [Everus], 57_000, rank: HaulRank.Junior, size: HaulSize.Small, legs: [Leg("Waste", 4, 16, CrateSize.Four)], areaKey: Board.StantonWide);
    private static readonly HaulTemplate SeraphimDirect = T([Seraphim], [Everus], 41_750, rank: HaulRank.Rookie, size: HaulSize.Small, direct: true, legs: [Leg("Waste", 4, 16, CrateSize.Four)], areaKey: Board.StantonWide);

    private static readonly HaulTemplate[] Leo = [Senior, JuniorMedium, JuniorDirect, Stims, Ice, SeraphimSmall, SeraphimDirect];

    [Fact]
    public void The_LEO_ring_of_design_8_6_from_its_missions_pays_its_sum_flies_Everus_Baijini_Port_Tressler_Seraphim_and_fits_a_Hull_B()
    {
        var plan = Plan(Leo, (Seraphim, Everus), (Tressler, Everus), (Baijini, Tressler), (Everus, Baijini));

        var result = plan.Evaluate(Context(HullB));

        result.Loop.Stations.Should().Equal(Baijini, Tressler, Seraphim);
        (result.Loop.Kind, result.Loop.Start, result.Loop.Reward, result.Loop.Landings).Should().Be((LoopKind.Custom, Everus, 767_250L, 4));
        result.Roles.Select(r => (r.Role.Template, r.Role.Kind, r.Role.AcceptAt)).Should().Equal(
            (Senior, RecipeKind.Bulk, null), (JuniorMedium, RecipeKind.Bulk, null), (JuniorDirect, RecipeKind.Direct, null),
            (Stims, RecipeKind.Direct, Baijini), (Ice, RecipeKind.Return, Tressler),
            (SeraphimSmall, RecipeKind.Return, Seraphim), (SeraphimDirect, RecipeKind.Return, Seraphim));
        result.Roles[0].Role.Destinations.Should().Equal(Baijini, Tressler, Seraphim);
        result.Fits.Should().BeTrue("463 SCU leave Everus, the ice fits once Baijini and Port Tressler unloaded");
        (result.PeakScu, result.PeakAt).Should().Be((463, Everus));
    }

    [Fact]
    public void The_start_is_the_boards_hub_also_when_the_first_mission_is_a_way_back()
    {
        // Martin's R1: the hub of the board; a mission from elsewhere is accepted at its pickup.
        var plan = Plan(Leo, (Seraphim, Everus));

        plan.Start.Should().Be(Everus);
        var result = plan.Evaluate(Context(HullB));
        result.Loop.Start.Should().Be(Everus);
        result.Loop.Stations.Should().Equal(Seraphim);
        result.Roles.Select(r => (r.Role.Kind, r.Role.AcceptAt)).Should().Equal((RecipeKind.Return, Seraphim), (RecipeKind.Return, Seraphim));
    }

    [Fact]
    public void The_order_is_worked_out_not_the_order_of_the_clicks()
    {
        // Seraphim's mission first, Baijini's second: either way round is as long, the one-stop contract's place first.
        var plan = CustomPlan.Empty(StarSystem.Stanton, "Stanton1")
            .Add(new CustomPick(Seraphim, Everus, SeraphimSmall)).Add(new CustomPick(Everus, Baijini, JuniorMedium));

        plan.Evaluate(Context(HullB)).Loop.Stations.Should().Equal(Baijini, Seraphim);
    }

    [Fact]
    public void A_pickup_comes_before_its_stop_even_where_the_tie_would_go_the_other_way()
    {
        // Baijini → Port Tressler alone: both ways round are as long, the tie would take Port Tressler (its one stop) first.
        var plan = Plan(Leo, (Baijini, Tressler));

        var result = plan.Evaluate(Context(HullB));

        result.Loop.Stations.Should().Equal(Baijini, Tressler);
        result.Roles.Should().ContainSingle().Which.Should().Match<CustomRole>(r => r.Fits && !r.Behind);
    }

    [Fact]
    public void Two_missions_each_way_between_two_places_break_one_rule_and_that_mission_is_marked_behind()
    {
        var back = T([Tressler], [Baijini], 50_000, rank: HaulRank.Junior, legs: [Leg("Waste", 1, 16)], areaKey: Board.StantonWide);
        var plan = Plan([Stims, back], (Baijini, Tressler), (Tressler, Baijini));

        var result = plan.Evaluate(Context(HullB));

        result.Roles.Count(r => r.Behind).Should().Be(1);
        result.Fits.Should().BeFalse();
    }

    [Fact]
    public void A_mission_of_another_board_is_refused_a_system_wide_one_is_on_every_Stanton_board()
    {
        var local = T([Everus], [Baijini], 10_000, areaKey: "Stanton1");
        var elsewhere = T([Baijini], [Tressler], 10_000, areaKey: "Stanton3");
        var plan = CustomPlan.Empty(StarSystem.Stanton, "Stanton1");

        plan.Add(new CustomPick(Baijini, Tressler, elsewhere)).Should().BeSameAs(plan);
        plan.Offer(new CustomPick(Baijini, Tressler, elsewhere), Context(HullB)).Should().Be((CustomOffer.DifferentBoard, (string?)null));
        plan.Add(new CustomPick(Everus, Baijini, local)).Picks.Should().ContainSingle();
        plan.Add(new CustomPick(Everus, Baijini, JuniorMedium)).Picks.Should().ContainSingle("system-wide: on every Stanton board");
        CustomPlan.Empty(StarSystem.Stanton, "Stanton3").Add(new CustomPick(Baijini, Tressler, elsewhere)).Start.Should().Be(Baijini);
    }

    [Fact]
    public void Pyros_board_is_Red_Winds_Solar_and_without_a_hub_it_starts_at_the_first_missions_pickup()
    {
        var redWind = T(["Ruin Station"], ["Checkmate"], null, contractor: HaulContractor.RedWind, system: StarSystem.Pyro, areaKey: Board.PyroSolar, faction: HaulFaction.Hh);
        var covalex = T(["Ruin Station"], ["Checkmate"], 10_000, system: StarSystem.Pyro, areaKey: Board.PyroSolar);
        var plan = CustomPlan.Empty(StarSystem.Pyro, Board.PyroSolar);

        plan.Start.Should().BeNull();
        plan.Add(new CustomPick("Ruin Station", "Checkmate", covalex)).Should().BeSameAs(plan);
        plan.Add(new CustomPick("Ruin Station", "Checkmate", redWind)).Start.Should().Be("Ruin Station");
    }

    [Fact]
    public void A_mission_that_tops_the_peak_is_taken_and_marked_where_it_doesnt_fit_and_unloading_first_makes_room()
    {
        // A Hull A holds 64 SCU: 40 to Baijini and 40 more from Everus don't, 40 from Baijini after unloading there do.
        var first = T([Everus], [Baijini], 50_000, legs: [Leg("Gold", 1, 40, CrateSize.Eight)]);
        var together = T([Everus], [Tressler], 60_000, legs: [Leg("Gold", 1, 40, CrateSize.Eight)]);
        var after = T([Baijini], [Tressler], 70_000, legs: [Leg("Gold", 1, 40, CrateSize.Eight)], areaKey: Board.StantonWide);
        var plan = CustomPlan.Empty(StarSystem.Stanton, "Stanton1").Add(new CustomPick(Everus, Baijini, first));

        plan.Offer(new CustomPick(Everus, Tressler, together), Context(HullA)).Should().Be((CustomOffer.LikelyNot, Everus));
        plan.Offer(new CustomPick(Baijini, Tressler, after), Context(HullA)).Should().Be((CustomOffer.Fits, (string?)null));

        var crowded = plan.Add(new CustomPick(Everus, Tressler, together));
        crowded.Picks.Should().HaveCount(2, "marked, not refused (Martin's R2)");
        crowded.Evaluate(Context(HullA)).Roles.Select(r => (r.Pick.Template, r.OverAt)).Should().Equal((first, null), (together, Everus));
        var unloaded = plan.Add(new CustomPick(Baijini, Tressler, after)).Evaluate(Context(HullA));
        (unloaded.Fits, unloaded.PeakScu).Should().Be((true, 40));
    }

    [Fact]
    public void An_edges_missions_are_each_template_on_each_pair_best_paying_first_and_the_plan_counts_its_own_there()
    {
        var map = Map(Leo);
        var edge = map.Edge(Everus, Baijini)!;

        CustomPlan.Missions(edge).Select(p => p.Template).Should().Equal(Senior, JuniorMedium, JuniorDirect);
        var plan = CustomPlan.Empty(StarSystem.Stanton, "Stanton1").Add(CustomPlan.Missions(edge)[1]);
        (plan.Count(edge), edge.Count).Should().Be((1, 3));
        plan.Offer(CustomPlan.Missions(edge)[1], Context(HullB)).Offer.Should().Be(CustomOffer.InPlan);
        plan.Add(CustomPlan.Missions(edge)[1]).Should().BeSameAs(plan, "each template once");
    }

    [Fact]
    public void A_minus_takes_one_mission_out_and_the_order_is_worked_out_again_a_right_click_the_edges_last()
    {
        var plan = Plan(Leo, (Everus, Baijini), (Baijini, Tressler));
        var stims = plan.Picks.Single(p => p.Template == Stims);

        // The Senior still goes to Port Tressler and Seraphim.
        plan.Remove(stims).Evaluate(Context(HullB)).Loop.Stations.Should().Equal(Baijini, Tressler, Seraphim);
        plan.Remove(Map(Leo).Edge(Everus, Baijini)!).Picks.Select(p => p.Template).Should().Equal(Senior, JuniorMedium, Stims);
        CustomPlan.Empty(StarSystem.Stanton, "Stanton1").Remove(stims).Picks.Should().BeEmpty();
    }

    [Fact]
    public void Its_route_keeps_where_each_mission_is_accepted_and_three_runs_measure_it_like_any_loop()
    {
        var loop = Plan(Leo, (Everus, Baijini), (Baijini, Tressler), (Tressler, Everus)).Evaluate(Context(HullB)).Loop;
        var monday = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        var route = Route.From(loop, "Own plan from Everus Harbor", ObservedEdges.Empty, monday);

        route.Kind.Should().Be(LoopKind.Custom);
        route.Placeholders.Select(p => (p.Kind, p.AcceptAt)).Should().Equal(
            (RecipeKind.Bulk, null), (RecipeKind.Bulk, null), (RecipeKind.Direct, null), (RecipeKind.Direct, Baijini), (RecipeKind.Return, Tressler));
        RouteRun[] runs =
        [
            .. Enumerable.Range(1, 3).Select(day => new RouteRun(route.Name, monday.AddDays(day), null, monday.AddDays(day).AddMinutes(70), Everus,
                LoopKind.Custom, StarSystem.Stanton, loop.Landings, loop.Crates, loop.LongJumps, loop.Reward ?? 0, 3, 3, 0, HullB.Id, loop.Stations)),
        ];
        MeasuredTimes.For(runs, HullB.Id).Of(loop)!.IsEnough.Should().BeTrue("the same key: kind, system, start, stations");
    }

    [Fact]
    public void More_than_eight_stops_go_to_the_nearest_place_whose_pickups_lie_behind()
    {
        // Too many to try every order: nearest next, but P1 waits for P2, its pickup.
        string[] stops = [.. Enumerable.Range(1, 9).Select(i => $"P{i}")];
        var line = new Positions([At("S", 0, 0), .. stops.Select((p, i) => At(p, i + 1, 0))]);

        Loops.Order(StarSystem.Stanton, "S", stops, line, [], [("P2", "P1")]).Should().Equal("P2", "P1", "P3", "P4", "P5", "P6", "P7", "P8", "P9");
        Loops.Order(StarSystem.Stanton, "S", stops, line, []).Should().Equal(stops, "without rules the place order, as before");
    }

    [Fact]
    [Trait("Category", "Timing")]
    public void Eight_stops_with_pickups_before_their_stops_are_ordered_in_under_50_ms()
    {
        // By hand (T13.7c): the most a panel row's "fits" asks, every order of eight places with four rules.
        string[] stops = [.. Enumerable.Range(1, 8).Select(i => $"P{i}")];
        var ring = new Positions([At("S", 0, 0), .. stops.Select((p, i) => At(p, 10 * Math.Cos(i), 10 * Math.Sin(i)))]);
        (string, string)[] rules = [("P2", "P1"), ("P4", "P3"), ("P6", "P5"), ("P8", "P7")];
        Loops.Order(StarSystem.Stanton, "S", stops, ring, [], rules);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        Loops.Order(StarSystem.Stanton, "S", stops, ring, [], rules);

        output.WriteLine(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"8 stops, 4 rules: {watch.Elapsed.TotalMilliseconds:0.0} ms"));
        watch.Elapsed.TotalMilliseconds.Should().BeLessThan(50);
    }

    [Fact]
    public void Without_a_mission_there_is_no_loop_to_start()
    {
        var result = CustomPlan.Empty(StarSystem.Stanton, "Stanton1").Evaluate(Context(HullB));

        (result.Loop.HasNumbers, result.Loop.Stations.Count, result.Roles.Count, result.PeakAt).Should().Be((false, 0, 0, (string?)null));
    }

    private static CustomContext Context(Ship ship) => new(StarSystem.Stanton, Places, ObservedEdges.Empty, ship, null);

    // Every mission of the given edges, edge by edge, on Everus's board.
    private static CustomPlan Plan(IReadOnlyList<HaulTemplate> templates, params (string From, string To)[] edges)
    {
        var map = Map(templates);
        var plan = CustomPlan.Empty(StarSystem.Stanton, "Stanton1");
        foreach (var (from, to) in edges)
        {
            plan = CustomPlan.Missions(map.Edge(from, to)!).Aggregate(plan, (p, pick) => p.Add(pick));
        }

        return plan;
    }

    private static RouteMap Map(IReadOnlyList<HaulTemplate> templates)
    {
        var matrix = PortMatrix.Build(templates, RouteFilter.Everything(StarSystem.Stanton));
        return RouteMap.Build(matrix, MapNodes.For(Places.All.Select(p => p.Name), Places, StarSystem.Stanton), StarSystem.Stanton);
    }

    private static StarPosition At(string name, double xGm, double yGm) =>
        new(name, name, StarSystem.Stanton, "Manmade", null, xGm * 1e9, yGm * 1e9, 0, []);
}
