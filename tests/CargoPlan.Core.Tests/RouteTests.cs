using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The route of T13.6 (D125): a loop becomes a route with one placeholder per role; an accepted scan fills one. Templates
/// and loops built here (A10).
/// </summary>
public class RouteTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L2 = "HUR-L2 Faithful Dream Station";
    private const string L4 = "HUR-L4 Melodic Fields Station";
    private const string Ruin = "Ruin Station";
    private const string Checkmate = "Checkmate";

    private static readonly DateTime Monday = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    private static readonly HaulTemplate Bulk =
        T([], [L1, L2, L4], 325_250, shape: HaulShape.SingleToMulti, stops: 3,
            legs: [Leg("Hydrogen Fuel", 41, 72), Leg("Quantum Fuel", 30, 90), Leg("Ship Ammunition", 20, 60)], title: "Senior Rank - Medium Cargo Haul");

    private static readonly HaulTemplate Direct =
        T([], [L2], 34_750, rank: HaulRank.Rookie, size: HaulSize.Small, direct: true, legs: [Leg("Iron", 10, 40, CrateSize.Four)],
            title: "Rookie Rank - Direct Small Cargo Haul");

    private static readonly HaulTemplate Back =
        T([L1, L4], [], 72_500, rank: HaulRank.Junior, size: HaulSize.Small, shape: HaulShape.MultiToSingle, stops: 2,
            legs: [Leg("Waste", 5, 30, CrateSize.Four), Leg("Waste", 5, 30, CrateSize.Four)], title: "Junior Rank - Small Cargo Haul");

    private static readonly LoopCandidate Ring = new(
        LoopKind.LocalRing, StarSystem.Stanton, Everus, [L1, L4, L2],
        [
            new RecipeRole(RecipeKind.Bulk, Bulk, [L1, L2, L4], 222, 15),
            new RecipeRole(RecipeKind.Direct, Direct, [L2], 40, 10),
            new RecipeRole(RecipeKind.Return, Back, [Everus], 60, 15, L1, [L4]),
        ],
        432_500, false, 40, 2, 0, 0, 3);

    [Fact]
    public void A_loop_becomes_a_route_with_one_placeholder_per_role_in_the_loops_order()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);

        (route.Name, route.Hub, route.Kind, route.System, route.StartedUtc).Should().Be(("Everus ring", Everus, LoopKind.LocalRing, StarSystem.Stanton, Monday));
        route.Stations.Should().Equal(L1, L4, L2);
        route.Placeholders.Select(p => (p.Id, p.Kind, p.TemplateUuid)).Should().Equal(
            (1, RecipeKind.Bulk, Bulk.Uuid), (2, RecipeKind.Direct, Direct.Uuid), (3, RecipeKind.Return, Back.Uuid));
        route.Placeholders.Should().OnlyContain(p => p.FilledBy == null);
        (route.Extras.Count, route.DepartedUtc, route.At).Should().Be((0, null, 0));
    }

    [Fact]
    public void A_placeholder_keeps_what_the_recipes_line_shows_and_a_way_back_where_to_accept_and_collect()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        var bulk = route.Placeholders[0];
        var back = route.Placeholders[2];

        (bulk.Title, bulk.Reward, bulk.MinScu, bulk.MaxScu, bulk.Container).Should().Be(("Senior Rank - Medium Cargo Haul", 325_250L, 91, 222, CrateSize.Sixteen));
        bulk.Legs.Should().Equal(Bulk.Legs);
        bulk.Destinations.Should().Equal(L1, L2, L4);
        (bulk.AcceptAt, bulk.CollectAt).Should().Be((null, null));
        (back.AcceptAt, back.Destinations.Single()).Should().Be((L1, Everus));
        back.CollectAt.Should().Equal(L4);
    }

    [Fact]
    public void The_sums_are_the_known_rewards_out_and_back_apart()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);

        route.Outbound.Select(p => p.Id).Should().Equal(1, 2);
        route.Back.Select(p => p.Id).Should().Equal(3);
        (route.OutboundReward, route.BackReward).Should().Be((360_000L, 72_500L));
    }

    [Fact]
    public void A_Red_Wind_placeholder_has_the_cards_title_and_the_reward_a_scan_saw()
    {
        var member = T([Ruin], [Checkmate], null, contractor: HaulContractor.RedWind, rank: HaulRank.Member, size: HaulSize.Medium, system: StarSystem.Pyro,
            areaKey: Board.PyroSolar, faction: HaulFaction.Hh, legs: [Leg("Carbon", 1, 20, CrateSize.Four)], title: "Member Hauler Needed for Medium Shipment");
        var direct = member with { Uuid = "rw-direct", Direct = true, Size = HaulSize.Xs };
        var pyro = new LoopCandidate(LoopKind.LocalRing, StarSystem.Pyro, Ruin, [Checkmate],
            [new RecipeRole(RecipeKind.Bulk, member, [Checkmate], 20, 5), new RecipeRole(RecipeKind.Direct, direct, [Checkmate], 20, 5)],
            null, false, 10, 0, null, 0, 2);
        var key = new ObservationKey(HaulContractor.RedWind, HaulRank.Member, HaulSize.Medium, null, 532_500, ["Carbon"]);
        var places = new Positions([At(Ruin, 0), At(Checkmate, 5)]);
        var edges = ObservedEdges.From(
            new Observations([Observation.Of(new ObservedScan(key, null, [Ruin], [new ObservedDelivery(Checkmate, "Carbon", 18)], Monday))]), [member, direct], places);

        var before = Route.From(pyro, "Ruin Station ring", ObservedEdges.Empty, Monday);
        var after = Route.From(pyro, "Ruin Station ring", edges, Monday);

        before.Placeholders.Select(p => p.Title).Should().Equal("Member | Stellar Medium Haul", "Member | Stellar Direct Extra Small Haul");
        before.Placeholders.Should().OnlyContain(p => p.Reward == null, "Red Wind's data have no reward");
        after.Placeholders[0].Reward.Should().Be(532_500);
        (after.System, after.OutboundReward).Should().Be((StarSystem.Pyro, 532_500L));
    }

    private static StarPosition At(string name, double xGm) => new(name, name, StarSystem.Pyro, "Manmade", null, xGm * 1e9, 0, 0, []);
}
