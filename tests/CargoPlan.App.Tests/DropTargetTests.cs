using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// Rack 1 of the Hull A (4×2×2 cells, x 0..4, y 1..3, z 3..5), seen with the default camera along −(1, 1, 1). Points are
/// on the face the cursor shows, in ship coordinates. The rack stacks to port (D26): stack 0 is the layer at the spindle
/// (y 1..2), stack 1 the outer one; width 0 is the lower row (z 3..4); length 0 is the aft end.
/// </summary>
public class DropTargetTests
{
    private static readonly Rack Rack1 = ShipLoader.Load("hull_a").Racks[0];
    private static readonly IsoProjection Default = IsoProjection.Default;

    private static Crate At(CrateSize size, int length, int width, int stack) =>
        new("Riker", "Titanium", size, Rack1.Id, new GridPosition(length, width, stack));

    private static Drop? Resolve(IReadOnlyList<Crate> crates, double x, double y, double z) =>
        DropTarget.Resolve(Rack1, crates, Default, Default.Project(x, y, z));

    [Fact]
    public void A_free_cell_drops_down_its_column_to_the_plate()
    {
        // Port face of the outer cell at length 2, upper row: nothing between it and the spindle, so the crate goes
        // onto the plate.
        Resolve([], 2.5, 3, 4.5).Should().Be(new Drop("rack-0", new GridPosition(2, 1, 0)));
    }

    [Fact]
    public void A_free_cell_drops_down_its_column_onto_a_crate()
    {
        Resolve([At(CrateSize.One, 1, 1, 0)], 1.5, 3, 4.5).Should().Be(new Drop("rack-0", new GridPosition(1, 1, 1)));
    }

    [Fact]
    public void A_crate_under_the_cursor_takes_the_drop_on_top_of_it()
    {
        // Its top face: the top of the rack the camera sees, no free cell in front of it.
        Resolve([At(CrateSize.One, 1, 1, 0)], 1.5, 1.5, 5).Should().Be(new Drop("rack-0", new GridPosition(1, 1, 1)));
    }

    [Fact]
    public void On_top_of_a_crate_keeps_the_cell_under_the_cursor()
    {
        // A 4 SCU crate is 2×2 cells flat against the plate; the cursor on its aft upper cell puts the min corner there,
        // not at the crate's own min corner.
        Resolve([At(CrateSize.Four, 0, 0, 0)], 0.5, 1.5, 5).Should().Be(new Drop("rack-0", new GridPosition(0, 1, 1)));
    }

    [Fact]
    public void A_crate_seen_through_free_outlines_takes_the_drop()
    {
        // Like the hit test (D21): the line enters the free outer cell first, then the crate at the spindle behind it.
        Resolve([At(CrateSize.Four, 0, 0, 0)], 1.9, 3, 4.9).Should().Be(new Drop("rack-0", new GridPosition(0, 0, 1)));
    }

    [Fact]
    public void A_crate_reaching_the_top_of_the_grid_is_no_target()
    {
        Resolve([At(CrateSize.Eight, 0, 0, 0)], 0.6, 1.7, 5).Should().BeNull();
    }

    [Fact]
    public void Crates_left_out_are_air()
    {
        // The dragged crate isn't passed in: the same point now shows the free cell where it lay.
        Resolve([], 1.5, 1.5, 5).Should().Be(new Drop("rack-0", new GridPosition(1, 1, 0)));
    }

    [Fact]
    public void Off_the_rack_is_no_target()
    {
        DropTarget.Resolve(Rack1, [], Default, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void From_below_a_free_cell_still_drops_onto_the_plate()
    {
        // Seen from below, the first cell is in the lower row at the spindle; it lies on the plate already.
        var below = new IsoProjection(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);

        DropTarget.Resolve(Rack1, [], below, below.Project(2.5, 1.5, 3)).Should().Be(new Drop("rack-0", new GridPosition(2, 0, 0)));
    }
}
