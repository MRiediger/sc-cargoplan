namespace CargoPlan.App.Settings;

/// <summary>
/// Content of settings.json (schema version 1). UI state only, so it lives in the app, not in Core.
/// New settings get a default value, so files written by an older version stay readable.
/// </summary>
/// <param name="Overlay">null = never saved: the window keeps its XAML size and Windows picks the position.</param>
public sealed record AppSettings(int SchemaVersion, WindowBounds? Overlay = null)
{
    public static AppSettings Default { get; } = new(SettingsStore.SupportedSchemaVersion);
}
