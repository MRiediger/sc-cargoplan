using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class LoadPlannerTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static readonly string[] Destinations =
        ["Riker", "Shubin", "Orison", "Lorville", "Area18", "Everus", "Baijini", "Tressler"];

    private static Stop StopOf(string destination, int order, int scu) =>
        new(destination, order, [new CargoItem("Titanium", scu, CrateSize.Two)]);

    /// <summary>One stop per SCU value, in input order, named from <see cref="Destinations"/>.</summary>
    private static IReadOnlyList<Stop> Route(params int[] scus) =>
        [.. scus.Select((scu, i) => StopOf(Destinations[i], i + 1, scu))];

    /// <summary>Destinations per Hull A rack, in rack order.</summary>
    private static string[][] StopsPerRack(LoadPlan plan) =>
        [.. HullA.Racks.Select(r => plan.PlacementsForRack(r.Id).Select(p => p.Destination).Distinct().ToArray())];

    // --- Cases from TASKS.md ---

    [Fact]
    public void Plan_example_missions_puts_each_stop_in_its_own_rack_from_rack_1()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Shubin"], ["Orison"], []], o => o.WithStrictOrdering());
        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Plan_two_8_scu_stops_and_one_16_scu_stop_uses_three_racks_largest_first()
    {
        var plan = LoadPlanner.Plan(HullA, Route(8, 8, 16));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Orison"], ["Riker"], ["Shubin"], []], o => o.WithStrictOrdering());
        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Plan_four_16_scu_stops_fill_all_racks_by_name()
    {
        var plan = LoadPlanner.Plan(HullA, Route(16, 16, 16, 16));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Lorville"], ["Orison"], ["Riker"], ["Shubin"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().BeEmpty();
    }

    // --- Stacking: no crate on another destination's cargo where a layout avoids it (D64) ---

    private static Stop StopIn(string destination, int order, int scu, CrateSize size) =>
        new(destination, order, [new CargoItem("Titanium", scu, size)]);

    [Fact]
    public void Plan_hints_where_a_split_destination_and_another_must_stack()
    {
        // Full Hull A: Riker spills one 4 into rack 2, where Shubin's three 4s need six of eight columns: one sits on the other.
        var plan = LoadPlanner.Plan(HullA,
            [StopIn("Riker", 1, 20, CrateSize.Four), StopIn("Shubin", 2, 12, CrateSize.Four),
             StopIn("Orison", 3, 16, CrateSize.Sixteen), StopIn("Lorville", 4, 16, CrateSize.Sixteen)]);

        StopsPerRack(plan)[1].Should().BeEquivalentTo("Riker", "Shubin");
        plan.Warnings.Should().Contain(new PlanWarning("rack-1", "Rack 2: crate for Shubin sits on cargo for Riker"));
        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Plan_four_stops_of_12_and_one_of_16_in_8s_lays_out_without_problems()
    {
        // 4 SCU are left in every rack; Area18's 8s go where they fit.
        var plan = LoadPlanner.Plan(HullA,
            [StopIn("Riker", 1, 12, CrateSize.Four), StopIn("Shubin", 2, 12, CrateSize.Four), StopIn("Orison", 3, 12, CrateSize.Four),
             StopIn("Lorville", 4, 12, CrateSize.Four), StopIn("Area18", 5, 16, CrateSize.Eight)]);

        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Plan_hints_at_a_rack_where_destinations_must_stack_instead_of_failing()
    {
        // 61 of 64 SCU: three 16s take racks 1 to 3, the three others share rack 4, where 8 and 4 cover the plate and the
        // 1 can only lie on one of them. The cargo fits; stacking is a hint, not a refusal.
        var plan = LoadPlanner.Plan(HullA,
            [StopIn("Riker", 1, 8, CrateSize.Eight), StopIn("Shubin", 2, 4, CrateSize.Four), StopIn("Orison", 3, 1, CrateSize.One),
             StopIn("Lorville", 4, 16, CrateSize.Sixteen), StopIn("Area18", 5, 16, CrateSize.Sixteen), StopIn("Everus", 6, 16, CrateSize.Sixteen)]);

        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
        plan.Warnings.Should().Contain(new PlanWarning("rack-3", "Rack 4: crate for Orison sits on cargo for Shubin"));
    }

    [Fact]
    public void Plan_without_stops_returns_empty_plan()
    {
        var plan = LoadPlanner.Plan(HullA, []);

        plan.ShipId.Should().Be("hull_a");
        plan.Placements.Should().BeEmpty();
        plan.Warnings.Should().BeEmpty();
    }

    // --- Ordering and items ---

    [Fact]
    public void Plan_lists_stops_by_order_and_racks_them_by_size_and_name()
    {
        // The order is the legend's (D64); the racks don't follow it.
        var plan = LoadPlanner.Plan(HullA, [StopOf("Orison", 3, 8), StopOf("Riker", 1, 8), StopOf("Shubin", 2, 8)]);

        plan.Route.Should().Equal("Riker", "Shubin", "Orison");
        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Orison"], ["Riker"], ["Shubin"], []], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Plan_keeps_all_items_of_a_stop_in_one_rack()
    {
        CargoItem titanium = new("Titanium", 8, CrateSize.Four);
        CargoItem quartz = new("Quartz", 6, CrateSize.Two);

        var plan = LoadPlanner.Plan(HullA, [new Stop("Riker", 1, [titanium, quartz])]);

        plan.Placements.Should().Equal(
            new Placement("Riker", "rack-0", titanium),
            new Placement("Riker", "rack-0", quartz));
    }

    // --- Sharing when there are more stops than racks ---

    [Fact]
    public void Plan_five_stops_share_one_rack_and_warn()
    {
        // By name, each into an empty rack; the fifth joins the first rack. The hint names them in list order.
        var plan = LoadPlanner.Plan(HullA, Route(4, 4, 4, 4, 4));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Shubin", "Area18"], ["Lorville"], ["Orison"], ["Riker"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(new PlanWarning("rack-0", "Rack 1 shared by Shubin, Area18 – identify crates by count"));
    }

    [Fact]
    public void Plan_eight_stops_spread_evenly_two_per_rack()
    {
        var plan = LoadPlanner.Plan(HullA, Route(2, 2, 2, 2, 2, 2, 2, 2));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Orison", "Area18"], ["Riker", "Baijini"], ["Shubin", "Everus"], ["Lorville", "Tressler"]],
            o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Orison, Area18 – identify crates by count"),
            new PlanWarning("rack-1", "Rack 2 shared by Riker, Baijini – identify crates by count"),
            new PlanWarning("rack-2", "Rack 3 shared by Shubin, Everus – identify crates by count"),
            new PlanWarning("rack-3", "Rack 4 shared by Lorville, Tressler – identify crates by count"));
    }

    [Fact]
    public void Plan_never_shares_a_rack_that_one_stop_fills()
    {
        var plan = LoadPlanner.Plan(HullA, Route(4, 16, 4, 4, 4, 4));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Shubin"], ["Orison", "Area18"], ["Riker", "Everus"], ["Lorville"]], o => o.WithStrictOrdering());
    }

    // --- Splitting a stop across racks ---

    [Fact]
    public void Plan_split_required_spans_two_racks()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);

        plan.Placements.Should().Equal(
            new Placement("Riker", "rack-0", new CargoItem("Titanium", 16, CrateSize.Four)),
            new Placement("Riker", "rack-1", new CargoItem("Titanium", 4, CrateSize.Four)));
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"));
    }

    [Fact]
    public void Plan_splits_a_stop_at_an_item_boundary_when_possible()
    {
        CargoItem titanium = new("Titanium", 12, CrateSize.Four);
        CargoItem quartz = new("Quartz", 8, CrateSize.Two);

        var plan = LoadPlanner.Plan(HullA, [new Stop("Riker", 1, [titanium, quartz])]);

        // 16 + 4 would fill rack 0 but leave Quartz in two racks.
        plan.Placements.Should().Equal(
            new Placement("Riker", "rack-0", titanium),
            new Placement("Riker", "rack-1", quartz));
    }

    [Fact]
    public void Plan_split_stop_leaves_the_next_stop_its_own_rack()
    {
        var plan = LoadPlanner.Plan(HullA, Route(20, 12));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Riker"], ["Shubin"], []], o => o.WithStrictOrdering());
        plan.PlacementsForStop("Riker").Select(p => (p.RackId, p.Item.Scu)).Should().Equal(("rack-0", 16), ("rack-1", 4));
    }

    [Fact]
    public void Plan_split_stop_shares_its_last_rack_when_racks_run_out()
    {
        var plan = LoadPlanner.Plan(HullA, Route(20, 12, 16, 16));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Riker", "Shubin"], ["Lorville"], ["Orison"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Rack 2 shared by Riker, Shubin – identify crates by count"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"));
    }

    [Fact]
    public void Plan_splits_the_fewest_stops()
    {
        // 54 of 64 SCU, but five 10 SCU stops can't pair up in four 16 SCU racks: one split is enough.
        var plan = LoadPlanner.Plan(HullA, Route(10, 10, 10, 10, 10, 4));

        plan.Placements.GroupBy(p => p.Destination)
            .Count(stop => stop.Select(p => p.RackId).Distinct().Count() > 1)
            .Should().Be(1);
    }

    [Fact]
    public void Plan_stop_over_two_racks_counts_as_one_split()
    {
        var plan = LoadPlanner.Plan(HullA, Route(40, 8));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Riker"], ["Riker"], ["Shubin"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2+3"),
            new PlanWarning("rack-1", "Riker spans racks 1+2+3"),
            new PlanWarning("rack-2", "Riker spans racks 1+2+3"));
    }

    // --- No valid plan ---

    [Fact]
    public void Plan_overflow_missions_throws_with_missing_scu()
    {
        var act = () => LoadPlanner.Plan(HullA, MissionFixtures.OverflowMissions);

        // No stop is named: without a route none of them is the one that doesn't fit (D64).
        act.Should().Throw<PlanException>().WithMessage("Ship capacity exceeded: 8 SCU missing (stops need 72 SCU*");
    }

    [Fact]
    public void Plan_throws_when_stops_exceed_ship_capacity()
    {
        var act = () => LoadPlanner.Plan(HullA, Route(16, 16, 16, 16, 4));

        act.Should().Throw<PlanException>().WithMessage("Ship capacity exceeded: 4 SCU missing*");
    }

    [Fact]
    public void Plan_fits_crates_that_no_cut_of_a_route_fitted()
    {
        // 60 of 64 SCU. In route order Shubin's 8s followed Riker, and Orison found no rack (before D64); without a route
        // Shubin takes three racks and Riker and Orison share the fourth.
        Stop[] stops =
        [
            new("Riker", 1, [new CargoItem("Titanium", 10, CrateSize.Two)]),
            new("Shubin", 2, [new CargoItem("Quartz", 48, CrateSize.Eight)]),
            new("Orison", 3, [new CargoItem("Agricium", 2, CrateSize.One)]),
        ];

        var plan = LoadPlanner.Plan(HullA, stops);

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Shubin"], ["Shubin"], ["Shubin"], ["Riker", "Orison"]], o => o.WithStrictOrdering());
    }

    // --- Crate shape ---

    [Fact]
    public void Plan_16_scu_crate_fills_one_rack()
    {
        CargoItem titanium = new("Titanium", 16, CrateSize.Sixteen);

        var plan = LoadPlanner.Plan(HullA, [new Stop("Riker", 1, [titanium])]);

        plan.Placements.Should().Equal(new Placement("Riker", "rack-0", titanium));
        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Plan_throws_when_a_crate_fits_no_rack()
    {
        // 24 SCU crates are 6 cells long, Hull A plates only 4: the SCU sum alone would fit.
        Stop[] route =
        [
            new("Riker", 1, [new CargoItem("Titanium", 4, CrateSize.Four)]),
            new("Shubin", 2, [new CargoItem("Agricium", 24, CrateSize.TwentyFour)]),
        ];

        var act = () => LoadPlanner.Plan(HullA, route);

        act.Should().Throw<PlanException>()
            .WithMessage("24 SCU crate of Agricium for Shubin doesn't fit any rack of hull_a (largest rack takes 16 SCU).");
    }

    // --- Guards ---

    [Fact]
    public void Plan_rejects_duplicate_order()
    {
        var act = () => LoadPlanner.Plan(HullA, [StopOf("Riker", 1, 4), StopOf("Shubin", 1, 4)]);

        act.Should().Throw<ArgumentException>().WithParameterName("stops");
    }

    [Fact]
    public void Plan_rejects_duplicate_destination_ignoring_case()
    {
        var act = () => LoadPlanner.Plan(HullA, [StopOf("Riker", 1, 4), StopOf("riker", 2, 4)]);

        act.Should().Throw<ArgumentException>().WithParameterName("stops");
    }
}
