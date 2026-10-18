using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class LoadPlannerOverrideTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    /// <summary>Rack 1 holds 16 SCU (4×2×2), Rack 2 only 8 SCU (2×2×2): a 16 SCU crate fits Rack 1 alone.</summary>
    private static readonly Ship SmallRackShip = new("small_rack", "Small rack test ship", 24,
    [
        new Rack("rack-0", 0, 16, new Dimensions(4, 2, 2), new RackPosition(RackSide.Port, RackLevel.Upper), new ShipPoint(0, 1, 3)),
        new Rack("rack-1", 1, 8, new Dimensions(2, 2, 2), new RackPosition(RackSide.Port, RackLevel.Lower), new ShipPoint(0, 1, 0)),
    ]);

    private static Placement Single(LoadPlan plan, string destination, string commodity) =>
        plan.Placements.Single(p => p.Destination == destination && p.Item.Commodity == commodity);

    // --- MovePlacement ---

    [Fact]
    public void MovePlacement_leaves_original_plan_unchanged()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        Placement[] placementsBefore = [.. plan.Placements];
        PlanWarning[] warningsBefore = [.. plan.Warnings];

        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Riker", "Quartz"), "rack-0");

        plan.Placements.Should().Equal(placementsBefore);
        plan.Warnings.Should().Equal(warningsBefore);
        moved.Should().NotBeSameAs(plan);
    }

    [Fact]
    public void MovePlacement_moves_to_target_rack_keeping_order_and_delivered()
    {
        var planned = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        var shubin = Single(planned, "Shubin", "Quartz") with { Delivered = true };
        var plan = planned with { Placements = [planned.Placements[0], shubin, planned.Placements[2]] };

        var moved = LoadPlanner.MovePlacement(plan, HullA, shubin, "rack-3");

        moved.Placements.Should().Equal(
            plan.Placements[0],
            shubin with { RackId = "rack-3" },
            plan.Placements[2]);
    }

    [Fact]
    public void MovePlacement_into_occupied_rack_warns_shared()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Orison", "Agricium"), "rack-0");

        moved.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Rack 1 shared by Riker, Orison – identify crates by count"));
    }

    [Fact]
    public void MovePlacement_joining_a_split_stop_drops_its_hints()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Riker", "Quartz"), "rack-0");

        // Riker no longer spans racks and Rack 2 holds only Shubin: stale hints would send the player to the wrong rack.
        moved.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void MovePlacement_part_of_split_stop_updates_spans()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);
        var part = plan.Placements.Single(p => p.RackId == "rack-1");

        var moved = LoadPlanner.MovePlacement(plan, HullA, part, "rack-3");

        moved.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+4"),
            new PlanWarning("rack-3", "Riker spans racks 1+4"));
    }

    [Fact]
    public void MovePlacement_rejects_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MovePlacement(plan, HullA, plan.Placements[0], "rack-9");

        act.Should().Throw<ArgumentException>().WithParameterName("targetRackId");
    }

    [Fact]
    public void MovePlacement_rejects_placement_not_in_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        Placement foreign = new("Lorville", "rack-3", new CargoItem("Gold", 2, CrateSize.One));

        var act = () => LoadPlanner.MovePlacement(plan, HullA, foreign, "rack-0");

        act.Should().Throw<ArgumentException>().WithParameterName("placement");
    }

    [Fact]
    public void MovePlacement_rejects_plan_for_other_ship()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MovePlacement(plan, SmallRackShip, plan.Placements[0], "rack-0");

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    // --- SetDelivered ---

    [Fact]
    public void SetDelivered_marks_every_placement_of_the_stop_and_keeps_order()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var delivered = LoadPlanner.SetDelivered(plan, HullA, "Riker", true);

        // Riker is split over Rack 1 and 2: both parts are unloaded in one visit.
        delivered.Placements.Should().Equal(plan.Placements.Select(p => p.Destination == "Riker" ? p with { Delivered = true } : p));
        delivered.PlacementsForStop("Riker").Select(p => p.RackId).Should().Equal("rack-0", "rack-1");
    }

    [Fact]
    public void SetDelivered_leaves_original_plan_unchanged()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        Placement[] placementsBefore = [.. plan.Placements];
        PlanWarning[] warningsBefore = [.. plan.Warnings];

        LoadPlanner.SetDelivered(plan, HullA, "Riker", true);

        plan.Placements.Should().Equal(placementsBefore);
        plan.Warnings.Should().Equal(warningsBefore);
    }

    [Fact]
    public void SetDelivered_back_to_false_restores_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var undone = LoadPlanner.SetDelivered(LoadPlanner.SetDelivered(plan, HullA, "Riker", true), HullA, "Riker", false);

        undone.Placements.Should().Equal(plan.Placements);
        undone.Warnings.Should().Equal(plan.Warnings);
    }

    [Fact]
    public void SetDelivered_drops_the_hints_of_the_delivered_stop()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var delivered = LoadPlanner.SetDelivered(plan, HullA, "Riker", true);

        // Rack 2 now holds only Shubin, and Riker's crates are gone: both hints would confuse the next unload.
        plan.Warnings.Should().NotBeEmpty();
        delivered.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void SetDelivered_keeps_the_hints_of_stops_still_on_board()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var delivered = LoadPlanner.SetDelivered(plan, HullA, "Shubin", true);

        delivered.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"));
    }

    [Fact]
    public void SetDelivered_rejects_unknown_destination()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.SetDelivered(plan, HullA, "Lorvile", true);

        act.Should().Throw<ArgumentException>().WithParameterName("destination").WithMessage("*'Lorvile'*");
    }

    [Fact]
    public void SetDelivered_rejects_plan_for_other_ship()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.SetDelivered(plan, SmallRackShip, "Riker", true);

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    // --- Validate ---

    [Theory]
    [InlineData("example_missions")]
    [InlineData("split_required")]
    [InlineData("split_and_shared")]
    public void Validate_planner_output_has_no_violations(string fixture)
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.Load(fixture));

        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Validate_overloaded_rack_warns_with_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Shubin", "Quartz"), "rack-0");

        LoadPlanner.Validate(moved, HullA).Should().Equal(
            new PlanWarning("rack-0", "Rack 1 overloaded: 22/16 SCU"));
    }

    [Fact]
    public void Validate_counts_delivered_cargo_towards_capacity()
    {
        // Until the lift takes the crates, they still take the room (D10, D16).
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions), HullA, "Riker", true);
        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Shubin", "Quartz"), "rack-0");

        LoadPlanner.Validate(moved, HullA).Should().Equal(
            new PlanWarning("rack-0", "Rack 1 overloaded: 22/16 SCU"));
    }

    [Fact]
    public void Validate_crate_too_large_for_rack_warns()
    {
        var plan = LoadPlanner.Plan(SmallRackShip, [new Stop("Riker", 1, [new CargoItem("Titanium", 16, CrateSize.Sixteen)])]);
        var moved = LoadPlanner.MovePlacement(plan, SmallRackShip, plan.Placements[0], "rack-1");

        LoadPlanner.Validate(moved, SmallRackShip).Should().Equal(
            new PlanWarning("rack-1", "Rack 2 overloaded: 16/8 SCU"),
            new PlanWarning("rack-1", "16 SCU crate of Titanium for Riker doesn't fit rack 2 (2×2×2)"));
    }

    [Fact]
    public void Validate_rejects_plan_for_other_ship()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.Validate(plan, SmallRackShip);

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    [Fact]
    public void Validate_rejects_placement_in_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        var broken = plan with { Placements = [plan.Placements[0] with { RackId = "rack-9" }] };

        var act = () => LoadPlanner.Validate(broken, HullA);

        act.Should().Throw<ArgumentException>().WithParameterName("plan");
    }
}
