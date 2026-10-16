using System.Reflection;

namespace CargoPlan.Core.Tests;

public class SmokeTests
{
    private static readonly string[] ForbiddenPrefixes =
        ["PresentationFramework", "PresentationCore", "WindowsBase", "System.Windows"];

    [Fact]
    public void Core_does_not_reference_wpf_or_windows_assemblies()
    {
        var core = Assembly.Load("CargoPlan.Core");

        var offending = core.GetReferencedAssemblies()
            .Select(a => a.Name ?? "")
            .Where(name => ForbiddenPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(offending);
    }
}
