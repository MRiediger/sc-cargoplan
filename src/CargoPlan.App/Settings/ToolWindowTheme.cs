namespace CargoPlan.App.Settings;

/// <summary>
/// Light or dark for the tool windows (settings, mission editor), not the overlay: its look is <c>appearance</c> (D34,
/// D36). <see cref="System"/> first, so <c>default</c> follows Windows. WPF-free like <see cref="LegendPosition"/>.
/// </summary>
public enum ToolWindowTheme
{
    System,
    Light,
    Dark,
}
