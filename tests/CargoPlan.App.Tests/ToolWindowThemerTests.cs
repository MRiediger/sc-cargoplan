using System.Windows;
using System.Windows.Media;
using CargoPlan.App.Settings;
using CargoPlan.App.Theme;

namespace CargoPlan.App.Tests;

public sealed class ToolWindowThemerTests
{
    // Fluent's Dark.xaml loads through pack://, which Application's static constructor sets up (ApplicationInit). Reading
    // Application.Current runs it without creating an Application, so no other test sees one.
    // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Application.cs
    static ToolWindowThemerTests() => _ = Application.Current;

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

    // Grey text in dark mode (T4.7b): Fluent's pure white glares next to the game.

    [Theory]
    [InlineData("WindowForeground")]
    [InlineData("ButtonForeground")]
    [InlineData("ToggleButtonForeground")]
    [InlineData("TextControlForeground")]
    [InlineData("ComboBoxForeground")]
    [InlineData("ComboBoxItemForeground")]
    [InlineData("CheckBoxForegroundUnchecked")]
    [InlineData("GroupBoxHeaderForeground")]
    [InlineData("DataGridColumnHeaderForeground")]
    [InlineData("ContextMenuForeground")]
    [InlineData("ToolTipForeground")]
    public void Dark_text_greys_every_brush_the_tool_windows_take_from_the_primary_text_colour(string key)
    {
        ToolWindowThemer.DarkText[key].Should().BeOfType<SolidColorBrush>().Which.Color.Should().Be(ToolWindowThemer.DarkTextColor);
    }

    [Fact]
    public void Dark_text_holds_only_grey_brushes_and_leaves_the_focus_ring_white()
    {
        var dark = ToolWindowThemer.DarkText;

        dark.Count.Should().BeGreaterThan(40); // Fluent builds 55 brushes from that colour (.NET 10)
        dark.Values.Cast<object>().Should().AllSatisfy(v => v.Should().BeOfType<SolidColorBrush>().Which.Color.Should().Be(ToolWindowThemer.DarkTextColor));
        dark.Contains("FocusStrokeColorOuterBrush").Should().BeFalse();
        dark.Contains("TextOnAccentFillColorSelectedTextBrush").Should().BeFalse();
    }

    [Fact]
    public void Dark_text_is_merged_once_and_removed_again()
    {
        ResourceDictionary resources = new();

        ToolWindowThemer.ApplyText(resources, dark: true);
        ToolWindowThemer.ApplyText(resources, dark: true);

        resources.MergedDictionaries.Should().ContainSingle().Which.Should().BeSameAs(ToolWindowThemer.DarkText);

        ToolWindowThemer.ApplyText(resources, dark: false);

        resources.MergedDictionaries.Should().BeEmpty();
    }

    // Selection (4d, variant C): dark marks with a neutral tone and a teal bar; the accent stays for controls.

    [Fact]
    public void Dark_selection_fills_rows_and_entries_with_the_neutral_tone_and_keeps_the_light_text()
    {
        var dark = ToolWindowThemer.SelectionDark;
        Color Of(string key) => ((SolidColorBrush)dark[key]).Color;

        ToolWindowThemer.SelectionFillDark.Should().Be(Color.FromRgb(0x3A, 0x4A, 0x55));
        dark[ToolWindowThemer.SelectionFillDarkKey].Should().Be(ToolWindowThemer.SelectionFillDark);
        Of("DataGridRowSelectedBackgroundThemeBrush").Should().Be(ToolWindowThemer.SelectionFillDark);
        Of("DataGridRowSelectedForegroundThemeBrush").Should().Be(ToolWindowThemer.DarkTextColor);
        Of(ToolWindowThemer.SelectionFillKey).Should().Be(ToolWindowThemer.SelectionFillDark);
        Of(ToolWindowThemer.SelectionHighlightFillKey).Should().Be(ToolWindowThemer.SelectionFillDark, "the picked entry stays full under the mouse");
        Of("ComboBoxItemBackgroundSelected").Should().Be(Color.FromArgb(0x99, 0x3A, 0x4A, 0x55), "hover is the tone at 60 %");
        dark[ToolWindowThemer.SelectedRowTextColorKey].Should().Be(ToolWindowThemer.DarkTextColor);
        dark[ToolWindowThemer.RowBarThicknessKey].Should().Be(new Thickness(3, 0, 0, 0));
    }

    [Fact]
    public void Light_selection_leaves_Fluents_marking_as_it_is()
    {
        var light = ToolWindowThemer.SelectionLight;

        light.Contains("DataGridRowSelectedBackgroundThemeBrush").Should().BeFalse();
        light.Contains("DataGridRowSelectedForegroundThemeBrush").Should().BeFalse();
        light.Contains("ComboBoxItemBackgroundSelected").Should().BeFalse();
        ((SolidColorBrush)light[ToolWindowThemer.SelectionFillKey]).Color.A.Should().Be(0, "Fluent fills no picked entry");
        ((SolidColorBrush)light[ToolWindowThemer.SelectionHighlightFillKey]).Color.Should().Be(Color.FromArgb(0x09, 0, 0, 0), "Fluent's own highlight");
        light[ToolWindowThemer.SelectedRowTextColorKey].Should().Be(Colors.White, "Fluent's selected row text in light");
        light[ToolWindowThemer.RowBarThicknessKey].Should().Be(new Thickness(0));
    }

    [Fact]
    public void The_selection_dictionary_is_swapped_never_doubled()
    {
        ResourceDictionary resources = new();

        ToolWindowThemer.ApplySelection(resources, dark: true);
        ToolWindowThemer.ApplySelection(resources, dark: true);

        resources.MergedDictionaries.Should().ContainSingle().Which.Should().BeSameAs(ToolWindowThemer.SelectionDark);

        ToolWindowThemer.ApplySelection(resources, dark: false);

        resources.MergedDictionaries.Should().ContainSingle().Which.Should().BeSameAs(ToolWindowThemer.SelectionLight);
    }

    [Fact]
    public void The_accent_is_teal_not_the_Windows_accent()
    {
        ResourceDictionary resources = new();

        ToolWindowThemer.ApplyAccent(resources);

        resources[SystemColors.AccentColorKey].Should().Be(Color.FromRgb(0x2A, 0x9D, 0xB0));
    }
}
