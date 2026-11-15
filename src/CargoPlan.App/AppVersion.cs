using System.Reflection;

namespace CargoPlan.App;

/// <summary>
/// The app's version as the Git tag gave it (<c>publish.ps1 -Version</c>, <c>release.yml</c>), without the commit hash the
/// SDK appends after "+". The same number <c>vpk pack -v</c> writes into the package (T11.3), so the update check compares
/// like with like (D109).
/// </summary>
public static class AppVersion
{
    /// <summary>Directory.Build.props' version when no tag is passed: a developer's build, never offered an update (E6).</summary>
    public const string DevVersion = "0.0.0-dev";

    /// <summary>
    /// Read from this assembly, not the entry assembly: in a test run that is the test host.
    /// </summary>
    public static string Current { get; } =
        WithoutMetadata(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static bool IsDevBuild => Current == DevVersion;

    /// <summary>"0.5.0+140be1f" → "0.5.0"; no attribute → "0.0.0".</summary>
    internal static string WithoutMetadata(string? informationalVersion) => (informationalVersion ?? "0.0.0").Split('+')[0];
}
