using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>The legend's markup read as XML, like <see cref="OverlayHeaderXamlTests"/>: the theme keys it takes.</summary>
public class OverlayLegendXamlTests
{
    private const string ResourceName = "xaml/OverlayWindow.xaml";

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement CheckBoxStyle()
    {
        using var stream = typeof(OverlayLegendXamlTests).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");
        return XDocument.Load(stream).Root!.Descendants().Single(e => (string?)e.Attribute(X + "Key") == "LegendCheckBoxStyle");
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
