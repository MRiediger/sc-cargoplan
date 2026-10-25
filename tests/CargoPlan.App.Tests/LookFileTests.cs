using System.Text;
using System.Text.Json;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Tests;

public class LookFileTests
{
    /// <summary>Every value of the look off its default, so a section lost on the way shows.</summary>
    internal static readonly Look Custom = new(
        new AppearanceSettings(
            new WindowAppearance("#E0300000", "#FF00FF00", 2),
            new HeaderAppearance("#FFFFFF00", new HaloSettings(true, "#FF000000", 4)),
            new LegendAppearance("#FF00FFFF", 16, new HaloSettings(true, "#80000000", 6), "#80FF0000", "#FF00FF00"),
            new CrateAppearance(120, 64, "#FFFF0000", 40, HaloSettings.Off, "tolMuted"),
            new ButtonAppearance("#40FFFFFF", "#FFFFFFFF", 1, 6, new HaloSettings(true, "#FF101010", 2))),
        new LegendSettings(false, "top-right"),
        ToolWindowTheme: "dark",
        ShipOutline: "holo-flat",
        HoloOpacity: 0.55,
        HoloHullColor: "#FF336699",
        HoloEdgeColor: "#FFFFCC00",
        MaxZoomPxPerCell: 120);

    private static (Look Look, IReadOnlyList<string> Problems) Read(string json, Look current) =>
        LookFile.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)), current);

    private static byte[] Written(Look look)
    {
        using MemoryStream stream = new();
        LookFile.Write(stream, look);
        return stream.ToArray();
    }

    [Fact]
    public void A_look_is_the_settings_that_make_the_overlay_look_so()
    {
        // K4.3, P9: no hotkeys, ContractTracker, bounds, cameras, ship or downloads; those belong to the machine or the run.
        var settings = AppSettings.Default with
        {
            Appearance = Custom.Appearance,
            Legend = Custom.Legend,
            ToolWindowTheme = "dark",
            ShipOutline = "holo-flat",
            HoloOpacity = 0.55,
            HoloHullColor = "#FF336699",
            HoloEdgeColor = "#FFFFCC00",
            ShipView = new ShipViewSettings(120, new Dictionary<string, ShipCamera> { ["hull_a"] = new(10, 20) }),
            Hotkeys = new HotkeySettings(ToggleLock: "Ctrl+Alt+L"),
            DownloadShipHolograms = false,
        };

        Look.Of(settings).Should().Be(Custom);
        Look.Default.Should().Be(Look.Of(AppSettings.Default));
    }

    [Fact]
    public void Written_and_read_back_gives_the_same_look_and_the_same_bytes()
    {
        var bytes = Written(Custom);

        var (look, problems) = LookFile.Read(new MemoryStream(bytes), Look.Default);

        look.Should().Be(Custom);
        problems.Should().BeEmpty();
        Written(look).Should().Equal(bytes);
    }

    [Fact]
    public void The_file_has_schema_1_and_the_settings_files_names()
    {
        var text = Encoding.UTF8.GetString(Written(Look.Default));

        text.Should().Contain("\"schemaVersion\": 1").And.Contain("\"appearance\"").And.Contain("\"stopPalette\": \"okabeIto\"")
            .And.Contain("\"shipView\": {").And.Contain("\"maxZoomPxPerCell\": 160");
        text.Should().NotContain("cameras").And.NotContain("hotkeys").And.NotContain("downloadShipHolograms");
    }

    [Fact]
    public void Missing_keys_and_sections_leave_the_current_look_and_a_present_section_takes_its_defaults()
    {
        // Missing = unchanged per look key and appearance section; inside a section as settings.json reads it (D31).
        var (look, problems) = Read("""{ "schemaVersion": 1, "appearance": { "crates": { "fillAlpha": 100 } }, "holoOpacity": 0.7 }""", Custom);

        look.Should().Be(Custom with
        {
            Appearance = Custom.Appearance with { Crates = CrateAppearance.Default with { FillAlpha = 100 } },
            HoloOpacity = 0.7,
        });
        problems.Should().BeEmpty();
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 1, "shipView": { "maxZoomPxPerCell": 100, "cameras": {} } }""")]   // state, not look
    [InlineData("""{ "schemaVersion": 1, "hotkeys": { "toggleLock": "Ctrl+Alt+L" } }""")]
    [InlineData("""{ "schemaVersion": 1, "contractTracker": { "autoSync": false } }""")]
    [InlineData("""{ "schemaVersion": 1, "downloadShipHolograms": false }""")]
    [InlineData("""{ "schemaVersion": 1, "appearance": { "legend": { "fontsize": 14 } } }""")]           // typo
    [InlineData("""{ "schemaVersion": 1, "legend": { "visible": "no" } }""")]                            // wrong type
    [InlineData("""not json""")]
    public void A_file_with_anything_else_is_refused(string json)
    {
        var read = () => Read(json, Custom);

        read.Should().Throw<JsonException>();
    }

    [Fact]
    public void Another_schema_is_refused_by_its_number_before_its_keys()
    {
        var read = () => Read("""{ "schemaVersion": 2, "overlay": { "left": 1, "top": 2, "width": 3, "height": 4 } }""", Custom);

        read.Should().Throw<JsonException>().WithMessage("schemaVersion 2 is not supported (expected 1).");
    }

    [Theory]
    [InlineData("""{ "appearance": {} }""")]
    [InlineData("""{ "schemaVersion": "1" }""")]
    [InlineData("""[1]""")]
    public void A_file_without_a_numeric_schema_is_refused(string json)
    {
        var read = () => Read(json, Custom);

        read.Should().Throw<JsonException>().WithMessage("Missing numeric \"schemaVersion\".");
    }

    [Fact]
    public void Values_are_checked_like_settings_json()
    {
        var (look, problems) = Read(
            """{ "schemaVersion": 1, "appearance": { "window": { "background": "red" } }, "shipView": { "maxZoomPxPerCell": 500 }, "toolWindowTheme": "blue" }""",
            Look.Default);

        look.Appearance.Window.Background.Should().Be(WindowAppearance.Default.Background);
        look.MaxZoomPxPerCell.Should().Be(AppearanceRules.MaxMaxZoom);
        look.ToolWindowTheme.Should().Be("system");
        problems.Should().HaveCount(3);
        problems.Should().Contain(p => p.Contains("appearance.window.background")).And.Contain(p => p.Contains("shipView.maxZoomPxPerCell"))
            .And.Contain(p => p.Contains("toolWindowTheme"));
    }
}
