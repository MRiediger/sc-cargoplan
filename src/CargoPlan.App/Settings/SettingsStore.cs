// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace CargoPlan.App.Settings;

/// <summary>Reads and writes settings.json. The file boundary: only here are file and JSON errors expected (D13).</summary>
public static class SettingsStore
{
    public const int SupportedSchemaVersion = 2;

    /// <summary>
    /// Schema 1 (T3.4–T3.15) is schema 2 without <c>appearance</c>: it reads into the same record, nothing to convert,
    /// and the next save writes 2. Not written back on loading: the file stays as the player left it until a change (D31).
    /// </summary>
    private const int OldestReadableSchemaVersion = 1;

    /// <summary>
    /// %LocalAppData%\SC-CargoPlan\settings.json. Not next to the EXE (Program Files needs admin rights),
    /// not in the roaming %AppData%: window bounds belong to this machine's monitor layout.
    /// </summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SC-CargoPlan", "settings.json");

    /// <summary>
    /// Missing file = defaults, no problem. Broken file or unknown schema version = defaults plus a problem
    /// text for the overlay; the file is left as it is, so a reader bug can't destroy it. Appearance values out of
    /// range or not a colour, an unknown legend position and an unknown tool window theme, are corrected one by one (<see cref="AppearanceRules"/>), the problem lists them, the rest
    /// applies. The result always has the current schema version.
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
            if (settings.SchemaVersion is < OldestReadableSchemaVersion or > SupportedSchemaVersion)
            {
                return (AppSettings.Default, Problem(path, $"schemaVersion {settings.SchemaVersion} is not supported."));
            }

            var (appearance, appearanceProblems) = AppearanceRules.Normalize(settings.Appearance);
            var (legend, legendProblems) = AppearanceRules.Normalize(settings.Legend);
            var (theme, themeProblems) = AppearanceRules.NormalizeToolWindowTheme(settings.ToolWindowTheme);
            var (contractTracker, contractTrackerProblems) = AppearanceRules.NormalizeContractTracker(settings.ContractTracker);
            var (shipOutline, shipOutlineProblems) = AppearanceRules.NormalizeShipOutline(settings.ShipOutline);
            return (
                settings with
                {
                    SchemaVersion = SupportedSchemaVersion,
                    Appearance = appearance,
                    Legend = legend,
                    ToolWindowTheme = theme,
                    ContractTracker = contractTracker,
                    ShipOutline = shipOutline,
                },
                Corrections([.. legendProblems, .. appearanceProblems, .. themeProblems, .. contractTrackerProblems, .. shipOutlineProblems]));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return (AppSettings.Default, Problem(path, ex.Message));
        }
    }

    /// <summary>
    /// The notice for values <see cref="AppearanceRules"/> corrected, one line each; null if none. Shared by loading and
    /// the settings window, so a correction reads the same wherever it came from (D34).
    /// </summary>
    public static string? Corrections(IReadOnlyList<string> problems) =>
        problems.Count == 0 ? null : string.Join(Environment.NewLine, problems.Select(p => $"settings.json: {p}"));

    /// <summary>Replaces the file in one step (<see cref="AtomicFile"/>).</summary>
    /// <exception cref="IOException">The file can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission for the folder or the file.</exception>
    public static void Save(string path, AppSettings settings) =>
        AtomicFile.Write(path, stream => JsonSerializer.Serialize(stream, settings, WriteTypeInfo));

    /// <summary>settings.json.bak, next to the file.</summary>
    public static string BackupPath(string path) => path + ".bak";

    /// <summary>
    /// Copies the file as it is, byte for byte, even if it isn't JSON: a hand edit the next save would overwrite stays
    /// recoverable (T6.1). No file = nothing to keep, and an older backup stays. Once per run is the caller's job (<see cref="SettingsSession"/>).
    /// Source: https://learn.microsoft.com/dotnet/api/system.io.file.copy (overwrite overload; keeps the file's attributes)
    /// </summary>
    /// <exception cref="IOException">The backup is locked or can't be written.</exception>
    /// <exception cref="UnauthorizedAccessException">No permission, or the backup is read-only or hidden.</exception>
    public static void BackUp(string path)
    {
        if (File.Exists(path))
        {
            File.Copy(path, BackupPath(path), overwrite: true);
        }
    }

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
        $"Settings not loaded, using defaults ({reason}). The next save overwrites {path}, keeping the old file as {Path.GetFileName(BackupPath(path))}.";
}
