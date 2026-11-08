using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class CrateMixTests
{
    // --- Decompose ---

    [Theory]
    [InlineData(6, 4, 4, 2)]
    [InlineData(15, 4, 4, 4, 4, 2, 1)]
    [InlineData(4, 4, 4)]
    [InlineData(13, 8, 8, 4, 1)]
    [InlineData(24, 32, 24)]          // the case a game finding most likely overturns (D23)
    [InlineData(20, 16, 16, 4)]
    [InlineData(3, 1, 1, 1, 1)]
    public void Decompose_takes_the_largest_allowed_crate_first(int scu, int max, params int[] expected)
    {
        CrateMix.Decompose(scu, (CrateSize)max).Select(c => (int)c).Should().Equal(expected);
    }

    [Theory]
    [InlineData(0, 4)]
    [InlineData(-1, 4)]
    [InlineData(6, 3)]
    [InlineData(6, 0)]
    public void Decompose_rejects_no_scu_or_an_unknown_max(int scu, int max)
    {
        var act = () => CrateMix.Decompose(scu, (CrateSize)max);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Decompose_sums_up_to_the_scu_for_every_amount_and_max()
    {
        foreach (var max in Enum.GetValues<CrateSize>())
        {
            for (var scu = 1; scu <= 64; scu++)
            {
                var crates = CrateMix.Decompose(scu, max);

                crates.Sum(c => (int)c).Should().Be(scu, $"{scu} SCU, max {(int)max}");
                crates.Should().OnlyContain(c => c <= max).And.BeInDescendingOrder();
            }
        }
    }

    // --- Sizes ---

    [Fact]
    public void Sizes_are_known_crate_sizes_largest_first_down_to_1()
    {
        CrateMix.Sizes.Should().OnlyContain(s => Enum.IsDefined(s)).And.OnlyHaveUniqueItems();
        CrateMix.Sizes.Should().BeInDescendingOrder();

        // Without 1 SCU crates an odd amount couldn't be decomposed.
        CrateMix.Sizes[^1].Should().Be(CrateSize.One);
    }

    // --- Problem ---

    [Theory]
    [InlineData(6, 4, 4, 2)]
    [InlineData(6, 4, 2, 2, 2)]
    [InlineData(6, 4, 2, 4)]
    [InlineData(6, 8, 1, 1, 4)]
    public void Problem_accepts_crates_that_add_up_and_stay_within_the_max(int scu, int max, params int[] crates)
    {
        CrateMix.Problem(scu, (CrateSize)max, crates).Should().BeNull();
    }

    [Theory]
    [InlineData(6, 4, new[] { 2, 2, 3 }, "3 SCU is not a crate size.")]
    [InlineData(6, 4, new[] { 8 }, "8 SCU crates are larger than the max crate size of 4 SCU.")]
    [InlineData(6, 4, new[] { 2, 2 }, "The crates add up to 4 SCU, not 6.")]
    [InlineData(6, 4, new int[0], "No crates given.")]
    public void Problem_explains_what_is_wrong_with_numbers(int scu, int max, int[] crates, string expected)
    {
        CrateMix.Problem(scu, (CrateSize)max, crates).Should().Be(expected);
    }

    // --- DefaultSplit: what one click on Split makes (D27) ---

    [Theory]
    [InlineData(2, 1, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(8, 4, 4)]
    [InlineData(16, 8, 8)]
    [InlineData(24, 16, 8)]
    [InlineData(32, 16, 16)]
    public void DefaultSplit_gives_the_two_largest_sizes_that_add_up_and_fit_the_footprint(int size, params int[] expected)
    {
        CrateMix.DefaultSplit((CrateSize)size).Select(c => (int)c).Should().Equal(expected);
    }

    [Fact]
    public void DefaultSplit_of_a_1_SCU_crate_is_empty()
    {
        CrateMix.DefaultSplit(CrateSize.One).Should().BeEmpty();
    }

    // --- Format and Parse (F9, D96) ---

    [Theory]
    [InlineData("6×32+16+4+2+1", 32, 32, 32, 32, 32, 32, 16, 4, 2, 1)]   // issue #21's example
    [InlineData("8", 8)]
    [InlineData("8×1", 1, 1, 1, 1, 1, 1, 1, 1)]
    [InlineData("4+2×2+1", 2, 4, 1, 2)]                                   // any order in, largest first out
    [InlineData("2×3", 3, 3)]                                             // no crate size: shown as typed, Problem names it
    [InlineData("")]
    public void Format_groups_each_size_largest_first_as_count_times_size(string expected, params int[] crates)
    {
        CrateMix.Format(crates).Should().Be(expected);
    }

    [Fact]
    public void Format_takes_crate_sizes_too()
    {
        CrateMix.Format(CrateMix.Decompose(215, CrateSize.ThirtyTwo)).Should().Be("6×32+16+4+2+1");
    }

    [Theory]
    [InlineData("2+2+2", 2, 2, 2)]
    [InlineData(" 2 + 2+2 ", 2, 2, 2)]
    [InlineData("3×2", 2, 2, 2)]
    [InlineData("3x2", 2, 2, 2)]
    [InlineData("3 X 2", 2, 2, 2)]
    [InlineData("2x32+16", 32, 32, 16)]
    [InlineData("16 + 2 × 32", 16, 32, 32)]
    [InlineData("1×8", 8)]
    [InlineData("2+3", 2, 3)]                                             // numbers only; sizes are Problem's check
    public void Parse_reads_sums_and_count_times_size(string text, params int[] expected)
    {
        CrateMix.Parse(text).Should().Equal(expected);
    }

    [Fact]
    public void Parse_reads_count_then_size_strictly()
    {
        // "32×2" is 32 crates of 2 SCU, legal; the sum check against the SCU shows a mix-up (D96).
        CrateMix.Parse("32×2").Should().Equal(Enumerable.Repeat(2, 32));
    }

    [Theory]
    [InlineData("")]
    [InlineData("2++4")]
    [InlineData("2,2,2")]
    [InlineData("-2")]
    [InlineData("0×4")]
    [InlineData("×4")]
    [InlineData("2×")]
    [InlineData("2×3×4")]
    [InlineData("2*4")]
    [InlineData("10000×1")]                                               // a bound at the input, not a ship's limit
    public void Parse_gives_null_for_text_that_is_no_crate_list(string text)
    {
        CrateMix.Parse(text).Should().BeNull();
    }

    [Theory]
    [InlineData("32+32+32+16")]
    [InlineData("3x32+16")]
    [InlineData(" 1 + 2X2 + 4 ")]
    [InlineData("32×2")]
    public void Formatting_what_was_parsed_is_stable(string text)
    {
        var once = CrateMix.Format(CrateMix.Parse(text)!);

        CrateMix.Format(CrateMix.Parse(once)!).Should().Be(once);
    }

    [Theory]
    [InlineData(32, 32, 32, 32, 32, 32, 16, 4, 2, 1)]
    [InlineData(1)]
    [InlineData(8, 2, 8, 2, 1)]
    public void Parsing_what_was_formatted_gives_the_same_crates(params int[] crates)
    {
        CrateMix.Parse(CrateMix.Format(crates)).Should().BeEquivalentTo(crates);
    }
}
