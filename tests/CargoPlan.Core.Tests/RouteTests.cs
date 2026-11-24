using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
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

    // The fill matrix (T13.6): the view as the scan's estimate (offers and a view not told apart are estimates, D102),
    // what the scan did to the plan, and the state of the bulk placeholder.
    public enum View
    {
        Offer,
        Accepted,
        Unknown,
    }

    public enum BulkState
    {
        Open,
        FilledByThisScan,
        FilledByAnother,
        NoTemplate,
    }

    public static TheoryData<View, ScanChange, BulkState> Matrix()
    {
        var data = new TheoryData<View, ScanChange, BulkState>();
        foreach (var view in Enum.GetValues<View>())
        {
            foreach (var change in Enum.GetValues<ScanChange>())
            {
                foreach (var state in Enum.GetValues<BulkState>())
                {
                    data.Add(view, change, state);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void Only_an_accepted_scan_the_plan_took_fills_and_only_an_open_placeholder_of_its_template(View view, ScanChange change, BulkState state)
    {
        const string This = "THIS-KEY";
        const string Other = "OTHER-KEY";
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        route = state switch
        {
            BulkState.FilledByThisScan => WithBulk(route, This),
            BulkState.FilledByAnother => WithBulk(route, Other),
            _ => route,
        };
        MatchResult match = state == BulkState.NoTemplate ? new MatchResult.None("places outside the template's pools") : new MatchResult.Matched(Bulk);
        var scan = new ScanIdentity(3, This, QuantitiesEstimated: view != View.Accepted);

        var (after, outcome) = Route.Fill(route, match, scan, change, new HashSet<string> { This, Other });

        if (view != View.Accepted || change == ScanChange.Refused)
        {
            outcome.Should().BeOfType<FillOutcome.NotAccepted>();
            after.Should().BeSameAs(route);
            return;
        }

        switch (state)
        {
            case BulkState.Open:
                outcome.Should().Be(new FillOutcome.Filled(after.Placeholders[0]));
                after.Placeholders[0].FilledBy.Should().Be(This);
                break;
            case BulkState.FilledByThisScan:
                outcome.Should().Be(new FillOutcome.AlreadyFilled(route.Placeholders[0]));
                after.Should().BeSameAs(route);
                break;
            default:
                outcome.Should().BeOfType<FillOutcome.Extra>();
                after.Extras.Should().Equal(This);
                after.Placeholders.Should().Equal(route.Placeholders);
                break;
        }
    }

    [Fact]
    public void An_ambiguous_match_fills_the_first_open_placeholder_of_one_of_its_candidates()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        var twin = Direct with { Uuid = "twin" };

        var (after, outcome) = Route.Fill(route, new MatchResult.Ambiguous([twin, Direct]), Accepted("K"), ScanChange.Added, Keys("K"));

        outcome.Should().Be(new FillOutcome.Filled(after.Placeholders[1]));
        after.Placeholders[1].FilledBy.Should().Be("K");
    }

    [Fact]
    public void Two_placeholders_of_one_template_fill_one_after_the_other_then_the_next_is_an_extra()
    {
        var two = Ring with { Contracts = [Ring.Contracts[0], Ring.Contracts[0]] };
        var route = Route.From(two, "two mediums", ObservedEdges.Empty, Monday);
        var bulk = new MatchResult.Matched(Bulk);

        var (first, _) = Route.Fill(route, bulk, Accepted("A"), ScanChange.Added, Keys("A"));
        var (second, _) = Route.Fill(first, bulk, Accepted("B"), ScanChange.Added, Keys("A", "B"));
        var (third, outcome) = Route.Fill(second, bulk, Accepted("C"), ScanChange.Added, Keys("A", "B", "C"));

        second.Placeholders.Select(p => p.FilledBy).Should().Equal("A", "B");
        outcome.Should().BeOfType<FillOutcome.Extra>();
        third.Extras.Should().Equal("C");
    }

    [Fact]
    public void Filled_is_what_the_plan_says_a_key_gone_from_it_opens_the_placeholder_and_the_next_scan_fills_it()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        var (filled, _) = Route.Fill(route, new MatchResult.Matched(Bulk), Accepted("A"), ScanChange.Added, Keys("A"));

        filled.FilledCount(Keys("A")).Should().Be(1);
        Route.IsFilled(filled.Placeholders[0], Keys("A")).Should().BeTrue();
        Route.IsFilled(filled.Placeholders[0], Keys()).Should().BeFalse("undo took the mission out of the plan");
        filled.FilledCount(Keys()).Should().Be(0);

        var (again, outcome) = Route.Fill(filled, new MatchResult.Matched(Bulk), Accepted("B"), ScanChange.Added, Keys("B"));

        outcome.Should().BeOfType<FillOutcome.Filled>();
        again.Placeholders[0].FilledBy.Should().Be("B");
    }

    [Fact]
    public void An_extra_scanned_again_once_its_placeholder_is_open_moves_to_it()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        var bulk = new MatchResult.Matched(Bulk);
        var (one, _) = Route.Fill(route, bulk, Accepted("A"), ScanChange.Added, Keys("A"));
        var (extra, _) = Route.Fill(one, bulk, Accepted("B"), ScanChange.Added, Keys("A", "B"));

        var (again, alreadyExtra) = Route.Fill(extra, bulk, Accepted("B"), ScanChange.AlreadyInPlan, Keys("A", "B"));
        var (moved, outcome) = Route.Fill(extra, bulk, Accepted("B"), ScanChange.AlreadyInPlan, Keys("B"));

        alreadyExtra.Should().Be(new FillOutcome.AlreadyFilled(null));
        again.Should().BeSameAs(extra);
        outcome.Should().BeOfType<FillOutcome.Filled>();
        moved.Placeholders[0].FilledBy.Should().Be("B");
        moved.Extras.Should().BeEmpty();
    }

    [Fact]
    public void Without_a_match_the_contract_is_an_extra_and_an_offer_is_told_its_open_placeholder()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);

        var (after, outcome) = Route.Fill(route, null, Accepted("K"), ScanChange.Added, Keys("K"));

        outcome.Should().BeOfType<FillOutcome.Extra>();
        after.Extras.Should().Equal("K");
        route.OpenFor(new MatchResult.Matched(Direct), Keys())!.Kind.Should().Be(RecipeKind.Direct);
        route.OpenFor(new MatchResult.None("no"), Keys()).Should().BeNull();
    }

    private static Route WithBulk(Route route, string key) =>
        route with { Placeholders = [route.Placeholders[0] with { FilledBy = key }, .. route.Placeholders.Skip(1)] };

    private static ScanIdentity Accepted(string key) => new(1, key, QuantitiesEstimated: false);

    private static HashSet<string> Keys(params string[] keys) => [.. keys];

    private static StarPosition At(string name, double xGm) => new(name, name, StarSystem.Pyro, "Manmade", null, xGm * 1e9, 0, 0, []);
}
