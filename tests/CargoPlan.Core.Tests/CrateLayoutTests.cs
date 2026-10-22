using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

public class CrateLayoutTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    // Port upper (stacks to +y) and starboard upper (stacks to −y), mirror images across the spindle (D26).
    private static readonly Rack Rack1 = HullA.Racks[0];
    private static readonly Rack Rack3 = HullA.Racks[2];

    // A 3×2×2 rack (12 SCU): an 8 SCU crate leaves a strip one cell long, too short for a 4 SCU crate.
    private static readonly Rack Short = new("short", 0, 12, new Dimensions(3, 2, 2), new RackPosition(RackSide.Port, RackLevel.Upper), new ShipPoint(0, 1, 0));

    private static Crate At(CrateSize size, int length, int width, int stack, string destination = "Riker", bool delivered = false) =>
        new(destination, "Titanium", size, Rack1.Id, new GridPosition(length, width, stack), delivered);

    /// <summary>Lays out <paramref name="sizes"/> in an empty rack and returns them as crates, in input order.</summary>
    private static Crate[] Laid(Rack rack, FillOrder order, params CrateSize[] sizes)
    {
        var positions = CrateLayout.Place(rack, [], Riker(sizes), [], order);

        positions.Should().NotBeNull();
        return [.. sizes.Zip(positions!).Select(p => new Crate("Riker", "Titanium", p.First, rack.Id, p.Second))];
    }

    private static IEnumerable<GridPosition> Layer(IEnumerable<Crate> crates, int stack) =>
        crates.SelectMany(c => c.Cells).Where(c => c.Stack == stack);

    private static CrateSize[] Repeat(CrateSize size, int count) => [.. Enumerable.Repeat(size, count)];

    /// <summary>Crates to place, all of one stop.</summary>
    private static (CrateSize Size, string Destination)[] Riker(params CrateSize[] sizes) => [.. sizes.Select(s => (s, "Riker"))];

    /// <summary>Crates to place, one stop per letter: "A" is the first stop of the route.</summary>
    private static (CrateSize Size, string Destination)[] Of(params (CrateSize Size, string Destination)[] crates) => crates;

    private static Crate[] AsCrates(Rack rack, IReadOnlyList<(CrateSize Size, string Destination)> crates, IReadOnlyList<GridPosition> positions) =>
        [.. crates.Zip(positions).Select(p => new Crate(p.First.Destination, "Titanium", p.First.Size, rack.Id, p.Second))];

    private static readonly string[] AbcRoute = ["A", "B", "C"];

    // An expression tree (OnlyContain) can't hold a collection expression.
    private static readonly string[] NoRoute = [];

    // --- Place: the drawing rule of the ship view, now in Core (D21, D24) ---

    [Theory]
    [InlineData(FillOrder.BowFirst)]
    [InlineData(FillOrder.SternFirst)]
    public void Crates_of_4_4_and_8_fill_the_rack_without_gaps(FillOrder order)
    {
        // The 8 SCU crate comes last; the largest crate goes first, so it still fits.
        var crates = Laid(Rack1, order, CrateSize.Four, CrateSize.Four, CrateSize.Eight);

        crates.SelectMany(c => c.Cells).Should().HaveCount(16).And.OnlyHaveUniqueItems();
        CrateLayout.Problems(Rack1, crates, []).Should().BeEmpty();
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
    public void Rows_fill_in_grid_order_on_both_sides()
    {
        // The layout knows only the grid (D26): the halves mirror through the stack axis (+y, −y), not the row order.
        Layer(Laid(Rack1, FillOrder.BowFirst, Repeat(CrateSize.One, 10)), 1).Should().OnlyContain(c => c.Width == 0);
        Layer(Laid(Rack3, FillOrder.BowFirst, Repeat(CrateSize.One, 10)), 1).Should().OnlyContain(c => c.Width == 0);
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

        var positions = CrateLayout.Place(Rack1, occupied, Riker(CrateSize.Four, CrateSize.Four), [], FillOrder.BowFirst);

        positions.Should().Equal(new GridPosition(0, 0, 0), new GridPosition(0, 0, 1));
    }

    [Fact]
    public void Place_lays_crates_into_the_cells_of_delivered_cargo()
    {
        // Delivered cargo is off the ship; its cells are free (D24 addendum).
        Crate[] occupied = [At(CrateSize.Sixteen, 0, 0, 0, delivered: true)];

        CrateLayout.Place(Rack1, occupied, Riker(CrateSize.Sixteen), [], FillOrder.BowFirst)
            .Should().Equal(CrateLayout.Place(Rack1, [], Riker(CrateSize.Sixteen), [], FillOrder.BowFirst));
    }

    [Fact]
    public void Place_returns_null_when_a_crate_has_no_room()
    {
        CrateLayout.Place(Rack1, [], Riker(CrateSize.Eight, CrateSize.Eight, CrateSize.One), [], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_returns_null_for_a_crate_the_grid_cannot_hold()
    {
        // 8 + 4 = 12 SCU fit the short rack by volume, but the 4 has only a strip one cell long left.
        CrateLayout.Place(Short, [], Riker(CrateSize.Eight, CrateSize.Four), [], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_never_lets_a_crate_float()
    {
        // 1s on every odd length of the bottom: no two free bottom cells are neighbours, so a 2 doesn't fit below,
        // and every place above would bridge an empty cell.
        Crate[] occupied = [At(CrateSize.One, 1, 0, 0), At(CrateSize.One, 3, 0, 0), At(CrateSize.One, 1, 1, 0), At(CrateSize.One, 3, 1, 0)];

        CrateLayout.Place(Rack1, occupied, Riker(CrateSize.Two), [], FillOrder.BowFirst).Should().BeNull();
    }

    [Fact]
    public void Place_stacks_on_other_crates()
    {
        // Bottom full: the next crate lies on top, where the crates below carry it.
        Crate[] occupied = [At(CrateSize.Four, 0, 0, 0), At(CrateSize.Four, 2, 0, 0)];

        var positions = CrateLayout.Place(Rack1, occupied, Riker(CrateSize.Four), [], FillOrder.BowFirst);

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
                    var positions = CrateLayout.Place(rack, [], Riker(mix), [], order);
                    Crate[] crates = positions is null ? [] : [.. mix.Zip(positions).Select(p => new Crate("Riker", "Titanium", p.First, rack.Id, p.Second))];
                    if (positions is null || CrateLayout.Problems(rack, crates, []).Count > 0)
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
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 2, 0, 0), At(CrateSize.Four, 0, 0, 0), At(CrateSize.Four, 0, 0, 1)], []).Should().BeEmpty();
    }

    [Fact]
    public void Problems_name_overlapping_crates()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 2, 0, 0, "Riker"), At(CrateSize.Two, 2, 0, 1, "Shubin")], [])
            .Should().Equal("Crates of Riker and Shubin overlap in rack 1");
    }

    [Fact]
    public void Problems_name_a_crate_without_support()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.One, 0, 0, 1)], [])
            .Should().Equal("1 SCU crate of Titanium for Riker in rack 1 has nothing below it");
    }

    [Fact]
    public void Problems_dont_count_a_delivered_crate_as_support()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.One, 0, 0, 0, delivered: true), At(CrateSize.One, 0, 0, 1)], [])
            .Should().Equal("1 SCU crate of Titanium for Riker in rack 1 has nothing below it");
    }

    [Fact]
    public void Problems_ignore_overlap_with_delivered_cargo()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 0, 0, 0, delivered: true), At(CrateSize.Two, 0, 0, 0, "Shubin")], []).Should().BeEmpty();
    }

    [Fact]
    public void Problems_skip_delivered_crates_themselves()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.One, 0, 0, 1, delivered: true)], []).Should().BeEmpty();
    }

    [Fact]
    public void A_crate_standing_on_itself_is_not_floating()
    {
        // An 8 is two cells high: its upper half rests on its own lower half, which lies on the plate.
        CrateLayout.Problems(Rack1, [At(CrateSize.Eight, 0, 0, 0)], []).Should().BeEmpty();
    }

    [Fact]
    public void Problems_name_a_crate_outside_the_grid()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Four, 3, 0, 0)], [])
            .Should().Equal("4 SCU crate of Titanium for Riker at [3, 0, 0] lies outside rack 1 (4×2×2)");
    }

    [Fact]
    public void Problems_name_a_crate_the_grid_cannot_hold()
    {
        CrateLayout.Problems(Short, [new Crate("Riker", "Titanium", CrateSize.Sixteen, Short.Id, new GridPosition(0, 0, 0))], [])
            .Should().Equal("16 SCU crate of Titanium for Riker doesn't fit rack 1 (3×2×2)");
    }

    // --- Faulty: the crates the problems name, for the ship view (T3.13b) ---

    [Fact]
    public void Faulty_of_a_laid_out_rack_is_empty()
    {
        CrateLayout.Faulty(Rack1, [At(CrateSize.Eight, 2, 0, 0), At(CrateSize.Four, 0, 0, 0), At(CrateSize.Four, 0, 0, 1)], []).Should().BeEmpty();
    }

    [Fact]
    public void Faulty_holds_both_overlapping_crates_and_not_the_others()
    {
        var (riker, shubin, orison) = (At(CrateSize.Eight, 2, 0, 0, "Riker"), At(CrateSize.Two, 2, 0, 1, "Shubin"), At(CrateSize.Four, 0, 0, 0, "Orison"));

        CrateLayout.Faulty(Rack1, [riker, shubin, orison], []).Should().BeEquivalentTo([riker, shubin]);
    }

    [Fact]
    public void Faulty_holds_a_crate_without_support_but_not_the_one_beside_it()
    {
        var (floating, below) = (At(CrateSize.One, 0, 0, 1, "Riker"), At(CrateSize.One, 1, 0, 0, "Shubin"));

        CrateLayout.Faulty(Rack1, [floating, below], []).Should().BeEquivalentTo([floating]);
    }

    [Fact]
    public void Faulty_names_the_crate_on_delivered_cargo_not_the_delivered_one()
    {
        var upper = At(CrateSize.One, 0, 0, 1);

        CrateLayout.Faulty(Rack1, [At(CrateSize.One, 0, 0, 0, delivered: true), upper], []).Should().BeEquivalentTo([upper]);
    }

    [Fact]
    public void Faulty_holds_a_crate_outside_the_grid()
    {
        var outside = At(CrateSize.Four, 3, 0, 0);

        CrateLayout.Faulty(Rack1, [outside], []).Should().BeEquivalentTo([outside]);
    }

    // --- NewProblem: what a move breaks, from the same findings as Problems (D25) ---

    [Fact]
    public void NewProblem_of_an_unchanged_rack_is_null_even_if_it_has_problems()
    {
        // A plan file edited by hand: the overlap was there before the move, the move didn't make it.
        Crate[] crates = [At(CrateSize.Eight, 0, 0, 0), At(CrateSize.Two, 0, 0, 1, "Shubin")];

        CrateLayout.NewProblem(Rack1, crates, crates).Should().BeNull();
    }

    [Fact]
    public void NewProblem_names_a_crate_moved_onto_another()
    {
        var riker = At(CrateSize.Eight, 0, 0, 0);

        CrateLayout.NewProblem(Rack1, [riker], [riker, At(CrateSize.Two, 0, 0, 1, "Shubin")])
            .Should().Be("Crates of Riker and Shubin overlap in rack 1");
    }

    [Fact]
    public void NewProblem_names_a_crate_that_loses_its_support()
    {
        var below = At(CrateSize.Four, 0, 0, 0);
        var above = At(CrateSize.Four, 0, 0, 1, "Shubin");

        CrateLayout.NewProblem(Rack1, [below, above], [above]).Should().Be("4 SCU crate of Titanium for Shubin in rack 1 has nothing below it");
    }

    [Fact]
    public void NewProblem_names_a_crate_moved_outside_the_grid()
    {
        CrateLayout.NewProblem(Rack1, [], [At(CrateSize.Four, 3, 0, 0)])
            .Should().Be("4 SCU crate of Titanium for Riker at [3, 0, 0] lies outside rack 1 (4×2×2)");
    }

    [Fact]
    public void Fits_is_true_only_inside_the_grid()
    {
        CrateLayout.Fits(Rack1, At(CrateSize.Four, 2, 0, 1)).Should().BeTrue();
        CrateLayout.Fits(Rack1, At(CrateSize.Four, 3, 0, 1)).Should().BeFalse();
        CrateLayout.Fits(Rack1, At(CrateSize.Eight, 0, 0, 1)).Should().BeFalse();
    }

    // --- Stacking order: never a later stop on an earlier one (D24 addendum, T5.4a) ---

    [Fact]
    public void Problems_name_a_later_stop_lying_on_an_earlier_one()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Four, 0, 0, 0, "Riker"), At(CrateSize.Four, 0, 0, 1, "Shubin")], ["Riker", "Shubin"])
            .Should().Equal("4 SCU crate of Titanium for Shubin in rack 1 lies on cargo for Riker, which is unloaded first");
    }

    [Fact]
    public void Problems_accept_an_earlier_stop_on_a_later_one()
    {
        // The upper crate goes first: that is the right order.
        CrateLayout.Problems(Rack1, [At(CrateSize.Four, 0, 0, 0, "Shubin"), At(CrateSize.Four, 0, 0, 1, "Riker")], ["Riker", "Shubin"])
            .Should().BeEmpty();
    }

    [Fact]
    public void Problems_know_no_order_on_delivered_cargo()
    {
        // Delivered cargo carries nothing, so the crate floats; there is no order to break (D24 addendum).
        CrateLayout.Problems(Rack1, [At(CrateSize.Four, 0, 0, 0, "Riker", delivered: true), At(CrateSize.Four, 0, 0, 1, "Shubin")], ["Riker", "Shubin"])
            .Should().Equal("4 SCU crate of Titanium for Shubin in rack 1 has nothing below it");
    }

    [Fact]
    public void Problems_without_a_route_know_no_order()
    {
        CrateLayout.Problems(Rack1, [At(CrateSize.Four, 0, 0, 0, "Riker"), At(CrateSize.Four, 0, 0, 1, "Shubin")], []).Should().BeEmpty();
    }

    [Fact]
    public void Faulty_holds_the_later_crate_on_top_not_the_one_below()
    {
        var (riker, shubin) = (At(CrateSize.Four, 0, 0, 0, "Riker"), At(CrateSize.Four, 0, 0, 1, "Shubin"));

        CrateLayout.Faulty(Rack1, [riker, shubin], ["Riker", "Shubin"]).Should().BeEquivalentTo([shubin]);
    }

    [Fact]
    public void NewProblem_never_refuses_for_the_order()
    {
        // Laying a later stop on an earlier one is possible, only tedious at the elevator: a finding, not a refusal.
        var riker = At(CrateSize.Four, 0, 0, 0, "Riker");

        CrateLayout.NewProblem(Rack1, [riker], [riker, At(CrateSize.Four, 0, 0, 1, "Shubin")]).Should().BeNull();
    }

    [Fact]
    public void Place_puts_an_earlier_stop_on_top_of_a_later_one()
    {
        // The greedy layout puts A on the plate (largest first, input order) and B on top of it; the search turns it round.
        var crates = Of((CrateSize.Four, "A"), (CrateSize.Four, "B"), (CrateSize.Four, "B"), (CrateSize.Four, "B"));

        var positions = CrateLayout.Place(Rack1, [], crates, AbcRoute, FillOrder.BowFirst);

        positions.Should().NotBeNull();
        positions![0].Stack.Should().Be(1);
        CrateLayout.Problems(Rack1, AsCrates(Rack1, crates, positions), AbcRoute).Should().BeEmpty();
    }

    [Fact]
    public void Place_keeps_the_order_around_occupied_crates()
    {
        // Riker, the first stop, takes the bow half of the plate: Shubin's 4s go stern, one on the other, not on Riker.
        Crate[] occupied = [At(CrateSize.Four, 2, 0, 0, "Riker")];

        var positions = CrateLayout.Place(Rack1, occupied, Of((CrateSize.Four, "Shubin"), (CrateSize.Four, "Shubin")), ["Riker", "Shubin"], FillOrder.BowFirst);

        positions.Should().Equal(new GridPosition(0, 0, 0), new GridPosition(0, 0, 1));
    }

    [Fact]
    public void Place_lays_out_as_before_when_no_ordered_layout_exists()
    {
        // 8 and 4 take the whole plate, and the 1 of the last stop may lie on nothing of an earlier one: no order fits.
        var crates = Of((CrateSize.Eight, "A"), (CrateSize.Four, "B"), (CrateSize.One, "C"));

        var positions = CrateLayout.Place(Rack1, [], crates, AbcRoute, FillOrder.BowFirst);

        positions.Should().Equal(CrateLayout.Place(Rack1, [], crates, [], FillOrder.BowFirst));
        CrateLayout.Problems(Rack1, AsCrates(Rack1, crates, positions!), AbcRoute)
            .Should().Equal("1 SCU crate of Titanium for C in rack 1 lies on cargo for B, which is unloaded first");
    }

    [Fact]
    public void Place_keeps_the_first_layout_when_the_order_search_runs_out_of_steps()
    {
        // The layout above that the search turns round: with one step it gives up, and the greedy layout stays, marked (D24 addendum T7.0).
        var crates = Of((CrateSize.Four, "A"), (CrateSize.Four, "B"), (CrateSize.Four, "B"), (CrateSize.Four, "B"));

        var laid = CrateLayout.Lay(Rack1, [], crates, AbcRoute, FillOrder.BowFirst, maxOrderSearchSteps: 1)!;

        laid.OrderUnchecked.Should().BeTrue();
        laid.Positions.Should().Equal(CrateLayout.Place(Rack1, [], crates, [], FillOrder.BowFirst));
        CrateLayout.Problems(Rack1, AsCrates(Rack1, crates, [.. laid.Positions]), AbcRoute)
            .Should().NotBeEmpty().And.OnlyContain(p => p.EndsWith("which is unloaded first"));
    }

    [Fact]
    public void Lay_says_the_order_was_checked_when_the_search_ends_in_time()
    {
        // No ordered layout exists (8, 4, 1 of three stops), but the search proves it within the limit: checked.
        var none = CrateLayout.Lay(Rack1, [], Of((CrateSize.Eight, "A"), (CrateSize.Four, "B"), (CrateSize.One, "C")), AbcRoute, FillOrder.BowFirst)!;
        var turned = CrateLayout.Lay(Rack1, [], Of((CrateSize.Four, "A"), (CrateSize.Four, "B"), (CrateSize.Four, "B"), (CrateSize.Four, "B")), AbcRoute, FillOrder.BowFirst)!;

        none.OrderUnchecked.Should().BeFalse();
        turned.OrderUnchecked.Should().BeFalse();
    }

    [Fact]
    public void Place_lays_out_16_crates_of_three_stops_in_under_100_ms()
    {
        (CrateSize Size, string Destination)[] crates =
            [.. Enumerable.Repeat((CrateSize.One, "A"), 5), .. Enumerable.Repeat((CrateSize.One, "B"), 5), .. Enumerable.Repeat((CrateSize.One, "C"), 6)];
        CrateLayout.Place(Rack1, [], crates, AbcRoute, FillOrder.BowFirst);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var positions = CrateLayout.Place(Rack1, [], crates, AbcRoute, FillOrder.BowFirst);
        watch.Stop();

        CrateLayout.Problems(Rack1, AsCrates(Rack1, crates, positions!), AbcRoute).Should().BeEmpty();
        watch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public void Place_gives_up_on_the_order_in_under_100_ms()
    {
        // 15 of 16 cells, no ordered layout: the search has to try them all before it falls back.
        var crates = Of((CrateSize.Eight, "A"), (CrateSize.Four, "B"), (CrateSize.One, "C"), (CrateSize.One, "C"), (CrateSize.One, "C"));
        CrateLayout.Place(Rack1, [], crates, AbcRoute, FillOrder.BowFirst);

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var positions = CrateLayout.Place(Rack1, [], crates, AbcRoute, FillOrder.BowFirst);
        watch.Stop();

        positions.Should().NotBeNull();
        watch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    /// <summary>
    /// The search finds an ordered layout whenever one exists: every split of every mix of up to 8 crates over 2 and 3
    /// stops, against a brute force that tries every crate at every place. Slow (about a minute), so not part of the
    /// default run (default.runsettings); run it with
    /// <c>dotnet test tests/CargoPlan.Core.Tests -p:RunSettingsFilePath= --filter Category=Exhaustive</c>.
    /// </summary>
    [Theory]
    [Trait("Category", "Exhaustive")]
    [InlineData(2)]
    [InlineData(3)]
    public void Place_finds_an_ordered_layout_whenever_one_exists(int stops)
    {
        var mixes = Mixes(16, [CrateSize.Sixteen, CrateSize.Eight, CrateSize.Four, CrateSize.Two, CrateSize.One])
            .Where(m => m.Length >= stops && m.Length <= 8)
            .ToArray();
        string[] route = [.. AbcRoute.Take(stops)];

        List<string> wrong = [];
        foreach (var split in mixes.SelectMany(m => Splits(m, stops)))
        {
            (CrateSize Size, string Destination)[] crates = [.. split.Select(c => (c.Size, route[c.Rank]))];
            var exists = OrderedLayoutExists(Rack1.Grid, [.. split]);
            foreach (var order in Enum.GetValues<FillOrder>())
            {
                var positions = CrateLayout.Place(Rack1, [], crates, route, order)!;
                var ordered = CrateLayout.Problems(Rack1, AsCrates(Rack1, crates, positions), route).Count == 0;
                if (ordered != exists)
                {
                    wrong.Add($"{order}: {string.Join(" ", split.Select(c => $"{(int)c.Size}@{c.Rank}"))} (exists {exists})");
                }
            }
        }

        wrong.Should().BeEmpty();
    }

    /// <summary>Every way to give the crates of <paramref name="mix"/> to <paramref name="stops"/> stops, each stop at least one.</summary>
    private static IEnumerable<(CrateSize Size, int Rank)[]> Splits(CrateSize[] mix, int stops)
    {
        (CrateSize Size, int Count)[] groups = [.. mix.GroupBy(s => s).Select(g => (g.Key, g.Count()))];

        IEnumerable<(CrateSize, int)[]> From(int group)
        {
            if (group == groups.Length)
            {
                yield return [];
                yield break;
            }

            foreach (var rest in From(group + 1))
            {
                foreach (var counts in Counts(groups[group].Count, stops))
                {
                    yield return [.. counts.SelectMany((n, rank) => Enumerable.Repeat((groups[group].Size, rank), n)), .. rest];
                }
            }
        }

        return From(0).Where(s => Enumerable.Range(0, stops).All(rank => s.Any(c => c.Item2 == rank)));
    }

    /// <summary>Every way to put <paramref name="count"/> equal crates into <paramref name="stops"/> stops.</summary>
    private static IEnumerable<int[]> Counts(int count, int stops)
    {
        if (stops == 1)
        {
            yield return [count];
            yield break;
        }

        for (var first = 0; first <= count; first++)
        {
            foreach (var rest in Counts(count - first, stops - 1))
            {
                yield return [first, .. rest];
            }
        }
    }

    /// <summary>
    /// Brute force for the test only: any crate at any free place, as long as it rests on the plate or on crates of its own
    /// or a later stop (rank). Memoised on the cells and the crates left.
    /// </summary>
    private static bool OrderedLayoutExists(Dimensions grid, List<(CrateSize Size, int Rank)> crates)
    {
        var owner = new int[grid.Length, grid.Width, grid.Stack];
        foreach (var cell in CellsOf(new GridPosition(0, 0, 0), grid))
        {
            owner[cell.Length, cell.Width, cell.Stack] = -1;
        }

        Dictionary<string, bool> memo = [];
        crates.Sort();
        return Search(crates);

        bool Search(List<(CrateSize Size, int Rank)> left)
        {
            if (left.Count == 0)
            {
                return true;
            }

            var key = string.Join(",", owner.Cast<int>()) + "|" + string.Join(",", left);
            if (memo.TryGetValue(key, out var known))
            {
                return known;
            }

            var found = false;
            foreach (var crate in left.Distinct().ToArray())
            {
                var size = crate.Size.Dimensions;
                for (var l = 0; l + size.Length <= grid.Length && !found; l++)
                {
                    for (var w = 0; w + size.Width <= grid.Width && !found; w++)
                    {
                        for (var s = 0; s + size.Stack <= grid.Stack && !found; s++)
                        {
                            GridPosition[] cells = [.. CellsOf(new GridPosition(l, w, s), size)];
                            if (cells.Any(c => owner[c.Length, c.Width, c.Stack] >= 0)
                                || cells.Any(c => c.Stack == s && s > 0 && owner[c.Length, c.Width, s - 1] < crate.Rank))
                            {
                                continue;
                            }

                            foreach (var c in cells)
                            {
                                owner[c.Length, c.Width, c.Stack] = crate.Rank;
                            }

                            left.Remove(crate);
                            found = Search(left);
                            left.Add(crate);
                            left.Sort();
                            foreach (var c in cells)
                            {
                                owner[c.Length, c.Width, c.Stack] = -1;
                            }
                        }
                    }
                }

                if (found)
                {
                    break;
                }
            }

            memo[key] = found;
            return found;
        }
    }

    private static IEnumerable<GridPosition> CellsOf(GridPosition position, Dimensions size) =>
        from l in Enumerable.Range(position.Length, size.Length)
        from w in Enumerable.Range(position.Width, size.Width)
        from s in Enumerable.Range(position.Stack, size.Stack)
        select new GridPosition(l, w, s);

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
        HullA.Racks.Should().OnlyContain(r => CrateLayout.Problems(r, crates.Where(c => c.RackId == r.Id).ToArray(), NoRoute).Count == 0);
    }

    [Fact]
    public void LayOut_throws_when_a_rack_holds_more_than_its_grid()
    {
        // An overloaded rack, allowed in schema 1 plan files (D10), has no layout.
        Placement[] placements = [new("Riker", "rack-0", new CargoItem("Titanium", 20, CrateSize.Four))];

        var act = () => CrateLayout.LayOut(HullA, placements, FillOrder.BowFirst);

        act.Should().Throw<PlanException>().WithMessage("*rack 1*20/16 SCU*");
    }

    // --- PlaceWithin: the parts of a split stay in the crate's footprint (D27) ---

    [Fact]
    public void PlaceWithin_lays_two_2s_side_by_side_in_the_cells_of_a_4()
    {
        var four = At(CrateSize.Four, 2, 0, 0);

        CrateLayout.PlaceWithin(Rack1, four, [CrateSize.Two, CrateSize.Two], FillOrder.BowFirst)
            .Should().Equal(new GridPosition(2, 0, 0), new GridPosition(2, 1, 0));
    }

    [Fact]
    public void PlaceWithin_counts_the_cells_below_the_box_as_support()
    {
        // The 4 lay on another crate, so its parts do too; the cells around it don't exist for them.
        var four = At(CrateSize.Four, 0, 0, 1);

        CrateLayout.PlaceWithin(Rack1, four, [CrateSize.Two, CrateSize.Two], FillOrder.BowFirst)
            .Should().Equal(new GridPosition(0, 0, 1), new GridPosition(0, 1, 1));
    }

    [Fact]
    public void PlaceWithin_returns_null_when_the_parts_do_not_fit_the_box()
    {
        CrateLayout.PlaceWithin(Rack1, At(CrateSize.Four, 2, 0, 0), [CrateSize.Eight], FillOrder.BowFirst).Should().BeNull();
    }

    // --- BlockProblem: crates merge only if together they are one crate (D27) ---

    [Fact]
    public void BlockProblem_is_null_when_the_crates_fill_the_box_of_the_size()
    {
        CrateLayout.BlockProblem([At(CrateSize.Two, 2, 0, 0), At(CrateSize.Two, 2, 1, 0)], CrateSize.Four).Should().BeNull();
    }

    [Fact]
    public void BlockProblem_names_crates_that_do_not_touch()
    {
        CrateLayout.BlockProblem([At(CrateSize.Two, 2, 0, 0), At(CrateSize.Two, 0, 1, 0)], CrateSize.Four)
            .Should().Be("The crates are not adjacent.");
    }

    [Fact]
    public void BlockProblem_names_crates_that_touch_but_form_no_block()
    {
        CrateLayout.BlockProblem([At(CrateSize.Two, 0, 0, 0), At(CrateSize.One, 0, 1, 0), At(CrateSize.One, 0, 1, 1)], CrateSize.Four)
            .Should().Be("The crates don't form a block.");
    }

    [Fact]
    public void BlockProblem_names_a_block_of_the_wrong_shape()
    {
        CrateLayout.BlockProblem([At(CrateSize.Two, 0, 0, 0), At(CrateSize.Two, 2, 0, 0)], CrateSize.Four)
            .Should().Be("Together they would be a 4×1×1 crate, 4 SCU crates are 2×2×1.");
    }

    [Fact]
    public void BlockProblem_names_overlapping_crates()
    {
        CrateLayout.BlockProblem([At(CrateSize.Two, 0, 0, 0), At(CrateSize.Two, 1, 0, 0)], CrateSize.Four)
            .Should().Be("The crates overlap.");
    }
}
