using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>
/// The overlay's XAML read as XML: what a view model test can't see. The project has no window tests (a window needs the
/// app's resources and a settings session), so the rule that something stays visible while locked is checked in the markup.
/// </summary>
public class OverlayHeaderXamlTests
{
    private const string ResourceName = "xaml/OverlayWindow.xaml";

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Root()
    {
        using var stream = typeof(OverlayHeaderXamlTests).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");
        return XDocument.Load(stream).Root!;
    }

    private static XElement Capacity() =>
        Root().Descendants().Single(e => (string?)e.Attribute(X + "Name") == "CapacityText");

    private static XElement Named(XElement root, string name) =>
        root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    /// <summary>The header part an element plays in the <c>HeaderPanel</c> (B15), null if none.</summary>
    private static string? PartOf(XElement element) =>
        (string?)element.Attributes().SingleOrDefault(a => a.Name.LocalName == "HeaderPanel.Part");

    [Fact]
    public void The_capacity_shows_the_scenes_text()
    {
        Capacity().Attribute("Text")!.Value.Should().Be("{Binding Scene.CapacityText, Mode=OneWay}");
        Capacity().Attribute("ToolTip")!.Value.Should().Be("{Binding Scene.CapacityToolTip, Mode=OneWay}");
    }

    [Fact]
    public void The_compact_capacity_shows_the_scenes_compact_text_with_the_same_tool_tip()
    {
        // B15: the tool tip spells out what the compact form leaves out, "All stops delivered." included (D110).
        var compact = Named(Root(), "CompactCapacityText");

        compact.Attribute("Text")!.Value.Should().Be("{Binding Scene.CompactCapacityText, Mode=OneWay}");
        compact.Attribute("ToolTip")!.Value.Should().Be("{Binding Scene.CapacityToolTip, Mode=OneWay}");
        compact.Attribute("TextWrapping")!.Value.Should().Be("NoWrap");
    }

    [Theory]
    [InlineData("CapacityText")]
    [InlineData("CompactCapacityText")]
    public void Nothing_hides_the_capacity_while_locked(string name)
    {
        // Information, not a control (D67): no IsLocked trigger or binding on the text or on anything that holds it, unlike
        // the header buttons and the ship selection.
        var root = Root();
        var capacity = Named(root, name);
        XElement[] chain = [capacity, .. capacity.Ancestors()];

        chain.Should().NotContain(e => e.Attributes().Any(a => a.Value.Contains("IsLocked"))
            || e.Elements().Any(child => child.Name.LocalName.EndsWith(".Style") && child.ToString().Contains("IsLocked"))
            || StyleOf(root, e).Contains("IsLocked"));
    }

    /// <summary>The markup of a style the element takes by <c>{StaticResource Key}</c> from the window, else empty.</summary>
    private static string StyleOf(XElement root, XElement element)
    {
        if ((string?)element.Attribute("Style") is not { } style || !style.StartsWith("{StaticResource "))
        {
            return "";
        }

        var key = style["{StaticResource ".Length..^1];
        return root.Descendants().FirstOrDefault(e => (string?)e.Attribute(X + "Key") == key)?.ToString() ?? "";
    }

