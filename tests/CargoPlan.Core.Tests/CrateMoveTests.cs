using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Hull A origins (D14): Rack 1 (0, 1, 3) port upper, Rack 2 (0, 1, 0) port lower, Rack 3 (0, −2, 3) starboard upper,
/// Rack 4 (0, −2, 0) starboard lower; every rack 4×2×2. Port racks stack to +y, starboard racks to −y (D26).
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
    public void Shift_keeps_a_stack_a_stack_from_port_to_starboard()
    {
        // One rack: grid offsets (D26). Port stacks to +y, starboard to −y; the upper crate stays outside the lower one.
        var anchor = At("rack-0", 0, 0, 0);
        var outer = At("rack-0", 0, 0, 1);

        CrateMove.Shift(HullA, [anchor, outer], anchor, "rack-2", new GridPosition(2, 0, 0)).Should().Equal(
            new CrateMove(anchor, "rack-2", new GridPosition(2, 0, 0)),
            new CrateMove(outer, "rack-2", new GridPosition(2, 0, 1)));
    }

    [Fact]
    public void Shift_keeps_the_offset_across_racks_in_ship_coordinates()
    {
        // Two racks: ship translation (D25). Both crates lie on the plate, port upper to starboard upper is two cells
        // to starboard, so the crate in port lower lands on the plate of starboard lower.
        var anchor = At("rack-0", 2, 0, 0);
        var below = At("rack-1", 0, 0, 0);

        CrateMove.Shift(HullA, [anchor, below], anchor, "rack-2", new GridPosition(2, 0, 0)).Should().Equal(
            new CrateMove(anchor, "rack-2", new GridPosition(2, 0, 0)),
            new CrateMove(below, "rack-3", new GridPosition(0, 0, 0)));
    }

    [Fact]
    public void Shift_across_racks_is_null_if_a_crate_lands_in_no_rack()
    {
        // Two cells towards the bow: the crate at the bow end of port lower would start beyond every rack.
        var anchor = At("rack-0", 0, 0, 0);
        var other = At("rack-1", 2, 0, 0);

        CrateMove.Shift(HullA, [anchor, other], anchor, "rack-0", new GridPosition(2, 0, 0)).Should().BeNull();
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
