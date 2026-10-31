using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public sealed class OutlinePlacementTests
{
    // 20 m × 10 m = 16 × 8 cells, origin in the middle like the traced outlines.
    private static readonly ShipOutline Rectangle = new("box", [new(10, 5), new(-10, 5), new(-10, -5), new(10, -5)]);

    // The Hull A's racks before K5.2: 4 cells long, 5 wide (with the spindle gap), 5 high. Smaller than the rectangle.
    private static readonly ShipBox HullA = new(new ShipPoint(0, -2, 0), new ShipPoint(4, 3, 5));

    // The Hull A's racks since K5.2 (D78): 7 cells wide, nearly the 8 of the rectangle.
    private static readonly ShipBox Wide = new(new ShipPoint(0, -4, 0), new ShipPoint(4, 3, 5));

    private static (double MinX, double MaxX, double MinY, double MaxY) Box(IReadOnlyList<Point3D> points) =>
        (points.Min(p => p.X), points.Max(p => p.X), points.Min(p => p.Y), points.Max(p => p.Y));

    [Fact]
    public void Racks_smaller_than_the_hull_leave_the_outline_at_true_size()
    {
        // k = max(4 / 16, 5 / 8) × 1.2 = 0.75 would shrink the hull below its size; the floor is 1 (K5.2c).
        var placed = OutlinePlacement.Place(Rectangle, HullA);

        var (minX, maxX, minY, maxY) = Box(placed);
        (maxX - minX).Should().BeApproximately(16, 1e-9);
        (maxY - minY).Should().BeApproximately(8, 1e-9);
    }

    [Fact]
    public void Racks_that_fill_the_hull_keep_a_fifth_to_spare()
    {
        // k = max(4 / 16, 7 / 8) × 1.2 = 1.05: the width binds, 8 cells × 1.05 = 8.4 = 1.2 × 7 (D62).
        var placed = OutlinePlacement.Place(Rectangle, Wide);

        var (minX, maxX, minY, maxY) = Box(placed);
        (maxY - minY).Should().BeApproximately(8.4, 1e-9);
        (maxX - minX).Should().BeApproximately(16.8, 1e-9);
    }

    [Fact]
    public void A_tiny_hold_shows_the_hull_at_true_size()
    {
        // The Aurora's case: one rack of 3 × 1 cells; D62 alone gave a hull of about 1.2 × the rack (Martin, K5.2c).
        ShipBox tiny = new(new ShipPoint(0, 0, 0), new ShipPoint(3, 1, 1));

        var (minX, maxX, minY, maxY) = Box(OutlinePlacement.Place(Rectangle, tiny));

        (maxX - minX).Should().BeApproximately(16, 1e-9);
        (maxY - minY).Should().BeApproximately(8, 1e-9);
    }

    [Fact]
    public void A_long_ship_scales_the_outline_by_its_length()
    {
        // k = max(40 / 16, 5 / 8) × 1.2 = 3: the length binds, 16 cells × 3 = 48 = 1.2 × 40.
        ShipBox row = new(new ShipPoint(0, 0, 0), new ShipPoint(40, 5, 2));

        var (minX, maxX, minY, maxY) = Box(OutlinePlacement.Place(Rectangle, row));

        (maxX - minX).Should().BeApproximately(48, 1e-9);
        (maxY - minY).Should().BeApproximately(24, 1e-9);
    }

    [Fact]
    public void The_outline_origin_lies_on_the_middle_of_the_racks_on_their_floor()
    {
        var placed = OutlinePlacement.Place(Rectangle, HullA);

        // (10 m, 5 m) is (8, 4) cells from the origin, at true size from the racks' middle (2, 0.5).
        placed[0].Should().Be(new Point3D(2 + 8, 0.5 + 4, 0));
        placed.Should().OnlyContain(p => p.Z == HullA.Min.Z);
    }

    [Fact]
    public void Bow_stays_bowwards_and_port_stays_port()
    {
        OutlinePoint[] arrow = [new(10, 0), new(-10, 4), new(-10, -2)];

        var placed = OutlinePlacement.Place(new ShipOutline("arrow", arrow), HullA);

        placed[0].X.Should().BeGreaterThan(placed[1].X);
        placed[1].Y.Should().BeGreaterThan(placed[2].Y);
    }

    [Fact]
    public void Every_point_is_kept_in_order()
    {
        OutlinePlacement.Place(Rectangle, HullA).Should().HaveCount(4);
    }

    // --- The floor: the hologram's bottom (D83), also where no hologram is loaded (T8.5) ---

    // One rack 4 × 2 cells, 6 high: its middle at z = 3.
    private static Ship Tall(ShipPoint? holoOffset = null) =>
        new("tall", "Tall", 48, [new Rack("rack-0", 0, 48, new Dimensions(4, 2, 6), null, new ShipPoint(0, 0, 0))], HoloOffset: holoOffset);

    private static ShipOutline WithHeight(double? metres) => Rectangle with { HeightM = metres };

    [Fact]
    public void The_floor_lies_half_the_hull_below_the_racks_middle_as_the_hologram_does()
    {
        var ship = Tall();
        ship.Bounds.Should().Be(new ShipBox(new ShipPoint(0, 0, 0), new ShipPoint(4, 2, 6)));

        // 10 m = 8 cells: the hologram's box from 3 − 4 to 3 + 4.
        OutlinePlacement.Floor(WithHeight(10), ship).Should().BeApproximately(-1, 1e-9);
    }

    [Fact]
    public void The_floor_moves_with_the_holo_offset()
    {
        // The Aurora Mk II's case: the hologram a cell higher (K5.3b), so its floor too.
        OutlinePlacement.Floor(WithHeight(10), Tall(new ShipPoint(0, 0, 1))).Should().BeApproximately(0, 1e-9);
    }

    [Fact]
    public void The_floor_never_lies_above_the_racks_floor()
    {
        // 5 m = 4 cells: 3 − 2 = 1 would cut through the rack.
        OutlinePlacement.Floor(WithHeight(5), Tall()).Should().Be(0);
    }

    [Fact]
    public void Without_a_height_the_floor_is_the_racks_floor()
    {
        OutlinePlacement.Floor(WithHeight(null), Tall()).Should().Be(0);
    }

    [Fact]
    public void A_ship_lays_its_outline_on_its_floor()
    {
        var ship = Tall();

        var placed = OutlinePlacement.Place(WithHeight(10), ship);

        placed.Should().OnlyContain(p => p.Z == OutlinePlacement.Floor(WithHeight(10), ship));
        placed.Select(p => (p.X, p.Y)).Should().Equal(OutlinePlacement.Place(Rectangle, ship.Bounds).Select(p => (p.X, p.Y)));
    }
}
