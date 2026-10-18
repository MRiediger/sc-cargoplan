// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Text.Json;

namespace CargoPlan.App.Settings;

/// <summary>Reads and writes settings.json. The file boundary: only here are file and JSON errors expected (D13).</summary>
public static class SettingsStore
{
    public const int SupportedSchemaVersion = 1;

    /// <summary>
    /// %LocalAppData%\SC-CargoPlan\settings.json. Not next to the EXE (Program Files needs admin rights),
    /// not in the roaming %AppData%: window bounds belong to this machine's monitor layout.
    /// </summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan", "settings.json");

    /// <summary>
    /// Missing file = defaults, no problem. Broken file or unknown schema version = defaults plus a problem
    /// text for the overlay; the file is left as it is, so a reader bug can't destroy it.
    /// </summary>
    public static (AppSettings Settings, string? Problem) Load(string path)
    {
        if (!File.Exists(path))
        {
            return (AppSettings.Default, null);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var settings = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.AppSettings)
                ?? throw new JsonException("The file contains null.");

            // After deserializing instead of Core's SchemaVersion.Check (internal there): one comparison,
            // and Core stays unchanged.
            return settings.SchemaVersion == SupportedSchemaVersion
                ? (settings, null)
                : (AppSettings.Default, Problem(path, $"schemaVersion {settings.SchemaVersion} is not supported."));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return (AppSettings.Default, Problem(path, ex.Message));
        }
    }

    /// <summary>
    /// Writes to a temp file first, then replaces the real one: a crash while writing leaves the old file,
    /// never half a file.
    /// Source: https://learn.microsoft.com/dotnet/api/system.io.file.move (overwrite overload, .NET Core 3.0+)
    /// </summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission for the folder or the file.</exception>
    public static void Save(string path, AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        var tempPath = path + ".tmp";
        using (var stream = File.Create(tempPath))
        {
            JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.AppSettings);
        }

        File.Move(tempPath, path, overwrite: true);
    }

    private static string Problem(string path, string reason) =>
        $"Settings not loaded, using defaults ({reason}). Moving the overlay overwrites {path}.";
}
