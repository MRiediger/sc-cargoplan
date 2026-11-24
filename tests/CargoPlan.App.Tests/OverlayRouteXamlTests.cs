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
    public void The_block_Plan_is_the_first_thing_in_the_legends_scroll_so_it_scrolls_with_the_legend()
    {
        // U9.1: above the legend, in its look; "scrolls with the legend, not on its own" (startprompt 6a).
        var root = Root();
        var scroll = Named(root, "StopLinesScroll");
        var block = Named(root, "RouteBlock");

        var content = scroll.Elements().Single(e => !e.Name.LocalName.StartsWith("ScrollViewer.", StringComparison.Ordinal));
        content.Name.LocalName.Should().Be("StackPanel");
        content.Elements().First(e => !e.Name.LocalName.Contains('.')).Should().BeSameAs(block);
        content.Elements().Last().Attribute("ItemsSource")!.Value.Should().Contain("Scene.Legend");
        ((string?)block.Attribute("DataContext")).Should().Be("{Binding ActiveRoute}");
        block.ToString().Should().Contain("HasRoute");
    }

    [Fact]
    public void The_scroll_shows_with_a_route_also_while_the_legend_is_hidden_and_the_empty_state_comes_after_it()
    {
        var root = Root();
        var scroll = Named(root, "StopLinesScroll");

        scroll.ToString().Should().Contain("MultiDataTrigger").And.Contain("ActiveRoute.HasRoute").And.Contain("IsLegendVisible");
        scroll.ElementsAfterSelf().First().ToString().Should().Contain("NoPlanText", "the block stays above \"No cargo\"");
    }

    [Fact]
    public void End_plan_is_a_text_button_in_the_blocks_head_that_takes_no_focus()
    {
        var end = Named(Root(), "EndPlanButton");

        ((string?)end.Attribute("Command")).Should().Be("{Binding EndCommand}");
        ((string?)end.Attribute("Content")).Should().Be("End plan");
        ((string?)end.Attribute("Focusable")).Should().Be("False", "a focus would activate the overlay over the game (U7)");
        ((string?)end.Attribute("Style")).Should().Be("{StaticResource TextButtonStyle}");
    }

    [Fact]
    public void The_chosen_loop_row_has_a_bar_on_its_left()
    {
        var style = Keyed(Root(), "LoopRowStyle").ToString();

        style.Should().Contain("Binding=\"{Binding Chosen}\"").And.Contain("Value=\"2,0,0,0\"");
    }
}
