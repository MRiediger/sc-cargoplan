using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public sealed class LegendPositionTests
{
    [Theory]
    [InlineData("top-left", LegendEdge.Top, LegendAlignment.Left)]
    [InlineData("top-center", LegendEdge.Top, LegendAlignment.Center)]
    [InlineData("top-right", LegendEdge.Top, LegendAlignment.Right)]
    [InlineData("bottom-left", LegendEdge.Bottom, LegendAlignment.Left)]
    [InlineData("bottom-center", LegendEdge.Bottom, LegendAlignment.Center)]
    [InlineData("bottom-right", LegendEdge.Bottom, LegendAlignment.Right)]
    public void Every_name_parses_and_writes_back_the_same(string text, LegendEdge edge, LegendAlignment alignment)
    {
        LegendPosition.TryParse(text, out var position).Should().BeTrue();

        position.Should().Be(new LegendPosition(edge, alignment));
        position.ToString().Should().Be(text);
    }

    [Fact]
    public void Names_are_the_six_positions()
    {
        LegendPosition.Names.Should().Equal("top-left", "top-center", "top-right", "bottom-left", "bottom-center", "bottom-right");
    }

    [Theory]
    [InlineData("up")]
    [InlineData("")]
    [InlineData("Bottom-Left")]     // strict like the keys: lower case only
    [InlineData("bottom left")]
    [InlineData("left-bottom")]
    [InlineData(" bottom-left")]
    [InlineData("bottom-centre")]
    public void Anything_else_is_not_a_position(string text)
    {
        LegendPosition.TryParse(text, out _).Should().BeFalse();
    }

    [Fact]
    public void Default_is_the_layout_before_it_was_a_setting()
    {
        LegendPosition.Default.Should().Be(new LegendPosition(LegendEdge.Bottom, LegendAlignment.Left));
        LegendSettings.Default.Should().Be(new LegendSettings(Visible: true, Position: "bottom-left", MaxHeightShare: 0.5));
    }

    [Fact]
    public void Parse_of_an_invalid_name_throws()
    {
        // After AppearanceRules.Normalize an invalid name is a bug, not a player error.
        var parse = () => LegendPosition.Parse("up");

        parse.Should().Throw<FormatException>().WithMessage("*up*");
    }
}
