using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Lanes (D116, F10a): a rack's width split into a first and a second side and, on an odd width, the middle column; each
/// lane fills like a whole rack from its outer edge, the middle last, and no crate lies across two lanes. Width 0 is
/// starboard on racks stacking along ±z or ±x; racks stacking along ±y and racks under four cells wide get none.
/// </summary>
public class LanesTests
{
    private static readonly Ship Raft = FrozenShips.Load("raft");

    private static Rack Flat(int length, int width, int stack, StackAxis axis = StackAxis.PlusZ) =>
        new("flat", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0), axis);

    private static Lanes LanesOf(Rack rack, Sides sides) => Lanes.Of(rack, sides) ?? throw new InvalidOperationException("no lanes");

    /// <summary>Layer 0 of a rank table, written as the picture: the highest width (port) on top, length 0 on the left.</summary>
    private static int[,] Layer0(int[,,] rank)
    {
        var picture = new int[rank.GetLength(1), rank.GetLength(0)];
        for (var w = 0; w < rank.GetLength(1); w++)
        {
            for (var l = 0; l < rank.GetLength(0); l++)
            {
                picture[rank.GetLength(1) - 1 - w, l] = rank[l, w, 0];
            }
        }

        return picture;
    }

    private static IEnumerable<GridPosition> CellsOf(GridPosition position, Dimensions size) =>
        from l in Enumerable.Range(position.Length, size.Length)
        from w in Enumerable.Range(position.Width, size.Width)
        from s in Enumerable.Range(position.Stack, size.Stack)
        select new GridPosition(l, w, s);

    // --- Which racks get lanes ---

    [Fact]
    public void Port_first_splits_the_raft_into_its_port_half_then_its_starboard_half()
    {
        var lanes = LanesOf(Raft.Racks[0], Sides.PortFirst);

        lanes.Count.Should().Be(2);
        lanes.Breadth(0).Should().Equal(7, 6, 5, 4);
        lanes.Breadth(1).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void Starboard_first_takes_the_starboard_half_first()
    {
        var lanes = LanesOf(Raft.Racks[0], Sides.StarboardFirst);

        lanes.Breadth(0).Should().Equal(0, 1, 2, 3);
        lanes.Breadth(1).Should().Equal(7, 6, 5, 4);
    }

    [Theory]
    [InlineData(7, new[] { 6, 5, 4 }, new[] { 0, 1, 2 }, 3)]
    [InlineData(5, new[] { 4, 3 }, new[] { 0, 1 }, 2)]
    public void An_odd_width_gets_two_equal_lanes_and_the_middle_column_last(int width, int[] first, int[] second, int middle)
    {
        var lanes = LanesOf(Flat(6, width, 2), Sides.PortFirst);

        lanes.Count.Should().Be(3);
        lanes.Breadth(0).Should().Equal(first);
        lanes.Breadth(1).Should().Equal(second);
        lanes.Breadth(2).Should().Equal(middle);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(2)]
    [InlineData(1)]
    public void A_rack_under_four_cells_wide_gets_no_lanes(int width) =>
        Lanes.Of(Flat(6, width, 2), Sides.PortFirst).Should().BeNull();

    [Theory]
    [InlineData(StackAxis.PlusY)]
    [InlineData(StackAxis.MinusY)]
    public void A_rack_stacking_sideways_gets_no_lanes(StackAxis axis) =>
        Lanes.Of(Flat(4, 4, 2, axis), Sides.PortFirst).Should().BeNull();

    [Theory]
    [InlineData(StackAxis.PlusX)]
    [InlineData(StackAxis.MinusZ)]
    public void A_rack_whose_width_lies_across_the_ship_gets_lanes_whatever_its_stack_axis(StackAxis axis) =>
        Lanes.Of(Flat(4, 4, 2, axis), Sides.PortFirst).Should().NotBeNull();

    [Fact]
    public void Without_sides_no_rack_gets_lanes() =>
        Lanes.Of(Raft.Racks[0], Sides.None).Should().BeNull();

    [Fact]
    public void Splits_says_whether_a_span_crosses_a_lane_boundary()
    {
        var lanes = LanesOf(Flat(6, 7, 2), Sides.PortFirst);

        lanes.Splits(0, 3).Should().BeFalse();
        lanes.Splits(1, 2).Should().BeFalse();
        lanes.Splits(2, 2).Should().BeTrue();
        lanes.Splits(3, 1).Should().BeFalse();
        lanes.Splits(3, 2).Should().BeTrue();
        lanes.Splits(4, 3).Should().BeFalse();
    }

    // --- The cell order ---

    [Fact]
    public void Crosswise_port_first_fills_the_port_lane_row_by_row_from_its_outer_edge_then_the_starboard_lane()
    {
        var rack = Flat(3, 4, 2);

        Layer0(CrateLayout.Ranks(rack, FillOrder.BowFirst, FillRows.Crosswise, LanesOf(rack, Sides.PortFirst))).Should().BeEquivalentTo(new[,]
        {
            { 4, 2, 0 },
            { 5, 3, 1 },
            { 17, 15, 13 },
            { 16, 14, 12 },
        }, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Lengthwise_port_first_runs_strips_from_the_port_edge_inwards_then_from_the_starboard_edge()
    {
        var rack = Flat(3, 4, 2);

        Layer0(CrateLayout.Ranks(rack, FillOrder.BowFirst, FillRows.Lengthwise, LanesOf(rack, Sides.PortFirst))).Should().BeEquivalentTo(new[,]
        {
            { 2, 1, 0 },
            { 5, 4, 3 },
            { 17, 16, 15 },
            { 14, 13, 12 },
        }, o => o.WithStrictOrdering());
    }

    [Theory]
    [InlineData(FillOrder.BowFirst, FillRows.Crosswise)]
    [InlineData(FillOrder.SternFirst, FillRows.Lengthwise)]
    public void A_lane_fills_both_layers_before_the_next_lane_starts(FillOrder order, FillRows rows)
    {
        var rack = Raft.Racks[0];
        var lanes = LanesOf(rack, Sides.PortFirst);
        var rank = CrateLayout.Ranks(rack, order, rows, lanes);

        var first = CellsOf(new GridPosition(0, 0, 0), rack.Grid).Where(c => lanes.LaneOf(c.Width) == 0).Max(c => rank[c.Length, c.Width, c.Stack]);
        var second = CellsOf(new GridPosition(0, 0, 0), rack.Grid).Where(c => lanes.LaneOf(c.Width) == 1).Min(c => rank[c.Length, c.Width, c.Stack]);

        first.Should().Be(rack.Grid.Volume / 2 - 1);
        second.Should().Be(rack.Grid.Volume / 2);
        CellsOf(new GridPosition(0, 0, 0), rack.Grid).Where(c => lanes.LaneOf(c.Width) == 0 && c.Stack == 0)
            .Max(c => rank[c.Length, c.Width, c.Stack]).Should().Be(rack.Grid.Volume / 4 - 1, "layer by layer within the lane");
    }

    [Theory]
    [InlineData(FillOrder.BowFirst, FillRows.Crosswise)]
    [InlineData(FillOrder.BowFirst, FillRows.Lengthwise)]
    [InlineData(FillOrder.SternFirst, FillRows.Crosswise)]
    [InlineData(FillOrder.SternFirst, FillRows.Lengthwise)]
    public void Starboard_first_is_the_mirror_image_of_port_first(FillOrder order, FillRows rows)
    {
        var rack = Flat(5, 7, 2);
        var port = CrateLayout.Ranks(rack, order, rows, LanesOf(rack, Sides.PortFirst));
        var starboard = CrateLayout.Ranks(rack, order, rows, LanesOf(rack, Sides.StarboardFirst));

        foreach (var cell in CellsOf(new GridPosition(0, 0, 0), rack.Grid))
        {
            starboard[cell.Length, cell.Width, cell.Stack].Should().Be(port[cell.Length, 6 - cell.Width, cell.Stack], $"cell {cell}");
        }
    }

    [Fact]
    public void The_middle_column_comes_after_both_lanes()
    {
        var rack = Flat(6, 7, 2);
        var lanes = LanesOf(rack, Sides.PortFirst);
        var rank = CrateLayout.Ranks(rack, FillOrder.BowFirst, FillRows.Crosswise, lanes);

        CellsOf(new GridPosition(0, 0, 0), rack.Grid).Where(c => c.Width == 3).Min(c => rank[c.Length, c.Width, c.Stack])
            .Should().Be(72, "6 × 3 × 2 cells a side");
    }

    // --- Laying crates in lanes ---

    [Fact]
    public void Crates_that_fit_the_first_lane_all_lie_in_it()
    {
        var rack = Raft.Racks[0];
        var laid = CrateLayout.Lay(rack, [], [.. Enumerable.Repeat((CrateSize.Four, "A"), 12)], FillOrder.BowFirst, FillRows.Crosswise, lanes: LanesOf(rack, Sides.PortFirst))!;

        laid.Poses.SelectMany(p => CellsOf(p.Position, CrateSize.Four.Shape(p.Rotated))).Should().OnlyContain(c => c.Width >= 4);
    }

    [Fact]
    public void Crates_beyond_the_first_lane_go_into_the_second_once_the_first_is_full()
    {
        var rack = Raft.Racks[0];
        var laid = CrateLayout.Lay(rack, [], [.. Enumerable.Repeat((CrateSize.Four, "A"), 30)], FillOrder.BowFirst, FillRows.Crosswise, lanes: LanesOf(rack, Sides.PortFirst))!;
        GridPosition[] cells = [.. laid.Poses.SelectMany(p => CellsOf(p.Position, CrateSize.Four.Shape(p.Rotated)))];

        cells.Count(c => c.Width >= 4).Should().Be(96);
        cells.Count(c => c.Width < 4).Should().Be(24);
    }

    [Fact]
    public void Starboard_first_puts_the_same_crates_on_the_starboard_half()
    {
        var rack = Raft.Racks[0];
        var laid = CrateLayout.Lay(rack, [], [.. Enumerable.Repeat((CrateSize.Four, "A"), 12)], FillOrder.BowFirst, FillRows.Crosswise, lanes: LanesOf(rack, Sides.StarboardFirst))!;

        laid.Poses.SelectMany(p => CellsOf(p.Position, CrateSize.Four.Shape(p.Rotated))).Should().OnlyContain(c => c.Width < 4);
    }

    [Fact]
    public void Single_crates_reach_the_middle_column_only_after_both_lanes()
    {
        var rack = Flat(6, 7, 2);
        var laid = CrateLayout.Lay(rack, [], [.. Enumerable.Repeat((CrateSize.One, "A"), 84)], FillOrder.BowFirst, FillRows.Crosswise, lanes: LanesOf(rack, Sides.PortFirst))!;

        laid.Poses.Take(72).Should().OnlyContain(p => p.Position.Width != 3);
        laid.Poses.Skip(72).Should().OnlyContain(p => p.Position.Width == 3);
    }

    [Fact]
    public void A_four_never_lies_in_the_middle_column_or_across_a_lane_boundary()
    {
        var rack = Flat(6, 7, 2);
        var lanes = LanesOf(rack, Sides.PortFirst);
        var space = CrateLayout.RackSpace.Around(rack, [], FillOrder.BowFirst, FillRows.Crosswise, lanes: lanes);
        List<Pose> fours = [];
        while (space.Add(CrateSize.Four) is { } pose)
        {
            fours.Add(pose);
        }

        fours.Should().HaveCount(12, "a lane of width 3 takes one 2-wide four per two cells of length and layer");
        fours.Should().OnlyContain(p => !lanes.Splits(p.Position.Width, CrateSize.Four.Shape(p.Rotated).Width));
        fours.SelectMany(p => CellsOf(p.Position, CrateSize.Four.Shape(p.Rotated))).Should().OnlyContain(c => c.Width != 3);
    }

    [Fact]
    public void A_turned_two_never_lies_in_the_middle_column()
    {
        // Length 1: a 2 can only lie turned, two cells wide.
        var rack = Flat(1, 7, 1);
        (CrateSize, string)[] twos = [(CrateSize.Two, "A"), (CrateSize.Two, "A"), (CrateSize.Two, "A")];

        CrateLayout.Lay(rack, [], twos, FillOrder.BowFirst, FillRows.Crosswise).Should().NotBeNull("three fit across seven cells without lanes");
        CrateLayout.Lay(rack, [], twos, FillOrder.BowFirst, FillRows.Crosswise, lanes: LanesOf(rack, Sides.PortFirst))
            .Should().BeNull("one a side, none in the middle column");
    }

    [Fact]
    public void Without_lanes_the_cell_order_is_as_before()
    {
        var rack = Raft.Racks[0];

        CrateLayout.Ranks(rack, FillOrder.BowFirst, FillRows.Crosswise, null).Should().BeEquivalentTo(CrateLayout.Ranks(rack, FillOrder.BowFirst, FillRows.Crosswise));
    }

    /// <summary>
    /// Seeded mixes of one to three destinations on the RAFT and on odd widths, both sides, both row axes and both fill
    /// orders: no crate lies across a lane boundary, also where the exact search for a layout without foreign stacking ran.
    /// </summary>
    [Fact]
    public void No_crate_ever_lies_across_a_lane_boundary()
    {
        Rack[] racks = [Raft.Racks[0], Flat(6, 7, 2), Flat(5, 5, 2)];
        CrateSize[] sizes = [CrateSize.One, CrateSize.Two, CrateSize.Four, CrateSize.Eight, CrateSize.Sixteen];
        var random = new Random(116);
        var laidOut = 0;
        for (var run = 0; run < 200; run++)
        {
            var rack = racks[run % racks.Length];
            var sides = run % 2 == 0 ? Sides.PortFirst : Sides.StarboardFirst;
            var rows = run / 2 % 2 == 0 ? FillRows.Crosswise : FillRows.Lengthwise;
            var order = run / 4 % 2 == 0 ? FillOrder.BowFirst : FillOrder.SternFirst;
            List<(CrateSize Size, string Destination)> crates = [];
            var budget = random.Next(rack.Grid.Volume / 4, rack.Grid.Volume * 3 / 4);
            while (crates.Sum(c => (int)c.Size) < budget)
            {
                crates.Add((sizes[random.Next(sizes.Length)], ((char)('A' + random.Next(3))).ToString()));
            }

            var lanes = LanesOf(rack, sides);
            if (CrateLayout.Lay(rack, [], crates, order, rows, lanes: lanes) is not { } laid)
            {
                continue;
            }

            laidOut++;
            foreach (var (crate, pose) in crates.Zip(laid.Poses))
            {
                var shape = crate.Size.Shape(pose.Rotated);
                lanes.Splits(pose.Position.Width, shape.Width).Should().BeFalse($"run {run}: {crate.Size} at {pose.Position} on {rack.Grid}");
            }
        }

        laidOut.Should().BeGreaterThan(150, "most mixes fit");
    }
}
