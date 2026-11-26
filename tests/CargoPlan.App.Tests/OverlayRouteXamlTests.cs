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
    public void Start_plan_is_a_filter_button_right_of_the_system_with_the_recipe_and_the_loops_and_says_why_while_off()
    {
        var button = Named(Root(), "StartPlanButton");

        button.Parent!.Name.LocalName.Should().Be("WrapPanel", "the filter row");
        button.ElementsBeforeSelf().Last().Attribute("CommandParameter")!.Value.Should().Be("system", "right of the system button (Martin, gate 6a)");
        ((string?)button.Attribute("Command")).Should().Be("{Binding StartPlanCommand}");
        ((string?)button.Attribute("Style")).Should().Be("{StaticResource FilterButtonStyle}");
        ((string?)button.Attribute("Visibility")).Should().Contain("OffersStart");
        ((string?)button.Attribute("ToolTipService.ShowOnDisabled")).Should().Be("True", "the tool tip says why it can't start");
    }

    [Fact]
    public void The_map_sits_beside_the_matrix_with_scene_focus_and_the_stop_palette_and_the_matrix_filters_show_with_it()
    {
        // T13.7: a view of its own in the Plan tab's grid, not fitted into another (A9).
        var root = Root();
        var map = Named(root, "RouteMap");

        map.Name.LocalName.Should().Be("RouteMapView");
        map.ElementsAfterSelf().Select(e => (string?)e.Attribute(X + "Name")).Should().Contain("PortMatrix", "beside the matrix in one grid");
        ((string?)map.Attribute("Scene")).Should().Be("{Binding Map}");
        ((string?)map.Attribute("FocusCommand")).Should().Be("{Binding FocusCommand}");
        ((string?)map.Attribute("Palette")).Should().Contain("StopPalette");
        ((string?)map.Attribute("Visibility")).Should().Contain("ShowsMap");

        var row = Named(root, "StartPlanButton").Parent!;
        row.Elements().Where(e => ((string?)e.Attribute("CommandParameter")) is "area" or "direct" or "contractor" or "faction")
            .Concat(row.Elements().Where(e => e.Name.LocalName == "CheckBox"))
            .Should().HaveCount(9).And.OnlyContain(e => ((string)e.Attribute("Visibility")!).Contains("ShowsMatrixFilters"));
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
    public void Placeholder_lines_wrap_and_accept_at_takes_the_notice_colour()
    {
        // Gate 6a: K1 the places and "collect at" read whole (wrap, no ellipsis), K3 "accept at" in the status line's
        // orange, "collect at" in the legend's colour.
        var block = Named(Root(), "RouteBlock");
        var lines = block.Descendants().Where(e => e.Name.LocalName == "DataTemplate").Select(t => t.Descendants().First(e => e.Name.LocalName == "TextBlock")).ToList();
        var runs = lines.SelectMany(l => l.Elements().Where(e => e.Name.LocalName == "Run")).ToList();

        lines.Should().HaveCount(3).And.OnlyContain(l => (string?)l.Attribute("TextWrapping") != "NoWrap" && l.Attribute("TextTrimming") == null);
        runs.Where(r => r.Attribute("Text")!.Value.Contains("Accept")).Should().HaveCount(2)
            .And.OnlyContain(r => (string?)r.Attribute("Foreground") == "{StaticResource NoticeForegroundBrush}");
        runs.Where(r => r.Attribute("Text")!.Value.Contains("Collect")).Should().HaveCount(2).And.OnlyContain(r => r.Attribute("Foreground") == null);
        runs.Should().OnlyContain(r => r.Attribute("Text")!.Value.Contains("Mode=OneWay"), "Run.Text binds two way by default");
    }

    [Fact]
    public void A_line_parts_the_block_from_the_stop_lines()
    {
        // E1 of gate 6a.
        var root = Root();
        var block = Named(root, "RouteBlock");
        var line = Named(root, "RouteSeparator");

        line.Parent.Should().BeSameAs(block);
        line.ElementsAfterSelf().Should().BeEmpty("the last thing of the block, right above the stop lines");
    }

    [Fact]
    public void The_station_bar_stands_right_above_the_legends_scroll_so_it_never_scrolls_away()
    {
        // U9.3: one line above the block "Plan", the Cargo view below unchanged; 6b, D126.
        var root = Root();
        var bar = Named(root, "StationBar");
        var scroll = Named(root, "StopLinesScroll");

        bar.ElementsAfterSelf().First().Should().BeSameAs(scroll);
        ((string?)bar.Attribute("DataContext")).Should().Be("{Binding ActiveRoute}");
        bar.ToString().Should().Contain("HasRoute").And.Contain("StationText").And.Contain("StationRows");
    }

    [Theory]
    [InlineData("PreviousStationButton", "{Binding PreviousStationCommand}", "‹")]
    [InlineData("NextStationButton", "{Binding NextStationCommand}", "›")]
    [InlineData("FinishPlanButton", "{Binding FinishPlanCommand}", "Finish plan")]
    public void The_bars_buttons_take_no_focus(string name, string command, string content)
    {
        var button = Named(Root(), name);

        (((string?)button.Attribute("Command")), ((string?)button.Attribute("Content"))).Should().Be((command, content));
        ((string?)button.Attribute("Focusable")).Should().Be("False", "a focus would activate the overlay over the game (U7)");
        ((string?)button.Attribute("Style")).Should().Be("{StaticResource TextButtonStyle}");
    }

    [Fact]
    public void The_blocks_head_is_a_line_button_that_folds_its_list_and_End_plan_stays()
    {
        // Gate 6b: no new element - the head in the legend line's button, the lines in a panel that folds.
        var root = Root();
        var block = Named(root, "RouteBlock");
        var head = Named(root, "RouteBlockHead");
        var list = Named(root, "RouteBlockList");

        ((string?)head.Attribute("Command")).Should().Be("{Binding ToggleBlockCommand}");
        ((string?)head.Attribute("Style")).Should().Be("{StaticResource LegendLineButtonStyle}");
        ((string?)head.Attribute("ToolTip")).Should().Be("{Binding HeaderToolTip}");
        ((string?)list.Attribute("Visibility")).Should().Contain("IsBlockOpen");
        list.Parent.Should().BeSameAs(block);
        list.Descendants().Select(e => (string?)e.Attribute("ItemsSource")).Should().Contain(["{Binding Rows}", "{Binding BackRows}", "{Binding ExtraRows}"]);
        list.ElementsAfterSelf().Single().Attribute(X + "Name")!.Value.Should().Be("RouteSeparator", "the line parts the block from the stop lines also while folded");
        Named(root, "EndPlanButton").Ancestors().Should().NotContain(list, "End plan stays with the head");
    }

    [Fact]
    public void The_chosen_loop_row_has_a_bar_on_its_left()
    {
        var style = Keyed(Root(), "LoopRowStyle").ToString();

        style.Should().Contain("Binding=\"{Binding Chosen}\"").And.Contain("Value=\"2,0,0,0\"");
    }
}
