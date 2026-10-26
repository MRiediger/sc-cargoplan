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

    [Fact]
    public void The_capacity_shows_the_scenes_text()
    {
        Capacity().Attribute("Text")!.Value.Should().Be("{Binding Scene.CapacityText, Mode=OneWay}");
        Capacity().Attribute("ToolTip")!.Value.Should().Be("{Binding Scene.CapacityToolTip, Mode=OneWay}");
    }

    [Fact]
    public void Nothing_hides_the_capacity_while_locked()
    {
        // Information, not a control (D67): no IsLocked trigger or binding on the text or on anything that holds it, unlike
        // the header buttons and the ship selection.
        var root = Root();
        var capacity = root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == "CapacityText");
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
    public void The_ship_name_gives_way_and_the_capacity_never_wraps()
    {
        // Docked right inside a left-aligned panel whose last child is the ship name: the name takes what is left and trims.
        var capacity = Capacity();

        capacity.Attribute("DockPanel.Dock")!.Value.Should().Be("Right");
        capacity.Attribute("TextWrapping")!.Value.Should().Be("NoWrap");
        capacity.Parent!.Name.LocalName.Should().Be("DockPanel");
        capacity.Parent.Attribute("HorizontalAlignment")!.Value.Should().Be("Left");
        capacity.Parent.Elements().Last().Descendants().Should().Contain(e => (string?)e.Attribute(X + "Name") == "ShipBox");
    }

    [Fact]
    public void Undo_and_redo_stand_left_of_the_reset_show_only_with_a_step_and_hide_while_locked()
    {
        // K2.4: in a DockPanel docked right, later in the markup is further left: gear, reset, redo, undo reads ↶ ↷ ⤢ ⚙.
        var buttons = Root().Descendants().Where(e => e.Name.LocalName == "Button").ToList();
        var gear = buttons.FindIndex(b => (string?)b.Attribute("ToolTip") == "Settings");
        var redo = buttons.FindIndex(b => (string?)b.Attribute("Command") == "{Binding RedoCommand, Mode=OneTime}");
        var undo = buttons.FindIndex(b => (string?)b.Attribute("Command") == "{Binding UndoCommand, Mode=OneTime}");

        (gear, redo, undo).Should().Be((gear, gear + 2, gear + 3));
        foreach (var (index, flag) in new[] { (redo, "CanRedo"), (undo, "CanUndo") })
        {
            var button = buttons[index];
            button.Attribute("Style")!.Value.Should().Be("{StaticResource HeaderButtonStyle}", "hidden while locked like every header button");
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

        reset.Should().Be(gear + 1);
        var button = buttons[reset];
        button.Attribute("ToolTip")!.Value.Should().Be("Reset view");
        button.Attribute("Style")!.Value.Should().Be("{StaticResource HeaderButtonStyle}", "hidden while locked like the gear");
        button.Attribute("Visibility").Should().BeNull();
        button.Parent!.Name.LocalName.Should().Be("DockPanel", "no border that collapses: it is always there");
        button.Descendants().Single(e => e.Name.LocalName == "TextBlock").Attribute("Text")!.Value.Should().Be("");
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
