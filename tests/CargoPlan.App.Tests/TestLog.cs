using System.IO;
using System.Runtime.CompilerServices;

namespace CargoPlan.App.Tests;

/// <summary>
/// Points <see cref="AppLog.Path"/> at a temp file before any test runs: every caught failure logs (K10, F9), and the
/// failure tests must not fill the player's cargoplan.log in %LocalAppData%, where the next bug report reads it.
/// </summary>
internal static class TestLog
{
    public static string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), $"cargoplan-tests-{Environment.ProcessId}", "cargoplan.log");

    // CA2255 warns libraries off module initializers; a test assembly is never shipped, which the rule allows.
    // Source: https://learn.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2255 (When to suppress)
#pragma warning disable CA2255
    [ModuleInitializer]
    internal static void Redirect() => AppLog.Path = Path;
#pragma warning restore CA2255

    /// <summary>The log's text so far; empty before the first entry.</summary>
    public static string Text() => File.Exists(Path) ? File.ReadAllText(Path) : "";
}
