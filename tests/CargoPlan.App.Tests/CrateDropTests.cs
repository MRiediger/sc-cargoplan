using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

/// <summary>
/// Split and shared: Orison's two Gold crates on top in Rack 3 at [1,1,1] and [0,1,1], Riker's Quartz on the plate of
/// Rack 2 (aft half, both rows), its upper layer free there.
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

    [Fact]
    public void Moves_put_the_dragged_crate_at_the_target()
    {
        var (ship, _, gold, _) = Gold();

        CrateDrop.Moves(ship, Drag(gold, "rack-1", new GridPosition(0, 0, 1))).Should().Equal(new CrateMove(gold, "rack-1", new GridPosition(0, 0, 1)));
    }

    [Fact]
    public void A_drop_onto_Quartz_has_no_problem()
    {
        var (ship, plan, gold, _) = Gold();

        CrateDrop.Problem(plan, ship, Drag(gold, "rack-1", new GridPosition(0, 0, 1))).Should().BeNull();
    }

    [Fact]
    public void A_drop_onto_a_taken_cell_names_the_reason()
    {
        // The preview shows it red with this text as tool tip; the drop shows it as a notice (D25).
        var (ship, plan, gold, _) = Gold();

        CrateDrop.Problem(plan, ship, Drag(gold, "rack-0", new GridPosition(0, 0, 1))).Should().Be("Crates of Riker and Orison overlap in rack 1");
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
        CrateDrop.Problem(plan, ship, move).Should().BeNull();
    }

    [Fact]
    public void A_selection_with_a_crate_beyond_every_rack_is_refused_as_a_whole()
    {
        var (ship, plan, bow, aft) = Gold();
        Gesture.MoveCrates move = new(aft, [bow, aft], "rack-1", new GridPosition(3, 1, 1));

        CrateDrop.Moves(ship, move).Should().BeNull();
        CrateDrop.Problem(plan, ship, move).Should().Be(CrateDrop.OutsideRacks);
    }
}
