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

/// <summary>
/// The modes as settings.json writes them, "system", "light", "dark". The file holds a string, not an enum the serializer
/// checks: an unknown name then follows Windows with a notice (<see cref="AppearanceRules"/>) instead of making the whole
/// file invalid (D13). A class of its own, because an enum can't have methods.
/// </summary>
public static class ToolWindowThemes
{
    /// <summary>What a missing or null key means: follow Windows, like before the setting existed.</summary>
    public static string DefaultName { get; } = ToName(ToolWindowTheme.System);

    private static readonly ToolWindowTheme[] All = Enum.GetValues<ToolWindowTheme>();

    /// <summary>Every valid name, in the enum's order; settings-format.md lists the same.</summary>
    public static IReadOnlyList<string> Names { get; } = [.. All.Select(ToName)];

    /// <summary>Exactly one of <see cref="Names"/>, lower case like the keys; not a number, unlike <c>Enum.TryParse</c>.</summary>
    public static bool TryParse(string text, out ToolWindowTheme theme)
    {
        var index = Array.FindIndex(All, t => ToName(t) == text);
        theme = index >= 0 ? All[index] : default;
        return index >= 0;
    }

    /// <exception cref="FormatException">Not a mode; after <see cref="AppearanceRules.NormalizeToolWindowTheme"/> that is a bug.</exception>
    public static ToolWindowTheme Parse(string text) =>
        TryParse(text, out var theme) ? theme : throw new FormatException($"\"{text}\" is not a tool window theme ({string.Join(", ", Names)}).");

    public static string ToName(ToolWindowTheme theme) => theme.ToString().ToLowerInvariant();

    /// <summary>
    /// What a window in <paramref name="theme"/> shows now: Light or Dark. <see cref="ToolWindowTheme.System"/> asks
    /// Windows the way Fluent does, so both agree: a non-zero value is light, 0 or none is dark.
    /// Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/ThemeManager.cs (IsSystemThemeLight)
    /// </summary>
    /// <param name="appsUseLightTheme">The registry's AppsUseLightTheme (or SystemUsesLightTheme), null if neither exists.</param>
    public static ToolWindowTheme Resolve(ToolWindowTheme theme, int? appsUseLightTheme) => theme switch
    {
        ToolWindowTheme.System => appsUseLightTheme is null or 0 ? ToolWindowTheme.Dark : ToolWindowTheme.Light,
        _ => theme,
    };
}
