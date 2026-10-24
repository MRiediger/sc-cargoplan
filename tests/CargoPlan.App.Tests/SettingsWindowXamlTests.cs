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

    [GeneratedRegex(@"\{x:Static (?<prefix>\w+):(?<type>\w+)\.(?<member>\w+)\}")]
    private static partial Regex StaticReference();
}
