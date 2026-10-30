using System.Text.Json.Serialization;
using CargoPlan.Core.Planning;
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
/// <param name="HoloHullColor">
/// The hologram's hull in the shaded and flat looks (D63), "#AARRGGBB", always opaque. Missing or null =
/// <see cref="AppearanceRules.DefaultHoloHullColor"/>; checked by <see cref="AppearanceRules.NormalizeHoloColor"/>.
/// </param>
/// <param name="HoloEdgeColor">The hologram's edges in the hidden-line look, like <paramref name="HoloHullColor"/>.</param>
/// <param name="ShipView">
/// Missing (before K1) or null = the defaults (D76). An object of its own, not flat like <paramref name="ShipOutline"/>:
/// zoom and the cameras per ship (K1.2) belong together; the Ship view section resets the zoom only.
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
    double HoloOpacity = AppearanceRules.DefaultHoloOpacity,
    string? HoloHullColor = null,
    string? HoloEdgeColor = null,
    ShipViewSettings? ShipView = null)
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

    public string HoloHullColor { get; init; } = HoloHullColor ?? AppearanceRules.DefaultHoloHullColor;

    public string HoloEdgeColor { get; init; } = HoloEdgeColor ?? AppearanceRules.DefaultHoloEdgeColor;

    public ContractTrackerSettings ContractTracker { get; init; } = ContractTracker ?? ContractTrackerSettings.Default;

    public ShipViewSettings ShipView { get; init; } = ShipView ?? ShipViewSettings.Default;
}

/// <summary>The ship view's zoom and the player's camera per ship (D76). Optional, no schema bump (D31).</summary>
/// <param name="MaxZoomPxPerCell">
/// How far the wheel zooms in, in pixels per cell (DIPs), <see cref="AppearanceRules.MinMaxZoom"/> to
/// <see cref="AppearanceRules.MaxMaxZoom"/>; checked by <see cref="AppearanceRules.Normalize(ShipViewSettings)"/>.
/// </param>
/// <param name="Cameras">
/// Catalog id → the view as the player left it on that ship (K1.2); only the recent ships and the current one
/// (<c>OverlayViewModel.Cameras</c>). Missing or null = none: every ship fits from its own default camera.
/// </param>
// IReadOnlyDictionary<string, T> reads and writes as a JSON object.
// Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/supported-types
public sealed record ShipViewSettings(double MaxZoomPxPerCell = AppearanceRules.DefaultMaxZoom, IReadOnlyDictionary<string, ShipCamera>? Cameras = null)
{
    public static ShipViewSettings Default { get; } = new();

    // Edited by hand, an entry without a camera or an id names nothing: dropped at the file boundary (A6), like a gap in
    // recentShips. RespectNullableAnnotations doesn't reach dictionary values ("Limitations").
    // Source: https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/nullable-annotations
    // A framing from before the perspective view (D83) means nothing there: that ship starts at its default camera.
    public IReadOnlyDictionary<string, ShipCamera>? Cameras { get; init; } =
        Cameras?.Where(c => !string.IsNullOrWhiteSpace(c.Key) && c.Value is { PxPerCell: null, PanX: null, PanY: null }).ToDictionary();
}

/// <summary>
/// The ship view as the player left it on one ship (D76, D83): the orientation, and distance and target once zoomed,
/// panned or turned. Written by the view, not by hand; read as the camera can be (the yaw wraps, the pitch and the
/// distance are clamped). Null values are not written.
/// </summary>
/// <param name="Yaw">Degrees, like <c>IsoProjection.Yaw</c>.</param>
/// <param name="Pitch">Degrees, like <c>IsoProjection.Pitch</c>.</param>
/// <param name="PxPerCell">
/// The orthographic view's framing before D83, with <paramref name="PanX"/> and <paramref name="PanY"/>: read only, so a
/// file from before Phase 8 still loads (the strict options refuse unknown keys); an entry with any of the three is dropped
/// (<see cref="ShipViewSettings.Cameras"/>). Goes with the next schema bump of settings.json (backlog).
/// </param>
/// <param name="Distance">Cells from the camera to <paramref name="Target"/>; with it the placement, both or none (none = fit).</param>
/// <param name="Target">The point the camera looks at, in ship coordinates (cells): fixed to the ship, so cargo doesn't move it.</param>
public sealed record ShipCamera(
    double Yaw,
    double Pitch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? PxPerCell = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? PanX = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? PanY = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Distance = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CameraTarget? Target = null);

/// <summary>A point in ship coordinates, in cells (x bow, y port, z up); the camera's target (D83).</summary>
public sealed record CameraTarget(double X, double Y, double Z);

/// <summary>
/// The legend's layout (D33): top level, not in <c>appearance.legend</c>, because it is layout, not look, and the
/// settings window resets the look per section (D30). Optional, no schema bump (D31).
/// </summary>
/// <param name="Visible">false hides the stop lines only; warnings and notices stay where the legend is.</param>
/// <param name="Position">One of <see cref="LegendPosition.Names"/>; checked by <see cref="AppearanceRules"/>.</param>
/// <param name="MaxHeightShare">
/// The stop lines take at most this share of the overlay's height and scroll beyond it (K7, D80);
/// <see cref="AppearanceRules.MinLegendMaxHeightShare"/> to <see cref="AppearanceRules.MaxLegendMaxHeightShare"/>, missing (before K7)
/// = <see cref="AppearanceRules.DefaultLegendMaxHeightShare"/>. Warnings below the lines don't count and never scroll.
/// </param>
public sealed record LegendSettings(bool Visible = true, string Position = "bottom-left", double MaxHeightShare = AppearanceRules.DefaultLegendMaxHeightShare)
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
/// <param name="DiscardPlan">Empties the plan, so the next sync starts over (K2.3).</param>
/// <param name="Undo">Goes back one step (K2.4).</param>
/// <param name="Redo">Goes forward one undone step (K2.4).</param>
/// <param name="ResetView">Fits the ship view from the ship's default camera (K1.3).</param>
public sealed record HotkeySettings(
    string? ToggleLock = null, string? ToggleOverlay = null, string? SyncContractTracker = null, string? Replan = null, string? DiscardPlan = null,
    string? Undo = null, string? Redo = null, string? ResetView = null);

/// <summary>
/// Where ContractTracker's database is and whether captures come into the plan by themselves (D44). Optional, no schema
/// bump (D31); without it the overlay does what T5.4 did.
/// </summary>
/// <param name="Path">null = detect it at ContractTracker's one place (D38); otherwise a full path, checked by
/// <see cref="AppearanceRules.NormalizeContractTracker"/>.</param>
/// <param name="AutoSync">false = a capture only gives the notice "N new contracts"; the editor's button or the hotkey takes them in.</param>
/// <param name="OnContractGone">What a sync does with a contract ContractTracker no longer lists (D81): "keep" or "remove",
/// checked by <see cref="AppearanceRules.NormalizeContractTracker"/>; a new key without a schema bump.</param>
public sealed record ContractTrackerSettings(string? Path = null, bool AutoSync = true, string OnContractGone = ContractGonePolicies.DefaultName)
{
    public static ContractTrackerSettings Default { get; } = new();

    /// <summary>The file the sync reads: <see cref="Path"/>, or ContractTracker's own place.</summary>
    public string DatabasePath => Path ?? ContractTrackerReader.DefaultPath;

    /// <summary><see cref="OnContractGone"/> parsed; not a key of its own in the file.</summary>
    /// <exception cref="FormatException">Not normalized: a bug.</exception>
    [JsonIgnore]
    public ContractGonePolicy GonePolicy => ContractGonePolicies.Parse(OnContractGone);
}
