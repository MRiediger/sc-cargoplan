using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class ModelsTests
{
    private static readonly Dimensions HullAGrid = new(4, 2, 2);
    private static readonly RackPosition PortUpper = new(RackSide.Port, RackLevel.Upper);

    private static readonly ShipPoint Zero = new(0, 0, 0);

    // Ship doesn't check overlap (the loader does), so test racks may share one origin.
    private static Rack RackOf(int index, int capacityScu = 16, Dimensions? grid = null, ShipPoint? origin = null) =>
        new($"rack-{index}", index, capacityScu, grid ?? HullAGrid, PortUpper, origin ?? Zero);

    private static IReadOnlyList<Rack> HullARacks() =>
        [RackOf(0), RackOf(1), RackOf(2), RackOf(3)];

    // --- CrateSize ---

    [Theory]
    [InlineData(CrateSize.One, 1)]
    [InlineData(CrateSize.Two, 2)]
    [InlineData(CrateSize.Four, 4)]
    [InlineData(CrateSize.Eight, 8)]
    [InlineData(CrateSize.Sixteen, 16)]
    [InlineData(CrateSize.TwentyFour, 24)]
    [InlineData(CrateSize.ThirtyTwo, 32)]
    public void CrateSize_value_equals_its_scu(CrateSize size, int expectedScu)
    {
        ((int)size).Should().Be(expectedScu);
    }

    // Measured in the hangar (Alpha 4.10): length x width x stack in cells, fixed orientation.
    [Theory]
    [InlineData(CrateSize.One, 1, 1, 1)]
    [InlineData(CrateSize.Two, 2, 1, 1)]
    [InlineData(CrateSize.Four, 2, 2, 1)]
    [InlineData(CrateSize.Eight, 2, 2, 2)]
    [InlineData(CrateSize.Sixteen, 4, 2, 2)]
    [InlineData(CrateSize.TwentyFour, 6, 2, 2)]
    [InlineData(CrateSize.ThirtyTwo, 4, 4, 2)]
    public void CrateSize_dimensions_match_the_game(CrateSize size, int length, int width, int stack)
    {
        size.Dimensions.Should().Be(new Dimensions(length, width, stack));
    }

    [Theory]
    [InlineData(CrateSize.One)]
    [InlineData(CrateSize.Two)]
    [InlineData(CrateSize.Four)]
    [InlineData(CrateSize.Eight)]
    [InlineData(CrateSize.Sixteen)]
    [InlineData(CrateSize.TwentyFour)]
    [InlineData(CrateSize.ThirtyTwo)]
    public void CrateSize_volume_equals_scu(CrateSize size)
    {
        size.Dimensions.Volume.Should().Be((int)size);
    }

    // --- Dimensions ---

    [Fact]
    public void Dimensions_volume_is_product_of_axes()
    {
        var grid = new Dimensions(4, 2, 2);

        grid.Volume.Should().Be(16);
        grid.ToString().Should().Be("4×2×2");
    }

    [Theory]
    [InlineData(0, 2, 2, "Length")]
    [InlineData(4, 0, 2, "Width")]
    [InlineData(4, 2, -1, "Stack")]
    public void Dimensions_rejects_non_positive_axis(int length, int width, int stack, string axis)
    {
        var act = () => new Dimensions(length, width, stack);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(axis);
    }

    // --- ShipPoint / ShipBox ---

    [Fact]
    public void ShipPoint_prints_like_the_json_array()
    {
        new ShipPoint(0, -2, 3).ToString().Should().Be("[0, -2, 3]");
    }

    [Fact]
    public void ShipBox_rejects_max_below_min()
    {
        var act = () => new ShipBox(new ShipPoint(0, 0, 0), new ShipPoint(4, -1, 2));

        act.Should().Throw<ArgumentException>();
    }

    private static ShipBox Box(int x, int y, int z, int length = 2, int width = 2, int stack = 2) =>
        new(new ShipPoint(x, y, z), new ShipPoint(x + length, y + width, z + stack));

    [Theory]
    [InlineData(1, 1, 1, true)]    // shares a 1×1×1 corner
    [InlineData(0, 0, 0, true)]    // same place
    [InlineData(2, 0, 0, false)]   // touches along x
    [InlineData(0, -2, 0, false)]  // touches along y
    [InlineData(0, 0, 5, false)]   // apart along z
    public void ShipBox_Overlaps_needs_shared_volume(int x, int y, int z, bool expected)
    {
        Box(0, 0, 0).Overlaps(Box(x, y, z)).Should().Be(expected);
        Box(x, y, z).Overlaps(Box(0, 0, 0)).Should().Be(expected);
    }

    [Fact]
    public void ShipBox_Union_encloses_both_boxes()
    {
        var union = Box(0, 1, 3).Union(Box(-1, -2, 0));

        union.Should().Be(new ShipBox(new ShipPoint(-1, -2, 0), new ShipPoint(2, 3, 5)));
    }

    // --- CargoItem ---

    [Theory]
    [InlineData(1, CrateSize.One)]
    [InlineData(6, CrateSize.Two)]
    [InlineData(12, CrateSize.Four)]
    [InlineData(16, CrateSize.Eight)]
    public void CargoItem_accepts_scu_that_is_a_multiple_of_crate_size(int scu, CrateSize size)
    {
        var item = new CargoItem("Titanium", scu, size);

        item.Scu.Should().Be(scu);
        item.CrateSize.Should().Be(size);
    }

    [Theory]
    [InlineData(3, CrateSize.Two)]
    [InlineData(10, CrateSize.Four)]
    [InlineData(12, CrateSize.Eight)]
    public void CargoItem_rejects_scu_that_is_not_a_multiple_of_crate_size(int scu, CrateSize size)
    {
        var act = () => new CargoItem("Titanium", scu, size);

        act.Should().Throw<ArgumentException>().WithMessage($"*{scu}*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-4)]
    public void CargoItem_rejects_non_positive_scu(int scu)
    {
        var act = () => new CargoItem("Titanium", scu, CrateSize.Four);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void CargoItem_rejects_undefined_crate_size(int rawSize)
    {
        var act = () => new CargoItem("Titanium", 8, (CrateSize)rawSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CargoItem_has_value_equality()
    {
        var a = new CargoItem("Quartz", 10, CrateSize.Two);
        var b = new CargoItem("Quartz", 10, CrateSize.Two);

        a.Should().Be(b);
    }

    // --- Rack ---

    [Fact]
    public void Rack_with_positive_capacity_and_index_is_valid()
    {
        var rack = RackOf(index: 0, capacityScu: 16);

        rack.CapacityScu.Should().Be(16);
        rack.Grid.Should().Be(HullAGrid);
        rack.Position.Should().Be(PortUpper);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-16)]
    public void Rack_rejects_non_positive_capacity(int capacityScu)
    {
        var act = () => RackOf(index: 0, capacityScu);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rack_rejects_negative_index()
    {
        var act = () => RackOf(index: -1);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rack_rejects_grid_that_does_not_match_capacity()
    {
        var act = () => RackOf(index: 0, capacityScu: 12, grid: HullAGrid);

        act.Should().Throw<ArgumentException>().WithMessage("*rack-0*16*12*");
    }

    [Theory]
    [InlineData(CrateSize.One, true)]
    [InlineData(CrateSize.Two, true)]
    [InlineData(CrateSize.Four, true)]
    [InlineData(CrateSize.Eight, true)]
    [InlineData(CrateSize.Sixteen, true)]
    [InlineData(CrateSize.TwentyFour, false)]  // 6 cells long, the plate has 4
    [InlineData(CrateSize.ThirtyTwo, false)]   // 4 cells wide, the plate has 2
    public void Rack_CanHold_checks_crate_shape_against_hull_a_grid(CrateSize size, bool expected)
    {
        RackOf(index: 0).CanHold(size).Should().Be(expected);
    }

    [Fact]
    public void Rack_Bounds_span_the_grid_from_the_origin()
    {
        var rack = RackOf(index: 0, origin: new ShipPoint(0, -2, 3));

        rack.Bounds.Should().Be(new ShipBox(new ShipPoint(0, -2, 3), new ShipPoint(4, 0, 5)));
    }

    // --- Ship ---

    [Fact]
    public void Ship_accepts_racks_that_sum_to_total_scu()
    {
        var ship = new Ship("hull_a", "MISC Hull A", 64, HullARacks());

        ship.Racks.Should().HaveCount(4);
        ship.TotalScu.Should().Be(64);
    }

    [Fact]
    public void Ship_rejects_racks_that_do_not_sum_to_total_scu()
    {
        IReadOnlyList<Rack> racks = [RackOf(0), RackOf(1), RackOf(2), RackOf(3, capacityScu: 12, grid: new(3, 2, 2))];

        var act = () => new Ship("hull_a", "MISC Hull A", 64, racks);

        act.Should().Throw<ArgumentException>().WithMessage("*60*64*");
    }

    [Fact]
    public void Ship_is_not_affected_by_later_changes_to_the_source_list()
    {
        List<Rack> racks = [.. HullARacks()];
        var ship = new Ship("hull_a", "MISC Hull A", 64, racks);

        racks.Add(RackOf(4));

        ship.Racks.Should().HaveCount(4);
    }

    [Fact]
    public void Ship_Bounds_enclose_all_racks()
    {
        IReadOnlyList<Rack> racks =
        [
            RackOf(0, origin: new ShipPoint(0, 1, 3)),
            RackOf(1, origin: new ShipPoint(0, -2, 0)),
        ];

        var ship = new Ship("two_racks", "Two racks", 32, racks);

        ship.Bounds.Should().Be(new ShipBox(new ShipPoint(0, -2, 0), new ShipPoint(4, 3, 5)));
    }

    // --- Stop ---

    [Fact]
    public void Stop_total_scu_is_sum_of_items()
    {
        var stop = new Stop("Riker", 1,
        [
            new CargoItem("Titanium", 12, CrateSize.Four),
            new CargoItem("Quartz", 6, CrateSize.Two),
        ]);

        stop.TotalScu.Should().Be(18);
    }

    [Fact]
    public void Stop_without_items_has_zero_total_scu()
    {
        new Stop("Riker", 1, []).TotalScu.Should().Be(0);
    }

    // --- Placement / LoadPlan ---

    [Fact]
    public void Placement_is_not_delivered_by_default_and_with_leaves_original_unchanged()
    {
        var original = new Placement("Riker", "rack-0", new CargoItem("Titanium", 12, CrateSize.Four));

        var delivered = original with { Delivered = true };

        original.Delivered.Should().BeFalse();
        delivered.Delivered.Should().BeTrue();
    }

    private static Crate Box(string destination, string commodity, CrateSize size, string rackId, int length, int stack = 0, bool delivered = false) =>
        new(destination, commodity, size, rackId, new GridPosition(length, 0, stack), delivered);

    [Fact]
    public void LoadPlan_derives_one_placement_per_rack_destination_commodity_size_and_state_in_order_of_first_crate()
    {
        var plan = new LoadPlan("hull_a",
        [
            Box("Riker", "Titanium", CrateSize.Four, "rack-0", 0),
            Box("Shubin", "Quartz", CrateSize.Two, "rack-1", 0),
            Box("Riker", "Titanium", CrateSize.Four, "rack-0", 2),
            Box("Riker", "Titanium", CrateSize.Four, "rack-1", 2),
            Box("Riker", "Titanium", CrateSize.Two, "rack-0", 0, stack: 1),
        ], []);

        plan.Placements.Should().Equal(
            new Placement("Riker", "rack-0", new CargoItem("Titanium", 8, CrateSize.Four)),
            new Placement("Shubin", "rack-1", new CargoItem("Quartz", 2, CrateSize.Two)),
            new Placement("Riker", "rack-1", new CargoItem("Titanium", 4, CrateSize.Four)),
            new Placement("Riker", "rack-0", new CargoItem("Titanium", 2, CrateSize.Two)));
    }

    [Fact]
    public void LoadPlan_keeps_delivered_and_on_board_crates_of_one_kind_apart()
    {
        var plan = new LoadPlan("hull_a",
            [Box("Riker", "Titanium", CrateSize.Four, "rack-0", 0, delivered: true), Box("Riker", "Titanium", CrateSize.Four, "rack-0", 2)], []);

        plan.Placements.Select(p => p.Delivered).Should().Equal(true, false);
    }

    [Fact]
    public void LoadPlan_placements_follow_a_with_on_its_crates()
    {
        // Derived on every read: a stored list would keep the old crates after `with`.
        var plan = new LoadPlan("hull_a", [Box("Riker", "Titanium", CrateSize.Four, "rack-0", 0)], []);

        var moved = plan with { Crates = [plan.Crates[0] with { RackId = "rack-3" }] };

        moved.Placements.Should().ContainSingle().Which.RackId.Should().Be("rack-3");
    }

    [Fact]
    public void LoadPlan_filters_crates_and_placements_by_rack_and_by_stop()
    {
        var plan = new LoadPlan("hull_a",
        [
            Box("Riker", "Titanium", CrateSize.Four, "rack-0", 0),
            Box("Shubin", "Quartz", CrateSize.Two, "rack-1", 0),
            Box("Riker", "Titanium", CrateSize.Four, "rack-1", 2),
        ], []);

        plan.CratesForRack("rack-1").Should().Equal(plan.Crates[1], plan.Crates[2]);
        plan.PlacementsForRack("rack-1").Select(p => p.Destination).Should().Equal("Shubin", "Riker");
        plan.PlacementsForStop("Riker").Select(p => p.RackId).Should().Equal("rack-0", "rack-1");
    }

    [Fact]
    public void LoadPlan_returns_empty_list_for_unknown_rack_or_stop()
    {
        var plan = new LoadPlan("hull_a", [Box("Riker", "Titanium", CrateSize.Four, "rack-0", 0)], []);

        plan.CratesForRack("rack-9").Should().BeEmpty();
        plan.PlacementsForRack("rack-9").Should().BeEmpty();
        plan.PlacementsForStop("Orison").Should().BeEmpty();
    }

    [Fact]
    public void LoadPlan_route_is_the_order_of_first_appearance_of_each_destination()
    {
        // The order of legend and palette, and the order the stacking rule follows (D24 addendum).
        var plan = new LoadPlan("hull_a",
        [
            Box("Shubin", "Quartz", CrateSize.Two, "rack-1", 0),
            Box("Riker", "Titanium", CrateSize.Four, "rack-0", 0),
            Box("Shubin", "Quartz", CrateSize.Two, "rack-0", 2),
        ], []);

        plan.Route.Should().Equal("Shubin", "Riker");
    }

    [Fact]
    public void LoadPlan_fills_bow_first_by_default()
    {
        new LoadPlan("hull_a", [], []).FillOrder.Should().Be(FillOrder.BowFirst);
    }

    // --- Crate / GridPosition ---

    [Fact]
    public void Crate_cells_are_its_shape_from_its_position()
    {
        var crate = Box("Riker", "Titanium", CrateSize.Four, "rack-0", 1, stack: 1);

        crate.Cells.Should().BeEquivalentTo(
        [
            new GridPosition(1, 0, 1), new GridPosition(2, 0, 1), new GridPosition(1, 1, 1), new GridPosition(2, 1, 1),
        ]);
        crate.Bottom.Should().HaveCount(4);
    }

    [Fact]
    public void Crate_bottom_of_an_8_is_its_lower_half()
    {
        Box("Riker", "Titanium", CrateSize.Eight, "rack-0", 0).Bottom.Should().OnlyContain(c => c.Stack == 0).And.HaveCount(4);
    }

    [Fact]
    public void Crate_rejects_an_unknown_crate_size()
    {
        var act = () => new Crate("Riker", "Titanium", (CrateSize)3, "rack-0", new GridPosition(0, 0, 0));

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("CrateSize");
    }

    [Theory]
    [InlineData(-1, 0, 0, "Length")]
    [InlineData(0, -1, 0, "Width")]
    [InlineData(0, 0, -1, "Stack")]
    public void GridPosition_rejects_negative_coordinates(int length, int width, int stack, string parameter)
    {
        var act = () => new GridPosition(length, width, stack);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName(parameter);
    }

    [Fact]
    public void LoadPlan_filters_warnings_by_rack()
    {
        var first = new PlanWarning("rack-0", "Riker spans racks 1+2");
        var second = new PlanWarning("rack-1", "Riker spans racks 1+2");
        var plan = new LoadPlan("hull_a", [], [first, second]);

        plan.WarningsForRack("rack-1").Should().Equal(second);
        plan.WarningsForRack("rack-9").Should().BeEmpty();
    }

    [Fact]
    public void LoadPlan_EnsureBelongsTo_accepts_its_own_ship()
    {
        var ship = new Ship("hull_a", "MISC Hull A", 64, HullARacks());

        var act = () => new LoadPlan("hull_a", [], []).EnsureBelongsTo(ship);

        act.Should().NotThrow();
    }

    [Fact]
    public void LoadPlan_EnsureBelongsTo_rejects_another_ship_naming_both()
    {
        var ship = new Ship("hull_a", "MISC Hull A", 64, HullARacks());

        var act = () => new LoadPlan("cutlass_black", [], []).EnsureBelongsTo(ship);

        act.Should().Throw<ArgumentException>().WithParameterName("ship").WithMessage("*'cutlass_black'*'hull_a'*");
    }
}
