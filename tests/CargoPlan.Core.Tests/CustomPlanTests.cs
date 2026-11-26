using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The player's own route from the map (T13.7b, D129): a click on an edge takes its next best-paying contract that fits
/// with what is aboard there, a right click takes that edge's last back; the stations in the order the clicked edges'
/// ends came, places only a multi-stop contract names before the way home in place order; a contract from the start
/// leaves the hub, one to the start is a way back, any other is accepted on the way. Built here, the ship from
/// FrozenShips (A10).
/// </summary>
public class CustomPlanTests
{
    private const string Everus = "Everus Harbor";
    private const string Seraphim = "Seraphim Station";
    private const string Baijini = "Baijini Point";
    private const string Tressler = "Port Tressler";

    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    // In Gm, as LoopsTests: every leg between the hubs is long. Port Tressler before Seraphim is not the shortest trip.
    private static readonly Positions Places = new(
    [
        At(Everus, 0, 0), At(Baijini, -23, -5), At(Tressler, -10, -40), At(Seraphim, 30, -10),
    ]);

    // Design §8.6 (Hull B, Senior): at Everus a Senior Medium of tin to three hubs, a Junior Medium and a Junior Direct
    // Medium to Baijini; at Baijini a Junior Direct Medium of stims to Port Tressler; at Port Tressler an Experienced Direct
    // Medium of ice home; at Seraphim a Junior Small and a Rookie Direct Small home. Bands at the SCU §8.6 saw.
    private static readonly HaulTemplate Senior = T([Everus], [Baijini, Tressler, Seraphim], 289_000, shape: HaulShape.SingleToMulti, stops: 3,
        legs: [Leg("Tin", 41, 113), Leg("Tin", 41, 129), Leg("Tin", 41, 91)], title: "Senior Rank - Medium Cargo Haul");

    private static readonly HaulTemplate JuniorMedium = T([Everus], [Baijini], 69_250, rank: HaulRank.Junior, legs: [Leg("Tin", 20, 41)]);
    private static readonly HaulTemplate JuniorDirect = T([Everus], [Baijini], 57_750, rank: HaulRank.Junior, direct: true, legs: [Leg("Tin", 60, 89)]);
    private static readonly HaulTemplate Stims = T([Baijini], [Tressler], 87_250, rank: HaulRank.Junior, direct: true, legs: [Leg("Stims", 60, 91, CrateSize.Eight)]);
    private static readonly HaulTemplate Ice = T([Tressler], [Everus], 165_250, rank: HaulRank.Experienced, direct: true, legs: [Leg("Pressurized Ice", 280, 339)]);
    private static readonly HaulTemplate SeraphimSmall = T([Seraphim], [Everus], 57_000, rank: HaulRank.Junior, size: HaulSize.Small, legs: [Leg("Waste", 4, 16, CrateSize.Four)]);
    private static readonly HaulTemplate SeraphimDirect = T([Seraphim], [Everus], 41_750, rank: HaulRank.Rookie, size: HaulSize.Small, direct: true, legs: [Leg("Waste", 4, 16, CrateSize.Four)]);

    private static readonly HaulTemplate[] Leo = [Senior, JuniorMedium, JuniorDirect, Stims, Ice, SeraphimSmall, SeraphimDirect];

    [Fact]
    public void The_LEO_ring_of_design_8_6_by_clicks_pays_its_sum_flies_Everus_Baijini_Port_Tressler_Seraphim_and_fits_a_Hull_B()
    {
        var map = Map(Leo);
        var plan = CustomPlan.Empty(Everus);
        foreach (var (from, to) in new[] { (Everus, Baijini), (Everus, Baijini), (Everus, Baijini), (Baijini, Tressler), (Tressler, Everus), (Seraphim, Everus), (Seraphim, Everus) })
        {
            plan = plan.Add(map.Edge(from, to)!, Context);
        }

        var result = plan.Evaluate(Context);

        result.Loop.Stations.Should().Equal(Baijini, Tressler, Seraphim);
        (result.Loop.Kind, result.Loop.Start, result.Loop.Reward, result.Loop.Landings).Should().Be((LoopKind.Custom, Everus, 767_250L, 4));
        result.Roles.Select(r => (r.Role.Template, r.Role.Kind, r.Role.AcceptAt)).Should().Equal(
            (Senior, RecipeKind.Bulk, null), (JuniorMedium, RecipeKind.Bulk, null), (JuniorDirect, RecipeKind.Direct, null),
            (Stims, RecipeKind.Direct, Baijini), (Ice, RecipeKind.Return, Tressler),
            (SeraphimSmall, RecipeKind.Return, Seraphim), (SeraphimDirect, RecipeKind.Return, Seraphim));
        result.Roles[0].Role.Destinations.Should().Equal(Baijini, Tressler, Seraphim);
        result.Roles.Should().OnlyContain(r => r.Fits, "463 SCU leave Everus, the ice fits once Baijini and Port Tressler unloaded");
        result.Fits.Should().BeTrue();
    }

