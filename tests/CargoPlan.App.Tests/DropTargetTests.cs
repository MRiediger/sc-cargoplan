using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// Rack 1 of the Hull A (4×2×2 cells, x 0..4, y 1..3, z 3..5), seen with the default orientation along −(1, 1, 1) from a
/// camera far away, so the lines of sight are almost parallel and every point keeps its meaning from the orthographic
/// view (T8.4). Points are on the face the cursor shows, in ship coordinates. The rack stacks to port (D26): stack 0 is
/// the layer at the spindle (y 1..2), stack 1 the outer one; width 0 is the lower row (z 3..4); length 0 is the aft end.
/// </summary>
public class DropTargetTests
{
    private static readonly Rack Rack1 = ShipLoader.Load("hull_a").Racks[0];
    private static readonly CameraOrientation Default = CameraOrientation.Default;
    private static readonly Size View = new(400, 300);

    private static Crate At(CrateSize size, int length, int width, int stack) =>
        new("Riker", "Titanium", size, Rack1.Id, new GridPosition(length, width, stack));

    private static CameraMath Far(CameraOrientation orientation) => new(orientation, 200, ScreenPoints.Centre(Rack1.Bounds), View);

    /// <summary>The line of sight to (x, y, z) from <see cref="Far"/>.</summary>
    private static Ray Sight(double x, double y, double z, CameraOrientation? orientation = null)
    {
        var camera = Far(orientation ?? Default);
        return camera.Unproject(camera.Project(new Point3D(x, y, z)));
    }

    private static Drop? Resolve(IReadOnlyList<Crate> crates, double x, double y, double z) =>
        DropTarget.Resolve(Rack1, crates, Sight(x, y, z));

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
    public void Delivered_cargo_is_air()
    {
        // Off the ship, its cells free (D24 addendum): the drop falls through it onto the plate.
        Resolve([At(CrateSize.One, 1, 1, 0) with { Delivered = true }], 1.5, 1.5, 5).Should().Be(new Drop("rack-0", new GridPosition(1, 1, 0)));
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
        DropTarget.Resolve(Rack1, [], Far(Default).Unproject(new Point(10, 10))).Should().BeNull();
    }

    [Fact]
    public void From_below_a_free_cell_still_drops_onto_the_plate()
    {
        // Seen from below, the first cell is in the lower row at the spindle; it lies on the plate already.
        var below = new CameraOrientation(CameraOrientation.DefaultYaw, -CameraOrientation.DefaultPitch);

        DropTarget.Resolve(Rack1, [], Sight(2.5, 1.5, 3, below)).Should().Be(new Drop("rack-0", new GridPosition(2, 0, 0)));
    }

    // --- Perspective (T8.4): the column falls in the grid, whatever the line of sight ---

    [Fact]
    public void Up_close_a_free_cell_still_drops_down_its_column()
    {
        // Four cells from the rack the lines of sight fan out; the cell the cursor shows decides, the fall is the grid's.
        var camera = new CameraMath(Default, 4, ScreenPoints.Centre(Rack1.Bounds), View);

        DropTarget.Resolve(Rack1, [At(CrateSize.One, 1, 1, 0)], camera.Unproject(camera.Project(new Point3D(1.5, 3, 4.5))))
            .Should().Be(new Drop("rack-0", new GridPosition(1, 1, 1)));
    }

    [Fact]
    public void Cells_behind_the_camera_are_not_seen()
    {
        // Zoomed into the outer cell at length 1, upper row, looking to the bow (+x): a crate in the cell behind, at
        // length 0, would take the drop on top if it counted, and that column is full. The cell around the camera counts
        // from the camera on; it is free, so the drop falls down its own column onto the plate.
        Point3D inside = new(1.5, 2.5, 4.5);
        var bowwards = new CameraOrientation(180, 0);
        var look = new CameraMath(bowwards, 1, inside, View).LookDirection;
        var camera = new CameraMath(bowwards, 1, inside + look, View);

        DropTarget.Resolve(Rack1, [At(CrateSize.One, 0, 1, 1)], camera.Unproject(new Point(View.Width / 2, View.Height / 2)))
            .Should().Be(new Drop("rack-0", new GridPosition(1, 1, 0)));
    }

    [Fact]
    public void A_grazing_line_of_sight_takes_the_first_cell_it_enters()
    {
        // From the bow, barely above the plate's plane and a little to port: the line enters the bow face of the outer
        // upper cell at length 3 first; it is free, so the drop falls to the spindle.
        DropTarget.Resolve(Rack1, [], Sight(4, 2.5, 4.5, new CameraOrientation(3, 5))).Should().Be(new Drop("rack-0", new GridPosition(3, 1, 0)));
    }

    [Fact]
    public void Under_the_hull_seen_from_below_the_drop_rises_along_the_stack_axis_to_the_plate()
    {
        // A rack hanging under the hull stacks downwards (−z, D26): its plate is the hull's underside at z = 0. Seen from
        // below, the line of sight runs up, against the stack axis, and enters the lowest cell first; the drop rises to
        // the plate, or onto the crate there. No ship file has such a rack yet (T8.4, E1).
        Rack under = new("under", 0, 8, new Dimensions(2, 2, 2), null, new ShipPoint(0, 0, -2), StackAxis.MinusZ);
        var below = new CameraOrientation(CameraOrientation.DefaultYaw, -60);
        var camera = new CameraMath(below, 200, ScreenPoints.Centre(under.Bounds), View);
        var sight = camera.Unproject(camera.Project(new Point3D(0.5, 0.5, -2)));

        DropTarget.Resolve(under, [], sight).Should().Be(new Drop("under", new GridPosition(0, 0, 0)));
        DropTarget.Resolve(under, [new Crate("Riker", "Titanium", CrateSize.One, "under", new GridPosition(0, 0, 0))], sight)
            .Should().Be(new Drop("under", new GridPosition(0, 0, 1)));
    }
}
