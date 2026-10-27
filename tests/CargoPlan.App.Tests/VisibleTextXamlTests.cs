using System.Xml.Linq;

namespace CargoPlan.App.Tests;

/// <summary>
/// What the windows show, read from their markup like <see cref="SettingsWindowXamlTests"/> (T7.5a2): American spelling,
/// as in the settings keys ("color"), and "Re-plan" with a hyphen, as on the buttons. Comments and bindings don't count.
/// </summary>
public class VisibleTextXamlTests
{
    private static readonly string[] TextAttributes = ["Text", "ToolTip", "Content", "Header", "Title"];

    private static string[] Texts(string window)
    {
        var name = $"xaml/{window}.xaml";
        using var stream = typeof(VisibleTextXamlTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Test resource '{name}' not found.");
        var root = XDocument.Load(stream).Root!;
        return
        [
            .. root.DescendantsAndSelf()
                .SelectMany(e => e.Attributes().Where(a => TextAttributes.Contains(a.Name.LocalName)).Select(a => a.Value)
                    .Concat(e.Nodes().OfType<XText>().Select(t => t.Value.Trim())))
                .Where(t => t.Length > 0 && !t.StartsWith('{')),
        ];
    }

    [Theory]
    [InlineData("OverlayWindow")]
    [InlineData("SettingsWindow")]
    [InlineData("MissionEditorWindow")]
    [InlineData("ThemeSampleWindow")]
    public void Visible_texts_say_color_and_re_plan(string window)
    {
        var texts = Texts(window);

        texts.Should().NotBeEmpty();
        texts.Should().NotContain(t => t.Contains("colour", StringComparison.OrdinalIgnoreCase));
        texts.Should().NotContain(t => t.Contains("replan", StringComparison.OrdinalIgnoreCase));
    }
}
