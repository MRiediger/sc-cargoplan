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
    /// The 8 alone in Rack 1 (largest first, D64), four 1s along the row next to the spindle in Rack 2: 12 SCU would fit one
    /// rack, the 8's shape and the 1s' row don't leave it a gap.
    /// </summary>
    private static readonly Stop[] GoldThenAgricium =
    [
        new Stop("Lorville", 1, [new CargoItem("Gold", 4, CrateSize.One)]),
        new Stop("Shubin", 2, [new CargoItem("Agricium", 8, CrateSize.Eight)]),
    ];

    private static Crate CrateAt(LoadPlan plan, string rackId, int length, int width, int stack) =>
        plan.Crates.Single(c => c.RackId == rackId && c.Position == new GridPosition(length, width, stack));

    /// <summary>Every plan the planner and its moves make is laid out correctly (D24).</summary>
    private static void ShouldBeValid(LoadPlan plan, Ship ship) => LoadPlanner.Validate(plan, ship).Should().BeEmpty();

    // --- MoveCrates ---
    // Example missions in Rack 1: Riker's three 4s at [2,0,0] and [0,0,0] on the plate, [2,0,1] on top (bow first);
    // Rack 4 stays empty. Titanium and Quartz in Rack 1: two 4s on the plate, the two 2s at [2,0,1] and [0,0,1].

    private static CrateMove Move(LoadPlan plan, string rackId, (int L, int W, int S) from, string targetRackId, (int L, int W, int S) to) =>
        new(CrateAt(plan, rackId, from.L, from.W, from.S), targetRackId, new GridPosition(to.L, to.W, to.S));

    [Fact]
    public void MoveCrates_moves_crates_that_carry_each_other_together()
    {
        // Alone, the crate on top would have nothing below it in Rack 4, and the one below would leave it floating in Rack 1.
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrates(plan, HullA,
        [
            Move(plan, "rack-0", (2, 0, 1), "rack-3", (0, 0, 1)),
            Move(plan, "rack-0", (2, 0, 0), "rack-3", (0, 0, 0)),
        ]);

        moved.CratesForRack("rack-3").Select(c => c.Position).Should().BeEquivalentTo([new GridPosition(0, 0, 0), new GridPosition(0, 0, 1)]);
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MoveCrates_lays_crates_that_cannot_keep_their_offsets_into_the_rack_and_leaves_the_original_unchanged()
    {
        // Three cells apart along the length, two 4s fit no 4×2×2 rack: Rack 4 is laid out anew with both (D65).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        Crate[] before = [.. plan.Crates];

        var moved = LoadPlanner.MoveCrates(plan, HullA,
        [
            Move(plan, "rack-0", (2, 0, 1), "rack-3", (0, 0, 1)),
            Move(plan, "rack-0", (2, 0, 0), "rack-3", (3, 0, 0)),
        ]);

        moved.CratesForRack("rack-3").Should().HaveCount(2);
        ShouldBeValid(moved, HullA);
        plan.Crates.Should().Equal(before);
    }

    [Fact]
    public void MoveCrates_keeps_crate_and_placement_order()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrates(plan, HullA, [Move(plan, "rack-0", (2, 0, 1), "rack-3", (0, 0, 0))]);

        moved.Crates.Select(c => c.Destination).Should().Equal(plan.Crates.Select(c => c.Destination));
        moved.Placements.Select(p => (p.Destination, p.RackId)).Should().Equal(
            ("Riker", "rack-0"), ("Riker", "rack-3"), ("Shubin", "rack-1"), ("Orison", "rack-2"));
    }

    [Fact]
    public void MoveCrates_into_an_occupied_rack_derives_the_hints_anew()
    {
        // Orison's two 1s from Rack 3 onto Riker's Quartz in Rack 2 (aft half, upper layer).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var moved = LoadPlanner.MoveCrates(plan, HullA,
        [
            Move(plan, "rack-2", (1, 0, 1), "rack-1", (0, 0, 1)),
            Move(plan, "rack-2", (0, 0, 1), "rack-1", (1, 0, 1)),
        ]);

        moved.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Rack 2 shared by Riker, Shubin, Orison – identify crates by count"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Orison spans racks 2+3"),
            new PlanWarning("rack-1", "Rack 2: crate for Orison sits on cargo for Riker"),
            new PlanWarning("rack-2", "Orison spans racks 2+3"));

        // A crate on other cargo is laid and hinted at, not refused or marked (D64).
        LoadPlanner.Validate(moved, HullA).Should().BeEmpty();
    }

    [Fact]
    public void MoveCrates_back_restores_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, TitaniumAndQuartz);

        var split = LoadPlanner.MoveCrates(plan, HullA,
        [
            Move(plan, "rack-0", (2, 0, 1), "rack-1", (2, 0, 0)),
            Move(plan, "rack-0", (0, 0, 1), "rack-1", (0, 0, 0)),
        ]);
        var back = LoadPlanner.MoveCrates(split, HullA,
        [
            Move(split, "rack-1", (2, 0, 0), "rack-0", (2, 0, 1)),
            Move(split, "rack-1", (0, 0, 0), "rack-0", (0, 0, 1)),
        ]);

        split.Warnings.Should().Equal(
            new PlanWarning("rack-0", "Riker spans racks 1+2"),
            new PlanWarning("rack-1", "Riker spans racks 1+2"));
        back.Crates.Should().Equal(plan.Crates);
        back.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void MoveCrates_without_moves_returns_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        LoadPlanner.MoveCrates(plan, HullA, []).Should().BeSameAs(plan);
    }

    [Fact]
    public void MoveCrates_rejects_the_same_crate_twice()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrates(plan, HullA,
        [
            Move(plan, "rack-0", (2, 0, 1), "rack-3", (0, 0, 0)),
            Move(plan, "rack-0", (2, 0, 1), "rack-3", (2, 0, 0)),
        ]);

        act.Should().Throw<ArgumentException>().WithParameterName("moves").WithMessage("*more than once*");
    }

    [Fact]
    public void MoveCrates_rejects_plan_for_other_ship()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrates(plan, SmallRackShip, [Move(plan, "rack-0", (2, 0, 1), "rack-0", (0, 0, 1))]);

        act.Should().Throw<ArgumentException>().WithParameterName("ship");
    }

    // --- ResolveMove: one result for preview and drop (D25, D65) ---

    [Fact]
    public void ResolveMove_keeps_a_valid_move_at_its_cell()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        LoadPlanner.ResolveMove(plan, HullA, [Move(plan, "rack-0", (2, 0, 1), "rack-3", (0, 0, 0))]).Stage.Should().Be(MoveStage.Gravity);
    }

    [Fact]
    public void ResolveMove_ignores_problems_the_plan_already_has()
    {
        // A plan file edited by hand: two crates overlap in Rack 1. Moving a third crate elsewhere doesn't touch them.
        LoadPlan plan = new(HullA.Id,
        [
            Box("Riker", CrateSize.Eight, 0, 0),
            Box("Shubin", CrateSize.Two, 0, 1),
            new Crate("Orison", "Gold", CrateSize.One, "rack-1", new GridPosition(0, 0, 0)),
        ], []);

        LoadPlanner.ResolveMove(plan, HullA, [new CrateMove(plan.Crates[2], "rack-3", new GridPosition(0, 0, 0))]).Stage.Should().Be(MoveStage.Gravity);
    }

    // --- MoveCrate: one crate, the wrapper of MoveCrates ---

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
    public void MoveCrate_onto_an_occupied_cell_takes_the_nearest_free_place()
    {
        // [0,0,0] holds Riker's other 4; on top of it is one cell away, the crate's own old place three (D65).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, [Move(plan, "rack-0", (2, 0, 1), "rack-0", (0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.FreeSpot);
        CrateAt(outcome.Plan!, "rack-0", 0, 0, 1).Destination.Should().Be("Riker");
    }

    [Fact]
    public void MoveCrate_sticking_out_of_the_grid_takes_the_nearest_place_inside()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(3, 0, 0));

        CrateAt(moved, "rack-3", 2, 0, 0).Destination.Should().Be("Riker");
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MoveCrate_of_a_crate_the_rack_cannot_hold_keeps_it_in_a_rack_that_can()
    {
        // Rack 2 is 2×2×2; no other rack than its own takes the 16, so it stays there, with a notice (D65).
        var plan = LoadPlanner.Plan(SmallRackShip, [new Stop("Riker", 1, [new CargoItem("Titanium", 16, CrateSize.Sixteen)])]);

        var outcome = LoadPlanner.ResolveMove(plan, SmallRackShip, [new CrateMove(plan.Crates[0], "rack-1", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Should().Equal(plan.Crates);
        outcome.Notices.Should().Equal("No room in rack 2 – placed in rack 1");
    }

    [Fact]
    public void MoveCrate_into_the_air_falls_onto_the_plate()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, [Move(plan, "rack-0", (2, 0, 1), "rack-3", (0, 0, 1))]);

        outcome.Stage.Should().Be(MoveStage.Gravity);
        CrateAt(outcome.Plan!, "rack-3", 0, 0, 0).Destination.Should().Be("Riker");
    }

    [Fact]
    public void MoveCrate_pulled_from_under_another_lets_the_upper_one_settle()
    {
        // The crate on top would float: it settles onto the plate where the moved one lay (D65).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 0), "rack-3", new GridPosition(0, 0, 0));

        moved.CratesForRack("rack-0").Select(c => c.Position).Should().BeEquivalentTo([new GridPosition(0, 0, 0), new GridPosition(2, 0, 0)]);
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void MoveCrate_rejects_a_crate_not_in_the_plan()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);
        Crate foreign = new("Lorville", "Gold", CrateSize.One, "rack-3", new GridPosition(0, 0, 0));

        var act = () => LoadPlanner.MoveCrate(plan, HullA, foreign, "rack-3", new GridPosition(1, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("moves");
    }

    [Fact]
    public void MoveCrate_rejects_a_delivered_crate()
    {
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions), HullA, "Riker", true);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("moves").WithMessage("*delivered*");
    }

    [Fact]
    public void MoveCrate_rejects_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var act = () => LoadPlanner.MoveCrate(plan, HullA, plan.Crates[0], "rack-9", new GridPosition(0, 0, 0));

        act.Should().Throw<ArgumentException>().WithParameterName("moves");
    }

    [Fact]
    public void MoveCrate_keeps_the_saved_plan_valid()
    {
        // PlanSaver saves SavedPlan(Missions, Plan); its constructor checks the crates per stop, commodity and size.
        var missions = MissionFixtures.LoadMissions("example_missions");
        var plan = LoadPlanner.Plan(HullA, missions.ToStops());

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-0", 2, 0, 1), "rack-3", new GridPosition(0, 0, 0));

        var act = () => SavedPlan.FromStops(missions, moved, CrateCap.Of(HullA));
        act.Should().NotThrow();
    }

    // --- MoveStop ---

    [Fact]
    public void MoveCrate_lays_a_crate_on_other_cargo_and_hints_at_it()
    {
        // Shubin's top 2 from Rack 2 onto Riker's free top in Rack 1: possible, only tedious at the elevator (D64).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.ExampleMissions);

        var moved = LoadPlanner.MoveCrate(plan, HullA, CrateAt(plan, "rack-1", 2, 0, 1), "rack-0", new GridPosition(0, 0, 1));

        moved.Warnings.Should().Contain(new PlanWarning("rack-0", "Rack 1: crate for Shubin sits on cargo for Riker"));
        LoadPlanner.Validate(moved, HullA).Should().BeEmpty();
    }

    // --- ResolveMoveToRack: a legend line or a mission's header dropped onto a rack (B6, D69) ---

    /// <summary>A destination's crates on board, as a legend line holds them (one mission per stop in these plans).</summary>
    private static Crate[] OnBoard(LoadPlan plan, string destination) => [.. plan.Crates.Where(c => c.Destination == destination && !c.Delivered)];

    private static LoadPlan ToRack(LoadPlan plan, string destination, string rackId) =>
        LoadPlanner.ResolveMoveToRack(plan, HullA, OnBoard(plan, destination), rackId).Plan!;

    [Fact]
    public void ToRack_lays_crates_on_other_cargo_and_hints_at_it()
    {
        // Riker's two 4s cover the plate of Rack 1; Shubin's 4 fits only on top of them.
        var plan = LoadPlanner.Plan(HullA,
            [new Stop("Riker", 1, [new CargoItem("Titanium", 8, CrateSize.Four)]), new Stop("Shubin", 2, [new CargoItem("Quartz", 4, CrateSize.Four)])]);

        var moved = ToRack(plan, "Shubin", "rack-0");

        moved.CratesForRack("rack-0").Should().ContainSingle(c => c.Destination == "Shubin" && c.Position.Stack == 1);
        moved.Warnings.Should().Contain(new PlanWarning("rack-0", "Rack 1: crate for Shubin sits on cargo for Riker"));
        LoadPlanner.Validate(moved, HullA).Should().BeEmpty();
    }

    [Fact]
    public void ToRack_gathers_a_split_stop_in_one_rack()
    {
        var plan = LoadPlanner.Plan(HullA, TitaniumAndQuartz);
        var split = LoadPlanner.MoveCrates(plan, HullA,
        [
            Move(plan, "rack-0", (2, 0, 1), "rack-1", (2, 0, 0)),
            Move(plan, "rack-0", (0, 0, 1), "rack-1", (0, 0, 0)),
        ]);

        var moved = ToRack(split, "Riker", "rack-2");

        moved.Placements.Select(p => (p.Item.Commodity, p.RackId)).Should().Equal(("Titanium", "rack-2"), ("Quartz", "rack-2"));
        moved.Warnings.Should().BeEmpty();
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void ToRack_lays_the_crates_out_as_a_fresh_plan_would()
    {
        var plan = LoadPlanner.Plan(HullA, TitaniumAndQuartz);

        // Rack 2 lies on the same side as Rack 1; a starboard rack would be its mirror image.
        var moved = ToRack(plan, "Riker", "rack-1");

        moved.Crates.Should().Equal(plan.Crates.Select(c => c with { RackId = "rack-1" }));
    }

    [Fact]
    public void ToRack_into_the_rack_that_holds_all_the_crates_changes_nothing()
    {
        // A drop of a legend line onto its own rack must not shuffle the crates there (D25).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        ToRack(plan, "Lorville", "rack-3").Should().BeSameAs(plan);
    }

    [Fact]
    public void ToRack_onto_a_full_rack_displaces_other_cargo()
    {
        // E2: stage 3b stays in the group run. Rack 1 is full of Riker's Titanium: two 4s go to Riker's Quartz in Rack 2,
        // where Shubin's 8 left room (D65).
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var outcome = LoadPlanner.ResolveMoveToRack(plan, HullA, OnBoard(plan, "Shubin"), "rack-0");

        outcome.Stage.Should().Be(MoveStage.Displace);
        outcome.Plan!.Crates.Single(c => c.Destination == "Shubin").RackId.Should().Be("rack-0");
        outcome.Plan.CratesForRack("rack-1").Should().OnlyContain(c => c.Destination == "Riker").And.HaveCount(4);
        outcome.Notices.Should().Equal("Moved 2 crates for Riker to rack 2 to make room");
        ShouldBeValid(outcome.Plan, HullA);
    }

    [Fact]
    public void ToRack_lays_crates_into_the_cells_of_delivered_cargo()
    {
        // Lorville's 1s are off the ship, their cells are free (D24 addendum).
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, GoldThenAgricium), HullA, "Lorville", true);

        var moved = ToRack(plan, "Shubin", "rack-1");

        moved.Crates.Single(c => c.Destination == "Shubin").RackId.Should().Be("rack-1");
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void ToRack_rejects_a_delivered_crate()
    {
        // A ticked line has no crates on board, so the legend never drags one; Core refuses it like a crate drag.
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared), HullA, "Riker", true);

        var act = () => LoadPlanner.ResolveMoveToRack(plan, HullA, [plan.Crates.First(c => c.Destination == "Riker")], "rack-3");

        act.Should().Throw<ArgumentException>().WithParameterName("crates");
    }

    [Fact]
    public void ToRack_rejects_unknown_rack()
    {
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);

        var act = () => LoadPlanner.ResolveMoveToRack(plan, HullA, OnBoard(plan, "Riker"), "rack-9");

        act.Should().Throw<ArgumentException>().WithParameterName("rackId");
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
    public void SetDelivered_refuses_to_tick_back_when_its_cells_are_taken_again()
    {
        var plan = LoadPlanner.SetDelivered(LoadPlanner.Plan(HullA, GoldThenAgricium), HullA, "Lorville", true);
        var reused = ToRack(plan, "Shubin", "rack-1");

        var act = () => LoadPlanner.SetDelivered(reused, HullA, "Lorville", false);

        act.Should().Throw<PlanException>().WithMessage("Lorville can't go back on board: *overlap*");
    }

    [Fact]
    public void SetDelivered_of_the_stop_below_leaves_the_crate_above_floating()
    {
        // The later stop was unloaded first: the player restacked in the game, the plan shows it red, never refuses.
        LoadPlan plan = new(HullA.Id, [Box("Riker", CrateSize.Four, 0, 1), Box("Shubin", CrateSize.Four, 0, 0)], []);

        var delivered = LoadPlanner.SetDelivered(plan, HullA, "Shubin", true);

        LoadPlanner.Validate(delivered, HullA).Should().Equal(
            new PlanWarning("rack-0", "4 SCU crate of Titanium for Riker in rack 1 has nothing below it"));
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

    // --- SetDelivered for one mission (B6, D69): the legend's line is mission + destination ---

    /// <summary>Riker by mission 1 and 2 side by side on the plate of Rack 1, Shubin by mission 1 on top.</summary>
    private static LoadPlan TwoMissionsToRiker(bool rikerTwoDelivered = false) => new(HullA.Id,
    [
        new Crate("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Mission: 1),
        new Crate("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(2, 0, 0), rikerTwoDelivered, Mission: 2),
        new Crate("Shubin", "Agricium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 1), Mission: 1),
    ], []);

    [Fact]
    public void SetDelivered_of_a_mission_ticks_only_its_crates_for_the_stop()
    {
        var plan = TwoMissionsToRiker();

        var delivered = LoadPlanner.SetDelivered(plan, HullA, "Riker", 2, true);

        delivered.Crates.Select(c => (c.Destination, c.Mission, c.Delivered)).Should().Equal(
            ("Riker", 1, false), ("Riker", 2, true), ("Shubin", 1, false));
    }

    [Fact]
    public void SetDelivered_of_a_mission_back_to_false_restores_the_plan()
    {
        var plan = TwoMissionsToRiker();

        var undone = LoadPlanner.SetDelivered(LoadPlanner.SetDelivered(plan, HullA, "Riker", 2, true), HullA, "Riker", 2, false);

        // The hand-built plan carries no hints; a tick derives them anew, so only the crates compare.
        undone.Crates.Should().Equal(plan.Crates);
    }

    [Fact]
    public void SetDelivered_of_a_mission_refuses_to_tick_back_when_its_cells_are_taken_again()
    {
        LoadPlan plan = new(HullA.Id,
        [
            new Crate("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Delivered: true, Mission: 2),
            new Crate("Shubin", "Agricium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0), Mission: 1),
        ], []);

        var act = () => LoadPlanner.SetDelivered(plan, HullA, "Riker", 2, false);

        act.Should().Throw<PlanException>().WithMessage("Riker can't go back on board: *overlap*");
    }

    [Fact]
    public void SetDelivered_of_a_mission_without_crates_for_the_stop_is_rejected()
    {
        var act = () => LoadPlanner.SetDelivered(TwoMissionsToRiker(), HullA, "Shubin", 2, true);

        act.Should().Throw<ArgumentException>().WithParameterName("mission").WithMessage("*mission 2*'Shubin'*");
    }

    [Fact]
    public void SetDelivered_of_the_whole_stop_still_ticks_every_mission()
    {
        // The ship view's click (D69): the station is the unit of unloading at the elevator.
        var delivered = LoadPlanner.SetDelivered(TwoMissionsToRiker(), HullA, "Riker", true);

        delivered.Crates.Where(c => c.Destination == "Riker").Should().AllSatisfy(c => c.Delivered.Should().BeTrue());
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
    public void Validate_ignores_delivered_crates()
    {
        // Delivered cargo is off the ship: no overlap, and it carries nothing (D24 addendum).
        LoadPlan plan = new(HullA.Id, [Box("Riker", CrateSize.Eight, 0, 0, delivered: true), Box("Shubin", CrateSize.Two, 0, 1)], []);

        LoadPlanner.Validate(plan, HullA).Should().ContainSingle().Which.Message.Should().Contain("nothing below it");
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
