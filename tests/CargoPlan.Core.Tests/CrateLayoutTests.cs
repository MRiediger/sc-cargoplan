using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class CrateLayoutTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    // Port upper (y 1..3) and starboard upper (y −2..0), mirror images across the spindle.
    private static readonly Rack Rack1 = HullA.Racks[0];
    private static readonly Rack Rack3 = HullA.Racks[2];

    // A 3×2×2 rack (12 SCU): an 8 SCU crate leaves a strip one cell long, too short for a 4 SCU crate.
    private static readonly Rack Short = new("short", 0, 12, new Dimensions(3, 2, 2), new RackPosition(RackSide.Port, RackLevel.Upper), new ShipPoint(0, 1, 0));

    private static Crate At(CrateSize size, int length, int width, int stack, string destination = "Riker", bool delivered = false) =>
        new(destination, "Titanium", size, Rack1.Id, new GridPosition(length, width, stack), delivered);

    /// <summary>Lays out <paramref name="sizes"/> in an empty rack and returns them as crates, in input order.</summary>
    private static Crate[] Laid(Rack rack, FillOrder order, params CrateSize[] sizes)
    {
        var positions = CrateLayout.Place(rack, [], sizes, order);

        positions.Should().NotBeNull();
        return [.. sizes.Zip(positions!).Select(p => new Crate("Riker", "Titanium", p.First, rack.Id, p.Second))];
    }

    private static IEnumerable<GridPosition> Layer(IEnumerable<Crate> crates, int stack) =>
        crates.SelectMany(c => c.Cells).Where(c => c.Stack == stack);

    private static CrateSize[] Repeat(CrateSize size, int count) => [.. Enumerable.Repeat(size, count)];

    // --- Place: the drawing rule of the ship view, now in Core (D21, D24) ---

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Crates_of_4_4_and_8_fill_the_rack_without_gaps(FillOrder order)
    {
        // The 8 SCU crate comes last; the largest crate goes first, so it still fits.
        var crates = Laid(Rack1, order, CrateSize.Four, CrateSize.Four, CrateSize.Eight);

        crates.SelectMany(c => c.Cells).Should().HaveCount(16).And.OnlyHaveUniqueItems();
        CrateLayout.Problems(Rack1, crates).Should().BeEmpty();
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Four_2_SCU_crates_fill_the_bottom_layer(FillOrder order)
    {
        var crates = Laid(Rack1, order, Repeat(CrateSize.Two, 4));

        Layer(crates, 0).Should().HaveCount(8);
        Layer(crates, 1).Should().BeEmpty();
    }

    [Fact]
    public void The_largest_crate_goes_first_whatever_the_input_order()
    {
        var crates = Laid(Rack1, FillOrder.BowFirst, CrateSize.Four, CrateSize.Four, CrateSize.Eight);

        // Bow first: the 8 takes the bow end (length 2..3), the 4s the stern half.
        crates[2].Position.Should().Be(new GridPosition(2, 0, 0));
        crates[0].Position.Should().Be(new GridPosition(0, 0, 0));
        crates[1].Position.Should().Be(new GridPosition(0, 0, 1));
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Twelve_1_SCU_crates_fill_the_first_layer_and_one_row_of_the_second(FillOrder order)
    {
        var crates = Laid(Rack1, order, Repeat(CrateSize.One, 12));

        Layer(crates, 0).Should().HaveCount(8);
        Layer(crates, 1).Select(c => c.Length).Should().BeEquivalentTo([0, 1, 2, 3]);
        Layer(crates, 1).Should().OnlyContain(c => c.Width == 0);
    }

    [Fact]
    public void Bow_first_starts_a_row_at_the_bow_end()
    {
        // Length runs along x towards the bow: index 3 is the bow end.
        var crates = Laid(Rack1, FillOrder.BowFirst, Repeat(CrateSize.One, 10));

        crates.Skip(8).Select(c => c.Position.Length).Should().Equal(3, 2);
    }

    [Fact]
    public void Stern_first_starts_a_row_at_the_stern_end()
    {
        var crates = Laid(Rack1, FillOrder.SternFirst, Repeat(CrateSize.One, 10));

        crates.Skip(8).Select(c => c.Position.Length).Should().Equal(0, 1);
    }

    [Fact]
    public void Rows_next_to_the_centreline_fill_first_on_both_sides()
    {
        // Rack 1's row 0 is y 1..2, Rack 3's row 1 is y −1..0: both next to the spindle, so the halves mirror.
        Layer(Laid(Rack1, FillOrder.BowFirst, Repeat(CrateSize.One, 10)), 1).Should().OnlyContain(c => c.Width == 0);
        Layer(Laid(Rack3, FillOrder.BowFirst, Repeat(CrateSize.One, 10)), 1).Should().OnlyContain(c => c.Width == 1);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Sixteen_1_SCU_crates_take_every_cell_once(FillOrder order)
    {
        Laid(Rack1, order, Repeat(CrateSize.One, 16)).Select(c => c.Position).Should().OnlyHaveUniqueItems().And.HaveCount(16);
    }

    [Fact]
    public void Place_puts_crates_around_occupied_cells()
    {
        // The bow half is taken by an 8; two 4s fit only on the stern half, one above the other.
        Crate[] occupied = [At(CrateSize.Eight, 2, 0, 0)];

        var positions = CrateLayout.Place(Rack1, occupied, [CrateSize.Four, CrateSize.Four], FillOrder.BowFirst);

        positions.Should().Equal(new GridPosition(0, 0, 0), new GridPosition(0, 0, 1));
    }

    [Fact]
    public void Place_counts_delivered_crates_as_occupied()
    {
        // Delivered cargo keeps its cells: unticking it must give a valid plan again (D16, D24).
        Crate[] occupied = [At(CrateSize.Sixteen, 0, 0, 0, delivered: true)];

        CrateLayout.Place(Rack1, occupied, [CrateSize.One], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_returns_null_when_a_crate_has_no_room()
    {
        CrateLayout.Place(Rack1, [], [CrateSize.Eight, CrateSize.Eight, CrateSize.One], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_returns_null_for_a_crate_the_grid_cannot_hold()
    {
        // 8 + 4 = 12 SCU fit the short rack by volume, but the 4 has only a strip one cell long left.
        CrateLayout.Place(Short, [], [CrateSize.Eight, CrateSize.Four], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_never_lets_a_crate_float()
    {
        // 1s on every odd length of the bottom: no two free bottom cells are neighbours, so a 2 doesn't fit below,
        // and every place above would bridge an empty cell.
        Crate[] occupied = [At(CrateSize.One, 1, 0, 0), At(CrateSize.One, 3, 0, 0), At(CrateSize.One, 1, 1, 0), At(CrateSize.One, 3, 1, 0)];

        CrateLayout.Place(Rack1, occupied, [CrateSize.Two], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_stacks_on_other_crates()
    {
        // Bottom full: the next crate lies on top, where the crates below carry it.
        Crate[] occupied = [At(CrateSize.Four, 0, 0, 0), At(CrateSize.Four, 2, 0, 0)];

        var positions = CrateLayout.Place(Rack1, occupied, [CrateSize.Four], FillOrder.BowFirst);

        positions.Should().Equal(new GridPosition(2, 0, 1));
    }

    /// <summary>
    /// D9 with the support rule: every mix of 1, 2, 4, 8 and 16 SCU crates up to 16 SCU fits a Hull A rack, in both fill
    /// orders, on both sides. If one fails, the planner's sum check (D6) is no longer exact for the Hull A.
    /// </summary>
    [Fact]
    public void Every_mix_up_to_16_SCU_fits_a_Hull_A_rack()
    {
        var mixes = Mixes(16, [CrateSize.Sixteen, CrateSize.Eight, CrateSize.Four, CrateSize.Two, CrateSize.One]).ToArray();

        List<string> failed = [];
        foreach (var rack in new[] { Rack1, Rack3 })
        {
            foreach (var order in Enum.GetValues<FillOrder>())
            {
                foreach (var mix in mixes)
                {
                    var positions = CrateLayout.Place(rack, [], mix, order);
                    Crate[] crates = positions is null ? [] : [.. mix.Zip(positions).Select(p => new Crate("Riker", "Titanium", p.First, rack.Id, p.Second))];
                    if (positions is null || CrateLayout.Problems(rack, crates).Count > 0)
                    {
                        failed.Add($"rack {rack.Number}, {order}: {string.Join("+", mix.Select(s => (int)s))}");
                    }
                }
            }
        }

        mixes.Should().HaveCountGreaterThan(100);
        failed.Should().BeEmpty();
    }

    /// <summary>Every multiset of <paramref name="sizes"/> (largest first) with a sum of at most <paramref name="room"/>, the empty one included.</summary>
    private static IEnumerable<CrateSize[]> Mixes(int room, CrateSize[] sizes)
    {
        if (sizes.Length == 0)
        {
            yield return [];
            yield break;
        }

        for (var count = 0; count * (int)sizes[0] <= room; count++)
        {
            foreach (var rest in Mixes(room - (count * (int)sizes[0]), sizes[1..]))
            {
                yield return [.. Enumerable.Repeat(sizes[0], count), .. rest];
            }
        }
    }

    // --- Problems ---

    [Fact]
    public void Problems_of_a_laid_out_rack_are_none()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 2, 0, 0), At(CrateSize.Four, 0, 0, 0), At(CrateSize.Four, 0, 0, 1)]).Should().BeEmpty();
    }

    [Fact]
    public void Problems_name_overlapping_crates()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 2, 0, 0, "Riker"), At(CrateSize.Two, 2, 0, 1, "Shubin")])
            .Should().Equal("Crates of Riker and Shubin overlap in rack 1");
    }

    [Fact]
    public void Problems_name_a_crate_without_support()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.One, 0, 0, 1)])
            .Should().Equal("1 SCU crate of Titanium for Riker in rack 1 has nothing below it");
    }

    [Fact]
    public void Problems_count_a_delivered_crate_as_support()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.One, 0, 0, 0, delivered: true), At(CrateSize.One, 0, 0, 1)]).Should().BeEmpty();
    }

    [Fact]
    public void A_crate_standing_on_itself_is_not_floating()
    {
        // An 8 is two cells high: its upper half rests on its own lower half, which lies on the plate.
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 0, 0, 0)]).Should().BeEmpty();
    }

    [Fact]
    public void Problems_name_a_crate_outside_the_grid()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Four, 3, 0, 0)])
            .Should().Equal("4 SCU crate of Titanium for Riker at [3, 0, 0] lies outside rack 1 (4×2×2)");
    }

    [Fact]
    public void Problems_name_a_crate_the_grid_cannot_hold()
    {
        CrateLayout.Problems(Short, [new Crate("Riker", "Titanium", CrateSize.Sixteen, Short.Id, new GridPosition(0, 0, 0))])
            .Should().Equal("16 SCU crate of Titanium for Riker doesn't fit rack 1 (3×2×2)");
    }

    // --- Faulty: the crates the problems name, for the ship view (T3.13b) ---

    [Fact]
    public void Faulty_of_a_laid_out_rack_is_empty()
    {
        CrateLayout.Faulty(Rack1, [At(CrateSize.Eight, 2, 0, 0), At(CrateSize.Four, 0, 0, 0), At(CrateSize.Four, 0, 0, 1)]).Should().BeEmpty();
    }

    [Fact]
    public void Faulty_holds_both_overlapping_crates_and_not_the_others()
    {
        var (riker, shubin, orison) = (At(CrateSize.Eight, 2, 0, 0, "Riker"), At(CrateSize.Two, 2, 0, 1, "Shubin"), At(CrateSize.Four, 0, 0, 0, "Orison"));

        CrateLayout.Faulty(Rack1, [riker, shubin, orison]).Should().BeEquivalentTo([riker, shubin]);
    }

    [Fact]
    public void Faulty_holds_a_crate_without_support_but_not_the_one_beside_it()
    {
        var (floating, below) = (At(CrateSize.One, 0, 0, 1, "Riker"), At(CrateSize.One, 1, 0, 0, "Shubin"));

        CrateLayout.Faulty(Rack1, [floating, below]).Should().BeEquivalentTo([floating]);
    }

    [Fact]
    public void Faulty_counts_a_delivered_crate_as_support()
    {
        CrateLayout.Faulty(Rack1, [At(CrateSize.One, 0, 0, 0, delivered: true), At(CrateSize.One, 0, 0, 1)]).Should().BeEmpty();
    }

    [Fact]
    public void Faulty_holds_a_crate_outside_the_grid()
    {
        var outside = At(CrateSize.Four, 3, 0, 0);

        CrateLayout.Faulty(Rack1, [outside]).Should().BeEquivalentTo([outside]);
    }

    [Fact]
    public void Fits_is_true_only_inside_the_grid()
    {
        CrateLayout.Fits(Rack1, At(CrateSize.Four, 2, 0, 1)).Should().BeTrue();
        CrateLayout.Fits(Rack1, At(CrateSize.Four, 3, 0, 1)).Should().BeFalse();
        CrateLayout.Fits(Rack1, At(CrateSize.Eight, 0, 0, 1)).Should().BeFalse();
    }

    // --- LayOut: placements of a whole ship, the one path for planner and plan file migration ---

    [Fact]
    public void LayOut_gives_every_placement_its_crates_in_plan_order()
    {
        Placement[] placements =
        [
            new("Riker", "rack-1", new CargoItem("Quartz", 4, CrateSize.Two)),
            new("Shubin", "rack-1", new CargoItem("Agricium", 8, CrateSize.Eight)),
            new("Riker", "rack-0", new CargoItem("Titanium", 16, CrateSize.Four), Delivered: true),
        ];

        var crates = CrateLayout.LayOut(HullA, placements, FillOrder.BowFirst);

        crates.Select(c => (c.Destination, c.Commodity, c.RackId, c.Delivered)).Should().Equal(
            ("Riker", "Quartz", "rack-1", false), ("Riker", "Quartz", "rack-1", false),
            ("Shubin", "Agricium", "rack-1", false),
            ("Riker", "Titanium", "rack-0", true), ("Riker", "Titanium", "rack-0", true),
            ("Riker", "Titanium", "rack-0", true), ("Riker", "Titanium", "rack-0", true));
        HullA.Racks.Should().OnlyContain(r => CrateLayout.Problems(r, crates.Where(c => c.RackId == r.Id).ToArray()).Count == 0);
    }

    [Fact]
    public void LayOut_throws_when_a_rack_holds_more_than_its_grid()
    {
        // An overloaded rack, allowed in schema 1 plan files (D10), has no layout.
        Placement[] placements = [new("Riker", "rack-0", new CargoItem("Titanium", 20, CrateSize.Four))];

        var act = () => CrateLayout.LayOut(HullA, placements, FillOrder.BowFirst);

        act.Should().Throw<PlanException>().WithMessage("*rack 1*20/16 SCU*");
    }
}
