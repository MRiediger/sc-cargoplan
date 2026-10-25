using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

/// <summary>
/// The settings window's XAML read as XML, like <see cref="OverlayHeaderXamlTests"/>: the project has no window tests,
/// so what only fails when the window opens is checked in the markup.
/// </summary>
public partial class SettingsWindowXamlTests
{
    private const string ResourceName = "xaml/SettingsWindow.xaml";

    private static XElement Root()
    {
        using var stream = typeof(SettingsWindowXamlTests).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Test resource '{ResourceName}' not found.");
        return XDocument.Load(stream).Root!;
    }

    [Fact]
    public void Every_static_slider_bound_is_a_double()
    {
        // x:Static hands its value over as it is, WPF doesn't convert it: an int on Slider.Minimum (a double) threw when
        // the window opened and closed the app (T7.4c-2, "10" is no valid value for Minimum).
        var root = Root();
        var namespaces = root.Attributes().Where(a => a.IsNamespaceDeclaration && a.Value.StartsWith("clr-namespace:", StringComparison.Ordinal))
            .ToDictionary(a => a.Name.LocalName, a => a.Value["clr-namespace:".Length..].Split(';')[0]);
        var bounds = root.Descendants().Where(e => e.Name.LocalName == "Slider")
            .SelectMany(s => new[] { s.Attribute("Minimum"), s.Attribute("Maximum") })
            .Select(a => a is null ? null : StaticReference().Match(a.Value))
            .Where(m => m is { Success: true })
            .ToList();

        bounds.Should().NotBeEmpty();
        foreach (var match in bounds)
        {
            var type = typeof(SettingsViewModel).Assembly.GetType($"{namespaces[match!.Groups["prefix"].Value]}.{match.Groups["type"].Value}")!;
            var member = type.GetMember(match.Groups["member"].Value, BindingFlags.Public | BindingFlags.Static).Single();
            var memberType = member switch { FieldInfo f => f.FieldType, PropertyInfo p => p.PropertyType, _ => null };
            memberType.Should().Be(typeof(double), because: $"{match.Value} sets a slider bound");
        }
    }

    [Fact]
    public void Preset_export_and_import_stand_on_top_outside_the_scroll()
    {
        // K4.3: always in reach, like the version footer below; the sections scroll between them.
        var root = Root();
        var box = root.Descendants().Single(e => e.Name.LocalName == "ComboBox" && (string?)e.Attribute("ItemsSource") == "{Binding PresetChoices, Mode=OneTime}");
        var row = box.Ancestors().First(a => a.Attribute(XName.Get("DockPanel.Dock")) is not null);

        box.Attribute("SelectedItem")!.Value.Should().Be("{Binding SelectedPreset}");
        row.Attribute(XName.Get("DockPanel.Dock"))!.Value.Should().Be("Top");
        box.Ancestors().Should().NotContain(a => a.Name.LocalName == "ScrollViewer");
        // Both docked right, so the markup's order is the reverse of the screen's: only which buttons, not in what order.
        row.Descendants().Where(e => e.Name.LocalName == "Button").Select(b => ((string?)b.Attribute("Content"), (string?)b.Attribute("Click")))
            .Should().BeEquivalentTo([("Export…", "OnExportLook"), ("Import…", "OnImportLook")]);
    }

    [GeneratedRegex(@"\{x:Static (?<prefix>\w+):(?<type>\w+)\.(?<member>\w+)\}")]
    private static partial Regex StaticReference();
}