    [Fact]
    public void The_header_is_one_panel_with_name_capacities_status_tools_and_chrome()
    {
        // B15 (D110, revises D67): the panel decides what gives way: status, long capacity, then the tools to a second
        // line while gear and exit stay; the name last. It is the header itself, docked on top with the lock window's margin.
        var root = Root();
        var capacity = Capacity();
        var panel = capacity.Parent!;

        panel.Name.LocalName.Should().Be("HeaderPanel");
        panel.Attribute("DockPanel.Dock")!.Value.Should().Be("Top");
        panel.Attribute("Margin")!.Value.Should().Be("0,0,30,0");
        panel.Attribute("LineHeight")!.Value.Should().Be("30", "the first line as high as the header was (D16)");
        panel.Attribute("Height").Should().BeNull("a second line makes it taller");
        panel.Attribute("HorizontalAlignment").Should().BeNull("it stretches");
        panel.Elements().Select(PartOf).Should().Equal("Name", "Capacity", "CompactCapacity", "Status", "Tools", "Chrome");
        panel.Elements().First().Descendants().Should().Contain(e => (string?)e.Attribute(X + "Name") == "ShipBox");
        capacity.Attribute("TextWrapping")!.Value.Should().Be("NoWrap");
        capacity.Attribute("DockPanel.Dock").Should().BeNull();

        string ButtonName(XElement b) => (string?)b.Attribute("ToolTip") ?? (string)b.Attribute("Command")!;
        XElement[] Buttons(string part)
        {
            var block = panel.Elements().Single(e => PartOf(e) == part);
            block.Name.LocalName.Should().Be("StackPanel");
            block.Attribute("Orientation")!.Value.Should().Be("Horizontal");
            XElement[] buttons = [.. block.Descendants().Where(e => e.Name.LocalName == "Button")];
            buttons.Should().OnlyContain(b => (string?)b.Attribute("Style") == "{StaticResource HeaderButtonStyle}" && b.Attribute("DockPanel.Dock") == null);
            return buttons;
        }

        Buttons("Tools").Select(ButtonName).Should().Equal(
            "{Binding SplitToolTip}", "{Binding MergeToolTip}", "{Binding ReplanToolTip}", "Enter missions", "Undo", "Redo", "Reset view");
        Buttons("Chrome").Select(ButtonName).Should().Equal("Settings", "Exit");

        var status = panel.Elements().Single(e => PartOf(e) == "Status");
        status.Attribute("Text")!.Value.Should().Be("{x:Static sv:ShipScene.AllDeliveredText}");
        status.Attribute("TextWrapping")!.Value.Should().Be("NoWrap");
        status.Attribute("Visibility")!.Value.Should().Be("{Binding Scene.AllDelivered, Converter={StaticResource BooleanToVisibilityConverter}}");
        root.Descendants().Should().NotContain(e => (string?)e.Attribute("Text") == "{x:Static vm:OverlayViewModel.AllDeliveredText}");
    }

    [Fact]
    public void Locked_the_header_buttons_give_their_room_to_the_ship_name()
    {
        // B15 (D110): Collapsed, not Hidden; hidden buttons kept 210 px (270 with undo and redo) in the game.
        var style = Root().Descendants().Single(e => (string?)e.Attribute(X + "Key") == "HeaderButtonStyle");
        var trigger = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger");

        trigger.Attribute("Binding")!.Value.Should().Be("{Binding IsLocked}");
        trigger.Elements().Single().Attribute("Value")!.Value.Should().Be("Collapsed");
    }

    [Fact]
    public void Undo_and_redo_stand_left_of_the_reset_show_only_with_a_step_and_hide_while_locked()
    {
        // K2.4: the button block reads left to right (B15): undo, redo, reset, gear reads ↶ ↷ ⤢ ⚙.
        var buttons = Root().Descendants().Where(e => e.Name.LocalName == "Button").ToList();
        var gear = buttons.FindIndex(b => (string?)b.Attribute("ToolTip") == "Settings");
        var redo = buttons.FindIndex(b => (string?)b.Attribute("Command") == "{Binding RedoCommand, Mode=OneTime}");
        var undo = buttons.FindIndex(b => (string?)b.Attribute("Command") == "{Binding UndoCommand, Mode=OneTime}");

        (gear, redo, undo).Should().Be((gear, gear - 2, gear - 3));
        foreach (var (index, flag) in new[] { (redo, "CanRedo"), (undo, "CanUndo") })
        {
            var button = buttons[index];
            button.Attribute("Style")!.Value.Should().Be("{StaticResource HeaderButtonStyle}", "gone while locked like every header button");
            button.Parent!.Attribute("Visibility")!.Value.Should().Be($"{{Binding {flag}, Converter={{StaticResource BooleanToVisibilityConverter}}}}");
        }
    }

