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
    public void The_ship_view_takes_the_selection_edge_from_the_theme()
    {
        // K4.4: colour, width and contour are settings, so DynamicResources the settings window changes live.
        var view = Named(Root(), "ShipView");

        view.Attribute("SelectionPen")!.Value.Should().Be("{DynamicResource CratesSelectionPen}");
        view.Attribute("SelectionOutlinePen")!.Value.Should().Be("{DynamicResource CratesSelectionOutlinePen}");
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

    // --- K6.1: the line in segments that shorten by rank ---

    private const string ToggleLine = "{Binding DataContext.ToggleLineCommand, RelativeSource={RelativeSource AncestorType=Window}}";

    private const string ToggleCrates = "{Binding DataContext.ToggleCratesCommand, RelativeSource={RelativeSource AncestorType=Window}}";

    private static bool IsInside(XElement element, XElement ancestor) => element.Ancestors().Contains(ancestor);

    [Theory]
    [InlineData("LineText", "{Binding Segments, Mode=OneTime}")]
    [InlineData("HeaderText", "{Binding HeaderSegments, Mode=OneTime}")]
    public void Line_and_header_lie_in_the_row_that_shortens_by_rank(string name, string source)
    {
        var text = Named(Root(), name);

        text.Attribute("ItemsSource")!.Value.Should().Be(source);
        text.Descendants().Should().Contain(e => e.Name.LocalName == "LegendRowPanel");
        text.Attribute("ItemTemplate")!.Value.Should().Be("{StaticResource LegendSegmentTemplate}");
        text.Descendants().Single(e => e.Name.LocalName == "Setter").Attribute("Property")!.Value.Should().Be("sv:LegendRowPanel.Rank");
    }

    [Fact]
    public void A_segment_ends_in_an_ellipsis_on_one_line()
    {
        var template = Root().Descendants().Single(e => (string?)e.Attribute(X + "Key") == "LegendSegmentTemplate");
        var texts = template.Descendants().Where(e => e.Name.LocalName == "TextBlock").ToArray();

        texts.Should().HaveCount(2).And.OnlyContain(t => (string?)t.Attribute("TextTrimming") == "CharacterEllipsis" && (string?)t.Attribute("TextWrapping") == "NoWrap");
    }

    [Fact]
    public void Line_and_header_show_their_whole_text_as_a_tool_tip()
    {
        var root = Root();

        Named(root, "LineButton").Attribute("ToolTip")!.Value.Should().Be("{Binding Label, Mode=OneTime}");
        Named(root, "HeaderRow").Attribute("ToolTip")!.Value.Should().Be("{Binding HeaderLabel, Mode=OneTime}");
    }

    [Fact]
    public void Rows_pass_their_width_on_so_the_text_can_shorten()
    {
        // A horizontal StackPanel measures its children at infinite width: nothing would ever shorten.
        var root = Root();

        foreach (var name in new[] { "HeaderRow", "LineRow", "LineContent" })
        {
            Named(root, name).Name.LocalName.Should().Be("DockPanel", name);
        }
    }

    [Fact]
    public void The_B6_gestures_stay_on_their_elements()
    {
        // E2 (K6.1): a click on the line ticks, its box selects the line, the header's box the mission; boxes lie outside
        // the line's button, so a click on a box never ticks; right drags start at line and header.
        var root = Root();
        var button = Named(root, "LineButton");
        var lineCheck = Named(root, "LineCheck");
        var missionCheck = Named(root, "MissionCheck");

        (button.Attribute("Command")!.Value, button.Attribute("CommandParameter")!.Value).Should().Be((ToggleLine, "{Binding Mode=OneTime}"));
        button.Attribute("MouseRightButtonDown")!.Value.Should().Be("OnLegendRightButtonDown");
        (lineCheck.Attribute("Command")!.Value, lineCheck.Attribute("CommandParameter")!.Value).Should().Be((ToggleCrates, "{Binding Crates, Mode=OneTime}"));
        (missionCheck.Attribute("Command")!.Value, missionCheck.Attribute("CommandParameter")!.Value).Should().Be((ToggleCrates, "{Binding MissionCrates, Mode=OneTime}"));
        Named(root, "HeaderRow").Attribute("MouseRightButtonDown")!.Value.Should().Be("OnLegendHeaderRightButtonDown");
        IsInside(lineCheck, button).Should().BeFalse();
        IsInside(Named(root, "LineText"), button).Should().BeTrue();
        IsInside(Named(root, "HeaderText"), Named(root, "HeaderRow")).Should().BeTrue();
    }

    // --- K7: the stop lines scroll ---

    private static XElement WithSource(XElement scope, string source) =>
        scope.Descendants().Single(e => (string?)e.Attribute("ItemsSource") == source);

    [Fact]
    public void The_stop_lines_scroll_within_their_share_of_the_overlay()
    {
        var root = Root();
        var scroll = Named(root, "StopLinesScroll");
        var multi = scroll.Elements().Single(e => e.Name.LocalName == "ScrollViewer.MaxHeight").Elements().Single();

        scroll.Name.LocalName.Should().Be("ScrollViewer");
        IsInside(WithSource(root, "{Binding Scene.Legend, Mode=OneWay}"), scroll).Should().BeTrue();
        multi.Name.LocalName.Should().Be("MultiBinding");
        multi.Attribute("Converter")!.Value.Should().Be("{StaticResource LegendMaxHeightConverter}");
        Paths(multi).Should().Equal("ActualHeight", "LegendMaxHeightShare");
        multi.Elements().First().Attribute("ElementName")!.Value.Should().Be("OverlayContent");
        Named(root, "OverlayContent").Parent!.Name.LocalName.Should().Be("Border");
    }

    [Fact]
    public void The_scroll_area_passes_its_width_on_and_never_takes_the_focus()
    {
        // A horizontal scroll range would measure the lines at infinite width, so K6.1 could never shorten them; a focus
        // would activate the overlay over the game, and a focused ScrollViewer takes the left click DragMove needs.
        var scroll = Named(Root(), "StopLinesScroll");

        scroll.Attribute("HorizontalScrollBarVisibility")!.Value.Should().Be("Disabled");
        scroll.Attribute("VerticalScrollBarVisibility")!.Value.Should().Be("Auto");
        scroll.Attribute("Focusable")!.Value.Should().Be("False");
    }

    [Fact]
    public void Hiding_the_stop_lines_hides_their_scroll_area()
    {
        var scroll = Named(Root(), "StopLinesScroll");
        var style = scroll.Elements().Single(e => e.Name.LocalName == "ScrollViewer.Style");
        var trigger = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");

        trigger.Attribute("Binding")!.Value.Should().Be("{Binding IsLegendVisible}");
        trigger.Elements().Single().Attribute("Property")!.Value.Should().Be("Visibility");
    }

    [Fact]
    public void Missions_that_dont_fit_violations_and_the_palette_notice_stay_outside_the_scroll_area()
    {
        // They ask for something (D41, T5.4), so they never scroll out of sight.
        var root = Root();
        var scroll = Named(root, "StopLinesScroll");

        IsInside(WithSource(root, "{Binding Scene.Unplaced, Mode=OneWay}"), scroll).Should().BeFalse();
        IsInside(WithSource(root, "{Binding Scene.Violations, Mode=OneWay}"), scroll).Should().BeFalse();
        IsInside(Named(root, "PaletteNotice"), scroll).Should().BeFalse();
    }

    [Fact]
    public void Ship_list_and_stop_lines_share_the_slim_scroll_bar()
    {
        // One template, coloured through Foreground: the header's colour in the ship list, the legend's in the legend.
        var root = Root();
        var bars = root.Descendants()
            .Where(e => e.Name.LocalName == "Style" && (string?)e.Attribute("TargetType") == "ScrollBar" && e.Attribute(X + "Key") is null)
            .ToArray();
        var slim = root.Descendants().Single(e => (string?)e.Attribute(X + "Key") == "SlimScrollBarStyle");

        bars.Should().HaveCount(2).And.OnlyContain(b => (string?)b.Attribute("BasedOn") == "{StaticResource SlimScrollBarStyle}");
        IsInside(bars.Single(b => IsInside(b, Named(root, "StopLinesScroll"))), Named(root, "StopLinesScroll")).Should().BeTrue();
        bars.Select(b => b.Elements().Single(s => (string?)s.Attribute("Property") == "Foreground").Attribute("Value")!.Value)
            .Should().BeEquivalentTo("{DynamicResource HeaderForegroundBrush}", "{DynamicResource LegendForegroundBrush}");
        Named(slim, "Grip").Attribute("Background")!.Value.Should().Be("{TemplateBinding Foreground}");
    }

    [Fact]
    public void Tick_and_bar_take_the_check_mark_colour()
    {
        var style = CheckBoxStyle();

        Named(style, "Mark").Attribute("Stroke")!.Value.Should().Be("{DynamicResource LegendCheckMarkBrush}");
        Named(style, "Part").Attribute("Fill")!.Value.Should().Be("{DynamicResource LegendCheckMarkBrush}");
    }
}
