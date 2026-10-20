using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public sealed class ToolWindowThemeTests
{
    [Theory]
    [InlineData("system", ToolWindowTheme.System)]
    [InlineData("light", ToolWindowTheme.Light)]
    [InlineData("dark", ToolWindowTheme.Dark)]
    public void Every_name_parses_and_writes_back_the_same(string text, ToolWindowTheme expected)
    {
        ToolWindowThemes.TryParse(text, out var theme).Should().BeTrue();

        theme.Should().Be(expected);
        ToolWindowThemes.ToName(theme).Should().Be(text);
    }

    [Fact]
    public void Names_are_the_three_modes_system_first()
    {
        ToolWindowThemes.Names.Should().Equal("system", "light", "dark");
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("")]
    [InlineData("Dark")]     // strict like the keys: lower case only
    [InlineData(" dark")]
    [InlineData("1")]        // Enum.TryParse would take a number
    public void Anything_else_is_not_a_mode(string text)
    {
        ToolWindowThemes.TryParse(text, out _).Should().BeFalse();
    }

    /// <summary>Like Fluent's own choice: a light registry value is light, 0 or none is dark.</summary>
    [Theory]
    [InlineData(ToolWindowTheme.System, 1, ToolWindowTheme.Light)]
    [InlineData(ToolWindowTheme.System, 0, ToolWindowTheme.Dark)]
    [InlineData(ToolWindowTheme.System, null, ToolWindowTheme.Dark)]
    [InlineData(ToolWindowTheme.Light, 0, ToolWindowTheme.Light)]
    [InlineData(ToolWindowTheme.Light, null, ToolWindowTheme.Light)]
    [InlineData(ToolWindowTheme.Dark, 1, ToolWindowTheme.Dark)]
    [InlineData(ToolWindowTheme.Dark, null, ToolWindowTheme.Dark)]
    public void Resolve_gives_light_or_dark_and_only_system_asks_Windows(ToolWindowTheme theme, int? appsUseLightTheme, ToolWindowTheme expected)
    {
        ToolWindowThemes.Resolve(theme, appsUseLightTheme).Should().Be(expected);
    }

    [Fact]
    public void Parse_of_an_invalid_name_throws()
    {
        // After AppearanceRules.NormalizeToolWindowTheme an invalid name is a bug, not a player error.
        var parse = () => ToolWindowThemes.Parse("blue");

        parse.Should().Throw<FormatException>().WithMessage("*blue*");
    }
}
