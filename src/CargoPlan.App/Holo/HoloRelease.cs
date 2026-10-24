// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Reflection;

namespace CargoPlan.App.Holo;

/// <summary>
/// Where the ship holograms come from and where they are kept (D63, docs/holo-format.md). The overlay's only network
/// access: plain GETs of release assets, no API, no token.
/// </summary>
public static class HoloRelease
{
    /// <summary>The data release this app reads; a new format or a new pipeline run gets a new tag and a new app.</summary>
    public const string Tag = "holo-v1";

    public const string ManifestFile = "manifest.json";

    /// <summary>Release assets, not api.github.com: no rate limit, no token. GitHub redirects to its asset host.</summary>
    public static Uri BaseUri { get; } = new($"https://github.com/MRiediger/sc-cargoplan/releases/download/{Tag}/");

    /// <summary>Per request; a hanging connection must not keep the outline from the player for long.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>A cached manifest younger than this is used without asking (Martin, T7.4c-2): at most one request a day.</summary>
    public static readonly TimeSpan ManifestMaxAge = TimeSpan.FromHours(24);

    /// <summary>%LocalAppData%\SC-CargoPlan\holo\holo-v1, next to settings.json; one folder per tag.</summary>
    public static string DefaultCacheFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan", "holo", Tag);

    /// <summary>
    /// The version for the User-Agent, without the commit hash the SDK appends after "+": the request names the app and
    /// its version, nothing else.
    /// </summary>
    public static string UserAgentVersion { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0").Split('+')[0];
}
