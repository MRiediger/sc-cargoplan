using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Hull A origins (D14): Rack 1 (0, 1, 3) port upper, Rack 2 (0, 1, 0) port lower, Rack 3 (0, −2, 3) starboard upper,
/// Rack 4 (0, −2, 0) starboard lower; every rack 4×2×2.
/// </summary>
public class CrateMoveTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static Crate At(string rackId, int length, int width, int stack, CrateSize size = CrateSize.Two) =>
        new("Riker", "Titanium", size, rackId, new GridPosition(length, width, stack));

    [Fact]
    public void Shift_of_the_anchor_alone_puts_it_at_the_target()
    {
        var anchor = At("rack-0", 2, 0, 0);

        CrateMove.Shift(HullA, [anchor], anchor, "rack-3", new GridPosition(0, 1, 1))
            .Should().Equal(new CrateMove(anchor, "rack-3", new GridPosition(0, 1, 1)));
    }

    [Fact]
    public void Shift_keeps_the_offset_within_a_rack()
    {
        var anchor = At("rack-0", 2, 1, 0);
        var other = At("rack-0", 0, 0, 0);

        CrateMove.Shift(HullA, [anchor, other], anchor, "rack-3", new GridPosition(2, 1, 1)).Should().Equal(
            new CrateMove(anchor, "rack-3", new GridPosition(2, 1, 1)),
            new CrateMove(other, "rack-3", new GridPosition(0, 0, 1)));
    }

    [Fact]
    public void Shift_keeps_the_offset_across_racks_in_ship_coordinates()
    {
        // Port upper to starboard upper is three cells to starboard: the crate below in port lower lands in starboard lower.
        var anchor = At("rack-0", 2, 0, 1);
        var below = At("rack-1", 0, 0, 0);

        CrateMove.Shift(HullA, [anchor, below], anchor, "rack-2", new GridPosition(2, 0, 1)).Should().Equal(
            new CrateMove(anchor, "rack-2", new GridPosition(2, 0, 1)),
            new CrateMove(below, "rack-3", new GridPosition(0, 0, 0)));
    }

    [Fact]
    public void Shift_is_null_if_a_crate_lands_in_no_rack()
    {
        // Two cells towards the bow: the crate at the bow end would start beyond the rack.
        var anchor = At("rack-0", 0, 0, 0);
        var other = At("rack-0", 2, 0, 0);

        CrateMove.Shift(HullA, [anchor, other], anchor, "rack-0", new GridPosition(2, 0, 0)).Should().BeNull();
    }
}
