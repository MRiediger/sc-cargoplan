using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>
/// The tab strip "Cargo | Plan" (T13.2, D120) in the overlay's XAML: a line of its own under the header, visible while
/// locked, and the Plan tab taking the room of legend and ship view. Read as XML, like the header (no window tests).
/// </summary>
public class OverlayTabStripXamlTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Root()
    {
        using var stream = typeof(OverlayTabStripXamlTests).Assembly.GetManifestResourceStream("xaml/OverlayWindow.xaml")
            ?? throw new InvalidOperationException("Test resource 'xaml/OverlayWindow.xaml' not found.");
        return XDocument.Load(stream).Root!;
    }

    private static XElement Named(XElement root, string name) =>
        root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    private static XElement Keyed(XElement root, string key) =>
        root.Descendants().Single(e => (string?)e.Attribute(X + "Key") == key);

    [Fact]
    public void The_strip_is_the_line_under_the_header_with_Cargo_then_Plan()
    {
        var root = Root();
        var strip = Named(root, "TabStrip");

        strip.ElementsBeforeSelf().Last().Name.LocalName.Should().Be("HeaderPanel", "a line of its own, not a part of the header (D110)");
        strip.Attribute("DockPanel.Dock")!.Value.Should().Be("Top");
        strip.Attribute("Orientation")!.Value.Should().Be("Horizontal");
        strip.Attribute("Background").Should().BeNull("free room moves the window");

        var tabs = strip.Elements().ToList();
        tabs.Select(t => (string?)t.Attribute(X + "Name")).Should().Equal("CargoTab", "PlanTab");
        tabs.Select(t => (string?)t.Attribute("Command")).Should().Equal("{Binding ShowCargoCommand, Mode=OneTime}", "{Binding ShowPlanCommand, Mode=OneTime}");
        tabs.Select(t => t.Descendants().First(e => e.Name.LocalName == "TextBlock").Attribute("Text")!.Value).Should().Equal("Cargo", "Plan");
        tabs.Should().OnlyContain(t => (string?)t.Attribute("Style") == "{StaticResource TabButtonStyle}");
    }

    [Fact]
    public void The_tabs_stay_while_locked_and_follow_the_appearance()
    {
        // E2: the selection shows where F3 goes, locked too; the buttons' look comes from settings.json like the header's.
        var root = Root();
        XElement[] markup = [Named(root, "TabStrip"), Keyed(root, "TabButtonStyle"), Keyed(root, "TabTextStyle"), Keyed(root, "TabMarkStyle")];

        markup.Should().NotContain(e => e.ToString().Contains("IsLocked"));
        Keyed(root, "TabButtonStyle").Attribute("BasedOn")!.Value.Should().Be("{StaticResource IconButtonStyle}");
        Keyed(root, "TabTextStyle").Attribute("BasedOn")!.Value.Should().Be("{StaticResource HeaderTextStyle}");
        Keyed(root, "TabMarkStyle").ToString().Should().Contain("HeaderForegroundBrush");
    }

    [Theory]
    [InlineData("LegendArea")]
    [InlineData("CargoView")]
    public void The_Plan_tab_collapses_legend_and_ship_view(string name)
    {
        var style = Named(Root(), name).Elements().Single(e => e.Name.LocalName.EndsWith(".Style"));
        var trigger = style.Descendants().Single(e => e.Name.LocalName == "DataTrigger" && (string?)e.Attribute("Binding") == "{Binding IsRoutesTabSelected}");

        trigger.Attribute("Value")!.Value.Should().Be("True");
        trigger.Elements().Single().Attribute("Property")!.Value.Should().Be("Visibility");
        trigger.Elements().Single().Attribute("Value")!.Value.Should().Be("Collapsed");
    }

    [Fact]
    public void The_plan_view_shows_only_with_the_Plan_tab_and_shares_the_ship_views_place()
    {
        var root = Root();
        var plan = Named(root, "PlanView");
        var cargo = Named(root, "CargoView");

        plan.Parent.Should().BeSameAs(cargo.Parent, "both fill the overlay's last child");
        plan.Parent!.ElementsAfterSelf().Should().BeEmpty("the last child of the overlay's dock panel fills the rest");
        var style = plan.Elements().Single(e => e.Name.LocalName.EndsWith(".Style")).ToString();
        style.Should().Contain("Value=\"Collapsed\"").And.Contain("{Binding IsRoutesTabSelected}");
        cargo.Descendants().Should().Contain(e => (string?)e.Attribute(X + "Name") == "ShipView");
    }
}
