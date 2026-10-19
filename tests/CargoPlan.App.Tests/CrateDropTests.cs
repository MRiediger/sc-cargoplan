using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Tests;

/// <summary>Split and shared: Orison's two Gold crates on top in Rack 3, Riker's Quartz on the plate of Rack 2 (aft half).</summary>
public class CrateDropTests
{
    private static Gesture.MoveCrates Drag(Crate crate, string rackId, GridPosition position) => new(crate, [crate], rackId, position);

    [Fact]
    public void Moves_put_the_dragged_crate_at_the_target()
    {
        var (_, plan) = TestPlans.Load();
        var gold = plan.Crates.First(c => c.Commodity == "Gold");

        CrateDrop.Moves(Drag(gold, "rack-1", new GridPosition(0, 0, 1))).Should().Equal(new CrateMove(gold, "rack-1", new GridPosition(0, 0, 1)));
    }

    [Fact]
    public void A_drop_onto_Quartz_has_no_problem()
    {
        var (ship, plan) = TestPlans.Load();
        var gold = plan.Crates.First(c => c.Commodity == "Gold");

        CrateDrop.Problem(plan, ship, Drag(gold, "rack-1", new GridPosition(0, 0, 1))).Should().BeNull();
    }

    [Fact]
    public void A_drop_onto_a_taken_cell_names_the_reason()
    {
        // The preview shows it red with this text as tool tip; the drop shows it as a notice (D25).
        var (ship, plan) = TestPlans.Load();
        var gold = plan.Crates.First(c => c.Commodity == "Gold");

        CrateDrop.Problem(plan, ship, Drag(gold, "rack-0", new GridPosition(0, 0, 1))).Should().Be("Crates of Riker and Orison overlap in rack 1");
    }
}
