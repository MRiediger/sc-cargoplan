using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

public sealed class SettingsViewModelTests
{
    private const double WindowsFontSize = 11;

    private readonly List<(AppearanceSettings Appearance, LegendSettings Legend)> _applied = [];

    private SettingsViewModel Create(AppearanceSettings? appearance = null, LegendSettings? legend = null, double windowsFontSize = WindowsFontSize) =>
        new(appearance ?? AppearanceSettings.Default, legend ?? LegendSettings.Default, windowsFontSize, (a, l) => _applied.Add((a, l)));

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

        foreach (var field in new[] { viewModel.WindowBackground, viewModel.WindowBorderColor, viewModel.LegendForeground, viewModel.LegendHalo.Colour })
        {
            field.Alpha = atMinimum ? field.MinAlpha : byte.MaxValue;
        }

        viewModel.WindowBorderThickness = atMinimum ? 0 : AppearanceRules.MaxBorderThickness;
        viewModel.LegendFontSize = atMinimum ? AppearanceRules.MinFontSize : AppearanceRules.MaxFontSize;
        viewModel.LegendHalo.Size = atMinimum ? AppearanceRules.MinHaloSize : AppearanceRules.MaxHaloSize;

        AppearanceRules.Normalize(Last.Appearance).Problems.Should().BeEmpty();
        AppearanceRules.Normalize(Last.Legend).Problems.Should().BeEmpty();
    }
}
