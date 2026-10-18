// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

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

    /// <summary>Replaces the file in one step (<see cref="AtomicFile"/>).</summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission for the folder or the file.</exception>
    public static void Save(string path, AppSettings settings) =>
        AtomicFile.Write(path, stream => JsonSerializer.Serialize(stream, settings, WriteTypeInfo));

    /// <summary>
    /// The default encoder writes '+' as + (HTML-sensitive), so a hand-written "Ctrl+Alt+L" would come back as
    /// "Ctrl+Alt+L" after the next save. The relaxed encoder leaves it alone; "unsafe" only matters for JSON
    /// embedded in HTML, and this file never is. The source-generation attribute has no Encoder, hence the copy.
    /// Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/character-encoding
    /// </summary>
    private static readonly JsonTypeInfo<AppSettings> WriteTypeInfo = (JsonTypeInfo<AppSettings>)
        new JsonSerializerOptions(SettingsJsonContext.Default.Options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }
            .GetTypeInfo(typeof(AppSettings));

    private static string Problem(string path, string reason) =>
        $"Settings not loaded, using defaults ({reason}). Moving the overlay overwrites {path}.";
}
