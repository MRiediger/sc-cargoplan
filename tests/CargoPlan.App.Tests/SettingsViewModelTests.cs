using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

public sealed class SettingsViewModelTests
{
    private const double WindowsFontSize = 11;

    private readonly List<(AppearanceSettings Appearance, LegendSettings Legend)> _applied = [];
    private readonly List<ToolWindowTheme> _themes = [];
    private readonly List<ShipOutlineMode> _outlines = [];
    private readonly List<bool> _downloads = [];
    private readonly List<double> _opacities = [];

    private SettingsViewModel Create(
        AppearanceSettings? appearance = null,
        LegendSettings? legend = null,
        double windowsFontSize = WindowsFontSize,
        ToolWindowTheme toolWindowTheme = ToolWindowTheme.System,
        ShipOutlineMode shipOutline = ShipOutlineMode.Outline,
        bool downloadShipHolograms = true,
        double holoOpacity = 0.4) =>
        new(
            appearance ?? AppearanceSettings.Default,
            legend ?? LegendSettings.Default,
            windowsFontSize,
            (a, l) => _applied.Add((a, l)),
            toolWindowTheme,
            _themes.Add,
            shipOutline,
            _outlines.Add,
            downloadShipHolograms,
            _downloads.Add,
            holoOpacity,
            _opacities.Add);

    private (AppearanceSettings Appearance, LegendSettings Legend) Last => _applied[^1];

    /// <summary>A look with every value of the window and legend sections off its default, the other sections too.</summary>
    private static readonly AppearanceSettings Changed = new(
        new WindowAppearance("#40102030", "#FF00FF00", 2),
        new HeaderAppearance("#FF123456", HaloSettings.On),
        new LegendAppearance("#FFABCDEF", 14, new HaloSettings(true, "#80FFFFFF", 5)),
        new CrateAppearance(FillAlpha: 100),
        new ButtonAppearance(CornerRadius: 0));

    [Fact]
    public void Opening_applies_nothing()
    {
        Create();

        _applied.Should().BeEmpty();
    }

