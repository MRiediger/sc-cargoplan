using CargoPlan.Core.ShipData;

namespace CargoPlan.Core.Tests;

public class ShipDataVersionTests
{
    [Theory]
    [InlineData("ships-2026.11", 2026, 11, 0)]
    [InlineData("ships-2026.11.1", 2026, 11, 1)]
    [InlineData("ships-2027.01.12", 2027, 1, 12)]
    public void Parses_the_release_tags_of_the_data_channel(string text, int year, int month, int patch)
    {
        ShipDataVersion.TryParse(text, out var version).Should().BeTrue();

        version.Should().Be(new ShipDataVersion(year, month, patch));
        version.ToString().Should().Be(text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026.11")]
    [InlineData("Ships-2026.11")]
    [InlineData("ships-2026.11.0")]
    [InlineData("ships-2026.11.01")]
    [InlineData("ships-2026.1")]
    [InlineData("ships-2026.13")]
    [InlineData("ships-2026.00")]
    [InlineData("ships-26.11")]
    [InlineData("ships-2026.11.1.1")]
    [InlineData("ships-2026.11-rc")]
    [InlineData(" ships-2026.11")]
    [InlineData("ships-２０２６.11")]
    [InlineData("holo-v3")]
    public void Refuses_everything_else(string? text) => ShipDataVersion.TryParse(text, out _).Should().BeFalse();

    [Fact]
    public void Orders_by_year_month_and_patch()
    {
        string[] tags = ["ships-2027.01", "ships-2026.11.1", "ships-2026.12", "ships-2026.11", "ships-2026.11.10", "ships-2026.11.2"];

        var ordered = tags.Select(Parse).Order().Select(v => v.ToString());

        ordered.Should().Equal("ships-2026.11", "ships-2026.11.1", "ships-2026.11.2", "ships-2026.11.10", "ships-2026.12", "ships-2027.01");
        (Parse("ships-2026.11") < Parse("ships-2026.11.1")).Should().BeTrue();
        (Parse("ships-2026.12") >= Parse("ships-2026.11.1")).Should().BeTrue();
    }

    [Theory]
    [InlineData("ships-2026.11", "2026.11")]
    [InlineData("ships-2026.11.1", "2026.11.1")]
    public void Display_drops_the_prefix(string text, string display) => Parse(text).Display.Should().Be(display);

    [Fact]
    public void The_baseline_is_the_seed_release_that_equals_ships_in_the_repo() =>
        ShipDataVersion.Baseline.ToString().Should().Be("ships-2026.11");

    private static ShipDataVersion Parse(string text) =>
        ShipDataVersion.TryParse(text, out var version) ? version : throw new ArgumentException(text);
}
