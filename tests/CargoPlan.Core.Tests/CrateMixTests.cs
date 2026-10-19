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
}