    [Fact]
    public void A_value_change_applies_once_with_only_that_value_changed()
    {
        var viewModel = Create(Changed);

        viewModel.WindowBorderThickness = 5;

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Window = Changed.Window with { BorderThickness = 5 } });
        Last.Legend.Should().Be(LegendSettings.Default);
    }

    [Fact]
    public void Fields_start_with_the_current_look()
    {
        var viewModel = Create(Changed);

        viewModel.WindowBackground.Hex.Should().Be("#102030");
        viewModel.WindowBackground.Alpha.Should().Be(0x40);
        viewModel.WindowBorderColor.Value.Should().Be("#FF00FF00");
        viewModel.WindowBorderThickness.Should().Be(2);
        viewModel.LegendForeground.Value.Should().Be("#FFABCDEF");
        viewModel.UseWindowsFontSize.Should().BeFalse();
        viewModel.LegendFontSize.Should().Be(14);
        viewModel.LegendHalo.Value.Should().Be(new HaloSettings(true, "#80FFFFFF", 5));
    }

    [Fact]
    public void Hex_and_alpha_make_the_colour()
    {
        var viewModel = Create();

        viewModel.WindowBackground.Hex = "#FF0000";
        viewModel.WindowBackground.Alpha = 0xA0;

        Last.Appearance.Window.Background.Should().Be("#A0FF0000");
    }

    [Fact]
    public void Alpha_keeps_the_hex_colour()
    {
        var viewModel = Create();

        viewModel.WindowBackground.Alpha = 0x20;

        Last.Appearance.Window.Background.Should().Be("#20141414");
    }

    [Theory]
    [InlineData("ff0000")]
    [InlineData("#ff0000")]
    [InlineData("#FF0000")]
    public void Hex_takes_any_case_with_or_without_hash(string hex)
    {
        var viewModel = Create();

        viewModel.WindowBorderColor.Hex = hex;

        viewModel.WindowBorderColor.IsValid.Should().BeTrue();
        Last.Appearance.Window.BorderColor.Should().Be("#FFFF0000");
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("red")]
    [InlineData("#80FF0000")]
    [InlineData("")]
    [InlineData("#GG0000")]
    public void Invalid_hex_is_marked_and_applies_nothing(string hex)
    {
        var viewModel = Create();

        viewModel.WindowBorderColor.Hex = hex;
        viewModel.WindowBorderColor.Alpha = 0x80;

        viewModel.WindowBorderColor.IsValid.Should().BeFalse();
        _applied.Should().BeEmpty();
    }

    [Fact]
    public void Fixing_an_invalid_hex_applies_again()
    {
        var viewModel = Create();
        viewModel.WindowBorderColor.Hex = "#12";

        viewModel.WindowBorderColor.Hex = "#123456";

        viewModel.WindowBorderColor.IsValid.Should().BeTrue();
        Last.Appearance.Window.BorderColor.Should().Be("#FF123456");
    }

    [Fact]
    public void Other_values_apply_with_the_last_valid_colour_while_a_hex_is_invalid()
    {
        var viewModel = Create();
        viewModel.WindowBorderColor.Hex = "nope";

        viewModel.WindowBorderThickness = 3;

        Last.Appearance.Window.Should().Be(WindowAppearance.Default with { BorderThickness = 3 });
    }

    [Fact]
    public void Clickable_backgrounds_keep_alpha_one()
    {
        var viewModel = Create();

        viewModel.WindowBackground.MinAlpha.Should().Be(AppearanceRules.MinClickableAlpha);
        viewModel.WindowBorderColor.MinAlpha.Should().Be(0);
        viewModel.LegendForeground.MinAlpha.Should().Be(0);
    }

    [Fact]
    public void Windows_text_size_means_no_font_size()
    {
        var viewModel = Create(Changed);

        viewModel.UseWindowsFontSize = true;

        Last.Appearance.Legend.FontSize.Should().BeNull();
        viewModel.LegendFontSize.Should().Be(WindowsFontSize);
    }

    [Fact]
    public void Default_legend_shows_the_windows_size_on_the_slider()
    {
        var viewModel = Create();

        viewModel.UseWindowsFontSize.Should().BeTrue();
        viewModel.LegendFontSize.Should().Be(WindowsFontSize);
    }

    [Theory]
    [InlineData(11, 11)]
    [InlineData(7, AppearanceRules.MinFontSize)]
    [InlineData(40, AppearanceRules.MaxFontSize)]
    public void Unticking_windows_text_size_starts_at_the_windows_size_in_range(double windows, double expected)
    {
        var viewModel = Create(windowsFontSize: windows);

        viewModel.UseWindowsFontSize = false;

        Last.Appearance.Legend.FontSize.Should().Be(expected);
    }

    [Fact]
    public void Slider_sets_the_font_size()
    {
        var viewModel = Create();
        viewModel.UseWindowsFontSize = false;

        viewModel.LegendFontSize = 18;

        Last.Appearance.Legend.FontSize.Should().Be(18);
    }

    [Fact]
    public void Turning_a_halo_off_keeps_colour_and_size()
    {
        var viewModel = Create(Changed);

        viewModel.LegendHalo.Enabled = false;

        Last.Appearance.Legend.Halo.Should().Be(new HaloSettings(false, "#80FFFFFF", 5));
    }

    [Fact]
    public void Halo_colour_and_size_apply()
    {
        var viewModel = Create();

        viewModel.LegendHalo.Colour.Hex = "#FFFFFF";
        viewModel.LegendHalo.Size = 7;

        Last.Appearance.Legend.Halo.Should().Be(HaloSettings.Off with { Color = "#E0FFFFFF", Size = 7 });
    }

    [Fact]
    public void Legend_layout_applies_with_the_look_unchanged()
    {
        var viewModel = Create(Changed);

        viewModel.IsLegendVisible = false;
        viewModel.LegendPosition = "top-right";

        Last.Legend.Should().Be(new LegendSettings(false, "top-right"));
        Last.Appearance.Should().Be(Changed);
    }

    [Fact]
    public void Positions_are_the_six_names()
    {
        Create().LegendPositions.Should().Equal(LegendPosition.Names);
    }

    [Fact]
    public void Reset_window_resets_only_the_window_once()
    {
        var viewModel = Create(Changed, new LegendSettings(false, "top-center"));

        viewModel.ResetWindowCommand.Execute(null);

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Window = WindowAppearance.Default });
        Last.Legend.Should().Be(new LegendSettings(false, "top-center"));
        viewModel.WindowBackground.Value.Should().Be(WindowAppearance.Default.Background);
        viewModel.WindowBorderThickness.Should().Be(WindowAppearance.Default.BorderThickness);
    }

    [Fact]
    public void Reset_legend_resets_its_look_but_not_its_position()
    {
        var viewModel = Create(Changed, new LegendSettings(false, "top-center"));

        viewModel.ResetLegendCommand.Execute(null);

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Legend = LegendAppearance.Default });
        Last.Legend.Should().Be(new LegendSettings(false, "top-center"));
        viewModel.UseWindowsFontSize.Should().BeTrue();
        viewModel.LegendHalo.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Reset_legend_layout_resets_only_the_layout()
    {
        var viewModel = Create(Changed, new LegendSettings(false, "top-center"));

        viewModel.ResetLegendLayoutCommand.Execute(null);

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed);
        Last.Legend.Should().Be(LegendSettings.Default);
        viewModel.IsLegendVisible.Should().BeTrue();
        viewModel.LegendPosition.Should().Be("bottom-left");
    }

    [Fact]
    public void Reset_heals_an_invalid_hex()
    {
        var viewModel = Create();
        viewModel.WindowBorderColor.Hex = "nope";

        viewModel.ResetWindowCommand.Execute(null);

        viewModel.WindowBorderColor.IsValid.Should().BeTrue();
        viewModel.WindowBorderColor.Hex.Should().Be("#E6E6E6");
    }

    // Crates, header, buttons (T4.2b)

    [Fact]
    public void Fields_of_crates_header_and_buttons_start_with_the_current_look()
    {
        var appearance = Changed with
        {
            Crates = new CrateAppearance(100, 50, "#FF00FFFF", 30, HaloSettings.Off),
            Buttons = new ButtonAppearance("#20FFFFFF", "#FF0000FF", 2, 5, HaloSettings.On),
        };

        var viewModel = Create(appearance);

        viewModel.CratesFillAlpha.Should().Be(100);
        viewModel.CratesEdgeAlpha.Should().Be(50);
        viewModel.CratesRackNumberColor.Value.Should().Be("#FF00FFFF");
        viewModel.CratesRackNumberFontSize.Should().Be(30);
        viewModel.CratesHalo.Enabled.Should().BeFalse();
        viewModel.HeaderForeground.Value.Should().Be("#FF123456");
        viewModel.HeaderHalo.Enabled.Should().BeTrue();
        viewModel.ButtonsBackground.Value.Should().Be("#20FFFFFF");
        viewModel.ButtonsBorderColor.Value.Should().Be("#FF0000FF");
        viewModel.ButtonsBorderThickness.Should().Be(2);
        viewModel.ButtonsCornerRadius.Should().Be(5);
        viewModel.ButtonsHalo.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Crate_values_apply_once_with_only_that_value_changed()
    {
        var viewModel = Create(Changed);

        viewModel.CratesEdgeAlpha = 10;

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Crates = Changed.Crates with { EdgeAlpha = 10 } });
    }

    [Fact]
    public void Every_crate_value_applies()
    {
        var viewModel = Create();

        viewModel.CratesFillAlpha = 64;
        viewModel.CratesRackNumberColor.Hex = "#FFFF00";
        viewModel.CratesRackNumberFontSize = 40;
        viewModel.CratesHalo.Enabled = false;

        Last.Appearance.Crates.Should().Be(new CrateAppearance(64, 0xC0, "#FFFFFF00", 40, HaloSettings.On with { Enabled = false }));
    }

    [Fact]
    public void Header_values_apply()
    {
        var viewModel = Create();

        viewModel.HeaderForeground.Hex = "#00FF00";
        viewModel.HeaderHalo.Enabled = true;

        Last.Appearance.Header.Should().Be(new HeaderAppearance("#FF00FF00", HaloSettings.On));
    }

    [Fact]
    public void Every_button_value_applies()
    {
        var viewModel = Create();

        viewModel.ButtonsBackground.Alpha = 0x80;
        viewModel.ButtonsBorderColor.Hex = "#FF0000";
        viewModel.ButtonsBorderThickness = 1;
        viewModel.ButtonsCornerRadius = 0;
        viewModel.ButtonsHalo.Size = 6;

        Last.Appearance.Buttons.Should().Be(new ButtonAppearance("#80000000", "#FFFF0000", 1, 0, HaloSettings.Off with { Size = 6 }));
    }

    [Fact]
    public void Button_background_keeps_alpha_one()
    {
        Create().ButtonsBackground.MinAlpha.Should().Be(AppearanceRules.MinClickableAlpha);
    }

    [Fact]
    public void Reset_crates_resets_only_the_crates_once()
    {
        var viewModel = Create(Changed, new LegendSettings(false, "top-center"));

        viewModel.ResetCratesCommand.Execute(null);

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Crates = CrateAppearance.Default });
        Last.Legend.Should().Be(new LegendSettings(false, "top-center"));
        viewModel.CratesFillAlpha.Should().Be(CrateAppearance.Default.FillAlpha);
        viewModel.CratesHalo.Enabled.Should().BeTrue();
    }

    [Fact]
    public void Reset_header_resets_only_the_header_once()
    {
        var viewModel = Create(Changed);

        viewModel.ResetHeaderCommand.Execute(null);

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Header = HeaderAppearance.Default });
        viewModel.HeaderForeground.Value.Should().Be(HeaderAppearance.Default.Foreground);
    }

    [Fact]
    public void Reset_buttons_resets_only_the_buttons_once()
    {
        var viewModel = Create(Changed);

        viewModel.ResetButtonsCommand.Execute(null);

        _applied.Should().ContainSingle();
        Last.Appearance.Should().Be(Changed with { Buttons = ButtonAppearance.Default });
        viewModel.ButtonsCornerRadius.Should().Be(ButtonAppearance.Default.CornerRadius);
    }

    /// <summary>The crates' halo is the sharp outline of the rack numbers, its size a stroke width (D31): the window says so.</summary>
    [Fact]
    public void Each_halo_says_what_it_surrounds()
    {
        var viewModel = Create();

        viewModel.LegendHalo.Label.Should().Be("Halo around the text");
        viewModel.CratesHalo.Label.Should().Be("Outline around the rack numbers and the bow label");
        viewModel.HeaderHalo.Label.Should().Be("Halo around the ship name");
        viewModel.ButtonsHalo.Label.Should().Be("Halo around the glyphs");
    }

    [Theory]
    [InlineData(0, "0 %")]
    [InlineData(208, "82 %")]
    [InlineData(255, "100 %")]
    public void Crate_alphas_show_as_percent_like_the_colours(int alpha, string expected)
    {
        var viewModel = Create();

        viewModel.CratesFillAlpha = alpha;
        viewModel.CratesEdgeAlpha = alpha;

        viewModel.CratesFillPercent.Should().Be(expected);
        viewModel.CratesEdgePercent.Should().Be(expected);
        ColourField.Percent(alpha).Should().Be(expected);
    }

    /// <summary>
    /// The sliders take their limits from <see cref="AppearanceRules"/> (x:Static in the window): at every limit the result
    /// needs no correction, so the settings window never causes a notice.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Values_at_the_slider_limits_need_no_correction(bool atMinimum)
    {
        var viewModel = Create();
        viewModel.UseWindowsFontSize = false;

        ColourField[] colours =
        [
            viewModel.WindowBackground, viewModel.WindowBorderColor, viewModel.LegendForeground, viewModel.CratesRackNumberColor,
            viewModel.HeaderForeground, viewModel.ButtonsBackground, viewModel.ButtonsBorderColor,
        ];
        HaloEditor[] halos = [viewModel.LegendHalo, viewModel.CratesHalo, viewModel.HeaderHalo, viewModel.ButtonsHalo];
        foreach (var field in colours.Concat(halos.Select(h => h.Colour)))
        {
            field.Alpha = atMinimum ? field.MinAlpha : byte.MaxValue;
        }

        foreach (var halo in halos)
        {
            halo.Size = atMinimum ? AppearanceRules.MinHaloSize : AppearanceRules.MaxHaloSize;
        }

        viewModel.WindowBorderThickness = atMinimum ? 0 : AppearanceRules.MaxBorderThickness;
        viewModel.LegendFontSize = atMinimum ? AppearanceRules.MinFontSize : AppearanceRules.MaxFontSize;
        viewModel.CratesFillAlpha = atMinimum ? byte.MinValue : byte.MaxValue;
        viewModel.CratesEdgeAlpha = atMinimum ? byte.MinValue : byte.MaxValue;
        viewModel.CratesRackNumberFontSize = atMinimum ? AppearanceRules.MinRackNumberFontSize : AppearanceRules.MaxRackNumberFontSize;
        viewModel.ButtonsBorderThickness = atMinimum ? 0 : AppearanceRules.MaxBorderThickness;
        viewModel.ButtonsCornerRadius = atMinimum ? 0 : AppearanceRules.MaxCornerRadius;

        AppearanceRules.Normalize(Last.Appearance).Problems.Should().BeEmpty();
        AppearanceRules.Normalize(Last.Legend).Problems.Should().BeEmpty();
    }

    // Tool windows (T4.7b): not part of the look, so it has its own callback and no reset.

    [Fact]
    public void The_tool_window_theme_starts_as_given_and_opening_changes_nothing()
    {
        var viewModel = Create(toolWindowTheme: ToolWindowTheme.Dark);

        viewModel.ToolWindowTheme.Should().Be(ToolWindowTheme.Dark);
        _themes.Should().BeEmpty();
    }

    [Fact]
    public void Choosing_a_tool_window_theme_calls_its_callback_not_apply()
    {
        var viewModel = Create();

        viewModel.ToolWindowTheme = ToolWindowTheme.Light;

        _themes.Should().Equal(ToolWindowTheme.Light);
        _applied.Should().BeEmpty();
    }

    [Fact]
    public void Choosing_the_same_tool_window_theme_again_calls_nothing()
    {
        var viewModel = Create(toolWindowTheme: ToolWindowTheme.Light);

        viewModel.ToolWindowTheme = ToolWindowTheme.Light;

        _themes.Should().BeEmpty();
    }

    [Fact]
    public void No_reset_touches_the_tool_window_theme()
    {
        var viewModel = Create(Changed, toolWindowTheme: ToolWindowTheme.Dark);

        viewModel.ResetWindowCommand.Execute(null);
        viewModel.ResetLegendCommand.Execute(null);
        viewModel.ResetLegendLayoutCommand.Execute(null);
        viewModel.ResetCratesCommand.Execute(null);
        viewModel.ResetHeaderCommand.Execute(null);
        viewModel.ResetButtonsCommand.Execute(null);

        viewModel.ToolWindowTheme.Should().Be(ToolWindowTheme.Dark);
        _themes.Should().BeEmpty();
    }

    [Fact]
    public void The_tool_window_choices_are_every_mode_in_the_file_order_with_player_words()
    {
        SettingsViewModel.ToolWindowThemeChoices.Select(c => ToolWindowThemes.ToName(c.Value)).Should().Equal(ToolWindowThemes.Names);
        SettingsViewModel.ToolWindowThemeChoices.Select(c => c.Text).Should().Equal("Follow Windows", "Light", "Dark");
    }

    [Fact]
    public void Choosing_a_ship_outline_mode_calls_its_callback_not_apply()
    {
        var viewModel = Create();

        viewModel.ShipOutline = ShipOutlineMode.None;

        _outlines.Should().Equal(ShipOutlineMode.None);
        _applied.Should().BeEmpty();
    }

    [Fact]
    public void The_constructor_loads_the_ship_outline_mode_without_calling_back()
    {
        var viewModel = Create(shipOutline: ShipOutlineMode.None);

        viewModel.ShipOutline.Should().Be(ShipOutlineMode.None);
        _outlines.Should().BeEmpty();
    }

    [Fact]
    public void Reset_of_the_ship_view_goes_back_to_the_outline_and_downloads_on()
    {
        var viewModel = Create(shipOutline: ShipOutlineMode.None, downloadShipHolograms: false);

        viewModel.ResetShipViewCommand.Execute(null);

        viewModel.ShipOutline.Should().Be(ShipOutlineMode.Outline);
        viewModel.DownloadShipHolograms.Should().BeTrue();
        _outlines.Should().Equal(ShipOutlineMode.Outline);
        _downloads.Should().Equal(true);
    }

    [Fact]
    public void The_download_switch_calls_its_callback_not_apply_and_loads_without_calling_back()
    {
        var viewModel = Create(downloadShipHolograms: false);
        viewModel.DownloadShipHolograms.Should().BeFalse();
        _downloads.Should().BeEmpty();

        viewModel.DownloadShipHolograms = true;

        _downloads.Should().Equal(true);
        _applied.Should().BeEmpty();
    }

    [Fact]
    public void The_opacity_slider_works_in_percent_and_calls_its_callback_with_the_share()
    {
        var viewModel = Create(holoOpacity: 0.65);
        viewModel.HoloOpacityPercent.Should().Be(65);
        viewModel.HoloOpacityText.Should().Be("65 %");
        _opacities.Should().BeEmpty();

        viewModel.HoloOpacityPercent = 30;

        _opacities.Should().Equal(0.3);
        _applied.Should().BeEmpty();
    }

    [Fact]
    public void Reset_of_the_ship_view_sets_the_opacity_back_to_40_percent()
    {
        var viewModel = Create(holoOpacity: 0.8);

        viewModel.ResetShipViewCommand.Execute(null);

        viewModel.HoloOpacityPercent.Should().Be(40);
        _opacities.Should().Equal(0.4);
    }

    [Theory]
    [InlineData(ShipOutlineMode.Outline, false)]
    [InlineData(ShipOutlineMode.HoloHiddenline, true)]
    [InlineData(ShipOutlineMode.None, false)]
    public void The_opacity_slider_is_on_only_for_a_hologram_look(ShipOutlineMode mode, bool on)
    {
        var viewModel = Create(shipOutline: ShipOutlineMode.HoloFlat);

        viewModel.ShipOutline = mode;

        viewModel.IsHologramLook.Should().Be(on);
    }

    [Fact]
    public void The_switch_says_what_it_sends_and_what_stays()
    {
        SettingsViewModel.DownloadShipHologramsHint.Should().Be(
            "Downloads one file per ship from github.com when you pick a ship. Nothing else is sent. Already downloaded holograms stay.");
    }

    [Fact]
    public void The_ship_outline_choices_are_every_mode_in_the_file_order_with_player_words()
    {
        SettingsViewModel.ShipOutlineChoices.Select(c => ShipOutlineModes.ToName(c.Value)).Should().Equal(ShipOutlineModes.Names);
        SettingsViewModel.ShipOutlineChoices.Select(c => c.Text).Should().Equal("Outline", "Hologram – shaded", "Hologram – flat", "Hologram – hidden line", "None");
    }
}
