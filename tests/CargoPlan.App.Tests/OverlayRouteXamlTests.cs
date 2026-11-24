using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>
/// The route in the overlay's XAML (T13.6): "Start plan" in the Plan tab's filter row, the chosen loop row's bar. Read as
/// XML like the tab strip (no window tests); the look is the picture gate's.
/// </summary>
public class OverlayRouteXamlTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Root()
    {
        using var stream = typeof(OverlayRouteXamlTests).Assembly.GetManifestResourceStream("xaml/OverlayWindow.xaml")
            ?? throw new InvalidOperationException("Test resource 'xaml/OverlayWindow.xaml' not found.");
        return XDocument.Load(stream).Root!;
    }

    private static XElement Named(XElement root, string name) =>
        root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement Keyed(XElement root, string key) =>
        root.Descendants().Single(e => (string?)e.Attribute(X + "Key") == key);

    [Fact]
    public void Start_plan_is_a_filter_button_after_the_sort_with_the_recipe_and_the_loops_and_says_why_while_off()
    {
        var button = Named(Root(), "StartPlanButton");

        button.Parent!.Name.LocalName.Should().Be("WrapPanel", "the filter row");
        button.ElementsBeforeSelf().Last().Attribute("CommandParameter")!.Value.Should().Be("sort");
        ((string?)button.Attribute("Command")).Should().Be("{Binding StartPlanCommand}");
        ((string?)button.Attribute("Style")).Should().Be("{StaticResource FilterButtonStyle}");
        ((string?)button.Attribute("Visibility")).Should().Contain("OffersStart");
        ((string?)button.Attribute("ToolTipService.ShowOnDisabled")).Should().Be("True", "the tool tip says why it can't start");
    }

    [Fact]
    public void The_chosen_loop_row_has_a_bar_on_its_left()
    {
        var style = Keyed(Root(), "LoopRowStyle").ToString();

        style.Should().Contain("Binding=\"{Binding Chosen}\"").And.Contain("Value=\"2,0,0,0\"");
    }
}
