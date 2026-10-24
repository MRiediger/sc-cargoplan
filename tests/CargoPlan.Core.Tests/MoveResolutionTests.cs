using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Moves are accepted whenever the cargo fits (D65): gravity, a free place, a re-layout of the target rack, displacing
/// other cargo, another rack; refused only when the cargo fits nowhere. Hull A racks are 4×2×2 (length × width × stack).
/// </summary>
public class MoveResolutionTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static Crate C(string destination, CrateSize size, string rackId, int length, int width, int stack, string commodity = "Titanium") =>
        new(destination, commodity, size, rackId, new GridPosition(length, width, stack));

    private static LoadPlan PlanOf(params Crate[] crates) => new(HullA.Id, crates, []);

    private static void ShouldBeValid(LoadPlan plan, Ship ship) => LoadPlanner.Validate(plan, ship).Should().BeEmpty();

    [Fact]
    public void The_screenshot_drop_whose_footprint_hangs_over_a_gap_is_accepted()
    {
        // Rebuilt from the play test (24.10.2026): a 4 SCU crate for Green Glade dropped onto Faithful Dream's 2 in
        // Rack 2, half of its footprint over the gap next to Melodic Fields' 1. Before B3: "… has nothing below it".
        var greenGlade = C("Green Glade Station", CrateSize.Four, "rack-0", 0, 0, 0, "Pressurized Ice");
        var plan = PlanOf(
            greenGlade,
            C("Faithful Dream Station", CrateSize.Two, "rack-1", 0, 0, 0, "Processed Food"),
            C("Melodic Fields Station", CrateSize.One, "rack-1", 0, 1, 0, "Processed Food"));
        CrateMove drop = new(greenGlade, "rack-1", new GridPosition(0, 0, 1));

        LoadPlanner.MoveProblem(plan, HullA, [drop]).Should().BeNull();
        var moved = LoadPlanner.MoveCrates(plan, HullA, [drop]);

        // The only free, supported place of its shape in Rack 2: the bow half of the plate.
        moved.Crates[0].Should().Be(greenGlade with { RackId = "rack-1", Position = new GridPosition(2, 0, 0) });
        moved.Crates.Skip(1).Should().Equal(plan.Crates.Skip(1));
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void A_crate_pulled_from_under_another_lets_the_upper_one_settle()
    {
        var riker = C("Riker", CrateSize.Four, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Shubin", CrateSize.Two, "rack-0", 0, 0, 1));

        var moved = LoadPlanner.MoveCrate(plan, HullA, riker, "rack-3", new GridPosition(0, 0, 0));

        moved.Crates[1].Position.Should().Be(new GridPosition(0, 0, 0));
        ShouldBeValid(moved, HullA);
    }

    [Fact]
    public void A_drop_into_the_air_falls_to_the_plate_and_nothing_else_moves()
    {
        var riker = C("Riker", CrateSize.One, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Riker", CrateSize.Four, "rack-0", 2, 0, 0));

        var moved = LoadPlanner.MoveCrate(plan, HullA, riker, "rack-3", new GridPosition(1, 1, 1));

        moved.Crates[0].Position.Should().Be(new GridPosition(1, 1, 0));
        moved.Crates[1].Should().Be(plan.Crates[1]);
    }

    /// <summary>Two racks of 2×2×2 (8 SCU each): small enough that the free space can be there in SCU, not in shape.</summary>
    private static readonly Ship Tiny = new("tiny", "Tiny test ship", 16,
    [
        new Rack("rack-0", 0, 8, new Dimensions(2, 2, 2), null, new ShipPoint(0, 0, 0)),
        new Rack("rack-1", 1, 8, new Dimensions(2, 2, 2), null, new ShipPoint(0, 4, 0)),
    ]);

    private static MoveOutcome Resolve(LoadPlan plan, Crate crate, string rackId, int length, int width, int stack, MoveStage upTo = MoveStage.Refused) =>
        LoadPlanner.ResolveMove(plan, HullA, [new CrateMove(crate, rackId, new GridPosition(length, width, stack))], upTo);

    // --- Stage 1: gravity ---

    [Fact]
    public void Gravity_keeps_a_supported_drop_where_it_is()
    {
        var riker = C("Riker", CrateSize.One, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Riker", CrateSize.Four, "rack-0", 2, 0, 0));

        var outcome = Resolve(plan, riker, "rack-0", 2, 0, 1);

        outcome.Stage.Should().Be(MoveStage.Gravity);
        outcome.Plan!.Crates[0].Position.Should().Be(new GridPosition(2, 0, 1));
        outcome.Notices.Should().BeEmpty();
    }

    [Fact]
    public void Gravity_lets_a_drop_into_the_air_fall()
    {
        var riker = C("Riker", CrateSize.One, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Riker", CrateSize.Four, "rack-0", 2, 0, 0));

        var outcome = Resolve(plan, riker, "rack-3", 1, 1, 1);

        outcome.Stage.Should().Be(MoveStage.Gravity);
        outcome.Plan!.Crates.Should().Equal(riker with { RackId = "rack-3", Position = new GridPosition(1, 1, 0) }, plan.Crates[1]);
    }

    [Fact]
    public void Gravity_carries_a_crate_on_other_cargo_the_player_chose_with_a_hint()
    {
        // The player decides (D65): a crate on another destination's cargo is laid, the B2 hint says it.
        var riker = C("Riker", CrateSize.One, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Shubin", CrateSize.Four, "rack-3", 0, 0, 0));

        var outcome = Resolve(plan, riker, "rack-3", 0, 0, 1);

        outcome.Stage.Should().Be(MoveStage.Gravity);
        outcome.Plan!.Warnings.Should().Contain(new PlanWarning("rack-3", "Rack 4: crate for Riker sits on cargo for Shubin"));
        ShouldBeValid(outcome.Plan, HullA);
    }

    // --- Stage 2: the nearest free place ---

    [Fact]
    public void A_taken_cell_gives_the_nearest_free_place_next_to_own_cargo_and_nothing_else_moves()
    {
        // From [2,0,0], taken by Shubin: [1,0,0], [3,0,0], [2,1,0] and on top of Shubin are one cell away; [1,0,0]
        // lies next to Riker's own crate.
        var mover = C("Riker", CrateSize.One, "rack-0", 0, 0, 0);
        var plan = PlanOf(mover, C("Riker", CrateSize.One, "rack-3", 0, 0, 0), C("Shubin", CrateSize.One, "rack-3", 2, 0, 0));

        var outcome = Resolve(plan, mover, "rack-3", 2, 0, 0);

        outcome.Stage.Should().Be(MoveStage.FreeSpot);
        outcome.Plan!.Crates[0].Position.Should().Be(new GridPosition(1, 0, 0));
        outcome.Plan.Crates.Skip(1).Should().Equal(plan.Crates.Skip(1));
    }

    [Fact]
    public void A_crate_sticking_out_of_the_grid_takes_the_nearest_place_inside()
    {
        var riker = C("Riker", CrateSize.Four, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker);

        var outcome = Resolve(plan, riker, "rack-3", 3, 0, 0);

        outcome.Stage.Should().Be(MoveStage.FreeSpot);
        outcome.Plan!.Crates[0].Position.Should().Be(new GridPosition(2, 0, 0));
    }

    // --- Stage 3: the target rack laid out anew ---

    [Fact]
    public void A_rack_with_room_but_no_gap_of_the_shape_is_laid_out_anew()
    {
        // Two 1s leave no free 2×2 on the plate of Rack 4; 6 of 16 SCU, so a new layout takes the 4.
        var riker = C("Riker", CrateSize.Four, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Shubin", CrateSize.One, "rack-3", 0, 0, 0), C("Orison", CrateSize.One, "rack-3", 2, 1, 0));

        var outcome = Resolve(plan, riker, "rack-3", 1, 0, 0);

        outcome.Stage.Should().Be(MoveStage.Relayout);
        outcome.Plan!.Crates.Should().OnlyContain(c => c.RackId == "rack-3");
        outcome.Notices.Should().BeEmpty();
        ShouldBeValid(outcome.Plan, HullA);
    }

    [Fact]
    public void Pending_instead_of_a_new_layout_when_the_preview_stops_at_the_free_place()
    {
        var riker = C("Riker", CrateSize.Four, "rack-0", 0, 0, 0);
        var plan = PlanOf(riker, C("Shubin", CrateSize.One, "rack-3", 0, 0, 0), C("Orison", CrateSize.One, "rack-3", 2, 1, 0));

        var outcome = Resolve(plan, riker, "rack-3", 1, 0, 0, upTo: MoveStage.FreeSpot);

        outcome.Stage.Should().Be(MoveStage.Pending);
        outcome.Plan.Should().BeNull();
        outcome.Refusal.Should().BeNull();
    }

    // --- Stage 3b: displacing other cargo ---

    /// <summary>
    /// Rack 1: Riker's 8 and 4 (12 SCU), or four Riker 4s; Racks 2 and 3 full of Orison and Lorville; Rack 4 full of
    /// Shubin's two 8s. The topmost Riker 4 at length 2 is dragged onto Rack 4, so nothing settles behind it.
    /// </summary>
    private static (LoadPlan Plan, Crate Dragged) FullRackFour(CrateSize sourceFill)
    {
        Crate[] source = sourceFill == CrateSize.Eight
            ? [C("Riker", CrateSize.Eight, "rack-0", 0, 0, 0), C("Riker", CrateSize.Four, "rack-0", 2, 0, 0)]
            : [C("Riker", CrateSize.Four, "rack-0", 0, 0, 0), C("Riker", CrateSize.Four, "rack-0", 0, 0, 1), C("Riker", CrateSize.Four, "rack-0", 2, 0, 1), C("Riker", CrateSize.Four, "rack-0", 2, 0, 0)];
        var plan = PlanOf(
        [
            .. source,
            C("Orison", CrateSize.Eight, "rack-1", 0, 0, 0), C("Orison", CrateSize.Eight, "rack-1", 2, 0, 0),
            C("Lorville", CrateSize.Eight, "rack-2", 0, 0, 0), C("Lorville", CrateSize.Eight, "rack-2", 2, 0, 0),
            C("Shubin", CrateSize.Eight, "rack-3", 0, 0, 0), C("Shubin", CrateSize.Eight, "rack-3", 2, 0, 0),
        ]);
        return (plan, plan.Crates.Where(c => c.RackId == "rack-0" && c.Position.Length == 2).MaxBy(c => c.Position.Stack)!);
    }

    [Fact]
    public void A_full_rack_makes_room_by_displacing_other_cargo_into_the_cells_the_drag_left()
    {
        var (plan, dragged) = FullRackFour(CrateSize.Eight);

        var outcome = Resolve(plan, dragged, "rack-3", 0, 0, 0);

        outcome.Stage.Should().Be(MoveStage.Displace);
        outcome.Plan!.Crates.Single(c => c.Destination == "Riker" && c.CrateSize == CrateSize.Four).RackId.Should().Be("rack-3");
        outcome.Plan.Crates.Where(c => c.Destination == "Shubin").Select(c => c.RackId).Should().BeEquivalentTo(["rack-0", "rack-3"]);
        outcome.Notices.Should().Equal("Moved 1 crate for Shubin to rack 1 to make room");
        ShouldBeValid(outcome.Plan, HullA);
    }

    [Fact]
    public void Without_room_for_the_displaced_cargo_the_crate_goes_elsewhere_its_own_rack_last()
    {
        // Rack 1 is full of Riker's 4s: the cells the drag leaves take no 8, and Racks 2 and 3 are full.
        var (plan, dragged) = FullRackFour(CrateSize.Four);

        var outcome = Resolve(plan, dragged, "rack-3", 0, 0, 0);

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Should().Equal(plan.Crates);
        outcome.Notices.Should().Equal("No room in rack 4 – placed in rack 1");
    }

    // --- Stage 4: other racks; stage 5: refusal ---

    /// <summary>
    /// Tiny: Rack 1 holds Riker's 4 on the plate with Shubin's and Orison's 1s on top at opposite corners; Rack 2 holds
    /// Lorville's two 4s. Without the Riker 4 the two 1s fall to the plate: 6 cells free, no 2×2 gap.
    /// </summary>
    private static LoadPlan TinyCorners() => new(Tiny.Id,
    [
        new Crate("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(0, 0, 0)),
        new Crate("Shubin", "Titanium", CrateSize.One, "rack-0", new GridPosition(0, 0, 1)),
        new Crate("Orison", "Titanium", CrateSize.One, "rack-0", new GridPosition(1, 1, 1)),
        new Crate("Lorville", "Titanium", CrateSize.Four, "rack-1", new GridPosition(0, 0, 0)),
        new Crate("Lorville", "Titanium", CrateSize.Four, "rack-1", new GridPosition(0, 0, 1)),
    ], []);

    [Fact]
    public void A_crate_is_refused_only_when_no_rack_takes_its_shape_and_is_never_split()
    {
        // 6 SCU free for a 4 SCU crate, but in pieces: the crate stays one crate, the move is refused.
        var plan = TinyCorners();

        var outcome = LoadPlanner.ResolveMove(plan, Tiny, [new CrateMove(plan.Crates[0], "rack-1", new GridPosition(0, 0, 0))]);

        outcome.Stage.Should().Be(MoveStage.Refused);
        outcome.Plan.Should().BeNull();
        outcome.Refusal.Should().Be("no room for 4 SCU crate");
        var act = () => LoadPlanner.MoveCrate(plan, Tiny, plan.Crates[0], "rack-1", new GridPosition(0, 0, 0));
        act.Should().Throw<PlanException>().WithMessage("no room for 4 SCU crate");
    }

    [Fact]
    public void A_selection_is_refused_as_a_whole_naming_only_the_crates_without_room()
    {
        // Shubin's 1 would find a cell in Rack 1, Riker's 4 none: nothing moves (atomic, D65).
        var plan = TinyCorners();

        var outcome = LoadPlanner.ResolveMove(plan, Tiny, [plan.Crates[0], plan.Crates[1]], plan.Crates[0], "rack-1", new GridPosition(0, 0, 0));

        outcome.Stage.Should().Be(MoveStage.Refused);
        outcome.Refusal.Should().Be("no room for 4 SCU crate");
    }

    // --- Source rack ---

    [Fact]
    public void Crates_left_floating_by_a_selection_settle_in_their_rack()
    {
        Crate[] riker = [C("Riker", CrateSize.One, "rack-0", 0, 0, 0), C("Riker", CrateSize.One, "rack-0", 1, 0, 0)];
        var plan = PlanOf([.. riker, C("Shubin", CrateSize.Two, "rack-0", 0, 0, 1)]);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, riker, riker[0], "rack-3", new GridPosition(0, 0, 0));

        outcome.Stage.Should().Be(MoveStage.Gravity);
        outcome.Plan!.Crates.Select(c => (c.RackId, c.Position)).Should().Equal(
            ("rack-3", new GridPosition(0, 0, 0)), ("rack-3", new GridPosition(1, 0, 0)), ("rack-0", new GridPosition(0, 0, 0)));
    }

    // --- Selections ---

    [Fact]
    public void A_selection_falls_as_a_block_and_keeps_its_offsets()
    {
        Crate[] riker = [C("Riker", CrateSize.One, "rack-0", 0, 0, 0), C("Riker", CrateSize.One, "rack-0", 1, 0, 0)];
        var plan = PlanOf(riker);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, riker, riker[0], "rack-3", new GridPosition(2, 1, 1));

        outcome.Stage.Should().Be(MoveStage.Gravity);
        outcome.Plan!.Crates.Select(c => c.Position).Should().Equal(new GridPosition(2, 1, 0), new GridPosition(3, 1, 0));
    }

    [Fact]
    public void A_selection_moves_as_a_block_to_the_nearest_free_place()
    {
        // [3,1,0] is taken: one cell along the width and one along the length are equally near; the plate row at width 0
        // comes first in the fill order.
        Crate[] riker = [C("Riker", CrateSize.One, "rack-0", 0, 0, 0), C("Riker", CrateSize.One, "rack-0", 1, 0, 0)];
        var plan = PlanOf([.. riker, C("Shubin", CrateSize.One, "rack-3", 3, 1, 0)]);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, riker, riker[0], "rack-3", new GridPosition(2, 1, 0));

        outcome.Stage.Should().Be(MoveStage.FreeSpot);
        outcome.Plan!.Crates.Take(2).Select(c => c.Position).Should().Equal(new GridPosition(2, 0, 0), new GridPosition(3, 0, 0));
    }

    [Fact]
    public void A_selection_reaching_past_the_rack_is_laid_into_it_without_cells()
    {
        // Three cells apart along the length: the second crate would land at length 5 of a 4 long grid (Shift is null).
        Crate[] riker = [C("Riker", CrateSize.One, "rack-0", 0, 0, 0), C("Riker", CrateSize.One, "rack-0", 3, 0, 0)];
        var plan = PlanOf(riker);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, riker, riker[0], "rack-3", new GridPosition(2, 0, 0));

        outcome.Stage.Should().Be(MoveStage.FreeSpot);
        outcome.Plan!.Crates.Should().OnlyContain(c => c.RackId == "rack-3");
        ShouldBeValid(outcome.Plan, HullA);
    }

    [Fact]
    public void A_selection_too_large_for_the_rack_is_split_over_racks_crate_by_crate()
    {
        // Rack 4 holds 8 SCU of Riker already: two of the three 4s fit, the third goes to the first empty rack.
        Crate[] selected = [C("Riker", CrateSize.Four, "rack-0", 0, 0, 0), C("Riker", CrateSize.Four, "rack-0", 2, 0, 0), C("Riker", CrateSize.Four, "rack-0", 0, 0, 1)];
        var plan = PlanOf(
        [
            .. selected,
            C("Shubin", CrateSize.Four, "rack-0", 2, 0, 1),
            C("Riker", CrateSize.Four, "rack-3", 0, 0, 0), C("Riker", CrateSize.Four, "rack-3", 2, 0, 0),
        ]);

        var outcome = LoadPlanner.ResolveMove(plan, HullA, selected, selected[0], "rack-3", new GridPosition(0, 0, 1));

        outcome.Stage.Should().Be(MoveStage.Elsewhere);
        outcome.Plan!.Crates.Where(c => c.Destination == "Riker").GroupBy(c => c.RackId).ToDictionary(g => g.Key, g => g.Count())
            .Should().BeEquivalentTo(new Dictionary<string, int> { ["rack-3"] = 4, ["rack-1"] = 1 });
        outcome.Notices.Should().Equal("No room in rack 4 – placed in rack 2");
        outcome.Plan.Warnings.Should().Contain(new PlanWarning("rack-3", "Riker spans racks 2+4"));
        ShouldBeValid(outcome.Plan, HullA);
    }

    // --- Legend drag ---

    [Fact]
    public void A_destination_goes_whole_into_a_rack_with_room()
    {
        var plan = PlanOf(C("Riker", CrateSize.Four, "rack-0", 0, 0, 0), C("Riker", CrateSize.Four, "rack-0", 2, 0, 0));

        var outcome = LoadPlanner.ResolveStopMove(plan, HullA, "Riker", "rack-3");

        outcome.Stage.Should().Be(MoveStage.FreeSpot);
        outcome.Plan!.Crates.Should().OnlyContain(c => c.RackId == "rack-3");
    }

    [Fact]
    public void A_destination_dragged_onto_a_full_rack_displaces_other_cargo()
    {
        var plan = PlanOf(
            C("Riker", CrateSize.Four, "rack-0", 0, 0, 0), C("Riker", CrateSize.Four, "rack-0", 2, 0, 0),
            C("Shubin", CrateSize.Eight, "rack-3", 0, 0, 0), C("Shubin", CrateSize.Eight, "rack-3", 2, 0, 0));

        var outcome = LoadPlanner.ResolveStopMove(plan, HullA, "Riker", "rack-3");

        outcome.Stage.Should().Be(MoveStage.Displace);
        outcome.Plan!.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => c.RackId == "rack-3");
        outcome.Notices.Should().Equal("Moved 1 crate for Shubin to rack 1 to make room");
        ShouldBeValid(outcome.Plan, HullA);
    }

    [Fact]
    public void A_destination_larger_than_the_rack_fills_it_and_the_rest_stays()
    {
        // Riker's five 4s: four in Rack 1, one in Rack 2. Rack 3 holds Shubin's two 8s; Rack 4 is empty.
        var plan = PlanOf(
            C("Riker", CrateSize.Four, "rack-0", 0, 0, 0), C("Riker", CrateSize.Four, "rack-0", 2, 0, 0),
            C("Riker", CrateSize.Four, "rack-0", 0, 0, 1), C("Riker", CrateSize.Four, "rack-0", 2, 0, 1),
            C("Riker", CrateSize.Four, "rack-1", 0, 0, 0),
            C("Shubin", CrateSize.Eight, "rack-2", 0, 0, 0), C("Shubin", CrateSize.Eight, "rack-2", 2, 0, 0));

        var outcome = LoadPlanner.ResolveStopMove(plan, HullA, "Riker", "rack-2");

        outcome.Stage.Should().Be(MoveStage.Displace);
        outcome.Plan!.Crates.Where(c => c.Destination == "Riker").GroupBy(c => c.RackId).ToDictionary(g => g.Key, g => g.Count())
            .Should().BeEquivalentTo(new Dictionary<string, int> { ["rack-2"] = 4, ["rack-1"] = 1 });
        outcome.Notices.Should().Equal("Moved 2 crates for Shubin to rack 1 to make room", "No room in rack 3 – 1 crate stays in rack 2");
        ShouldBeValid(outcome.Plan, HullA);
    }

    [Fact]
    public void A_destination_with_no_crate_fitting_the_rack_is_refused()
    {
        var plan = TinyCorners();

        var outcome = LoadPlanner.ResolveStopMove(plan, Tiny, "Riker", "rack-1");

        outcome.Stage.Should().Be(MoveStage.Refused);
        outcome.Refusal.Should().Be("no room for 4 SCU crate");
    }

    // --- Preview and drop agree; the same gesture gives the same plan ---

    [Fact]
    public void The_preview_up_to_the_free_place_equals_the_drop_wherever_that_is_enough()
    {
        // Every cell of every rack as a target, for a 1 and a 4 of the split-and-shared plan.
        var plan = LoadPlanner.Plan(HullA, MissionFixtures.SplitAndShared);
        Crate[] dragged = [plan.Crates.First(c => c.CrateSize == CrateSize.One), plan.Crates.First(c => c.CrateSize == CrateSize.Four)];

        foreach (var crate in dragged)
        {
            foreach (var rack in HullA.Racks)
            {
                for (var l = 0; l < rack.Grid.Length; l++)
                {
                    for (var w = 0; w < rack.Grid.Width; w++)
                    {
                        for (var s = 0; s < rack.Grid.Stack; s++)
                        {
                            var drop = Resolve(plan, crate, rack.Id, l, w, s);
                            var preview = Resolve(plan, crate, rack.Id, l, w, s, upTo: MoveStage.FreeSpot);
                            if (drop.Stage <= MoveStage.FreeSpot)
                            {
                                preview.Stage.Should().Be(drop.Stage);
                                preview.Plan!.Crates.Should().Equal(drop.Plan!.Crates);
                            }
                            else
                            {
                                preview.Stage.Should().Be(MoveStage.Pending);
                            }
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public void The_same_gesture_on_the_same_plan_gives_the_same_plan()
    {
        var (plan, dragged) = FullRackFour(CrateSize.Eight);

        var first = Resolve(plan, dragged, "rack-3", 0, 0, 0);
        var second = Resolve(plan, dragged, "rack-3", 0, 0, 0);

        second.Plan!.Crates.Should().Equal(first.Plan!.Crates);
        second.Notices.Should().Equal(first.Notices);
    }
}
