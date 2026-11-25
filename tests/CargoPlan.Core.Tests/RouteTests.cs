using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
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

    // --- Start plan takes the cargo (gate 6a, Martin 25 Nov: option 1) ---

    private static readonly Positions Hurston = new([.. new[] { Everus, L1, L2, L4 }.Select(n => new StarPosition(n, n, StarSystem.Stanton, "Manmade", null, 0, 0, 0, []))]);

    // An accepted (or offered) contract in the cargo: one item per (destination, commodity, SCU), picked up at Everus.
    private static Mission Cargo(int no, long? reward, bool estimated = false, params (string To, string Commodity, int Scu)[] items) => new(
        no, [.. items.GroupBy(i => i.To).Select((g, i) => new MissionStop(g.Key, i + 1,
            [.. g.Select(x => new MissionItem(x.Commodity, x.Scu, CrateSize.Four, Pickup: [Everus]))]))],
        new ContractSource(new ScanIdentity(no, $"KEY-{no}", estimated), Reward: reward), Unplaced: true);

    private static SavedPlan PlanOf(params Mission[] missions)
    {
        var ship = FrozenShips.Load("hull_b");
        return new([.. missions], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship));
    }

    private static readonly Mission BulkCargo = Cargo(1, 325_250, false, (L1, "Hydrogen Fuel", 50), (L2, "Quantum Fuel", 60), (L4, "Ship Ammunition", 40));

    [Fact]
    public void Start_plan_fills_a_placeholder_with_the_cargo_of_its_template_and_makes_the_other_accepted_contracts_extras()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        var plan = PlanOf(
            BulkCargo,
            Cargo(2, 41_750, false, ("Teasa Spaceport", "Stims", 12)),
            Cargo(3, 34_750, estimated: true, (L2, "Iron", 20)),
            new Mission(4, [new MissionStop(L1, 1, [new MissionItem("Gold", 4, CrateSize.Four)])], Unplaced: true));

        var (started, adopted) = Route.Adopt(route, plan, [Bulk, Direct, Back], Hurston);

        started.Placeholders.Select(p => p.FilledBy).Should().Equal("KEY-1", null, null);
        started.Extras.Should().Equal(["KEY-2"], "accepted, no placeholder's; the offer #3 and the typed #4 are no contracts yet");
        adopted.Should().Equal(new Adoption(1, 1), new Adoption(2, null));
        started.FilledCount(new HashSet<string> { "KEY-1", "KEY-2", "KEY-3" }).Should().Be(1);
    }

    [Fact]
    public void A_contract_that_fits_two_placeholders_templates_fills_neither_and_is_an_extra_two_of_one_template_fill_in_turn()
    {
        var twin = Bulk with { Uuid = "bulk-twin" };
        var route = Route.From(Ring with { Contracts = [Ring.Contracts[0], Ring.Contracts[0] with { Template = twin }] }, "twins", ObservedEdges.Empty, Monday);
        var two = Route.From(Ring with { Contracts = [Ring.Contracts[0], Ring.Contracts[0]] }, "two mediums", ObservedEdges.Empty, Monday);
        var second = Cargo(2, 325_250, false, (L1, "Hydrogen Fuel", 70), (L2, "Quantum Fuel", 30), (L4, "Ship Ammunition", 20));

        var (unsure, _) = Route.Adopt(route, PlanOf(BulkCargo), [Bulk, twin], Hurston);
        var (both, _) = Route.Adopt(two, PlanOf(BulkCargo, second), [Bulk], Hurston);

        unsure.Placeholders.Should().OnlyContain(p => p.FilledBy == null);
        unsure.Extras.Should().Equal("KEY-1");
        both.Placeholders.Select(p => p.FilledBy).Should().Equal("KEY-1", "KEY-2");
    }

    [Fact]
    public void A_Covalex_contract_with_another_reward_is_no_placeholders_and_a_Red_Wind_contract_stays_open()
    {
        // Red Wind has no reward to check: rank and size come from the template alone, so the start leaves it to F3.
        var member = T([Ruin], [Checkmate], null, contractor: HaulContractor.RedWind, rank: HaulRank.Member, system: StarSystem.Pyro,
            areaKey: Board.PyroSolar, faction: HaulFaction.Hh, legs: [Leg("Carbon", 1, 200, CrateSize.Four)]);
        var pyro = new LoopCandidate(LoopKind.LocalRing, StarSystem.Pyro, Ruin, [Checkmate],
            [new RecipeRole(RecipeKind.Bulk, member, [Checkmate], 200, 50)], null, false, 50, 0, null, 0, 1);
        var redWindCargo = new Mission(1, [new MissionStop(Checkmate, 1, [new MissionItem("Carbon", 180, CrateSize.Four, Pickup: [Ruin])])],
            new ContractSource(new ScanIdentity(1, "RW-KEY", false), Reward: 532_500), Unplaced: true);
        var places = new Positions([At(Ruin, 0), At(Checkmate, 5)]);

        var (covalex, _) = Route.Adopt(Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday), PlanOf(BulkCargo with
        {
            Contract = BulkCargo.Contract! with { Reward = 300_000 },
        }), [Bulk, Direct, Back], Hurston);
        var (redWind, adopted) = Route.Adopt(Route.From(pyro, "Ruin Station ring", ObservedEdges.Empty, Monday), PlanOf(redWindCargo), [member], places);

        covalex.Placeholders[0].FilledBy.Should().BeNull();
        redWind.Placeholders.Single().FilledBy.Should().BeNull();
        (redWind.Extras.Single(), adopted.Single()).Should().Be(("RW-KEY", new Adoption(1, null)));
    }

    [Fact]
    public void Start_plan_leaves_what_the_route_holds_already_and_without_cargo_nothing()
    {
        var route = Route.From(Ring, "Everus ring", ObservedEdges.Empty, Monday);
        var (once, _) = Route.Adopt(route, PlanOf(BulkCargo, Cargo(2, 41_750, false, ("Teasa Spaceport", "Stims", 12))), [Bulk, Direct, Back], Hurston);

        var (twice, again) = Route.Adopt(once, PlanOf(BulkCargo, Cargo(2, 41_750, false, ("Teasa Spaceport", "Stims", 12))), [Bulk, Direct, Back], Hurston);
        var (empty, none) = Route.Adopt(route, null, [Bulk, Direct, Back], Hurston);

        (twice, again.Count).Should().Be((once, 0));
        (empty, none.Count).Should().Be((route, 0));
    }

    private static Route WithBulk(Route route, string key) =>
        route with { Placeholders = [route.Placeholders[0] with { FilledBy = key }, .. route.Placeholders.Skip(1)] };

    private static ScanIdentity Accepted(string key) => new(1, key, QuantitiesEstimated: false);

    private static HashSet<string> Keys(params string[] keys) => [.. keys];

    private static StarPosition At(string name, double xGm) => new(name, name, StarSystem.Pyro, "Manmade", null, xGm * 1e9, 0, 0, []);
}
