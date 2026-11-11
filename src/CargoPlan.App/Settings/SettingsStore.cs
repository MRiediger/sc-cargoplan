// WPF projects leave System.IO out of the implicit usings (Path clashes with System.Windows.Shapes.Path).
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace CargoPlan.App.Settings;

/// <summary>Reads and writes settings.json. The file boundary: only here are file and JSON errors expected (D13).</summary>
public static class SettingsStore
{
    public const int SupportedSchemaVersion = 3;

    /// <summary>
    /// Schema 1 (T3.4–T3.15) is schema 2 without <c>appearance</c>; schema 2 (to v0.3.x) is 3 with the ContractTracker keys
    /// and the framing from before the perspective view (T10.4a, D105). Both go through <see cref="Migrate"/> first, so the
    /// strict options see a schema 3 file. Not written back on loading: the file stays as the player left it until a change
    /// (D31), unless the migration took a key the player set (the session's <c>WriteBack</c>).
    /// </summary>
    private const int OldestReadableSchemaVersion = 1;

    // The orthographic view's framing per ship before D83: an entry with any of them meant nothing for the perspective
    // view, the code before the migration dropped it whole.
    private static readonly string[] OrthographicCameraKeys = ["pxPerCell", "panX", "panY"];

    /// <summary>
    /// %LocalAppData%\SC-CargoPlan\settings.json. Not next to the EXE (Program Files needs admin rights),
    /// not in the roaming %AppData%: window bounds belong to this machine's monitor layout.
    /// </summary>
    public static string DefaultPath => Path.Combine(AppFolders.LocalRoot, "settings.json");

    /// <summary>
    /// Missing file = defaults, no problem. Broken file or unknown schema version = defaults plus a problem
    /// text for the overlay; the file is left as it is, so a reader bug can't destroy it. Appearance values out of
    /// range or not a colour, an unknown legend position and an unknown tool window theme, are corrected one by one (<see cref="AppearanceRules"/>), the problem lists them, the rest
    /// applies. The result always has the current schema version; an older file is migrated first (<see cref="Migrate"/>).
    /// </summary>
    public static SettingsLoad Load(string path)
    {
        if (!File.Exists(path))
        {
            return new(AppSettings.Default, null);
        }

        try
        {
            // A tree first, so the migration can take keys out before the strict options see them (T10.4a, F1).
            // Source: https://learn.microsoft.com/dotnet/api/system.text.json.nodes.jsonnode.parse (Stream overload)
            // Source: https://learn.microsoft.com/dotnet/api/system.text.json.jsonserializer.deserialize (JsonNode, JsonTypeInfo<TValue>)
            JsonNode? root;
            using (var stream = File.OpenRead(path))
            {
                root = JsonNode.Parse(stream);
            }

            var (settings, removed) = MigratedSettings(root);

            // After deserializing instead of Core's SchemaVersion.Check (internal there): one comparison,
            // and Core stays unchanged.
            if (settings.SchemaVersion is < OldestReadableSchemaVersion or > SupportedSchemaVersion)
            {
                return new(AppSettings.Default, Problem(path, $"schemaVersion {settings.SchemaVersion} is not supported."));
            }

            var (appearance, appearanceProblems) = AppearanceRules.Normalize(settings.Appearance);
            var (legend, legendProblems) = AppearanceRules.Normalize(settings.Legend);
            var (theme, themeProblems) = AppearanceRules.NormalizeToolWindowTheme(settings.ToolWindowTheme);
            var (shipOutline, shipOutlineProblems) = AppearanceRules.NormalizeShipOutline(settings.ShipOutline);
            var (holoOpacity, holoOpacityProblems) = AppearanceRules.NormalizeHoloOpacity(settings.HoloOpacity);
            var (holoHull, holoHullProblems) = AppearanceRules.NormalizeHoloColor("holoHullColor", settings.HoloHullColor, AppearanceRules.DefaultHoloHullColor);
            var (holoEdge, holoEdgeProblems) = AppearanceRules.NormalizeHoloColor("holoEdgeColor", settings.HoloEdgeColor, AppearanceRules.DefaultHoloEdgeColor);
            var (shipView, shipViewProblems) = AppearanceRules.Normalize(settings.ShipView);
            return new(
                settings with
                {
                    SchemaVersion = SupportedSchemaVersion,
                    Appearance = appearance,
                    Legend = legend,
                    ToolWindowTheme = theme,
                    ShipOutline = shipOutline,
                    HoloOpacity = holoOpacity,
                    HoloHullColor = holoHull,
                    HoloEdgeColor = holoEdge,
                    ShipView = shipView,
                },
                Corrections([
                    .. legendProblems, .. appearanceProblems, .. themeProblems, .. shipOutlineProblems, .. holoOpacityProblems,
                    .. holoHullProblems, .. holoEdgeProblems, .. shipViewProblems]),
                removed);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            var problem = Problem(path, ex.Message);
            AppLog.Write("Settings", $"{problem} [{ex.GetType().Name}]");
            return new(AppSettings.Default, problem);
        }
    }

