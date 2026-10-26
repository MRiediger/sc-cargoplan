using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public sealed class AppearanceRulesTests
{
    [Fact]
    public void Defaults_are_the_look_before_settings()
    {
        // The literals the views and MissionPalette used up to T3.15: without a file the overlay must look the same (T4.1).
        var appearance = AppearanceSettings.Default;

        appearance.Window.Should().Be(new WindowAppearance(Background: "#A0141414", BorderColor: "#FFE6E6E6", BorderThickness: 0));
        appearance.Header.Should().Be(new HeaderAppearance(Foreground: "#FFE6E6E6", Halo: new HaloSettings(false, "#E0141414", 3)));
        appearance.Legend.Should().Be(new LegendAppearance(
            Foreground: "#FFE6E6E6", FontSize: null, Halo: new HaloSettings(false, "#E0141414", 3), CheckBoxColor: "#FFB0B0B0", CheckMarkColor: "#FFB0B0B0"));
        appearance.Crates.Should().Be(new CrateAppearance(
            FillAlpha: 0xD0, EdgeAlpha: 0xC0, RackNumberColor: "#FFFFFFFF", RackNumberFontSize: 27, Halo: new HaloSettings(true, "#E0141414", 3), StopPalette: "okabeIto",
            SelectionColor: "#FFFF00FF", SelectionWidth: 2.5, SelectionOutline: new HaloSettings(true, "#A0000000", 1)));
        appearance.Buttons.Should().Be(new ButtonAppearance(
            Background: "#01000000", BorderColor: "#FFE6E6E6", BorderThickness: 0, CornerRadius: 3, Halo: new HaloSettings(false, "#E0141414", 3)));
    }

    [Fact]
    public void Defaults_pass_without_problems()
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default);

        appearance.Should().Be(AppearanceSettings.Default);
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Valid_values_pass_unchanged()
    {
        AppearanceSettings custom = new(
            new WindowAppearance("#E0300000", "#FF00FF00", 10),
            new HeaderAppearance("#FFFFFF00", new HaloSettings(true, "#FF000000", 20)),
            new LegendAppearance("#FF00FFFF", 32, new HaloSettings(true, "#80000000", 1), "#40FF0000", "#FF00FF00"),
            new CrateAppearance(0, 255, "#FFFF0000", 48, HaloSettings.Off, "tolMuted", "#8000FF00", 8, new HaloSettings(false, "#FFFFFFFF", 20)),
            new ButtonAppearance("#01FFFFFF", "#FFFFFFFF", 0, 13, HaloSettings.Off));

        var (appearance, problems) = AppearanceRules.Normalize(custom);

        appearance.Should().Be(custom);
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Font_size_null_means_the_windows_size_and_passes()
    {
        // null is the default; resolving it to the Windows message font size is the theme's job, not the model's.
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with { Legend = LegendAppearance.Default with { FontSize = null } });

        appearance.Legend.FontSize.Should().BeNull();
        problems.Should().BeEmpty();
    }

    public static TheoryData<string, Func<AppearanceSettings, AppearanceSettings>, Func<AppearanceSettings, double>, double> OutOfRange => new()
    {
        { "legend.fontSize", a => a with { Legend = a.Legend with { FontSize = 0 } }, a => a.Legend.FontSize!.Value, 8 },
        { "legend.fontSize", a => a with { Legend = a.Legend with { FontSize = 100 } }, a => a.Legend.FontSize!.Value, 32 },
        { "legend.halo.size", a => a with { Legend = a.Legend with { Halo = a.Legend.Halo with { Size = 0 } } }, a => a.Legend.Halo.Size, 1 },
        { "header.halo.size", a => a with { Header = a.Header with { Halo = a.Header.Halo with { Size = 50 } } }, a => a.Header.Halo.Size, 20 },
        { "crates.halo.size", a => a with { Crates = a.Crates with { Halo = a.Crates.Halo with { Size = 21 } } }, a => a.Crates.Halo.Size, 20 },
        { "buttons.halo.size", a => a with { Buttons = a.Buttons with { Halo = a.Buttons.Halo with { Size = -1 } } }, a => a.Buttons.Halo.Size, 1 },
        { "window.borderThickness", a => a with { Window = a.Window with { BorderThickness = -1 } }, a => a.Window.BorderThickness, 0 },
        { "window.borderThickness", a => a with { Window = a.Window with { BorderThickness = 11 } }, a => a.Window.BorderThickness, 10 },
        { "buttons.borderThickness", a => a with { Buttons = a.Buttons with { BorderThickness = 12 } }, a => a.Buttons.BorderThickness, 10 },
        { "buttons.cornerRadius", a => a with { Buttons = a.Buttons with { CornerRadius = -2 } }, a => a.Buttons.CornerRadius, 0 },
        { "buttons.cornerRadius", a => a with { Buttons = a.Buttons with { CornerRadius = 20 } }, a => a.Buttons.CornerRadius, 13 },
        { "crates.fillAlpha", a => a with { Crates = a.Crates with { FillAlpha = -5 } }, a => a.Crates.FillAlpha, 0 },
        { "crates.fillAlpha", a => a with { Crates = a.Crates with { FillAlpha = 300 } }, a => a.Crates.FillAlpha, 255 },
        { "crates.edgeAlpha", a => a with { Crates = a.Crates with { EdgeAlpha = 300 } }, a => a.Crates.EdgeAlpha, 255 },
        { "crates.rackNumberFontSize", a => a with { Crates = a.Crates with { RackNumberFontSize = 0 } }, a => a.Crates.RackNumberFontSize, 8 },
        { "crates.rackNumberFontSize", a => a with { Crates = a.Crates with { RackNumberFontSize = 100 } }, a => a.Crates.RackNumberFontSize, 48 },
        { "crates.selectionWidth", a => a with { Crates = a.Crates with { SelectionWidth = 0 } }, a => a.Crates.SelectionWidth, 1 },
        { "crates.selectionWidth", a => a with { Crates = a.Crates with { SelectionWidth = 12 } }, a => a.Crates.SelectionWidth, 8 },
        { "crates.selectionOutline.size", a => a with { Crates = a.Crates with { SelectionOutline = a.Crates.SelectionOutline with { Size = 0 } } }, a => a.Crates.SelectionOutline.Size, 1 },
    };

    [Theory]
    [MemberData(nameof(OutOfRange))]
    public void Value_out_of_range_is_clamped_with_a_problem(
        string key, Func<AppearanceSettings, AppearanceSettings> set, Func<AppearanceSettings, double> get, double expected)
    {
        var (appearance, problems) = AppearanceRules.Normalize(set(AppearanceSettings.Default));

        get(appearance).Should().Be(expected);
        problems.Should().ContainSingle().Which.Should().Contain($"appearance.{key}").And.Contain($"using {expected}");
    }

    [Fact]
    public void Short_and_lower_case_colours_are_written_in_full()
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Header = HeaderAppearance.Default with { Foreground = "#00ff00" },
            Legend = LegendAppearance.Default with { Foreground = "#80abcdef" },
        });

        appearance.Header.Foreground.Should().Be("#FF00FF00");
        appearance.Legend.Foreground.Should().Be("#80ABCDEF");
        problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("red")]
    [InlineData("#12345")]
    [InlineData("#GG000000")]
    [InlineData("")]
    [InlineData("A0141414")]
    [InlineData("#A0141414 ")]
    public void Invalid_colour_takes_its_default_with_a_problem(string text)
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Crates = CrateAppearance.Default with { RackNumberColor = text },
        });

        appearance.Crates.RackNumberColor.Should().Be(CrateAppearance.Default.RackNumberColor);
        problems.Should().ContainSingle().Which.Should().Contain("appearance.crates.rackNumberColor");
    }

    [Theory]
    [InlineData("checkBoxColor")]
    [InlineData("checkMarkColor")]
    public void Invalid_check_box_colour_takes_the_grey_with_a_problem(string key)
    {
        var legend = key == "checkBoxColor"
            ? LegendAppearance.Default with { CheckBoxColor = "grey" }
            : LegendAppearance.Default with { CheckMarkColor = "grey" };

        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with { Legend = legend });

        appearance.Legend.Should().Be(LegendAppearance.Default);
        problems.Should().ContainSingle().Which.Should().Contain($"appearance.legend.{key}").And.Contain("using #FFB0B0B0");
    }

    [Theory]
    [InlineData("ibm")]
    [InlineData("OkabeIto")]
    [InlineData("tol-bright")]
    [InlineData("")]
    public void Unknown_stop_palette_takes_okabe_ito_with_a_problem(string name)
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Crates = CrateAppearance.Default with { StopPalette = name },
        });

        appearance.Crates.StopPalette.Should().Be("okabeIto");
        problems.Should().ContainSingle().Which.Should().Be(
            $"appearance.crates.stopPalette \"{name}\" is not valid (okabeIto, tolBright or tolMuted), using okabeIto.");
    }

    [Fact]
    public void Invalid_selection_colour_takes_the_magenta_with_a_problem()
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Crates = CrateAppearance.Default with { SelectionColor = "pink" },
        });

        appearance.Crates.SelectionColor.Should().Be("#FFFF00FF");
        problems.Should().ContainSingle().Which.Should().Contain("appearance.crates.selectionColor");
    }

    [Fact]
    public void Invalid_halo_colour_takes_the_halo_default()
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Legend = LegendAppearance.Default with { Halo = new HaloSettings(true, "black", 4) },
        });

        appearance.Legend.Halo.Should().Be(new HaloSettings(true, HaloSettings.Off.Color, 4));
        problems.Should().ContainSingle().Which.Should().Contain("appearance.legend.halo.color");
    }

    [Fact]
    public void Transparent_backgrounds_that_take_clicks_get_alpha_1()
    {
        // A layered window passes clicks on pixels with alpha 0 to the window below: the overlay could no longer be moved.
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Window = WindowAppearance.Default with { Background = "#00141414" },
            Buttons = ButtonAppearance.Default with { Background = "#00FFFFFF" },
        });

        appearance.Window.Background.Should().Be("#01141414");
        appearance.Buttons.Background.Should().Be("#01FFFFFF");
        problems.Should().HaveCount(2);
        problems.Should().Contain(p => p.Contains("appearance.window.background")).And.Contain(p => p.Contains("appearance.buttons.background"));
    }

    [Fact]
    public void Transparent_text_and_borders_are_allowed()
    {
        var (appearance, problems) = AppearanceRules.Normalize(AppearanceSettings.Default with
        {
            Window = WindowAppearance.Default with { BorderColor = "#00FFFFFF" },
            Legend = LegendAppearance.Default with { Foreground = "#00FFFFFF", CheckBoxColor = "#00B0B0B0", CheckMarkColor = "#00B0B0B0" },
        });

        // The check box keeps its own hit-test background, so a see-through box still takes the click.
        appearance.Window.BorderColor.Should().Be("#00FFFFFF");
        appearance.Legend.Foreground.Should().Be("#00FFFFFF");
        appearance.Legend.CheckBoxColor.Should().Be("#00B0B0B0");
        appearance.Legend.CheckMarkColor.Should().Be("#00B0B0B0");
        problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("system")]
    [InlineData("light")]
    [InlineData("dark")]
    public void Valid_tool_window_theme_passes_unchanged(string theme)
    {
        var (normalized, problems) = AppearanceRules.NormalizeToolWindowTheme(theme);

        normalized.Should().Be(theme);
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_tool_window_theme_follows_Windows_with_a_problem()
    {
        var (normalized, problems) = AppearanceRules.NormalizeToolWindowTheme("blue");

        normalized.Should().Be("system");
        problems.Should().ContainSingle().Which.Should()
            .Be("toolWindowTheme \"blue\" is not valid (system, light or dark), using system.");
    }

    [Theory]
    [InlineData(true, "bottom-left")]
    [InlineData(false, "top-right")]
    [InlineData(true, "top-center")]
    public void Valid_legend_passes_unchanged(bool visible, string position)
    {
        LegendSettings legend = new(visible, position);

        var (normalized, problems) = AppearanceRules.Normalize(legend);

        normalized.Should().Be(legend);
        problems.Should().BeEmpty();
    }

    [Fact]
    public void Invalid_legend_position_takes_the_default_with_a_short_problem()
    {
        var (legend, problems) = AppearanceRules.Normalize(new LegendSettings(Visible: false, Position: "up"));

        legend.Should().Be(new LegendSettings(Visible: false, Position: "bottom-left"));

        // Short on purpose: the list of six names would be three amber lines in the default overlay (settings-format.md has it).
        problems.Should().ContainSingle().Which.Should()
            .Be("legend.position \"up\" is not valid (edge-alignment, e.g. bottom-right), using bottom-left.");
    }

    [Theory]
    [InlineData(0.2)]
    [InlineData(0.5)]
    [InlineData(0.8)]
    public void Legend_height_share_in_range_passes_unchanged(double share)
    {
        LegendSettings legend = new(MaxHeightShare: share);

        var (normalized, problems) = AppearanceRules.Normalize(legend);

        normalized.Should().Be(legend);
        problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0.1, 0.2, "legend.maxHeightShare 0.1 is outside 0.2–0.8, using 0.2.")]
    [InlineData(0.95, 0.8, "legend.maxHeightShare 0.95 is outside 0.2–0.8, using 0.8.")]
    public void Legend_height_share_outside_its_range_is_clamped_with_a_problem(double share, double expected, string problem)
    {
        // K7: clamped like holoOpacity, closer to what the player meant than the default; top level, no "appearance." in front.
        var (legend, problems) = AppearanceRules.Normalize(new LegendSettings(Visible: false, MaxHeightShare: share));

        legend.Should().Be(new LegendSettings(Visible: false, MaxHeightShare: expected));
        problems.Should().ContainSingle().Which.Should().Be(problem);
    }

    [Theory]
    [InlineData("#A0141414", 0xA0, 0x14, 0x14, 0x14)]
    [InlineData("#00ff7F", 0xFF, 0x00, 0xFF, 0x7F)]
    public void Argb_parses_both_forms(string text, byte a, byte r, byte g, byte b)
    {
        Argb.TryParse(text, out var color).Should().BeTrue();

        color.Should().Be(new Argb(a, r, g, b));
    }

    [Fact]
    public void Argb_writes_eight_upper_case_digits()
    {
        new Argb(0x01, 0xAB, 0x0C, 0xFF).ToString().Should().Be("#01AB0CFF");
    }
}
