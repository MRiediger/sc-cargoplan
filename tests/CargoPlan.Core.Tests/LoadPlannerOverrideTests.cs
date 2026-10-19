using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
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
    public void MovePlacement_moves_to_target_rack_keeping_order()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        var shubin = Single(plan, "Shubin", "Quartz");

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

        act.Should().Throw<ArgumentException>().WithParameterName("moves");
    }

    [Fact]
    public void MovePlacement_rejects_plan_for_other_ship()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MovePlacement(plan, SmallRackShip, plan.Placements[0], "rack-0");

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    // --- MoveCrates ---

    /// <summary>One stop, 8 SCU in 2 SCU crates: the planner puts it into Rack 1.</summary>
    private static readonly Stop[] EightQuartz = [new Stop("Shubin", 1, [new CargoItem("Quartz", 8, CrateSize.Two)])];

    private static Placement Resized(Placement placement, int scu, string? rackId = null) =>
        placement with { RackId = rackId ?? placement.RackId, Item = new CargoItem(placement.Item.Commodity, scu, placement.Item.CrateSize) };

    /// <summary>D22: after a move every rack holds at most one placement per destination, commodity, crate size and state.</summary>
    private static void ShouldHaveNoTwins(LoadPlan plan) =>
        plan.Placements
            .GroupBy(p => (p.RackId, p.Destination, p.Item.Commodity, p.Item.CrateSize, p.Delivered))
            .Should().OnlyContain(g => g.Count() == 1);

    [Fact]
    public void MoveCrates_splits_a_placement()
    {
        var plan = LoadPlanner.Plan(HullA, EightQuartz);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(plan.Placements[0], 2)], "rack-1");

        moved.Placements.Should().Equal(
            Resized(plan.Placements[0], 6),
            Resized(plan.Placements[0], 2, "rack-1"));
        moved.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Shubin spans racks 1+2"),
            new PlanWarning("rack-1", "Shubin spans racks 1+2"));
        ShouldHaveNoTwins(moved);
    }

    [Fact]
    public void MoveCrates_puts_a_new_part_behind_the_cargo_of_the_target_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Riker", "Quartz"), 2)], "rack-3");

        moved.PlacementsForRack("rack-3").Select(p => p.Destination).Should().Equal("Lorville", "Riker");
        moved.Warnings.Should().Contain(new PlanWarning("rack-3", "Rack 4 shared by Lorville, Riker – identify crates by count"));
        ShouldHaveNoTwins(moved);
    }

    [Fact]
    public void MoveCrates_joins_cargo_of_the_same_kind_in_the_target_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(plan.Placements[0], 4)], "rack-1");

        // Riker 16 + 4 becomes 12 + 8: the part joins the placement already in Rack 2, at its place in the plan.
        moved.Placements.Should().Equal(
            Resized(plan.Placements[0], 12),
            Resized(plan.Placements[1], 8));
        ShouldHaveNoTwins(moved);
    }

    [Fact]
    public void MoveCrates_back_joins_into_one_placement_and_restores_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        var split = LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Riker", "Quartz"), 2)], "rack-3");
        var part = split.PlacementsForRack("rack-3").Single(p => p.Destination == "Riker");

        var back = LoadPlanner.MoveCrates(split, HullA, [(part, 2)], "rack-1");

        back.Placements.Should().Equal(plan.Placements);
        back.Warnings.Should().Equal(plan.Warnings);
    }

    [Fact]
    public void MoveCrates_all_crates_without_partner_keep_their_place_in_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        var quartz = Single(plan, "Riker", "Quartz");

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(quartz, 4)], "rack-3");

        moved.Placements.Should().Equal(plan.Placements.Select(p => p == quartz ? p with { RackId = "rack-3" } : p));
    }

    [Fact]
    public void MoveCrates_several_moves_into_one_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        var quartz = Single(plan, "Riker", "Quartz");
        var gold = Single(plan, "Orison", "Gold");

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(quartz, 2), (gold, 1)], "rack-3");

        moved.Placements.Should().Equal(
            plan.Placements[0],
            Resized(quartz, 2),
            plan.Placements[2],
            plan.Placements[3],
            Resized(gold, 1),
            plan.Placements[5],
            Resized(quartz, 2, "rack-3"),
            Resized(gold, 1, "rack-3"));
        ShouldHaveNoTwins(moved);
    }

    [Fact]
    public void MoveCrates_two_parts_of_a_split_into_a_third_rack_become_one_placement()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(plan.Placements[0], 16), (plan.Placements[1], 4)], "rack-2");

        moved.Placements.Should().Equal(Resized(plan.Placements[0], 20, "rack-2"));
        moved.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void MoveCrates_within_the_same_rack_changes_nothing()
    {
        var plan = LoadPlanner.Plan(HullA, EightQuartz);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(plan.Placements[0], 2)], "rack-0");

        moved.Placements.Should().Equal(plan.Placements);
    }

    [Fact]
    public void MoveCrates_joins_twins_from_the_planner_anywhere()
    {
        // Two equal mission rows give two equal placements in Rack 1.
        var plan = LoadPlanner.Plan(HullA,
        [
            new Stop("Riker", 1, [new CargoItem("Quartz", 4, CrateSize.Two), new CargoItem("Quartz", 4, CrateSize.Two)]),
            new Stop("Shubin", 2, [new CargoItem("Gold", 2, CrateSize.One)]),
        ]);
        plan.PlacementsForRack("rack-0").Should().HaveCount(2);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Shubin", "Gold"), 2)], "rack-2");

        moved.Placements.Should().Equal(
            new Placement("Riker", "rack-0", new CargoItem("Quartz", 8, CrateSize.Two)),
            new Placement("Shubin", "rack-2", new CargoItem("Gold", 2, CrateSize.One)));
    }

    [Fact]
    public void MoveCrates_takes_twins_as_two_sources()
    {
        var plan = LoadPlanner.Plan(HullA,
            [new Stop("Riker", 1, [new CargoItem("Quartz", 4, CrateSize.Two), new CargoItem("Quartz", 4, CrateSize.Two)])]);
        var twin = plan.Placements[0];

        var moved = LoadPlanner.MoveCrates(plan, HullA, [(twin, 4), (twin, 2)], "rack-1");

        // The first twin moves whole and keeps its place, the second one's part joins it.
        moved.Placements.Should().Equal(
            new Placement("Riker", "rack-1", new CargoItem("Quartz", 6, CrateSize.Two)),
            new Placement("Riker", "rack-0", new CargoItem("Quartz", 2, CrateSize.Two)));
    }

    [Fact]
    public void MoveCrates_leaves_original_plan_unchanged()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        Placement[] placementsBefore = [.. plan.Placements];
        PlanWarning[] warningsBefore = [.. plan.Warnings];

        LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Riker", "Quartz"), 2)], "rack-3");

        plan.Placements.Should().Equal(placementsBefore);
        plan.Warnings.Should().Equal(warningsBefore);
    }

    [Theory]
    [InlineData(3, "3 SCU of Quartz for Riker is not a whole number of its 2 SCU crates.*")]
    [InlineData(0, "0 SCU of Quartz for Riker is not a whole number of its 2 SCU crates.*")]
    [InlineData(6, "6 SCU of Quartz for Riker to move, but only 4 SCU are placed in 'rack-1'.*")]
    public void MoveCrates_rejects_scu_that_is_not_whole_crates_of_the_source(int scu, string message)
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Riker", "Quartz"), scu)], "rack-3");

        act.Should().Throw<ArgumentException>().WithParameterName("moves").WithMessage(message);
    }

    [Fact]
    public void MoveCrates_rejects_delivered_source()
    {
        // D16/D20: delivered crates are off the ship.
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared), HullA, "Riker", true);

        var act = () => LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Riker", "Quartz"), 2)], "rack-3");

        act.Should().Throw<ArgumentException>().WithParameterName("moves").WithMessage("*delivered*");
    }

    [Fact]
    public void MoveCrates_rejects_the_same_source_twice()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        var quartz = Single(plan, "Riker", "Quartz");

        var act = () => LoadPlanner.MoveCrates(plan, HullA, [(quartz, 2), (quartz, 2)], "rack-3");

        act.Should().Throw<ArgumentException>().WithParameterName("moves");
    }

    [Fact]
    public void MoveCrates_split_and_join_keep_the_saved_plan_valid()
    {
        var missions = MissionFixtures.SplitAndSharedMissions;
        var plan = LoadPlanner.Plan(HullA, missions.ToStops());
        var split = LoadPlanner.MoveCrates(plan, HullA, [(Single(plan, "Riker", "Quartz"), 2), (Single(plan, "Orison", "Gold"), 1)], "rack-3");
        var joined = LoadPlanner.MoveStop(split, HullA, "Riker", "rack-0");

        var act = () => new SavedPlan(missions, joined);

        act.Should().NotThrow();
        ShouldHaveNoTwins(joined);
    }

    // --- MoveStop ---

    [Fact]
    public void MoveStop_of_a_split_stop_becomes_one_placement()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitRequired);

        var moved = LoadPlanner.MoveStop(plan, HullA, "Riker", "rack-2");

        moved.Placements.Should().Equal(new Placement("Riker", "rack-2", new CargoItem("Titanium", 20, CrateSize.Four)));
        moved.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void MoveStop_moves_every_item_of_the_stop()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var moved = LoadPlanner.MoveStop(plan, HullA, "Orison", "rack-0");

        moved.PlacementsForStop("Orison").Select(p => p.RackId).Should().Equal("rack-0", "rack-0");
        moved.PlacementsForRack("rack-2").Should().BeEmpty();
        ShouldHaveNoTwins(moved);
    }

    [Fact]
    public void MoveStop_of_a_delivered_stop_moves_nothing()
    {
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared), HullA, "Riker", true);

        var moved = LoadPlanner.MoveStop(plan, HullA, "Riker", "rack-3");

        moved.Placements.Should().Equal(plan.Placements);
    }

    [Fact]
    public void MoveStop_rejects_unknown_destination()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.MoveStop(plan, HullA, "Lorvile", "rack-0");

        act.Should().Throw<ArgumentException>().WithParameterName("destination").WithMessage("*'Lorvile'*");
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
