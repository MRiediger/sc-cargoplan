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

    /// <summary>
    /// The log's text so far; empty before the first entry. Read with FileShare.ReadWrite (B11): a parallel test may be
    /// appending under AppLog's lock, and File.ReadAllText (FileShare.Read) made either the read or that write fail.
    /// AppLog's move at MaxBytes would still fail during a read; a test run writes about 25 KB.
    /// Source: https://learn.microsoft.com/dotnet/api/system.io.fileshare
    /// </summary>
    public static string Text()
    {
        if (!File.Exists(Path))
        {
            return "";
        }

        using FileStream stream = new(Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
