using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class RackCellsTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    // Port upper (y 1..3) and starboard upper (y −2..0), mirror images across the spindle.
    private static readonly Rack Rack1 = HullA.Racks[0];
    private static readonly Rack Rack3 = HullA.Racks[2];

    private static Placement Cargo(string destination, int scu, bool delivered = false) =>
        new(destination, Rack1.Id, new CargoItem("Titanium", scu, CrateSize.One), delivered);

    private static IEnumerable<RackCell> Filled(IEnumerable<RackCell> cells, int stack) =>
        cells.Where(c => c.Stack == stack && !c.IsFree);

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Twelve_SCU_fill_the_first_layer_and_one_row_of_the_second(FillOrder order)
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 12)], order);

        Filled(cells, 0).Should().HaveCount(8);
        Filled(cells, 1).Select(c => c.Length).Should().BeEquivalentTo([0, 1, 2, 3]);
        Filled(cells, 1).Should().OnlyContain(c => c.Width == 0);
    }

    [Fact]
    public void Bow_first_starts_a_row_at_the_bow_end()
    {
        // Length runs along x towards the bow: index 3 is the bow end.
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 10)], FillOrder.BowFirst);

        Filled(cells, 1).Select(c => c.Length).Should().Equal(3, 2);
    }

    [Fact]
    public void Stern_first_starts_a_row_at_the_stern_end()
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 10)], FillOrder.SternFirst);

        Filled(cells, 1).Select(c => c.Length).Should().Equal(0, 1);
    }

    [Fact]
    public void Rows_next_to_the_centreline_fill_first_on_both_sides()
    {
        // Rack 1's row 0 is y 1..2, Rack 3's row 1 is y −1..0: both next to the spindle, so the halves mirror.
        Filled(RackCells.Build(Rack1, [Cargo("Riker", 10)], FillOrder.BowFirst), 1).Should().OnlyContain(c => c.Width == 0);
        Filled(RackCells.Build(Rack3, [Cargo("Riker", 10)], FillOrder.BowFirst), 1).Should().OnlyContain(c => c.Width == 1);
    }

    [Fact]
    public void Placements_take_their_SCU_in_cells_in_plan_order_then_the_rest_is_free()
    {
        var (riker, shubin) = (Cargo("Riker", 4), Cargo("Shubin", 8));

        var cells = RackCells.Build(Rack1, [riker, shubin], FillOrder.BowFirst);

        cells.Select(c => c.Placement).Should().Equal(
            [.. Enumerable.Repeat(riker, 4), .. Enumerable.Repeat(shubin, 8), .. Enumerable.Repeat<Placement?>(null, 4)]);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Every_grid_position_comes_once(FillOrder order)
    {
        var cells = RackCells.Build(Rack1, [], order);

        cells.Should().HaveCount(Rack1.CapacityScu);
        cells.Select(c => (c.Length, c.Width, c.Stack)).Should().OnlyHaveUniqueItems();
        cells.Should().OnlyContain(c =>
            c.Length >= 0 && c.Length < Rack1.Grid.Length
            && c.Width >= 0 && c.Width < Rack1.Grid.Width
            && c.Stack >= 0 && c.Stack < Rack1.Grid.Stack);
    }

    [Fact]
    public void An_empty_rack_is_all_free()
    {
        RackCells.Build(Rack1, [], FillOrder.BowFirst).Should().OnlyContain(c => c.IsFree);
    }

    [Fact]
    public void An_overloaded_rack_shares_its_cells_by_SCU()
    {
        // 24 SCU in a 16 SCU rack, only possible after a manual move (D10): 16 × 16/24 = 10.7, 16 × 8/24 = 5.3.
        var (riker, shubin) = (Cargo("Riker", 16), Cargo("Shubin", 8));

        var cells = RackCells.Build(Rack1, [riker, shubin], FillOrder.BowFirst);

        cells.Should().NotContain(c => c.IsFree);
        cells.Count(c => c.Placement == riker).Should().Be(11);
        cells.Count(c => c.Placement == shubin).Should().Be(5);
    }

    [Fact]
    public void Every_placement_of_an_overloaded_rack_keeps_a_cell_to_drag_it_out()
    {
        // Gold's share is 16/33 of a cell; it takes one from the first of the largest.
        var (riker, shubin, gold) = (Cargo("Riker", 16), Cargo("Shubin", 16), Cargo("Gold", 1));

        var cells = RackCells.Build(Rack1, [riker, shubin, gold], FillOrder.BowFirst);

        new[] { riker, shubin, gold }.Select(p => cells.Count(c => c.Placement == p)).Should().Equal(7, 8, 1);
    }

    [Fact]
    public void A_delivered_placement_keeps_its_cells()
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 8, delivered: true), Cargo("Shubin", 8)], FillOrder.BowFirst);

        cells.Take(8).Should().OnlyContain(c => c.Placement!.Delivered);
    }
}
