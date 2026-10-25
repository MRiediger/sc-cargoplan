using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.Theme;
using Keys = CargoPlan.App.Theme.ThemeResources.Keys;

namespace CargoPlan.App.Tests;

public sealed class ThemeResourcesTests
{
    private static ResourceDictionary Applied(AppearanceSettings appearance)
    {
        ResourceDictionary resources = new();
        ThemeResources.Apply(resources, appearance);
        return resources;
    }

    private static Color ColorOf(ResourceDictionary resources, string key) => ((SolidColorBrush)resources[key]).Color;

    [Fact]
    public void Defaults_set_every_key_but_the_halos_that_are_off()
    {
        var resources = Applied(AppearanceSettings.Default);

        resources.Keys.Cast<string>().Should().BeEquivalentTo(
            Keys.All.Except([Keys.HeaderHaloEffect, Keys.LegendHaloEffect, Keys.ButtonsHaloEffect]));
    }

    [Fact]
    public void Defaults_give_the_values_the_views_had_before()
    {
        var resources = Applied(AppearanceSettings.Default);

        ColorOf(resources, Keys.WindowBackgroundBrush).Should().Be(Color.FromArgb(0xA0, 0x14, 0x14, 0x14));
        resources[Keys.WindowBorderThickness].Should().Be(new Thickness(0));
        ColorOf(resources, Keys.HeaderForegroundBrush).Should().Be(Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6));
        ColorOf(resources, Keys.LegendForegroundBrush).Should().Be(Color.FromArgb(0xFF, 0xE6, 0xE6, 0xE6));
        resources[Keys.LegendFontSize].Should().Be(SystemFonts.MessageFontSize);
        ColorOf(resources, Keys.LegendCheckBoxBrush).Should().Be(Color.FromRgb(0xB0, 0xB0, 0xB0));
        ColorOf(resources, Keys.LegendCheckMarkBrush).Should().Be(Color.FromRgb(0xB0, 0xB0, 0xB0));
        resources[Keys.CratesFillAlpha].Should().Be((byte)0xD0);
        ColorOf(resources, Keys.CratesEdgeBrush).Should().Be(MissionPalette.RackEdge);
        ColorOf(resources, Keys.CratesRackNumberBrush).Should().Be(Colors.White);
        resources[Keys.CratesRackNumberFontSize].Should().Be(27.0);
        ColorOf(resources, Keys.CratesHaloBrush).Should().Be(Color.FromArgb(0xE0, 0x14, 0x14, 0x14));
        resources[Keys.CratesHaloSize].Should().Be(3.0);
        ColorOf(resources, Keys.ButtonsBackgroundBrush).Should().Be(Color.FromArgb(0x01, 0x00, 0x00, 0x00));
        resources[Keys.ButtonsBorderThickness].Should().Be(new Thickness(0));
        resources[Keys.ButtonsCornerRadius].Should().Be(new CornerRadius(3));
    }

    [Fact]
    public void The_opaque_window_background_is_the_window_colour_without_its_alpha()
    {
        var resources = Applied(AppearanceSettings.Default);

        ColorOf(resources, Keys.WindowOpaqueBackgroundBrush).Should().Be(Color.FromArgb(0xFF, 0x14, 0x14, 0x14));
    }

    [Fact]
    public void A_font_size_overrides_the_windows_size()
    {
        var resources = Applied(AppearanceSettings.Default with { Legend = LegendAppearance.Default with { FontSize = 16 } });

        resources[Keys.LegendFontSize].Should().Be(16.0);
    }

    [Fact]
    public void Check_box_and_check_mark_take_their_own_colours()
    {
        var resources = Applied(AppearanceSettings.Default with
        {
            Legend = LegendAppearance.Default with { CheckBoxColor = "#40FF0000", CheckMarkColor = "#FF00FF00" },
        });

        ColorOf(resources, Keys.LegendCheckBoxBrush).Should().Be(Color.FromArgb(0x40, 0xFF, 0x00, 0x00));
        ColorOf(resources, Keys.LegendCheckMarkBrush).Should().Be(Color.FromArgb(0xFF, 0x00, 0xFF, 0x00));
    }

    [Fact]
    public void The_selection_edge_defaults_to_the_look_before_it_was_a_setting()
    {
        // K4.4: magenta 2.5 px over a dark 4.5 px contour, as K4.2 drew it (D25 addendum).
        var resources = Applied(AppearanceSettings.Default);

        var pen = resources[Keys.CratesSelectionPen].Should().BeOfType<Pen>().Subject;
        ((SolidColorBrush)pen.Brush).Color.Should().Be(Color.FromRgb(0xFF, 0x00, 0xFF));
        pen.Thickness.Should().Be(2.5);
        var outline = resources[Keys.CratesSelectionOutlinePen].Should().BeOfType<Pen>().Subject;
        ((SolidColorBrush)outline.Brush).Color.Should().Be(Color.FromArgb(0xA0, 0, 0, 0));
        outline.Thickness.Should().Be(4.5);
        outline.LineJoin.Should().Be(PenLineJoin.Round);
        pen.IsFrozen.Should().BeTrue();
        outline.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void The_contour_is_the_edge_plus_its_size_on_both_sides_and_gone_when_off()
    {
        ResourceDictionary resources = new();
        ThemeResources.Apply(resources, AppearanceSettings.Default with
        {
            Crates = CrateAppearance.Default with { SelectionColor = "#FF00FF00", SelectionWidth = 4, SelectionOutline = new HaloSettings(true, "#FFFFFFFF", 2) },
        });

        ((Pen)resources[Keys.CratesSelectionPen]).Thickness.Should().Be(4);
        ((SolidColorBrush)((Pen)resources[Keys.CratesSelectionPen]).Brush).Color.Should().Be(Colors.Lime);
        ((Pen)resources[Keys.CratesSelectionOutlinePen]).Thickness.Should().Be(8);

        ThemeResources.Apply(resources, AppearanceSettings.Default with
        {
            Crates = CrateAppearance.Default with { SelectionOutline = CrateAppearance.Default.SelectionOutline with { Enabled = false } },
        });

        resources.Contains(Keys.CratesSelectionOutlinePen).Should().BeFalse();
    }

    [Fact]
    public void A_rack_number_font_size_is_passed_on()
    {
        var resources = Applied(AppearanceSettings.Default with { Crates = CrateAppearance.Default with { RackNumberFontSize = 40 } });

        resources[Keys.CratesRackNumberFontSize].Should().Be(40.0);
    }

    [Fact]
    public void Crate_edges_keep_their_hue_and_take_the_alpha()
    {
        var resources = Applied(AppearanceSettings.Default with { Crates = CrateAppearance.Default with { EdgeAlpha = 0x40 } });

        ColorOf(resources, Keys.CratesEdgeBrush).Should().Be(MissionPalette.RackEdge with { A = 0x40 });
    }

    [Fact]
    public void A_halo_that_is_on_is_a_frozen_glow()
    {
        var resources = Applied(AppearanceSettings.Default with
        {
            Legend = LegendAppearance.Default with { Halo = new HaloSettings(true, "#80102030", 6) },
        });

        var glow = resources[Keys.LegendHaloEffect].Should().BeOfType<DropShadowEffect>().Subject;
        glow.ShadowDepth.Should().Be(0);
        glow.BlurRadius.Should().Be(6);
        glow.Color.Should().Be(Color.FromRgb(0x10, 0x20, 0x30));
        glow.Opacity.Should().BeApproximately(0x80 / 255.0, 1e-9);
        glow.IsFrozen.Should().BeTrue();
    }

    [Fact]
    public void Turning_a_halo_off_removes_its_key()
    {
        // A null value through the indexer makes DynamicResource fall back to the property's default, not deliver null
        // (spike T4.1); removing says the same openly. Every property bound to a halo key defaults to null.
        ResourceDictionary resources = new();
        HaloSettings on = new(true, "#FF000000", 3);
        ThemeResources.Apply(resources, AppearanceSettings.Default with
        {
            Header = HeaderAppearance.Default with { Halo = on },
            Legend = LegendAppearance.Default with { Halo = on },
            Buttons = ButtonAppearance.Default with { Halo = on },
        });

        ThemeResources.Apply(resources, AppearanceSettings.Default with { Crates = CrateAppearance.Default with { Halo = HaloSettings.Off } });

        resources.Contains(Keys.HeaderHaloEffect).Should().BeFalse();
        resources.Contains(Keys.LegendHaloEffect).Should().BeFalse();
        resources.Contains(Keys.ButtonsHaloEffect).Should().BeFalse();
        resources.Contains(Keys.CratesHaloBrush).Should().BeFalse();
    }

    [Fact]
    public void Brushes_are_frozen()
    {
        var resources = Applied(AppearanceSettings.Default);

        resources.Values.OfType<Freezable>().Should().OnlyContain(f => f.IsFrozen);
    }
}
