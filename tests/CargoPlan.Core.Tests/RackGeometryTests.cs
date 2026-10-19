using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The grid-to-ship mapping of a rack (D26). A 4×3×2 grid at (10, 20, 30): length, width and stack differ, so a
/// swapped axis shows.
/// </summary>
public class RackGeometryTests
{
    private static readonly ShipPoint Origin = new(10, 20, 30);

    private static Rack RackOf(StackAxis axis) =>
        new("rack-0", 0, 24, new Dimensions(4, 3, 2), new RackPosition(RackSide.Port, RackLevel.Upper), Origin, axis);

    private static ShipBox Box(int x0, int y0, int z0, int x1, int y1, int z1) => new(new(x0, y0, z0), new(x1, y1, z1));

    public static TheoryData<StackAxis> Axes => [.. Enum.GetValues<StackAxis>()];

    [Fact]
    public void Default_stack_axis_is_up()
    {
        // The old drawing convention (D14), so racks built in code keep their picture.
        new Rack("rack-0", 0, 24, new Dimensions(4, 3, 2), new RackPosition(RackSide.Port, RackLevel.Upper), Origin)
            .StackAxis.Should().Be(StackAxis.PlusZ);
    }

    [Theory]
    [InlineData(StackAxis.PlusZ, 10, 20, 30, 14, 23, 32)]   // length x, width y, stack z
    [InlineData(StackAxis.MinusZ, 10, 20, 30, 14, 23, 32)]
    [InlineData(StackAxis.PlusY, 10, 20, 30, 14, 22, 33)]   // length x, width z, stack y
    [InlineData(StackAxis.MinusY, 10, 20, 30, 14, 22, 33)]
    [InlineData(StackAxis.PlusX, 10, 20, 30, 12, 23, 34)]   // length z, width y, stack x
    [InlineData(StackAxis.MinusX, 10, 20, 30, 12, 23, 34)]
    public void Bounds_follow_the_stack_axis(StackAxis axis, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        RackOf(axis).Bounds.Should().Be(Box(x0, y0, z0, x1, y1, z1));
    }

    [Theory]
    [InlineData(StackAxis.PlusZ, 11, 22, 31, 13, 23, 32)]
    [InlineData(StackAxis.MinusZ, 11, 22, 30, 13, 23, 31)]   // stack counts down from the top
    [InlineData(StackAxis.PlusY, 11, 21, 32, 13, 22, 33)]
    [InlineData(StackAxis.MinusY, 11, 20, 32, 13, 21, 33)]
    [InlineData(StackAxis.PlusX, 11, 22, 31, 12, 23, 33)]
    [InlineData(StackAxis.MinusX, 10, 22, 31, 11, 23, 33)]
    public void CellBox_places_a_crate_along_the_rack_axes(StackAxis axis, int x0, int y0, int z0, int x1, int y1, int z1)
    {
        // A 2 SCU crate (2×1×1) at length 1, width 2, stack 1.
        RackOf(axis).CellBox(new GridPosition(1, 2, 1), CrateSize.Two).Should().Be(Box(x0, y0, z0, x1, y1, z1));
    }

    [Fact]
    public void Stacking_up_is_the_old_mapping()
    {
        // Before D26 every rack mapped origin + position, size along x, y, z (D14).
        var rack = RackOf(StackAxis.PlusZ);
        foreach (var size in Enum.GetValues<CrateSize>().Where(rack.CanHold))
        {
            var d = size.Dimensions;
            rack.CellBox(new GridPosition(0, 1, 0), size).Should().Be(Box(10, 21, 30, 10 + d.Length, 21 + d.Width, 30 + d.Stack));
        }
    }

    [Theory]
    [MemberData(nameof(Axes))]
    public void ToGrid_inverts_CellBox(StackAxis axis)
    {
        var rack = RackOf(axis);
        foreach (var size in new[] { CrateSize.One, CrateSize.Two, CrateSize.Eight })
        {
            var position = new GridPosition(1, 0, 0);
            rack.ToGrid(rack.CellBox(position, size)).Should().Be(position, $"{size} at {position}");
        }

        rack.ToGrid(rack.CellBox(new GridPosition(3, 2, 1), CrateSize.One)).Should().Be(new GridPosition(3, 2, 1));
    }

    [Theory]
    [MemberData(nameof(Axes))]
    public void ToGrid_of_a_box_outside_the_rack_is_null(StackAxis axis)
    {
        RackOf(axis).ToGrid(Box(0, 0, 0, 1, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void ToGrid_keeps_a_crate_whose_min_corner_is_inside_even_if_it_sticks_out()
    {
        // Whether the crate fits is the layout's call ("outside", D24), not the mapping's.
        var rack = RackOf(StackAxis.MinusY);

        rack.ToGrid(rack.CellBox(new GridPosition(3, 0, 0), CrateSize.Two)).Should().Be(new GridPosition(3, 0, 0));
    }

    // --- Hull A: stacks from the spindle outwards (D26) ---

    [Fact]
    public void Hull_A_port_racks_stack_towards_port()
    {
        var rack1 = ShipLoader.Load("hull_a").Racks[0];

        rack1.CellBox(new GridPosition(0, 0, 0), CrateSize.One).Should().Be(Box(0, 1, 3, 1, 2, 4));
        rack1.CellBox(new GridPosition(0, 0, 1), CrateSize.One).Should().Be(Box(0, 2, 3, 1, 3, 4));
        rack1.CellBox(new GridPosition(0, 1, 0), CrateSize.One).Should().Be(Box(0, 1, 4, 1, 2, 5), "width runs up on the plate");
    }

    [Fact]
    public void Hull_A_starboard_racks_stack_towards_starboard()
    {
        var rack3 = ShipLoader.Load("hull_a").Racks[2];

        rack3.CellBox(new GridPosition(0, 0, 0), CrateSize.One).Should().Be(Box(0, -1, 3, 1, 0, 4));
        rack3.CellBox(new GridPosition(0, 0, 1), CrateSize.One).Should().Be(Box(0, -2, 3, 1, -1, 4));
    }

    [Fact]
    public void Hull_A_8_SCU_crate_takes_both_stack_layers()
    {
        var rack1 = ShipLoader.Load("hull_a").Racks[0];

        rack1.CellBox(new GridPosition(2, 0, 0), CrateSize.Eight).Should().Be(Box(2, 1, 3, 4, 3, 5));
    }
}
