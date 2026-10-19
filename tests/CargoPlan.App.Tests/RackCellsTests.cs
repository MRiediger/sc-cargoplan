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

    // A 3×2×2 rack (12 SCU): an 8 SCU crate leaves a strip one cell long, too short for a 4 SCU crate.
    private static readonly Rack Short = new("short", 0, 12, new Dimensions(3, 2, 2), new RackPosition(RackSide.Port, RackLevel.Upper), new ShipPoint(0, 1, 0));

    private static Placement Cargo(string destination, int scu, CrateSize size = CrateSize.One, bool delivered = false) =>
        new(destination, Rack1.Id, new CargoItem("Titanium", scu, size), delivered);

    private static IEnumerable<RackCell> Filled(IEnumerable<RackCell> cells, int stack) =>
        cells.Where(c => c.Stack == stack && !c.IsFree);

    private static IEnumerable<(int Length, int Width, int Stack)> CellsOf(GridBox box) =>
        from l in Enumerable.Range(box.Length, box.Size.Length)
        from w in Enumerable.Range(box.Width, box.Size.Width)
        from s in Enumerable.Range(box.Stack, box.Size.Stack)
        select (l, w, s);

    /// <summary>Every crate is one block in its crate's shape, and its cells are exactly the cells of that block.</summary>
    private static void ShouldBeBlocks(IReadOnlyList<RackCell> cells)
    {
        foreach (var crate in cells.Where(c => c.Crate is not null).GroupBy(c => c.Crate))
        {
            var block = crate.First().Block;

            crate.Should().OnlyContain(c => c.Block == block);
            block.Size.Should().Be(crate.Key!.Placement.Item.CrateSize.Dimensions);
            crate.Select(c => (c.Length, c.Width, c.Stack)).Should().BeEquivalentTo(CellsOf(block));
        }
    }

    private static bool IsSingleCell(RackCell cell) =>
        cell.Block == new GridBox(cell.Length, cell.Width, cell.Stack, new Dimensions(1, 1, 1));

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Crates_of_4_4_and_8_fill_the_rack_without_gaps(FillOrder order)
    {
        // The 8 SCU crate comes last in the plan; the largest crate goes first, so it still fits.
        Placement[] placements = [Cargo("Riker", 4, CrateSize.Four), Cargo("Shubin", 4, CrateSize.Four), Cargo("Orison", 8, CrateSize.Eight)];

        var cells = RackCells.Build(Rack1, placements, order, isViolated: false);

        cells.Should().NotContain(c => c.IsFree);
        cells.Select(c => c.Crate).Distinct().Should().HaveCount(3);
        ShouldBeBlocks(cells);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Crates_of_2_2_2_and_2_fill_the_bottom_layer_without_gaps(FillOrder order)
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 8, CrateSize.Two)], order, isViolated: false);

        Filled(cells, 0).Should().HaveCount(8);
        Filled(cells, 1).Should().BeEmpty();
        cells.Select(c => c.Crate).OfType<CrateId>().Distinct().Should().HaveCount(4);
        ShouldBeBlocks(cells);
    }

    [Fact]
    public void Crate_numbers_count_from_one_within_each_placement()
    {
        var (riker, shubin) = (Cargo("Riker", 8, CrateSize.Two), Cargo("Shubin", 4, CrateSize.Two));

        var cells = RackCells.Build(Rack1, [riker, shubin], FillOrder.BowFirst, isViolated: false);

        cells.Where(c => c.Placement == riker).Select(c => c.Crate!.Number).Distinct().Should().BeEquivalentTo([1, 2, 3, 4]);
        cells.Where(c => c.Placement == shubin).Select(c => c.Crate!.Number).Distinct().Should().BeEquivalentTo([1, 2]);
        cells.Where(c => c.Crate is not null).GroupBy(c => c.Crate).Should().OnlyContain(g => g.Count() == 2);
    }

    [Fact]
    public void Equal_placements_continue_the_crate_numbers()
    {
        // Two equal mission rows give two equal placements (D22); their crates must not share an id.
        var twin = Cargo("Riker", 4, CrateSize.Two);

        var cells = RackCells.Build(Rack1, [twin, twin], FillOrder.BowFirst, isViolated: false);

        cells.Select(c => c.Crate).OfType<CrateId>().Distinct().Should().BeEquivalentTo(
            [new CrateId(twin, 1), new CrateId(twin, 2), new CrateId(twin, 3), new CrateId(twin, 4)]);
    }

    [Fact]
    public void Ids_are_values_so_a_rebuilt_rack_has_the_same_ids()
    {
        Placement[] placements = [Cargo("Riker", 8, CrateSize.Four), Cargo("Shubin", 8, CrateSize.Eight)];

        var first = RackCells.Build(Rack1, placements, FillOrder.BowFirst, isViolated: false);
        var again = RackCells.Build(Rack1, [.. placements.Select(p => p with { })], FillOrder.BowFirst, isViolated: false);

        again.Select(c => c.Crate).Should().Equal(first.Select(c => c.Crate));
    }

    [Fact]
    public void The_largest_crates_go_first_whatever_the_plan_order()
    {
        var (riker, shubin) = (Cargo("Riker", 8, CrateSize.Four), Cargo("Shubin", 8, CrateSize.Eight));

        var cells = RackCells.Build(Rack1, [riker, shubin], FillOrder.BowFirst, isViolated: false);

        cells[0].Placement.Should().Be(shubin);
        cells.Should().NotContain(c => c.IsFree);
        ShouldBeBlocks(cells);
    }

    [Fact]
    public void A_crate_that_fits_no_block_is_drawn_cell_by_cell()
    {
        var (shubin, riker) = (Cargo("Shubin", 8, CrateSize.Eight), Cargo("Riker", 4, CrateSize.Four));

        var cells = RackCells.Build(Short, [shubin, riker], FillOrder.BowFirst, isViolated: false);

        // A local: an expression tree can't hold an extension property.
        var eight = CrateSize.Eight.Dimensions;
        cells.Should().NotContain(c => c.IsFree);
        cells.Where(c => c.Placement == shubin).Should().OnlyContain(c => c.Block.Size == eight);
        cells.Where(c => c.Placement == riker).Should().HaveCount(4)
            .And.OnlyContain(c => c.Crate == new CrateId(riker, 1) && IsSingleCell(c));
    }

    [Fact]
    public void A_placement_falls_back_as_a_whole()
    {
        // Two 4 SCU crates fit as blocks, the third doesn't: all three go cell by cell, four cells per crate in fill order.
        var riker = Cargo("Riker", 12, CrateSize.Four);

        var cells = RackCells.Build(Short, [riker], FillOrder.BowFirst, isViolated: false);

        cells.Should().OnlyContain(c => IsSingleCell(c));
        cells.Select(c => c.Crate!.Number).Should().Equal(1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3);
    }

    [Fact]
    public void An_overloaded_rack_has_no_crates()
    {
        // 20 SCU on 16: cells are shares, no longer SCU, so no cell can stand for a crate.
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 16, CrateSize.Four), Cargo("Shubin", 4, CrateSize.Four)], FillOrder.BowFirst, isViolated: true);

        cells.Should().NotContain(c => c.IsFree);
        cells.Should().OnlyContain(c => c.Crate == null && IsSingleCell(c));
    }

    [Fact]
    public void A_violated_rack_draws_its_crates_cell_by_cell()
    {
        var shubin = Cargo("Shubin", 8, CrateSize.Eight);

        var cells = RackCells.Build(Rack1, [shubin], FillOrder.BowFirst, isViolated: true);

        cells.Should().OnlyContain(c => IsSingleCell(c));
        cells.Take(8).Should().OnlyContain(c => c.Crate == new CrateId(shubin, 1));
        cells.Skip(8).Should().OnlyContain(c => c.IsFree);
    }

    [Fact]
    public void Free_cells_are_single_cells()
    {
        RackCells.Build(Rack1, [Cargo("Riker", 8, CrateSize.Eight)], FillOrder.BowFirst, isViolated: false)
            .Where(c => c.IsFree).Should().HaveCount(8).And.OnlyContain(c => IsSingleCell(c) && c.Crate == null);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Twelve_SCU_fill_the_first_layer_and_one_row_of_the_second(FillOrder order)
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 12)], order, isViolated: false);

        Filled(cells, 0).Should().HaveCount(8);
        Filled(cells, 1).Select(c => c.Length).Should().BeEquivalentTo([0, 1, 2, 3]);
        Filled(cells, 1).Should().OnlyContain(c => c.Width == 0);
    }

    [Fact]
    public void Bow_first_starts_a_row_at_the_bow_end()
    {
        // Length runs along x towards the bow: index 3 is the bow end.
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 10)], FillOrder.BowFirst, isViolated: false);

        Filled(cells, 1).Select(c => c.Length).Should().Equal(3, 2);
    }

    [Fact]
    public void Stern_first_starts_a_row_at_the_stern_end()
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 10)], FillOrder.SternFirst, isViolated: false);

        Filled(cells, 1).Select(c => c.Length).Should().Equal(0, 1);
    }

    [Fact]
    public void Rows_next_to_the_centreline_fill_first_on_both_sides()
    {
        // Rack 1's row 0 is y 1..2, Rack 3's row 1 is y −1..0: both next to the spindle, so the halves mirror.
        Filled(RackCells.Build(Rack1, [Cargo("Riker", 10)], FillOrder.BowFirst, isViolated: false), 1).Should().OnlyContain(c => c.Width == 0);
        Filled(RackCells.Build(Rack3, [Cargo("Riker", 10)], FillOrder.BowFirst, isViolated: false), 1).Should().OnlyContain(c => c.Width == 1);
    }

    [Fact]
    public void Placements_of_one_crate_size_take_their_SCU_in_plan_order_then_the_rest_is_free()
    {
        var (riker, shubin) = (Cargo("Riker", 4), Cargo("Shubin", 8));

        var cells = RackCells.Build(Rack1, [riker, shubin], FillOrder.BowFirst, isViolated: false);

        cells.Select(c => c.Placement).Should().Equal(
            [.. Enumerable.Repeat(riker, 4), .. Enumerable.Repeat(shubin, 8), .. Enumerable.Repeat<Placement?>(null, 4)]);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Every_grid_position_comes_once(FillOrder order)
    {
        var cells = RackCells.Build(Rack1, [], order, isViolated: false);

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
        RackCells.Build(Rack1, [], FillOrder.BowFirst, isViolated: false).Should().OnlyContain(c => c.IsFree);
    }

    [Fact]
    public void An_overloaded_rack_shares_its_cells_by_SCU()
    {
        // 24 SCU in a 16 SCU rack, only possible after a manual move (D10): 16 × 16/24 = 10.7, 16 × 8/24 = 5.3.
        var (riker, shubin) = (Cargo("Riker", 16), Cargo("Shubin", 8));

        var cells = RackCells.Build(Rack1, [riker, shubin], FillOrder.BowFirst, isViolated: true);

        cells.Should().NotContain(c => c.IsFree);
        cells.Count(c => c.Placement == riker).Should().Be(11);
        cells.Count(c => c.Placement == shubin).Should().Be(5);
    }

    [Fact]
    public void Every_placement_of_an_overloaded_rack_keeps_a_cell_to_drag_it_out()
    {
        // Gold's share is 16/33 of a cell; it takes one from the first of the largest.
        var (riker, shubin, gold) = (Cargo("Riker", 16), Cargo("Shubin", 16), Cargo("Gold", 1));

        var cells = RackCells.Build(Rack1, [riker, shubin, gold], FillOrder.BowFirst, isViolated: true);

        new[] { riker, shubin, gold }.Select(p => cells.Count(c => c.Placement == p)).Should().Equal(7, 8, 1);
    }

    [Fact]
    public void A_delivered_placement_keeps_its_cells()
    {
        var cells = RackCells.Build(Rack1, [Cargo("Riker", 8, delivered: true), Cargo("Shubin", 8)], FillOrder.BowFirst, isViolated: false);

        cells.Take(8).Should().OnlyContain(c => c.Placement!.Delivered);
    }
}
