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

    /// <summary>One stop per SCU value, in route order, named from <see cref="Destinations"/>.</summary>
    private static IReadOnlyList<Stop> Route(params int[] scus) =>
        [.. scus.Select((scu, i) => StopOf(Destinations[i], i + 1, scu))];

    /// <summary>Destinations per Hull A rack, front to back.</summary>
    private static string[][] StopsPerRack(LoadPlan plan) =>
        [.. HullA.Racks.Select(r => plan.PlacementsForRack(r.Id).Select(p => p.Destination).Distinct().ToArray())];

    /// <summary>Drops a name that repeats its predecessor, so a stop split over neighbouring racks counts once.</summary>
    private static List<string> WithoutRepeats(IEnumerable<string> names)
    {
        List<string> result = [];
        foreach (var name in names)
        {
            if (result.Count == 0 || result[^1] != name)
            {
                result.Add(name);
            }
        }

        return result;
    }

    // --- Cases from TASKS.md ---

    [Fact]
    public void Plan_example_missions_puts_each_stop_in_its_own_rack_from_the_front()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Shubin"], ["Orison"], []], o => o.WithStrictOrdering());
        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Plan_two_8_scu_stops_and_one_16_scu_stop_uses_three_racks()
    {
        var plan = LoadPlanner.Plan(HullA, Route(8, 8, 16));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Shubin"], ["Orison"], []], o => o.WithStrictOrdering());
        plan.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void Plan_four_16_scu_stops_fills_all_racks_in_route_order()
    {
        var plan = LoadPlanner.Plan(HullA, Route(16, 16, 16, 16));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Shubin"], ["Orison"], ["Lorville"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().BeEmpty();
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
    public void Plan_orders_stops_by_order_not_by_list_position()
    {
        var plan = LoadPlanner.Plan(HullA, [StopOf("Orison", 3, 8), StopOf("Riker", 1, 8), StopOf("Shubin", 2, 8)]);

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Shubin"], ["Orison"], []], o => o.WithStrictOrdering());
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
    public void Plan_five_stops_share_the_front_rack_and_warn()
    {
        var plan = LoadPlanner.Plan(HullA, Route(4, 4, 4, 4, 4));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker", "Shubin"], ["Orison"], ["Lorville"], ["Area18"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal("Rack 1 shared by Riker, Shubin – identify crates by count");
    }

    [Fact]
    public void Plan_eight_stops_spread_evenly_two_per_rack()
    {
        var plan = LoadPlanner.Plan(HullA, Route(2, 2, 2, 2, 2, 2, 2, 2));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker", "Shubin"], ["Orison", "Lorville"], ["Area18", "Everus"], ["Baijini", "Tressler"]],
            o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(
            "Rack 1 shared by Riker, Shubin – identify crates by count",
            "Rack 2 shared by Orison, Lorville – identify crates by count",
            "Rack 3 shared by Area18, Everus – identify crates by count",
            "Rack 4 shared by Baijini, Tressler – identify crates by count");
    }

    [Fact]
    public void Plan_never_shares_a_rack_that_one_stop_fills()
    {
        var plan = LoadPlanner.Plan(HullA, Route(4, 16, 4, 4, 4, 4));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker"], ["Shubin"], ["Orison", "Lorville"], ["Area18", "Everus"]], o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(new[] { 4, 4, 4, 4, 4 })]
    [InlineData(new[] { 2, 2, 2, 2, 2, 2, 2, 2 })]
    [InlineData(new[] { 4, 16, 4, 4, 4, 4 })]
    [InlineData(new[] { 12, 10, 8 })]
    [InlineData(new[] { 20, 12, 16, 16 })]
    [InlineData(new[] { 10, 10, 10, 10, 10, 4 })]
    public void Plan_racks_front_to_back_hold_the_route_in_order(int[] scus)
    {
        var route = Route(scus);

        var plan = LoadPlanner.Plan(HullA, route);

        // Reading the racks front to back must give the route: shared racks hold only neighbours,
        // a split stop fills neighbouring racks.
        WithoutRepeats(StopsPerRack(plan).SelectMany(r => r)).Should().Equal(route.Select(s => s.Destination));
    }

    // --- Splitting a stop across racks ---

    [Fact]
    public void Plan_split_required_spans_two_neighbouring_racks()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);

        plan.Placements.Should().Equal(
            new Placement("Riker", "rack-0", new CargoItem("Titanium", 16, CrateSize.Four)),
            new Placement("Riker", "rack-1", new CargoItem("Titanium", 4, CrateSize.Four)));
        plan.Warnings.Should().Equal("Rack 1: Riker spans racks 1+2", "Rack 2: Riker spans racks 1+2");
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
            [["Riker"], ["Riker", "Shubin"], ["Orison"], ["Lorville"]], o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(
            "Rack 1: Riker spans racks 1+2",
            "Rack 2 shared by Riker, Shubin – identify crates by count",
            "Rack 2: Riker spans racks 1+2");
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
            "Rack 1: Riker spans racks 1+2+3",
            "Rack 2: Riker spans racks 1+2+3",
            "Rack 3: Riker spans racks 1+2+3");
    }

    // --- No valid plan ---

    [Fact]
    public void Plan_overflow_missions_throws_with_missing_scu_and_stop()
    {
        var act = () => LoadPlanner.Plan(HullA, MissionFixtures.OverflowMissions);

        act.Should().Throw<PlanException>().WithMessage("Ship capacity exceeded: 8 SCU missing, stop 'Orison'*");
    }

    [Fact]
    public void Plan_throws_when_stops_exceed_ship_capacity()
    {
        var act = () => LoadPlanner.Plan(HullA, Route(16, 16, 16, 16, 4));

        act.Should().Throw<PlanException>().WithMessage("Ship capacity exceeded: 4 SCU missing, stop 'Area18'*");
    }

    [Fact]
    public void Plan_throws_when_whole_crates_do_not_fit_behind_previous_stops()
    {
        // 60 of 64 SCU: no 8 SCU crate fits next to Riker, Shubin fills racks 1–3, Orison has no rack left.
        Stop[] route =
        [
            new("Riker", 1, [new CargoItem("Titanium", 10, CrateSize.Two)]),
            new("Shubin", 2, [new CargoItem("Quartz", 48, CrateSize.Eight)]),
            new("Orison", 3, [new CargoItem("Agricium", 2, CrateSize.One)]),
        ];

        var act = () => LoadPlanner.Plan(HullA, route);

        act.Should().Throw<PlanException>().WithMessage("Crates don't fit*Orison*");
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
