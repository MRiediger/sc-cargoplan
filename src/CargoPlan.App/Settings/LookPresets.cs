using System.IO;

namespace CargoPlan.App.Settings;

/// <summary>A look that ships with the app (K4.3, D77).</summary>
/// <param name="Name">As the settings window lists it.</param>
/// <param name="FileName">The embedded look file, without <see cref="LookFile.Extension"/>.</param>
public sealed record LookPreset(string Name, string FileName, Look Look);

/// <summary>
/// The presets, embedded look files in the same form a player exports, so a preset is made by exporting it. Not next to
/// the exe like <c>ships/</c>: they are the app's, not the community's, and a player shares a look by Import instead.
/// </summary>
public static class LookPresets
{
    /// <summary>Default first: the look without settings, the way back to the start.</summary>
    public static IReadOnlyList<LookPreset> All { get; } =
    [
        Load("Default", "default"),
        Load("High contrast", "high-contrast"),
        Load("Minimal", "minimal"),
    ];

    /// <summary>The embedded file; the csproj names it <c>looks/&lt;name&gt;.cargoplan-look.json</c>.</summary>
    /// <exception cref="InvalidOperationException">No such preset in the assembly: a build error, not a player's.</exception>
    public static Stream Open(string fileName)
    {
        var name = $"looks/{fileName}{LookFile.Extension}";
        return typeof(LookPresets).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded look '{name}' not found.");
    }

    // Read onto the defaults, though every preset is complete (tested): no runtime check, the presets are no boundary (A6).
    private static LookPreset Load(string name, string fileName)
    {
        using var stream = Open(fileName);
        return new(name, fileName, LookFile.Read(stream, Look.Default).Look);
    }
}
