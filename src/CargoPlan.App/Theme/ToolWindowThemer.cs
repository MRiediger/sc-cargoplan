using System.Windows;
using System.Windows.Media;
using CargoPlan.App.Settings;

namespace CargoPlan.App.Theme;

/// <summary>
/// Puts a tool window into WPF's Fluent theme with a fixed teal accent (D36). Per window, never
/// <c>Application.ThemeMode</c>: that would restyle the overlay and its tool tips (D34).
/// </summary>
public static class ToolWindowThemer
{
    // Fluent reads the accent only through these colour keys, each a DynamicResource in its Light.xaml/Dark.xaml; dark
    // mode fills with the light shades, light mode with the dark ones. Teal stays apart from warning amber and error red.
    // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Resources/Theme/Dark.xaml
    private static readonly (ResourceKey Key, Color Value)[] Accent =
    [
        (SystemColors.AccentColorKey, Color.FromRgb(0x2A, 0x9D, 0xB0)),
        (SystemColors.AccentColorLight1Key, Color.FromRgb(0x3F, 0xB6, 0xC8)),
        (SystemColors.AccentColorLight2Key, Color.FromRgb(0x6F, 0xD0, 0xDE)),
        (SystemColors.AccentColorLight3Key, Color.FromRgb(0xA3, 0xE4, 0xEC)),
        (SystemColors.AccentColorDark1Key, Color.FromRgb(0x00, 0x78, 0x8A)),
        (SystemColors.AccentColorDark2Key, Color.FromRgb(0x00, 0x5D, 0x6B)),
        (SystemColors.AccentColorDark3Key, Color.FromRgb(0x00, 0x43, 0x4D)),
    ];

    public static void ApplyAccent(ResourceDictionary resources)
    {
        foreach (var (key, value) in Accent)
        {
            resources[key] = value;
        }
    }

    // ThemeMode is experimental in .NET 9 and 10 (WPF0001); suppressed here only, the one place that uses it.
    // Source: https://learn.microsoft.com/dotnet/desktop/wpf/whats-new/net90#set-in-code
#pragma warning disable WPF0001

    /// <summary>
    /// Call before <c>InitializeComponent</c>: Fluent's dictionary then is in the window's resources while its XAML
    /// loads, so own styles find it with <c>BasedOn="{StaticResource {x:Type Slider}}"</c>. Calling it again switches
    /// the mode; Fluent swaps its dictionary in place, and a <c>System</c> window follows Windows by itself.
    /// </summary>
    public static void Apply(Window window, ToolWindowTheme theme)
    {
        // ThemeMode first: its setter loads Fluent at once only while the window's resources don't exist yet, otherwise
        // it waits for the window handle, after the XAML. Reading window.Resources creates them.
        // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/Window.cs
        window.ThemeMode = ThemeModeOf(theme);
        ApplyAccent(window.Resources);
    }

    public static ThemeMode ThemeModeOf(ToolWindowTheme theme) => theme switch
    {
        ToolWindowTheme.System => ThemeMode.System,
        ToolWindowTheme.Light => ThemeMode.Light,
        ToolWindowTheme.Dark => ThemeMode.Dark,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };

#pragma warning restore WPF0001
}
