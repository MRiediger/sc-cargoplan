namespace CargoPlan.App.Settings;

/// <summary>
/// Content of settings.json (schema version 2). UI state only, so it lives in the app, not in Core.
/// New settings get a default value, so files written by an older version stay readable.
/// </summary>
/// <param name="Overlay">null = never saved: the window keeps its XAML size and Windows picks the position.</param>
/// <param name="Hotkeys">null = no hotkeys; set by hand in the file (D17).</param>
/// <param name="Appearance">Missing (schema 1) or null = the default look (D31).</param>
/// <param name="Legend">Missing (before T4.3) or null = the legend at the bottom left with its stop lines (D33).</param>
/// <param name="ToolWindowTheme">
/// Missing (before T4.7b) or null = "system". Top level, not in <c>appearance</c>: "Reset to defaults" resets the overlay's
/// look per section and must not switch the tool windows (D36). One of <see cref="ToolWindowThemes.Names"/>.
/// </param>
public sealed record AppSettings(
    int SchemaVersion,
    WindowBounds? Overlay = null,
    HotkeySettings? Hotkeys = null,
    AppearanceSettings? Appearance = null,
    LegendSettings? Legend = null,
    string? ToolWindowTheme = null)
{
    public static AppSettings Default { get; } = new(SettingsStore.SupportedSchemaVersion);

    // Not a constant, so not a default parameter value; see AppearanceSettings.
    public AppearanceSettings Appearance { get; init; } = Appearance ?? AppearanceSettings.Default;

    public LegendSettings Legend { get; init; } = Legend ?? LegendSettings.Default;

    public string ToolWindowTheme { get; init; } = ToolWindowTheme ?? ToolWindowThemes.DefaultName;
}

/// <summary>
/// The legend's layout (D33): top level, not in <c>appearance.legend</c>, because it is layout, not look, and the
/// settings window resets the look per section (D30). Optional, no schema bump (D31).
/// </summary>
/// <param name="Visible">false hides the stop lines only; hints, warnings and notices stay where the legend is.</param>
/// <param name="Position">One of <see cref="LegendPosition.Names"/>; checked by <see cref="AppearanceRules"/>.</param>
public sealed record LegendSettings(bool Visible = true, string Position = "bottom-left")
{
    public static LegendSettings Default { get; } = new();
}

/// <summary>
/// Global hotkeys as text, e.g. "Ctrl+Alt+L" (<see cref="Hotkeys.HotkeyParser"/>). No defaults: a registered hotkey
/// takes its keys from every program, Star Citizen included, so only the player picks them (D17).
/// </summary>
/// <param name="ToggleLock">Switches the manual lock, like the padlock.</param>
/// <param name="ToggleOverlay">Hides the overlay and shows it again.</param>
public sealed record HotkeySettings(string? ToggleLock = null, string? ToggleOverlay = null);
