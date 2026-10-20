using System.Windows;
using System.Windows.Media;
using CargoPlan.App.Settings;
using Microsoft.Win32;

namespace CargoPlan.App.Theme;

/// <summary>
/// Puts a tool window into WPF's Fluent theme with a fixed teal accent and, in dark mode, grey instead of white text
/// (D36). Per window, never <c>Application.ThemeMode</c>: that would restyle the overlay and its tool tips (D34).
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

    /// <summary>
    /// Text in dark mode (T4.7b): Fluent's is pure white and glares next to the game. Chosen by eye from #E6E6E6, #D0D0D0
    /// and #BEBEBE. Light mode keeps Fluent's near-black.
    /// </summary>
    public static readonly Color DarkTextColor = Color.FromRgb(0xD0, 0xD0, 0xD0);

    // Every brush Fluent builds from this colour is a text foreground (55 in .NET 10: window, button, text box, combo box,
    // group box header, column header, tool tip...); each control reads its own, so one override per brush.
    // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Resources/Theme/Dark.xaml
    private const string FluentDarkColorsUri = "pack://application:,,,/PresentationFramework.Fluent;component/Resources/Theme/Dark.xaml";
    private const string PrimaryTextColorKey = "TextFillColorPrimary";

    // Just as white, but built from colours of their own: the keyboard focus ring and selected text on the accent stay white.
    private static readonly string[] NotText = ["FocusStrokeColorOuterBrush", "TextOnAccentFillColorSelectedTextBrush"];

    // Where Fluent reads light or dark for ThemeMode.System: same values, same fallback (ToolWindowThemes.Resolve).
    // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationFramework/System/Windows/ThemeManager.cs
    private const string PersonalizeKey = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private const string SystemUsesLightThemeValue = "SystemUsesLightTheme";

    private static bool _watchingWindows;

    /// <summary>Grey for each of Fluent's white text brushes; one frozen set, merged into every dark tool window.</summary>
    public static ResourceDictionary DarkText => field ??= CreateDarkText();

    public static void ApplyAccent(ResourceDictionary resources)
    {
        foreach (var (key, value) in Accent)
        {
            resources[key] = value;
        }
    }

    /// <summary>
    /// Merges <see cref="DarkText"/> after Fluent's dictionary, so it wins, or takes it out again. Fluent replaces its own
    /// dictionary in place when the mode changes, this one stays where it is.
    /// </summary>
    public static void ApplyText(ResourceDictionary resources, bool dark)
    {
        var merged = resources.MergedDictionaries;
        if (!dark)
        {
            merged.Remove(DarkText);
        }
        else if (!merged.Contains(DarkText))
        {
            merged.Add(DarkText);
        }
    }

    private static ResourceDictionary CreateDarkText()
    {
        ResourceDictionary fluent = new() { Source = new Uri(FluentDarkColorsUri, UriKind.Absolute) };
        var white = (Color)fluent[PrimaryTextColorKey];
        SolidColorBrush grey = new(DarkTextColor);
        grey.Freeze();

        ResourceDictionary text = new();
        foreach (var key in fluent.Keys)
        {
            if (key is string name && !NotText.Contains(name) && fluent[key] is SolidColorBrush { Color: var colour } && colour == white)
            {
                text[key] = grey;
            }
        }

        return text;
    }

    /// <summary>
    /// The text follows Windows like Fluent does: grey only while the window really shows dark, never in high contrast,
    /// where Fluent has a dictionary of its own and its colours are the user's.
    /// </summary>
    private static bool UsesDarkText(ToolWindowTheme theme) =>
        !SystemParameters.HighContrast && ToolWindowThemes.Resolve(theme, AppsUseLightTheme()) == ToolWindowTheme.Dark;

    private static int? AppsUseLightTheme() =>
        Registry.GetValue(PersonalizeKey, AppsUseLightThemeValue, null) as int?
        ?? Registry.GetValue(PersonalizeKey, SystemUsesLightThemeValue, null) as int?;

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
        ApplyText(window.Resources, UsesDarkText(theme));
        WatchWindows();
    }

    /// <summary>
    /// Fluent switches a System window by itself and tells nobody, so the grey text listens to the same Windows message
    /// (WM_SETTINGCHANGE "ImmersiveColorSet" arrives as category General; high contrast comes this way too). Once, from
    /// the UI thread: the handler runs on the subscriber's synchronization context. Static and never detached: it holds
    /// no window, and it lives as long as the app.
    /// Source: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Win32.SystemEvents/src/Microsoft/Win32/SystemEvents.cs (SystemEventInvokeInfo)
    /// </summary>
    private static void WatchWindows()
    {
        if (_watchingWindows)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        _watchingWindows = true;
    }

    // Every themed window, not only System ones: high contrast switches Light and Dark windows too.
    private static void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        foreach (var window in Application.Current.Windows.OfType<Window>())
        {
            if (ThemeOf(window.ThemeMode) is { } theme)
            {
                ApplyText(window.Resources, UsesDarkText(theme));
            }
        }
    }

    /// <summary>The setting behind a window's mode; null for the overlay and the lock window, which stay unthemed (D34).</summary>
    private static ToolWindowTheme? ThemeOf(ThemeMode mode) =>
        mode == ThemeMode.System ? ToolWindowTheme.System
        : mode == ThemeMode.Light ? ToolWindowTheme.Light
        : mode == ThemeMode.Dark ? ToolWindowTheme.Dark
        : null;

    public static ThemeMode ThemeModeOf(ToolWindowTheme theme) => theme switch
    {
        ToolWindowTheme.System => ThemeMode.System,
        ToolWindowTheme.Light => ThemeMode.Light,
        ToolWindowTheme.Dark => ThemeMode.Dark,
        _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null),
    };

#pragma warning restore WPF0001
}
