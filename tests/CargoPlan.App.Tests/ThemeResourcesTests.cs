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
        resources[Keys.CratesFillAlpha].Should().Be((byte)0xD0);
        ColorOf(resources, Keys.CratesEdgeBrush).Should().Be(MissionPalette.RackEdge);
        ColorOf(resources, Keys.CratesRackNumberBrush).Should().Be(Colors.White);
        resources[Keys.CratesRackNumberFontSize].Should().Be(27.0);
        ColorOf(resources, Keys.CratesHaloBrush).Should().Be(Color.FromArgb(0xE0, 0x14, 0x14, 0x14));
        resources[Keys.CratesHaloSize].Should().Be(3.0);
        resources[Keys.CratesShowRackLoad].Should().Be(true);
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
    public void A_rack_number_font_size_is_passed_on()
    {
        var resources = Applied(AppearanceSettings.Default with { Crates = CrateAppearance.Default with { RackNumberFontSize = 40 } });

        resources[Keys.CratesRackNumberFontSize].Should().Be(40.0);
    }

    [Fact]
    public void Rack_load_off_is_passed_on()
    {
        var resources = Applied(AppearanceSettings.Default with { Crates = CrateAppearance.Default with { ShowRackLoad = false } });

        resources[Keys.CratesShowRackLoad].Should().Be(false);
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
