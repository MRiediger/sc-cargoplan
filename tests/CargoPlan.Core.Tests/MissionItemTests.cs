using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

public class MissionItemTests
{
    // --- Expansion into cargo items (D23) ---

    [Fact]
    public void ToCargoItems_gives_one_item_per_crate_size_largest_first()
    {
        MissionItem stims = new("Stims", 15, CrateSize.Four);

        stims.ToCargoItems().Should().Equal(
            new CargoItem("Stims", 12, CrateSize.Four),
            new CargoItem("Stims", 2, CrateSize.Two),
            new CargoItem("Stims", 1, CrateSize.One));
    }

    [Fact]
    public void ToCargoItems_uses_the_override_instead_of_the_derived_mix()
    {
        MissionItem stims = new("Stims", 6, CrateSize.Four, [CrateSize.Two, CrateSize.Two, CrateSize.Two]);

        stims.ToCargoItems().Should().Equal(new CargoItem("Stims", 6, CrateSize.Two));
    }

    [Fact]
    public void ToCargoItems_sorts_an_override_largest_first()
    {
        MissionItem stims = new("Stims", 6, CrateSize.Four, [CrateSize.Two, CrateSize.Four]);

        stims.ToCargoItems().Should().Equal(new CargoItem("Stims", 4, CrateSize.Four), new CargoItem("Stims", 2, CrateSize.Two));
    }

    [Fact]
    public void CrateList_is_the_derived_mix_without_an_override_and_the_override_with_one()
    {
        new MissionItem("Stims", 6, CrateSize.Four).CrateList.Should().Equal(CrateSize.Four, CrateSize.Two);
        new MissionItem("Stims", 6, CrateSize.Four, [CrateSize.Two, CrateSize.Four]).CrateList
            .Should().Equal(CrateSize.Two, CrateSize.Four);
    }

    // --- Validation ---

    [Theory]
    [InlineData(0)]
    [InlineData(-6)]
    public void Rejects_non_positive_scu(int scu)
    {
        var act = () => new MissionItem("Stims", scu, CrateSize.Four);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Rejects_an_unknown_max_crate_size()
    {
        var act = () => new MissionItem("Stims", 6, (CrateSize)3);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(new[] { 2, 2 }, "*add up to 4 SCU, not 6*")]
    [InlineData(new[] { 8 }, "*8 SCU crates are larger than the max crate size of 4 SCU*")]
    [InlineData(new[] { 3, 3 }, "*3 SCU is not a crate size*")]
    [InlineData(new int[0], "*No crates*")]
    public void Rejects_an_override_that_does_not_fit_the_item(int[] crates, string expected)
    {
        var act = () => new MissionItem("Stims", 6, CrateSize.Four, [.. crates.Select(c => (CrateSize)c)]);

        act.Should().Throw<ArgumentException>().WithMessage(expected);
    }

    // --- WithCrateList: split and merge rebook the item (D27) ---

    [Fact]
    public void WithCrateList_sets_the_crates_as_an_override_largest_first()
    {
        MissionItem titanium = new("Titanium", 16, CrateSize.Four);

        var split = titanium.WithCrateList([CrateSize.Four, CrateSize.Two, CrateSize.Four, CrateSize.Two, CrateSize.Four], CrateCap.None);

        split.Should().BeEquivalentTo(new { Commodity = "Titanium", Scu = 16, MaxCrateSize = CrateSize.Four });
        split.Crates.Should().Equal(CrateSize.Four, CrateSize.Four, CrateSize.Four, CrateSize.Two, CrateSize.Two);
    }

    [Fact]
    public void WithCrateList_equal_to_the_derived_mix_drops_the_override()
    {
        // Nobody typed it, so it isn't kept as typed (T3.12 keeps only typed overrides).
        MissionItem titanium = new("Titanium", 6, CrateSize.Four, [CrateSize.Two, CrateSize.Two, CrateSize.Two]);

        titanium.WithCrateList([CrateSize.Two, CrateSize.Four], CrateCap.None).Crates.Should().BeNull();
    }

    [Fact]
    public void WithCrateList_rejects_crates_that_do_not_fit_the_item()
    {
        MissionItem titanium = new("Titanium", 6, CrateSize.Four);

        var act = () => titanium.WithCrateList([CrateSize.Four, CrateSize.Four], CrateCap.None);

        act.Should().Throw<ArgumentException>().WithMessage("*add up to 8 SCU, not 6*");
    }
}
