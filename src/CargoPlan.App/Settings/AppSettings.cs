using CargoPlan.Import.ContractTracker;

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
/// <param name="ContractTracker">Missing (before T5.5) or null = detect ContractTracker and sync by itself (D44).</param>
/// <param name="Ship">
/// The ship's catalog id (D59), written only by a choice in the overlay header (T7.2b). Missing (before T7.2) or null = the ship of
/// plan.json, else the Hull A. A ship that doesn't load is kept here; the start falls back and says so (T7.2).
/// </param>
/// <param name="RecentShips">
/// Catalog ids of the ships chosen last, latest first, the current one included (T7.2c, <c>ShipChoices.Remember</c>); written
/// with <paramref name="Ship"/>. Missing (before T7.2c) or null = none yet. Unknown ids stay, like <c>ship</c>.
/// </param>
/// <param name="ShipOutline">
/// What lies behind the racks in the ship view (D62, D63), one of <see cref="ShipOutlineModes.Names"/>. Missing (before
/// T7.4) or null = "outline". Top level like <paramref name="ToolWindowTheme"/>: the crates' "Reset to defaults" must not
/// switch it.
/// </param>
/// <param name="DownloadShipHolograms">
/// Missing (before T7.4c-2) = true: a hologram look downloads the ship's file from the release (D63). false = no network
/// access at all, holograms already downloaded still show. A default like <c>ContractTrackerSettings.AutoSync</c>, optional,
/// no schema bump (D31).
/// </param>
/// <param name="HoloOpacity">
/// The hologram backdrop's opacity, <see cref="AppearanceRules.MinHoloOpacity"/> to 1 (D63); missing (before T7.4c-2) =
/// <see cref="AppearanceRules.DefaultHoloOpacity"/>. Top level next to <paramref name="ShipOutline"/>: the Ship view
/// section resets it, the crates' reset doesn't.
/// </param>
public sealed record AppSettings(
    int SchemaVersion,
    WindowBounds? Overlay = null,
    HotkeySettings? Hotkeys = null,
    AppearanceSettings? Appearance = null,
    LegendSettings? Legend = null,
    string? ToolWindowTheme = null,
    ContractTrackerSettings? ContractTracker = null,
    string? Ship = null,
    IReadOnlyList<string>? RecentShips = null,
    string? ShipOutline = null,
    bool DownloadShipHolograms = true,
    double HoloOpacity = AppearanceRules.DefaultHoloOpacity)
{
    public static AppSettings Default { get; } = new(SettingsStore.SupportedSchemaVersion);

    // Edited by hand, a gap names no ship: dropped here, at the file boundary (A6). RespectNullableAnnotations doesn't
    // reach list elements ("Limitations"). Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/nullable-annotations
    public IReadOnlyList<string>? RecentShips { get; init; } =
        RecentShips is null ? null : [.. RecentShips.Where(id => !string.IsNullOrWhiteSpace(id))];

    // Not a constant, so not a default parameter value; see AppearanceSettings.
    public AppearanceSettings Appearance { get; init; } = Appearance ?? AppearanceSettings.Default;

    public LegendSettings Legend { get; init; } = Legend ?? LegendSettings.Default;

    public string ToolWindowTheme { get; init; } = ToolWindowTheme ?? ToolWindowThemes.DefaultName;

    public string ShipOutline { get; init; } = ShipOutline ?? ShipOutlineModes.DefaultName;

    public ContractTrackerSettings ContractTracker { get; init; } = ContractTracker ?? ContractTrackerSettings.Default;
}

/// <summary>
/// The legend's layout (D33): top level, not in <c>appearance.legend</c>, because it is layout, not look, and the
/// settings window resets the look per section (D30). Optional, no schema bump (D31).
/// </summary>
/// <param name="Visible">false hides the stop lines only; warnings and notices stay where the legend is.</param>
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
/// <param name="SyncContractTracker">Takes new ContractTracker captures into the plan (D44).</param>
/// <param name="Replan">Lays out the cargo on board anew (D66).</param>
public sealed record HotkeySettings(string? ToggleLock = null, string? ToggleOverlay = null, string? SyncContractTracker = null, string? Replan = null);

/// <summary>
/// Where ContractTracker's database is and whether captures come into the plan by themselves (D44). Optional, no schema
/// bump (D31); without it the overlay does what T5.4 did.
/// </summary>
/// <param name="Path">null = detect it at ContractTracker's one place (D38); otherwise a full path, checked by
/// <see cref="AppearanceRules.NormalizeContractTracker"/>.</param>
/// <param name="AutoSync">false = a capture only gives the notice "N new contracts"; the editor's button or the hotkey takes them in.</param>
public sealed record ContractTrackerSettings(string? Path = null, bool AutoSync = true)
{
    public static ContractTrackerSettings Default { get; } = new();

    /// <summary>The file the sync reads: <see cref="Path"/>, or ContractTracker's own place.</summary>
    public string DatabasePath => Path ?? ContractTrackerReader.DefaultPath;
}
