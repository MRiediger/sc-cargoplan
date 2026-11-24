using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.TemplateFixtures;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The recipe of a hub's board for a ship (T13.3b, D123, design §3): one bulk (a Medium, a second only when both take at
/// most half the ship), one direct, smalls by aUEC per crate, never a Large or Bulk size, at most ten contracts, each step
/// checked by the load planner, not by a sum; the way back per ring station as a list outside that check. Ships from
/// FrozenShips, templates built here (A10).
/// </summary>
public class RecipeTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L2 = "HUR-L2 Faithful Dream Station";
    private const string L3 = "HUR-L3 Thundering Express Station";

    private static readonly DateTime Monday = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Ship HullA = FrozenShips.Load("hull_a");
    private static readonly Ship HullB = FrozenShips.Load("hull_b");
    private static readonly Ship Freelancer = FrozenShips.Load("freelancer");

    // A local template of the Everus board: from the hub (an empty pickup pool, D121) to its drop-offs.
    private static HaulTemplate Local(
        HaulSize size, int maxScu, long reward, CrateSize crate = CrateSize.Sixteen, bool direct = false, string[]? to = null,
        HaulShape shape = HaulShape.AToB, int stops = 1, int legs = 1) =>
        T([], to ?? [L1], reward, size: size, direct: direct, shape: shape, stops: stops,
            legs: [.. Enumerable.Range(0, legs).Select(_ => Leg("Gold", 1, maxScu / legs, crate))]);

    private static Board Everus1(params HaulTemplate[] templates) => new(StarSystem.Stanton, "Stanton1", Everus, templates);

    private static Recipe For(Board board, Ship ship, ObservedEdges? edges = null) => Recipe.For(board, ship, edges ?? ObservedEdges.Empty);

    [Fact]
    public void A_board_without_a_hub_has_no_recipe()
    {
        var recipe = For(new Board(StarSystem.Pyro, "Solar", null, [Local(HaulSize.Small, 20, 50_000)]), HullB);

        recipe.Outbound.Should().BeEmpty();
        recipe.Returns.Should().BeEmpty();
        recipe.Note.Should().Be("Recipe: Stanton hubs only");
    }

    [Fact]
    public void Hull_B_takes_the_best_paying_medium_a_direct_and_the_smalls_never_a_large()
    {
        var bulk = Local(HaulSize.Medium, 300, 400_000);
        var lesserBulk = Local(HaulSize.Medium, 280, 350_000);
        var direct = Local(HaulSize.Small, 20, 60_000, CrateSize.Four, direct: true);
        var small = Local(HaulSize.Small, 24, 50_000, CrateSize.Four, to: [L3]);
        var xs = Local(HaulSize.Xs, 8, 10_000, CrateSize.One);
        var large = Local(HaulSize.Large, 500, 2_000_000, CrateSize.ThirtyTwo);

        var recipe = For(Everus1(large, xs, small, direct, lesserBulk, bulk), HullB);

        recipe.Outbound.Select(r => (r.Kind, r.Template)).Should().Equal(
            (RecipeKind.Bulk, bulk), (RecipeKind.Direct, direct), (RecipeKind.Small, small), (RecipeKind.Small, xs));
        recipe.Outbound.Single(r => r.Template == small).Should().Match<RecipeRole>(r => r.WorstScu == 24 && r.Crates == 6 && r.Destinations.SequenceEqual(new[] { L3 }));
        // 300 in 16s is 18 × 16 + 8 + 4: 20 crates; 20 in 4s 5, 24 in 4s 6, 8 in 1s 8.
        (recipe.RewardSum, recipe.WorstScu, recipe.Crates).Should().Be((520_000L, 352, 20 + 5 + 6 + 8));
    }

    [Theory]
    [InlineData(250, 240, true)]
    [InlineData(300, 200, false)]
    public void A_second_medium_only_when_both_take_at_most_half_the_ship(int first, int second, bool both)
    {
        var a = Local(HaulSize.Medium, first, 400_000);
        var b = Local(HaulSize.Medium, second, 300_000);

        var bulks = For(Everus1(a, b), HullB).Outbound.Where(r => r.Kind == RecipeKind.Bulk).Select(r => r.Template);

        bulks.Should().Equal(both ? [a, b] : [a]);
    }

    [Fact]
    public void Hull_A_takes_no_medium_that_overflows_it_and_the_smalls_that_fit()
    {
        var medium = Local(HaulSize.Medium, 144, 400_000);
        var small = Local(HaulSize.Small, 24, 50_000, CrateSize.Four);
        var another = Local(HaulSize.Small, 24, 40_000, CrateSize.Four, to: [L3]);
        var tooMany = Local(HaulSize.Small, 24, 30_000, CrateSize.Four, to: [L2]);

        var recipe = For(Everus1(medium, small, another, tooMany), HullA);

        recipe.Outbound.Select(r => r.Template).Should().Equal(small, another);
        recipe.WorstScu.Should().BeLessThanOrEqualTo(HullA.TotalScu);
    }

    [Fact]
    public void The_load_planner_decides_not_the_sum()
    {
        // The Freelancer's big rack (9×2×3) lays two 16s; three are 48 SCU of its 66 and still don't go in.
        var medium = Local(HaulSize.Medium, 48, 400_000);
        var small = Local(HaulSize.Small, 8, 20_000, CrateSize.Four, to: [L3]);

        var recipe = For(Everus1(medium, small), Freelancer);

        (medium.MaxScu + small.MaxScu).Should().BeLessThanOrEqualTo(Freelancer.TotalScu, "by the sum both would fit");
        recipe.Outbound.Select(r => r.Template).Should().Equal(small);
    }

    [Fact]
    public void A_system_wide_template_needs_an_observed_edge_from_the_hub()
    {
        var wide = T([], [], 80_000, size: HaulSize.Small, areaKey: Board.StantonWide, legs: [Leg("Gold", 1, 20, CrateSize.Four)]);
        var key = new ObservationKey(HaulContractor.Covalex, HaulRank.Senior, HaulSize.Small, false, 80_000, ["Gold"]);
        var seen = new Observations([Observation.Of(new ObservedScan(key, null, [Everus], [new ObservedDelivery(L3, "Gold", 20)], Monday))]);
        Positions places = new([.. new[] { Everus, L3 }.Select(n => new StarPosition(n, n, StarSystem.Stanton, "Manmade", null, 0, 0, 0, []))]);

        For(Everus1(wide), HullB).Outbound.Should().BeEmpty("the data name no place for it");
        For(Everus1(wide), HullB, ObservedEdges.From(seen, [wide], places)).Outbound
            .Should().ContainSingle().Which.Should().Match<RecipeRole>(r => r.Template == wide && r.Destinations.SequenceEqual(new[] { L3 }));
    }

    [Fact]
    public void A_multi_stop_role_goes_to_the_first_places_of_its_pool_legs_in_turn()
    {
        var multi = Local(HaulSize.Small, 24, 50_000, CrateSize.Four, to: [L3, L1, L2], shape: HaulShape.SingleToMulti, stops: 2, legs: 3);

        For(Everus1(multi), HullB).Outbound.Single().Destinations.Should().Equal(L1, L2);
    }

    [Fact]
    public void The_way_back_is_the_best_local_return_per_ring_station_outside_the_check()
    {
        var outbound = Local(HaulSize.Small, 24, 50_000, CrateSize.Four);
        var back = T([L1], [], 30_000, size: HaulSize.Small, legs: [Leg("Gold", 1, 20, CrateSize.Four)]);
        var betterBack = T([L1], [], 45_000, size: HaulSize.Small, legs: [Leg("Gold", 1, 20, CrateSize.Four)]);
        var offRing = T([L3], [], 90_000, size: HaulSize.Small, legs: [Leg("Gold", 1, 20, CrateSize.Four)]);

        var recipe = For(Everus1(outbound, back, betterBack, offRing), HullB);

        recipe.Outbound.Select(r => r.Template).Should().Equal(outbound);
        recipe.Returns.Should().ContainSingle().Which.Should().Match<RecipeRole>(r =>
            r.Kind == RecipeKind.Return && r.Template == betterBack && r.AcceptAt == L1 && r.Destinations.SequenceEqual(new[] { Everus }));
        recipe.WorstScu.Should().Be(24, "the way back is not in the departure's check");
    }

    [Fact]
    public void At_most_ten_contracts_leave_the_hub()
    {
        HaulTemplate[] tiny = [.. Enumerable.Range(0, 15).Select(i => Local(HaulSize.Xs, 1, 1_000 + i, CrateSize.One))];

        For(Everus1(tiny), HullB).Outbound.Should().HaveCount(Recipe.MaxContracts);
    }
}
