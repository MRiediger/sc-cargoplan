using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>The legend's markup read as XML, like <see cref="OverlayHeaderXamlTests"/>: the theme keys it takes.</summary>
public class OverlayLegendXamlTests
{
    private const string ResourceName = "xaml/OverlayWindow.xaml";

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Root()
    {
        using var stream = typeof(OverlayLegendXamlTests).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");
        return XDocument.Load(stream).Root!;
    }

    private static XElement CheckBoxStyle() => Root().Descendants().Single(e => (string?)e.Attribute(X + "Key") == "LegendCheckBoxStyle");

    private static string[] Paths(XElement multiBinding) =>
        [.. multiBinding.Elements().Where(e => e.Name.LocalName == "Binding").Select(b => (string?)b.Attribute("Path") ?? "")];

    [Fact]
    public void The_swatch_takes_the_ordinal_and_the_palette_the_ship_view_takes()
    {
        // K4.2, P8: one function makes the brush for view and legend, so the hatch means the same crate in both.
        var root = Root();
        var swatch = Named(root, "Swatch");
        var multi = swatch.Descendants().Single(e => e.Name.LocalName == "MultiBinding");

        multi.Attribute("Converter")!.Value.Should().Be("{StaticResource PaletteBrushConverter}");
        Paths(multi).Should().Equal("PaletteIndex", "DataContext.StopPalette");
        Named(root, "ShipView").Attribute("StopPalette")!.Value.Should().Be("{Binding StopPalette, Mode=OneWay}");
    }

    [Fact]
    public void The_repeat_notice_comes_from_the_stop_count_and_the_palette()
    {
        var root = Root();
        var notice = Named(root, "PaletteNotice");
        var multi = notice.Descendants().Single(e => e.Name.LocalName == "MultiBinding");

        multi.Attribute("Converter")!.Value.Should().Be("{StaticResource PaletteNoticeConverter}");
        Paths(multi).Should().Equal("Scene.StopCount", "StopPalette");
        root.ToString().Should().NotContain("MissionPalette.RepeatNotice");
    }

    private static XElement Named(XElement scope, string name) =>
        scope.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Fact]
    public void The_check_box_frame_takes_the_check_box_colour()
    {
        // K4.1, D69: a setting now, so a DynamicResource the settings window can change while the overlay is open.
        var border = CheckBoxStyle().Descendants().Single(e => e.Name.LocalName == "Border");

        border.Attribute("BorderBrush")!.Value.Should().Be("{DynamicResource LegendCheckBoxBrush}");
    }

    [Fact]
    public void Tick_and_bar_take_the_check_mark_colour()
    {
        var style = CheckBoxStyle();

        Named(style, "Mark").Attribute("Stroke")!.Value.Should().Be("{DynamicResource LegendCheckMarkBrush}");
        Named(style, "Part").Attribute("Fill")!.Value.Should().Be("{DynamicResource LegendCheckMarkBrush}");
    }
}