    /// <summary>
    /// The tree migrated and read with the strict options. A key twice in one object (edited by hand) makes the tree throw
    /// an ArgumentException when it builds that object; it costs the file like a typo (T10.4a, D105), never the start.
    /// Before the migration the serializer let the last one win.
    /// </summary>
    /// <exception cref="JsonException">The file isn't valid settings.</exception>
    private static (AppSettings Settings, string? Removed) MigratedSettings(JsonNode? root)
    {
        try
        {
            var removed = root is JsonObject file ? Migrate(file) : null;
            var settings = root.Deserialize(SettingsJsonContext.Default.AppSettings) ?? throw new JsonException("The file contains null.");
            return (settings, removed);
        }
        catch (ArgumentException ex)
        {
            throw new JsonException($"A key appears twice: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Schema 1 or 2 → 3 in place (T10.4a, D105): <c>contractTracker</c> and <c>hotkeys.syncContractTracker</c> go,
    /// a camera framed before the perspective view goes whole (any of <c>pxPerCell</c>, <c>panX</c>, <c>panY</c> set), the
    /// three keys go from every other camera, <c>schemaVersion</c> becomes 3. Nothing else changes: a typo still meets the
    /// strict options and costs the file, as before. Any other version is left as it is for the check after reading.
    /// </summary>
    /// <returns>The notice for a sync hotkey the player had set; null if there was none.</returns>
    internal static string? Migrate(JsonObject file)
    {
        if (file["schemaVersion"] is not JsonValue version || !version.TryGetValue(out int schema)
            || schema is < OldestReadableSchemaVersion or >= SupportedSchemaVersion)
        {
            return null;
        }

        string? removed = null;
        file.Remove("contractTracker");
        if (file["hotkeys"] is JsonObject hotkeys)
        {
            if (hotkeys["syncContractTracker"] is JsonValue sync && sync.TryGetValue(out string? key) && !string.IsNullOrWhiteSpace(key))
            {
                removed = SyncHotkeyRemovedText(key);
            }

            hotkeys.Remove("syncContractTracker");
        }

        if (file["shipView"] is JsonObject { } shipView && shipView["cameras"] is JsonObject cameras)
        {
            foreach (var (ship, camera) in cameras.ToList())
            {
                if (camera is not JsonObject entry)
                {
                    continue;
                }

                // A JSON null reads as a null node: only a set value counts.
                if (OrthographicCameraKeys.Any(k => entry[k] is not null))
                {
                    cameras.Remove(ship);
                }
                else
                {
                    Array.ForEach(OrthographicCameraKeys, k => entry.Remove(k));
                }
            }
        }

        file["schemaVersion"] = SupportedSchemaVersion;
        return removed;
    }

    /// <summary>The notice for a sync hotkey the migration took (T10.4a): which key is free again, and how contracts come in now.</summary>
    public static string SyncHotkeyRemovedText(string hotkey) =>
        $"settings.json: hotkey {hotkey} for the ContractTracker sync was removed; contracts come in with the scan hotkey.";

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

/// <summary>What <see cref="SettingsStore.Load"/> read.</summary>
/// <param name="Settings">Checked, with the current schema version; the defaults if the file couldn't be used.</param>
/// <param name="Problem">The file couldn't be used, or values were corrected (D34); null if neither.</param>
/// <param name="Removed">A key the player set that the migration took (T10.4a); null if none.</param>
public sealed record SettingsLoad(AppSettings Settings, string? Problem, string? Removed = null)
{
    /// <summary>Settings and problem alone, as every caller before T10.4a read them.</summary>
    public void Deconstruct(out AppSettings settings, out string? problem) => (settings, problem) = (Settings, Problem);
}
