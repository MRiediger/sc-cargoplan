using CargoPlan.App.Settings;

namespace CargoPlan.App.ViewModels;

/// <summary>One entry of the settings window's stop palettes (K4.2): the value for the file, "Okabe-Ito · 8 colors" for the player.</summary>
public sealed record StopPaletteChoice(StopPaletteName Value, string Text);
