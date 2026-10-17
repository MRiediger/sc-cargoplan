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
        plan.Warnings.Should().Equal("Rack 0 shared by Riker, Shubin – identify crates by count");
    }

    [Fact]
    public void Plan_eight_stops_spread_evenly_two_per_rack()
    {
        var plan = LoadPlanner.Plan(HullA, Route(2, 2, 2, 2, 2, 2, 2, 2));

        StopsPerRack(plan).Should().BeEquivalentTo<string[]>(
            [["Riker", "Shubin"], ["Orison", "Lorville"], ["Area18", "Everus"], ["Baijini", "Tressler"]],
            o => o.WithStrictOrdering());
        plan.Warnings.Should().Equal(
            "Rack 0 shared by Riker, Shubin – identify crates by count",
            "Rack 1 shared by Orison, Lorville – identify crates by count",
            "Rack 2 shared by Area18, Everus – identify crates by count",
            "Rack 3 shared by Baijini, Tressler – identify crates by count");
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
    public void Plan_racks_front_to_back_hold_the_route_in_order(int[] scus)
    {
        var route = Route(scus);

        var plan = LoadPlanner.Plan(HullA, route);

        // Reading the racks front to back must give the route: shared racks hold only neighbours.
        StopsPerRack(plan).SelectMany(r => r).Should().Equal(route.Select(s => s.Destination));
    }

    // --- No valid plan ---

    [Fact]
    public void Plan_throws_when_a_stop_exceeds_rack_capacity()
    {
        var act = () => LoadPlanner.Plan(HullA, Route(20));

        act.Should().Throw<PlanException>().WithMessage("Rack capacity exceeded*Riker*");
    }

    [Fact]
    public void Plan_throws_when_stops_exceed_ship_capacity()
    {
        var act = () => LoadPlanner.Plan(HullA, Route(16, 16, 16, 16, 4));

        act.Should().Throw<PlanException>().WithMessage("Ship capacity exceeded*Area18*");
    }

    [Fact]
    public void Plan_throws_when_stops_fit_the_ship_but_not_the_racks_unsplit()
    {
        // 50 of 64 SCU, but five 10 SCU stops can't pair up in four 16 SCU racks.
        var act = () => LoadPlanner.Plan(HullA, Route(10, 10, 10, 10, 10, 4));

        act.Should().Throw<PlanException>().WithMessage("Rack capacity exceeded*Area18*");
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