    [Fact]
    public void Reset_view_stands_right_next_to_the_gear_always_there_and_hides_while_locked()
    {
        // K1.3: a view control, so right of the plan's undo and redo (D34: window right, plan left); undo and redo come
        // and go further left, so the reset never jumps.
        var buttons = Root().Descendants().Where(e => e.Name.LocalName == "Button").ToList();
        var gear = buttons.FindIndex(b => (string?)b.Attribute("ToolTip") == "Settings");
        var reset = buttons.FindIndex(b => (string?)b.Attribute("Command") == "{Binding ResetViewCommand, Mode=OneTime}");

        reset.Should().Be(gear - 1, "the block reads left to right (B15)");
        var button = buttons[reset];
        button.Attribute("ToolTip")!.Value.Should().Be("Reset view");
        button.Attribute("Style")!.Value.Should().Be("{StaticResource HeaderButtonStyle}", "gone while locked like the gear");
        button.Attribute("Visibility").Should().BeNull();
        button.Parent!.Name.LocalName.Should().Be("StackPanel", "no border that collapses: it is always there");
        button.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text")!.Value.Should().Be("");
    }

    [Fact]
    public void The_ship_list_scrolls_by_pixels_not_by_groups()
    {
        // F2: with content scrolling, the list's StackPanel counts a group as one item: a wheel notch skipped three groups and
        // the end of a group taller than the list was out of reach. The theme's own template turns it off when grouping.
        var style = Root().Descendants().Single(e => (string?)e.Attribute(X + "Key") == "ShipBoxStyle");
        var scroller = style.Descendants().Single(e => e.Name.LocalName == "ScrollViewer");

        ((string?)scroller.Attribute("CanContentScroll")).Should().Be("False");
    }

    [Fact]
    public void In_the_ship_list_the_name_gives_way_and_the_capacity_never_wraps()
    {
        // F1, the rule of the header (D67): docked right, and the name as the last child takes what is left and trims.
        var style = Root().Descendants().Single(e => (string?)e.Attribute(X + "Key") == "ShipBoxStyle");
        var template = style.Elements().Single(e => (string?)e.Attribute("Property") == "ItemTemplate");
        var row = template.Descendants().Single(e => e.Name.LocalName == "DockPanel");
        XElement[] texts = [.. row.Elements()];

        row.Attribute("MaxWidth")!.Value.Should().Be("420");
        row.Attribute("HorizontalAlignment").Should().BeNull("the row stretches, so the capacities stand in a right-aligned column");
        texts.Should().HaveCount(2);
        texts[0].Attribute("Text")!.Value.Should().Be("{Binding CapacityText, Mode=OneTime}");
        texts[0].Attribute("DockPanel.Dock")!.Value.Should().Be("Right");
        texts[0].Attribute("TextWrapping")!.Value.Should().Be("NoWrap");
        texts[1].Attribute("Text")!.Value.Should().Be("{Binding Text, Mode=OneTime}");
        texts[1].Attribute("TextTrimming")!.Value.Should().Be("CharacterEllipsis");
        texts[1].Attribute("TextWrapping")!.Value.Should().Be("NoWrap");
    }

    [Fact]
    public void The_ship_name_trims_on_one_line_instead_of_wrapping()
    {
        // The header's text style wraps and the 30 px header holds two lines: without NoWrap the name broke in two
        // instead of trimming once the capacity took its room (gate B5, "MISC" over "Hull A").
        XElement[] names = [.. Root().Descendants().Where(e => (string?)e.Attribute("Text") == "{Binding Ship.Name, Mode=OneWay}")];

        names.Should().HaveCount(2).And.OnlyContain(e =>
            (string?)e.Attribute("TextTrimming") == "CharacterEllipsis" && (string?)e.Attribute("TextWrapping") == "NoWrap");
    }
}
