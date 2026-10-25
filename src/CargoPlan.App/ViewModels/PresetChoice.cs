using CargoPlan.App.Settings;

namespace CargoPlan.App.ViewModels;

/// <summary>One entry of the settings window's preset box (K4.3): a preset and its look, or "Custom" without one.</summary>
/// <param name="Look">null for "Custom": the look matches no preset; choosing it changes nothing.</param>
public sealed record PresetChoice(string Text, Look? Look);