    [Fact]
    public void Places_only_a_multi_stop_contract_names_come_before_the_way_home_in_place_order()
    {
        // The Senior names Port Tressler and Seraphim, nobody clicked them: in place order Seraphim (Stanton2) comes first.
        var plan = CustomPlan.Empty(Everus).Add(Map(Leo).Edge(Everus, Baijini)!, Context);

        plan.Evaluate(Context).Loop.Stations.Should().Equal(Baijini, Seraphim, Tressler);
    }

    [Fact]
    public void A_right_click_takes_the_edges_last_contract_back_and_a_click_on_an_edge_with_none_left_changes_nothing()
    {
        var edge = Map(Leo).Edge(Everus, Baijini)!;
        var three = CustomPlan.Empty(Everus).Add(edge, Context).Add(edge, Context).Add(edge, Context);

        three.Add(edge, Context).Should().BeSameAs(three, "Everus → Baijini has three templates, all taken");
        three.Remove(edge).Picks.Select(p => p.Template).Should().Equal(Senior, JuniorMedium);
        three.Remove(edge).Remove(edge).Remove(edge).Picks.Should().BeEmpty();
        CustomPlan.Empty(Everus).Remove(edge).Picks.Should().BeEmpty();
    }

    [Fact]
    public void A_click_takes_the_best_paying_contract_that_fits_with_what_is_aboard_else_the_best_marked_too_big()
    {
        // Baijini first (the first click's end), then 400 SCU leave Everus for Port Tressler; at Baijini 200 SCU more fit
        // no Hull B, 96 do.
        var first = T([Everus], [Baijini], 10_000, size: HaulSize.Small, legs: [Leg("Gold", 1, 4, CrateSize.Four)]);
        var big = T([Everus], [Tressler], 300_000, legs: [Leg("Gold", 1, 400)]);
        var heavy = T([Baijini], [Tressler], 200_000, legs: [Leg("Gold", 1, 200)]);
        var light = T([Baijini], [Tressler], 100_000, legs: [Leg("Gold", 1, 96)]);
        var map = Map([first, big, heavy, light]);

        var plan = CustomPlan.Empty(Everus).Add(map.Edge(Everus, Baijini)!, Context).Add(map.Edge(Everus, Tressler)!, Context)
            .Add(map.Edge(Baijini, Tressler)!, Context);

        plan.Picks.Select(p => p.Template).Should().Equal(first, big, light);
        var crowded = plan.Add(map.Edge(Baijini, Tressler)!, Context).Evaluate(Context);
        crowded.Loop.Stations.Should().Equal(Baijini, Tressler);
        crowded.Roles.Select(r => (r.Role.Template, r.Fits)).Should().Equal((first, true), (big, true), (light, true), (heavy, false));
        crowded.Fits.Should().BeFalse();
    }

    [Fact]
    public void Its_route_keeps_where_each_contract_is_accepted_and_three_runs_measure_it_like_any_loop()
    {
        var map = Map(Leo);
        var loop = CustomPlan.Empty(Everus).Add(map.Edge(Everus, Baijini)!, Context).Add(map.Edge(Baijini, Tressler)!, Context)
            .Add(map.Edge(Tressler, Everus)!, Context).Evaluate(Context).Loop;
        var monday = new DateTime(2026, 11, 26, 10, 0, 0, DateTimeKind.Utc);

        var route = Route.From(loop, "Own plan from Everus Harbor", ObservedEdges.Empty, monday);

        route.Kind.Should().Be(LoopKind.Custom);
        route.Placeholders.Select(p => (p.Kind, p.AcceptAt)).Should().Equal((RecipeKind.Bulk, null), (RecipeKind.Direct, Baijini), (RecipeKind.Return, Tressler));
        RouteRun[] runs =
        [
            .. Enumerable.Range(1, 3).Select(day => new RouteRun(route.Name, monday.AddDays(day), null, monday.AddDays(day).AddMinutes(70), Everus,
                LoopKind.Custom, StarSystem.Stanton, loop.Landings, loop.Crates, loop.LongJumps, loop.Reward ?? 0, 3, 3, 0, HullB.Id, loop.Stations)),
        ];
        MeasuredTimes.For(runs, HullB.Id).Of(loop)!.IsEnough.Should().BeTrue("the same key: kind, system, start, stations");
    }

    [Fact]
    public void Without_a_pick_there_is_no_loop_to_start()
    {
        var result = CustomPlan.Empty(Everus).Evaluate(Context);

        (result.Loop.HasNumbers, result.Loop.Stations.Count, result.Roles.Count).Should().Be((false, 0, 0));
    }

    private static CustomContext Context => new(StarSystem.Stanton, Places, ObservedEdges.Empty, HullB, null);

    private static RouteMap Map(IReadOnlyList<HaulTemplate> templates)
    {
        var matrix = PortMatrix.Build(templates, RouteFilter.Everything(StarSystem.Stanton));
        return RouteMap.Build(matrix, MapNodes.For(Places.All.Select(p => p.Name), Places, StarSystem.Stanton), StarSystem.Stanton);
    }

    private static StarPosition At(string name, double xGm, double yGm) =>
        new(name, name, StarSystem.Stanton, "Manmade", null, xGm * 1e9, yGm * 1e9, 0, []);
}
