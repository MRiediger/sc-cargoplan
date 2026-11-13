using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>
/// The notice lines and the update line in the overlay's XAML, read as XML like the B6 tests (<see cref="OverlayLegendXamlTests"/>):
/// the project has no window tests. T11.2 put the update line next to the notices without touching how they render.
/// </summary>
public class OverlayNoticeXamlTests
{
    private const string ResourceName = "xaml/OverlayWindow.xaml";

    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static XElement Root()
    {
        using var stream = typeof(OverlayNoticeXamlTests).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");
        return XDocument.Load(stream).Root!;
    }

    private static XElement Notices(XElement root) =>
        root.Descendants().Single(e => e.Name.LocalName == "ItemsControl" && (string?)e.Attribute("ItemsSource") == "{Binding Notices, Mode=OneWay}");

    private static XElement Named(XElement root, string name) =>
        root.Descendants().Single(e => (string?)e.Attribute(X + "Name") == name);

    [Fact]
    public void The_notice_lines_render_as_before()
    {
        // Regression (T11.2): one TextBlock per line, its text once, amber in the legend's size; docked at the bottom.
        var notices = Notices(Root());

        notices.Attribute("DockPanel.Dock")!.Value.Should().Be("Bottom");
        var template = notices.Descendants().Single(e => e.Name.LocalName == "DataTemplate");
        template.Elements().Should().ContainSingle();
        var line = template.Elements().Single();
        line.Name.LocalName.Should().Be("TextBlock");
        line.Attributes().Select(a => $"{a.Name.LocalName}={a.Value}").Should().BeEquivalentTo(
            "Text={Binding Mode=OneTime}", "Style={StaticResource NoticeTextStyle}");
    }

    [Fact]
    public void The_update_line_is_the_bottom_line()
    {
        // Docked at the bottom before the notices: in a DockPanel the earlier of two bottom children lies lower.
        var root = Root();
        var notices = Notices(root);
        var update = Named(root, "UpdateLine");

        update.Parent.Should().BeSameAs(notices.Parent);
        update.Attribute("DockPanel.Dock")!.Value.Should().Be("Bottom");
        update.ElementsAfterSelf().Should().Contain(notices);
    }

    [Fact]
    public void The_update_text_looks_like_a_notice()
    {
        var text = Named(Root(), "UpdateText");

        text.Attribute("Style")!.Value.Should().Be("{StaticResource NoticeTextStyle}");
        text.Attribute("Text")!.Value.Should().Be("{Binding Text, Mode=OneWay}");
    }

    [Fact]
    public void Its_controls_hide_while_locked_and_keep_their_space()
    {
        // U2: like the gear and exit (Hidden, not Collapsed).
        var root = Root();
        var controls = Named(root, "UpdateControls");

        var trigger = controls.Descendants().Single(e => e.Name.LocalName == "DataTrigger");
        trigger.Attribute("Binding")!.Value.Should().Be("{Binding AreControlsShown}");
        trigger.Attribute("Value")!.Value.Should().Be("False");
        trigger.Elements().Single().Attribute("Value")!.Value.Should().Be("Hidden");
    }

    [Theory]
    [InlineData("Button", "Update", "{Binding UpdateCommand}", "{Binding ShowsUpdate, Converter={StaticResource BooleanToVisibilityConverter}}")]
    [InlineData("Button", "Retry", "{Binding RetryCommand}", "{Binding ShowsRetry, Converter={StaticResource BooleanToVisibilityConverter}}")]
    [InlineData("CheckBox", "Skip this version", "{Binding SkipCommand}", "{Binding ShowsSkip, Converter={StaticResource BooleanToVisibilityConverter}}")]
    public void Each_control_has_its_command_and_its_state(string type, string content, string command, string visibility)
    {
        var control = Named(Root(), "UpdateControls").Elements().Single(e => e.Name.LocalName == type && (string?)e.Attribute("Content") == content);

        control.Attribute("Command")!.Value.Should().Be(command);
        control.Attribute("Visibility")!.Value.Should().Be(visibility);
    }
}
