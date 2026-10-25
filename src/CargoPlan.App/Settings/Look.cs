namespace CargoPlan.App.Settings;

/// <summary>
/// What makes the overlay look as it does, the part of settings.json a look file carries and a preset sets (K4.3, D77):
/// the appearance, the legend's layout, the tool windows' theme, the ship view's backdrop and max zoom. Not in it: hotkeys,
/// ContractTracker, window bounds, cameras, the ship and the hologram download switch, which belong to the machine, the
/// run or the network (D63).
/// </summary>
/// <remarks>
/// Only scalars and records, never a collection: the compiler's value equality then compares everything, which is how
/// the settings window tells a preset from "Custom". That is why the max zoom stands here alone and not as
/// <see cref="ShipViewSettings"/>, whose cameras are a dictionary.
/// </remarks>
/// <param name="ToolWindowTheme">One of <see cref="ToolWindowThemes.Names"/>.</param>
/// <param name="ShipOutline">One of <see cref="ShipOutlineModes.Names"/>.</param>
public sealed record Look(
    AppearanceSettings Appearance,
    LegendSettings Legend,
    string ToolWindowTheme,
    string ShipOutline,
    double HoloOpacity,
    string HoloHullColor,
    string HoloEdgeColor,
    double MaxZoomPxPerCell)
{
    /// <summary>The look without settings.json.</summary>
    public static Look Default { get; } = Of(AppSettings.Default);

    public static Look Of(AppSettings settings) => new(
        settings.Appearance,
        settings.Legend,
        settings.ToolWindowTheme,
        settings.ShipOutline,
        settings.HoloOpacity,
        settings.HoloHullColor,
        settings.HoloEdgeColor,
        settings.ShipView.MaxZoomPxPerCell);
}
