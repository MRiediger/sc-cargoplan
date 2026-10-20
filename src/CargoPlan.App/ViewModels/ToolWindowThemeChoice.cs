using CargoPlan.App.Settings;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// One entry of the settings window's theme list (T4.7b): the value for the session, the text for the player. A record
/// like <see cref="Editor.FillOrderChoice"/>, because "system" means nothing to a player and "Follow Windows" does.
/// </summary>
public sealed record ToolWindowThemeChoice(ToolWindowTheme Value, string Text);
