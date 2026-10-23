using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public sealed class ShipOutlineModeTests
{
    [Theory]
    [InlineData("outline", ShipOutlineMode.Outline)]
    [InlineData("none", ShipOutlineMode.None)]
    public void Every_name_parses_and_writes_back_the_same(string text, ShipOutlineMode expected)
    {
        ShipOutlineModes.TryParse(text, out var mode).Should().BeTrue();

        mode.Should().Be(expected);
        ShipOutlineModes.ToName(mode).Should().Be(text);
    }

    [Fact]
    public void Names_are_the_two_modes_outline_first()
    {
        ShipOutlineModes.Names.Should().Equal("outline", "none");
        ShipOutlineModes.DefaultName.Should().Be("outline");
    }

    [Theory]
    [InlineData("prism")]
    [InlineData("")]
    [InlineData("Outline")] // strict like the keys: lower case only
    [InlineData("1")]       // Enum.TryParse would take a number
    public void Anything_else_is_not_a_mode(string text)
    {
        ShipOutlineModes.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Parse_of_an_invalid_name_throws()
    {
        var parse = () => ShipOutlineModes.Parse("prism");

        parse.Should().Throw<FormatException>().WithMessage("*prism*");
    }

    [Fact]
    public void Normalize_keeps_a_valid_name_and_corrects_an_unknown_one_to_outline()
    {
        AppearanceRules.NormalizeShipOutline("none").Should().Be(("none", Array.Empty<string>()));

        var (mode, problems) = AppearanceRules.NormalizeShipOutline("prism");

        mode.Should().Be("outline");
        problems.Should().Equal("shipOutline \"prism\" is not valid (outline or none), using outline.");
    }
}
