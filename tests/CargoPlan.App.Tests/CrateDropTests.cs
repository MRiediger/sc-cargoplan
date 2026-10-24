using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>
/// Split and shared: Orison's two Gold crates on top in Rack 3 at [1,1,1] and [0,1,1], Riker's Quartz on the plate of
/// Rack 2 (aft half, both rows), its upper layer free there; Rack 1 full of Riker's Titanium.
/// </summary>
public class CrateDropTests
{
    private static Gesture.MoveCrates Drag(Crate crate, string rackId, GridPosition position) => new(crate, [crate], rackId, position);

    private static (Ship Ship, LoadPlan Plan, Crate Bow, Crate Aft) Gold()
    {
        var (ship, plan) = TestPlans.Load();
        return (ship, plan,
            plan.Crates.Single(c => c.Commodity == "Gold" && c.Position.Length == 1),
            plan.Crates.Single(c => c.Commodity == "Gold" && c.Position.Length == 0));
    }

    private static Rack RackOf(Ship ship, string id) => ship.Racks.Single(r => r.Id == id);

    [Fact]
    public void Moves_put_the_dragged_crate_at_the_target()
    {
        var (ship, _, gold, _) = Gold();

        CrateDrop.Moves(ship, Drag(gold, "rack-1", new GridPosition(0, 0, 1))).Should().Equal(new CrateMove(gold, "rack-1", new GridPosition(0, 0, 1)));
    }

    [Fact]
    public void A_drop_onto_Quartz_lands_there_and_its_ghost_is_the_one_change()
    {
        var (ship, plan, gold, _) = Gold();

        var outcome = CrateDrop.Outcome(plan, ship, Drag(gold, "rack-1", new GridPosition(0, 0, 1)));

        outcome.Stage.Should().Be(MoveStage.Gravity);
        CrateDrop.Changed(plan, outcome).Should().Equal((gold, gold with { RackId = "rack-1", Position = new GridPosition(0, 0, 1) }));
        CrateDrop.Text(outcome, RackOf(ship, "rack-1")).Should().BeNull();
    }

    [Fact]
    public void A_drop_onto_the_full_rack_makes_room_and_says_what_moved()
    {
        // The preview shows every crate that changes place and the notice as tool tip; the drop shows it as a notice (D65).
        var (ship, plan, gold, _) = Gold();

        var outcome = CrateDrop.Outcome(plan, ship, Drag(gold, "rack-0", new GridPosition(0, 0, 1)));

        outcome.Stage.Should().Be(MoveStage.Displace);
        CrateDrop.Changed(plan, outcome).Select(c => c.Before.Destination).Should().Contain(["Orison", "Riker"]);
        CrateDrop.Text(outcome, RackOf(ship, "rack-0")).Should().Be(string.Join(Environment.NewLine, outcome.Notices)).And.StartWith("Moved ");
    }

    [Fact]
    public void A_selection_keeps_its_offset_and_fits_onto_Quartz()
    {
        // Rack 3 to Rack 2: both Gold crates keep their row and their distance, one cell apart along the length.
        var (ship, plan, bow, aft) = Gold();
        Gesture.MoveCrates move = new(bow, [bow, aft], "rack-1", new GridPosition(1, 1, 1));

        CrateDrop.Moves(ship, move).Should().BeEquivalentTo([
            new CrateMove(bow, "rack-1", new GridPosition(1, 1, 1)),
            new CrateMove(aft, "rack-1", new GridPosition(0, 1, 1))]);
        CrateDrop.Outcome(plan, ship, move).Stage.Should().Be(MoveStage.Gravity);
    }

    [Fact]
    public void A_selection_reaching_past_every_rack_is_laid_into_the_target_rack()
    {
        var (ship, plan, bow, aft) = Gold();
        Gesture.MoveCrates move = new(aft, [bow, aft], "rack-1", new GridPosition(3, 1, 1));

        var outcome = CrateDrop.Outcome(plan, ship, move);

        CrateDrop.Moves(ship, move).Should().BeNull();
        outcome.Plan!.Crates.Where(c => c.Commodity == "Gold").Should().OnlyContain(c => c.RackId == "rack-1");
    }

    [Fact]
    public void A_preview_that_stops_before_a_new_layout_says_the_rack_will_be_rearranged()
    {
        var (ship, plan, gold, _) = Gold();

        var outcome = CrateDrop.Outcome(plan, ship, Drag(gold, "rack-0", new GridPosition(0, 0, 1)), MoveStage.FreeSpot);

        outcome.Stage.Should().Be(MoveStage.Pending);
        CrateDrop.Changed(plan, outcome).Should().BeEmpty();
        CrateDrop.Text(outcome, RackOf(ship, "rack-0")).Should().Be("Rack 1 will be rearranged");
    }

    // --- The preview key (E2): Core resolves again only when it changes ---

    [Fact]
    public void Equal_drags_have_equal_keys()
    {
        var (ship, _, bow, aft) = Gold();

        CrateDrop.KeyOf(ship, new(bow, [bow, aft], "rack-1", new GridPosition(1, 1, 1)))
            .Should().Be(CrateDrop.KeyOf(ship, new(bow, [bow, aft], "rack-1", new GridPosition(1, 1, 1))));
    }

    [Fact]
    public void Another_offset_at_the_same_cell_changes_the_key()
    {
        // Held by the other crate, the selection lands one cell further along the same target cell.
        var (ship, _, bow, aft) = Gold();

        CrateDrop.KeyOf(ship, new(aft, [bow, aft], "rack-1", new GridPosition(1, 1, 1)))
            .Should().NotBe(CrateDrop.KeyOf(ship, new(bow, [bow, aft], "rack-1", new GridPosition(1, 1, 1))));
    }

    [Fact]
    public void A_selection_reaching_past_every_rack_has_a_key_of_its_own()
    {
        var (ship, _, bow, aft) = Gold();
        var outside = CrateDrop.KeyOf(ship, new(aft, [bow, aft], "rack-1", new GridPosition(3, 1, 1)));

        outside.Moves.Should().BeNull();
        outside.Should().NotBe(CrateDrop.KeyOf(ship, new(aft, [bow, aft], "rack-1", new GridPosition(2, 1, 1))));
    }
}
