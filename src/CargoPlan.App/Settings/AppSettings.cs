namespace CargoPlan.App.Settings;

/// <summary>
/// Content of settings.json (schema version 2). UI state only, so it lives in the app, not in Core.
/// New settings get a default value, so files written by an older version stay readable.
/// </summary>
/// <param name="Overlay">null = never saved: the window keeps its XAML size and Windows picks the position.</param>
/// <param name="Hotkeys">null = no hotkeys; set by hand in the file (D17).</param>
/// <param name="Appearance">Missing (schema 1) or null = the default look (D31).</param>
public sealed record AppSettings(
    int SchemaVersion,
    WindowBounds? Overlay = null,
    HotkeySettings? Hotkeys = null,
    AppearanceSettings? Appearance = null)
{
    public static AppSettings Default { get; } = new(SettingsStore.SupportedSchemaVersion);

    // Not a constant, so not a default parameter value; see AppearanceSettings.
    public AppearanceSettings Appearance { get; init; } = Appearance ?? AppearanceSettings.Default;
}

/// <summary>
/// Global hotkeys as text, e.g. "Ctrl+Alt+L" (<see cref="Hotkeys.HotkeyParser"/>). No defaults: a registered hotkey
/// takes its keys from every program, Star Citizen included, so only the player picks them (D17).
/// </summary>
/// <param name="ToggleLock">Switches the manual lock, like the padlock.</param>
/// <param name="ToggleOverlay">Hides the overlay and shows it again.</param>
public sealed record HotkeySettings(string? ToggleLock = null, string? ToggleOverlay = null);
