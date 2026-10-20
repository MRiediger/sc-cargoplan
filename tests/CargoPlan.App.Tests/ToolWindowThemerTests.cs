using System.Windows;
using System.Windows.Media;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;

namespace CargoPlan.App.Tests;

public sealed class ToolWindowThemerTests
{
    private static readonly ResourceKey[] AccentKeys =
    [
        SystemColors.AccentColorKey,
        SystemColors.AccentColorLight1Key,
        SystemColors.AccentColorLight2Key,
        SystemColors.AccentColorLight3Key,
        SystemColors.AccentColorDark1Key,
        SystemColors.AccentColorDark2Key,
        SystemColors.AccentColorDark3Key,
    ];

    [Theory]
    [InlineData(ToolWindowTheme.System, "System")]
    [InlineData(ToolWindowTheme.Light, "Light")]
    [InlineData(ToolWindowTheme.Dark, "Dark")]
    public void Every_theme_is_a_Fluent_mode_never_the_system_look(ToolWindowTheme theme, string mode)
    {
        ToolWindowThemer.ThemeModeOf(theme).Value.Should().Be(mode);
    }

    [Fact]
    public void The_default_follows_Windows()
    {
        default(ToolWindowTheme).Should().Be(ToolWindowTheme.System);
    }

    [Fact]
    public void The_accent_sets_exactly_the_colour_keys_Fluent_reads()
    {
        ResourceDictionary resources = new();

        ToolWindowThemer.ApplyAccent(resources);

        resources.Count.Should().Be(AccentKeys.Length);
        AccentKeys.Should().OnlyContain(key => resources.Contains(key) && resources[key] is Color);
    }

    [Fact]
    public void The_accent_is_teal_not_the_Windows_accent()
    {
        ResourceDictionary resources = new();

        ToolWindowThemer.ApplyAccent(resources);

        resources[SystemColors.AccentColorKey].Should().Be(Color.FromRgb(0x2A, 0x9D, 0xB0));
    }
}
