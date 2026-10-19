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

    /// <summary>One stop, two items, 12 SCU in Rack 1: two 4s on the plate, the two 2s on top of them.</summary>
    private static readonly Stop[] TitaniumAndQuartz =
        [new Stop("Riker", 1, [new CargoItem("Titanium", 8, CrateSize.Four), new CargoItem("Quartz", 4, CrateSize.Two)])];

    /// <summary>
    /// Four 1s along the row next to the spindle in Rack 1, an 8 alone in Rack 2: 12 SCU would fit Rack 1, the 8's shape doesn't.
    /// </summary>
    private static readonly Stop[] GoldThenAgricium =
    [
        new Stop("Lorville", 1, [new CargoItem("Gold", 4, CrateSize.One)]),
        new Stop("Shubin", 2, [new CargoItem("Agricium", 8, CrateSize.Eight)]),
    ];

    private static Placement Single(LoadPlan plan, string destination, string commodity) =>
        plan.Placements.Single(p => p.Destination == destination && p.Item.Commodity == commodity);

    private static Crate CrateAt(LoadPlan plan, string rackId, int length, int width, int stack) =>
        plan.Crates.Single(c => c.RackId == rackId && c.Position == new GridPosition(length, width, stack));

    /// <summary>Every plan the planner and its moves make is laid out correctly (D24).</summary>
    private static void ShouldBeValid(LoadPlan plan, Ship ship) => LoadPlanner.Validate(plan, ship).Should().BeEmpty();

    // --- MovePlacement ---

    [Fact]
    public void MovePlacement_leaves_original_plan_unchanged()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        Crate[] cratesBefore = [.. plan.Crates];
        PlanWarning[] warningsBefore = [.. plan.Warnings];

        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Orison", "Gold"), "rack-1");

        plan.Crates.Should().Equal(cratesBefore);
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
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MovePlacement_lays_the_crates_out_around_the_cargo_of_the_target_rack()
    {
        // Rack 2 holds Riker's two 2s on the plate and Shubin's 8: Orison's two 1s go on top of the 2s.
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Orison", "Gold"), "rack-1");

        moved.Crates.Where(c => c.Commodity == "Gold").Select(c => (c.RackId, c.Position.Stack)).Should().Equal(("rack-1", 1), ("rack-1", 1));
        plan.CratesForRack("rack-1").Should().BeSubsetOf(moved.Crates);
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MovePlacement_into_occupied_rack_derives_the_hints_anew()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var moved = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Orison", "Gold"), "rack-1");

        moved.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Rack 2 shared by Riker, Shubin, Orison – identify crates by count"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Orison spans racks 2+3"),
            new PlanWarning("rack-2", "Orison spans racks 2+3"));
    }

    [Fact]
    public void MovePlacement_back_joins_the_split_and_restores_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, TitaniumAndQuartz);

        var split = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Riker", "Quartz"), "rack-1");
        var back = LoadPlanner.MovePlacement(split, HullA, Single(split, "Riker", "Quartz"), "rack-0");

        split.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"));
        back.Crates.Should().Equal(plan.Crates);
        back.Warnings.Should().BeEmpty();
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
    public void MovePlacement_refuses_a_full_rack()
    {
        // No overload any more (D24): the plan stays, the reason names the rack and what it holds.
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Riker", "Quartz"), "rack-0");

        act.Should().Throw<PlanException>().WithMessage("4 SCU Quartz for Riker doesn't fit rack 1: 16/16 SCU taken.");
    }

    [Fact]
    public void MovePlacement_refuses_a_crate_whose_shape_finds_no_room()
    {
        // 4 + 8 = 12 SCU, but the 1s take the row next to the spindle on the plate: no 2×2×2 place is left.
        var plan = LoadPlanner.Plan(HullA, GoldThenAgricium);

        var act = () => LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Shubin", "Agricium"), "rack-0");

        act.Should().Throw<PlanException>().WithMessage("8 SCU Agricium for Shubin doesn't fit rack 1: 4/16 SCU taken.");
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
    public void MovePlacement_rejects_delivered_placement()
    {
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions), HullA, "Shubin", true);

        var act = () => LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Shubin", "Quartz"), "rack-3");

        act.Should().Throw<ArgumentException>().WithParameterName("placement").WithMessage("*delivered*");
    }

    [Fact]
    public void MovePlacement_rejects_plan_for_other_ship()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MovePlacement(plan, SmallRackShip, plan.Placements[0], "rack-0");

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    // --- MoveCrate ---
    // Example missions in Rack 1: Riker's three 4s at [2,0,0] and [0,0,0] on the plate, [2,0,1] on top (bow first).

    [Fact]
    public void MoveCrate_puts_a_crate_at_a_free_supported_cell_of_another_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 0));

        CrateAt(moved, "rack-3", 0, 0, 0).Destination.Should().Be("Riker");
        moved.PlacementsForStop("Riker").Select(p => (p.RackId, p.Item.Scu)).Should().Equal(("rack-0", 8), ("rack-3", 4));
        moved.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+4"),
            new PlanWarning("rack-3", "Riker spans racks 1+4"));
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MoveCrate_within_a_rack_onto_another_crate()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-0", new GridPosition(0, 0, 1));

        CrateAt(moved, "rack-0", 0, 0, 1).Destination.Should().Be("Riker");
        moved.Placements.Should().Equal(plan.Placements);
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MoveCrate_leaves_original_plan_unchanged()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        Crate[] before = [.. plan.Crates];

        LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 0));

        plan.Crates.Should().Equal(before);
    }

    [Fact]
    public void MoveCrate_refuses_an_occupied_cell()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-0", new GridPosition(0, 0, 0));

        act.Should().Throw<PlanException>().WithMessage("4 SCU crate of Titanium for Riker would overlap cargo for Riker in rack 1.");
    }

    [Fact]
    public void MoveCrate_refuses_a_place_outside_the_grid()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(3, 0, 0));

        act.Should().Throw<PlanException>().WithMessage("4 SCU crate of Titanium for Riker doesn't fit rack 4 (4×2×2) at [3, 0, 0].");
    }

    [Fact]
    public void MoveCrate_refuses_a_crate_the_rack_cannot_hold()
    {
        var plan = LoadPlanner.Plan(SmallRackShip, [new Stop("Riker", 1, [new CargoItem("Titanium", 16, CrateSize.Sixteen)])]);

        var act = () => LoadPlanner.MoveCrate(plan, SmallRackShip, plan.Crates[0], "rack-1", new GridPosition(0, 0, 0));

        act.Should().Throw<PlanException>().WithMessage("*doesn't fit rack 2 (2×2×2)*");
    }

    [Fact]
    public void MoveCrate_refuses_a_crate_with_nothing_below_it()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 1));

        act.Should().Throw<PlanException>().WithMessage("4 SCU crate of Titanium for Riker would have nothing below it in rack 4.");
    }

    [Fact]
    public void MoveCrate_refuses_to_pull_a_crate_from_under_another()
    {
        // Nothing slides down (D24): the crate on top would float, so the move is refused.
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 0), "rack-3", new GridPosition(0, 0, 0));

        act.Should().Throw<PlanException>().WithMessage("*would leave the 4 SCU crate of Titanium for Riker with nothing below it.");
    }

    [Fact]
    public void MoveCrate_rejects_a_crate_not_in_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        Crate foreign = new("Lorville", "Gold", CrateSize.One, "rack-3", new GridPosition(0, 0, 0));

        var act = () => LoadPlanner.MoveCrate(plan, HullA, foreign, "rack-3", new GridPosition(1, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("crate");
    }

    [Fact]
    public void MoveCrate_rejects_a_delivered_crate()
    {
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions), HullA, "Riker", true);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("crate").WithMessage("*delivered*");
    }

    [Fact]
    public void MoveCrate_rejects_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, plan.Crates[0], "rack-9", new GridPosition(0, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("targetRackId");
    }

    [Fact]
    public void MoveCrate_keeps_the_saved_plan_valid()
    {
        // PlanSaver saves SavedPlan(Missions, Plan); its constructor checks the crates per stop, commodity and size.
        var missions = MissionFixtures.LoadMissions("example_missions");
        var plan = LoadPlanner.Plan(HullA, missions.ToStops());

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 0));

        var act = () => new SavedPlan(missions, moved);
        act.Should().NotThrow();
    }

    // --- MoveStop ---

    [Fact]
    public void MoveStop_gathers_a_split_stop_in_one_rack()
    {
        var plan = LoadPlanner.Plan(HullA, TitaniumAndQuartz);
        var split = LoadPlanner.MovePlacement(plan, HullA, Single(plan, "Riker", "Quartz"), "rack-1");

        var moved = LoadPlanner.MoveStop(split, HullA, "Riker", "rack-2");

        moved.Placements.Select(p => (p.Item.Commodity, p.RackId)).Should().Equal(("Titanium", "rack-2"), ("Quartz", "rack-2"));
        moved.Warnings.Should().BeEmpty();
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MoveStop_lays_the_stop_out_as_a_fresh_plan_would()
    {
        var plan = LoadPlanner.Plan(HullA, TitaniumAndQuartz);

        // Rack 2 lies on the same side as Rack 1; a starboard rack would be its mirror image.
        var moved = LoadPlanner.MoveStop(plan, HullA, "Riker", "rack-1");

        moved.Crates.Should().Equal(plan.Crates.Select(c => c with { RackId = "rack-1" }));
    }

    [Fact]
    public void MoveStop_refuses_a_rack_without_room()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.MoveStop(plan, HullA, "Shubin", "rack-0");

        act.Should().Throw<PlanException>().WithMessage("Shubin doesn't fit rack 1: 16/16 SCU taken.");
    }

    [Fact]
    public void MoveStop_counts_delivered_crates_as_taken()
    {
        // Lorville's 1s are off the ship but keep their cells, so ticking back gives a valid plan (D24).
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, GoldThenAgricium), HullA, "Lorville", true);

        var act = () => LoadPlanner.MoveStop(plan, HullA, "Shubin", "rack-0");

        act.Should().Throw<PlanException>();
    }

    [Fact]
    public void MoveStop_of_a_delivered_stop_moves_nothing()
    {
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared), HullA, "Riker", true);

        var moved = LoadPlanner.MoveStop(plan, HullA, "Riker", "rack-3");

        moved.Crates.Should().Equal(plan.Crates);
    }

    [Fact]
    public void MoveStop_rejects_unknown_destination()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.MoveStop(plan, HullA, "Lorvile", "rack-0");

        act.Should().Throw<ArgumentException>().WithParameterName("destination").WithMessage("*'Lorvile'*");
    }

    [Fact]
    public void MoveStop_rejects_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.MoveStop(plan, HullA, "Riker", "rack-9");

        act.Should().Throw<ArgumentException>().WithParameterName("targetRackId");
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

    [Fact]
    public void SetDelivered_keeps_every_crate_where_it_is()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var delivered = LoadPlanner.SetDelivered(plan, HullA, "Riker", true);

        delivered.Crates.Select(c => (c.RackId, c.Position)).Should().Equal(plan.Crates.Select(c => (c.RackId, c.Position)));
    }

    // --- Validate ---

    private static Crate Box(string destination, CrateSize size, int length, int stack, bool delivered = false) =>
        new(destination, "Titanium", size, "rack-0", new GridPosition(length, 0, stack), delivered);

    [Theory]
    [InlineData("example_missions")]
    [InlineData("split_required")]
    [InlineData("split_and_shared")]
    [InlineData("crate_mix")]
    public void Validate_planner_output_has_no_violations(string fixture)
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.Load(fixture));

        LoadPlanner.Validate(plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Validate_reports_overlapping_crates_per_rack()
    {
        // Only a plan file edited by hand gets here; the overlay shows the rack red (D24).
        LoadPlan plan = new(HullA.Id, [Box("Riker", CrateSize.Eight, 0, 0), Box("Shubin", CrateSize.Two, 0, 1)], []);

        LoadPlanner.Validate(plan, HullA).Should().Equal(
            new PlanWarning("rack-0", "Crates of Riker and Shubin overlap in rack 1"));
    }

    [Fact]
    public void Validate_reports_a_crate_with_nothing_below_it()
    {
        LoadPlan plan = new(HullA.Id, [Box("Riker", CrateSize.One, 0, 1)], []);

        LoadPlanner.Validate(plan, HullA).Should().Equal(
            new PlanWarning("rack-0", "1 SCU crate of Titanium for Riker in rack 1 has nothing below it"));
    }

    [Fact]
    public void Validate_counts_delivered_crates()
    {
        // Until the lift takes the crates, they still take the room (D16, D24).
        LoadPlan plan = new(HullA.Id, [Box("Riker", CrateSize.Eight, 0, 0, delivered: true), Box("Shubin", CrateSize.Two, 0, 1)], []);

        LoadPlanner.Validate(plan, HullA).Should().ContainSingle().Which.Message.Should().Contain("overlap");
    }

    [Fact]
    public void Validate_crate_too_large_for_rack_warns()
    {
        var plan = LoadPlanner.Plan(SmallRackShip, [new Stop("Riker", 1, [new CargoItem("Titanium", 16, CrateSize.Sixteen)])]);
        var moved = plan with { Crates = [plan.Crates[0] with { RackId = "rack-1" }] };

        LoadPlanner.Validate(moved, SmallRackShip).Should().Equal(
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
    public void Validate_rejects_crate_in_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        var broken = plan with { Crates = [plan.Crates[0] with { RackId = "rack-9" }] };

        var act = () => LoadPlanner.Validate(broken, HullA);

        act.Should().Throw<ArgumentException>().WithParameterName("plan");
    }
}
