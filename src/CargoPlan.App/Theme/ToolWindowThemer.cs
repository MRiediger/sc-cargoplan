using System.Windows;
using System.Windows.Controls;
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

    /// <summary>Every tool window's combo box (session 4d, D36 addendum): Fluent's, with <see cref="ComboBoxItemStyleKey"/>.</summary>
    public const string ComboBoxStyleKey = "ToolComboBoxStyle";

    /// <summary>Every tool window's combo box entry: Fluent's, with a filled picked entry (ToolComboBox.xaml).</summary>
    public const string ComboBoxItemStyleKey = "ToolComboBoxItemStyle";

    private static readonly Uri ComboBoxTemplatesUri = new("pack://application:,,,/CargoPlan.App;component/Theme/ToolComboBox.xaml", UriKind.Absolute);

    /// <summary>
    /// The selection in dark mode (4d, variant C): a neutral tone, opaque, for the grid's selected row and the picked entry;
    /// the accent stays for controls and marks the selection only as a 3 px bar. Chosen by Martin at the picture.
    /// </summary>
    public static readonly Color SelectionFillDark = Color.FromRgb(0x3A, 0x4A, 0x55);

    /// <summary>The named tone in the dark dictionary, as <see cref="SelectionFillDark"/>.</summary>
    public const string SelectionFillDarkKey = "SelectionFillDark";

    /// <summary>The picked entry's fill: the tone in dark, none in light (Fluent's).</summary>
    public const string SelectionFillKey = "ToolSelectionFill";

    /// <summary>The picked entry under the mouse: still the full tone in dark, Fluent's highlight in light.</summary>
    public const string SelectionHighlightFillKey = "ToolSelectionHighlightFill";

    /// <summary>The selected row's text colour, for the grid's editing combo box: grey in dark, Fluent's white in light.</summary>
    public const string SelectedRowTextColorKey = "ToolSelectedRowTextColor";

    /// <summary>The selected row's bar: 3 px in dark, none in light.</summary>
    public const string RowBarThicknessKey = "ToolRowBarThickness";

    // Hover one step below the selection: the tone at 60 %.
    private const byte SelectionHoverAlpha = 0x99;

    // Fluent's own light values, read from its file rather than written down again.
    // Source: https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/Themes/PresentationFramework.Fluent/Resources/Theme/Light.xaml
    private const string FluentLightColorsUri = "pack://application:,,,/PresentationFramework.Fluent;component/Resources/Theme/Light.xaml";

    /// <summary>
    /// The entry states with a background of their own, as resource keys: background, and text unless it is the normal
    /// text colour; the grid's selected row too. Read by the contrast test (WCAG ≥ 4.5:1).
    /// </summary>
    public static IReadOnlyList<(string Name, string Background, string? Foreground)> ComboBoxItemStates { get; } =
    [
        ("plain", "ComboBoxDropDownBackground", null),
        ("picked", SelectionFillKey, null),
        ("picked under the mouse", SelectionHighlightFillKey, null),
        ("hovered", "ComboBoxItemBackgroundSelected", null),
        ("selected row", "DataGridRowSelectedBackgroundThemeBrush", "DataGridRowSelectedForegroundThemeBrush"),
    ];

    /// <summary>Dark selection (variant C): the neutral tone for rows and entries, the light text kept, a 3 px bar.</summary>
    public static ResourceDictionary SelectionDark => field ??= CreateSelectionDark();

    /// <summary>Light selection: Fluent's, untouched; only our own keys get Fluent's values.</summary>
    public static ResourceDictionary SelectionLight => field ??= CreateSelectionLight();

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

    /// <summary>
    /// Merges exactly one of <see cref="SelectionLight"/> and <see cref="SelectionDark"/> after Fluent's dictionary, like
    /// <see cref="ApplyText"/>; a mode switch swaps them.
    /// </summary>
    public static void ApplySelection(ResourceDictionary resources, bool dark)
    {
        var merged = resources.MergedDictionaries;
        merged.Remove(dark ? SelectionLight : SelectionDark);
        var wanted = dark ? SelectionDark : SelectionLight;
        if (!merged.Contains(wanted))
        {
            merged.Add(wanted);
        }
    }

    private static ResourceDictionary CreateSelectionDark()
    {
        var fill = Frozen(SelectionFillDark);
        return new()
        {
            [SelectionFillDarkKey] = SelectionFillDark,
            [SelectionFillKey] = fill,
            [SelectionHighlightFillKey] = fill,
            [SelectedRowTextColorKey] = DarkTextColor,
            [RowBarThicknessKey] = new Thickness(3, 0, 0, 0),
            // Fluent's keys: the selected row and the hovered entry.
            ["DataGridRowSelectedBackgroundThemeBrush"] = fill,
            ["DataGridRowSelectedForegroundThemeBrush"] = Frozen(DarkTextColor),
            ["ComboBoxItemBackgroundSelected"] = Frozen(Color.FromArgb(SelectionHoverAlpha, SelectionFillDark.R, SelectionFillDark.G, SelectionFillDark.B)),
        };
    }

    private static ResourceDictionary CreateSelectionLight()
    {
        ResourceDictionary fluent = new() { Source = new Uri(FluentLightColorsUri, UriKind.Absolute) };
        return new()
        {
            [SelectionFillKey] = Frozen(Colors.Transparent),
            [SelectionHighlightFillKey] = Frozen(((SolidColorBrush)fluent["ComboBoxItemBackgroundSelected"]).Color),
            [SelectedRowTextColorKey] = (Color)fluent["TextOnAccentFillColorPrimary"],
            [RowBarThicknessKey] = new Thickness(0),
        };
    }

    private static SolidColorBrush Frozen(Color color)
    {
        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
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
        ApplySelection(window.Resources, UsesDarkText(theme));
        ApplyComboBoxes(window.Resources);
        WatchWindows();
    }

    /// <summary>
    /// ToolComboBoxStyle and ToolComboBoxItemStyle, based on Fluent's, by key and implicit, so every combo box of the
    /// window wears them and the grid's editing combo box can build on them. In code, not in a shared XAML file: Fluent
    /// lives in each window, where a separate dictionary's StaticResource can't see it. Ours from an earlier call go
    /// first, so a mode switch bases the new ones on Fluent's again, never on themselves.
    /// </summary>
    private static void ApplyComboBoxes(ResourceDictionary resources)
    {
        resources.Remove(typeof(ComboBox));
        resources.Remove(typeof(ComboBoxItem));

        // Loaded per window, not cached: a template belongs to the thread that loaded it.
        ResourceDictionary templates = new() { Source = ComboBoxTemplatesUri };
        Style item = new(typeof(ComboBoxItem), (Style)resources[typeof(ComboBoxItem)]);
        item.Setters.Add(new Setter(Control.TemplateProperty, templates["ToolComboBoxItemTemplate"]));
        // Fluent's is ComboBoxForeground, which the grid's editing combo box turns to the selected row's text colour.
        item.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("TextFillColorPrimaryBrush")));
        Style combo = new(typeof(ComboBox), (Style)resources[typeof(ComboBox)]);
        combo.Setters.Add(new Setter(ItemsControl.ItemContainerStyleProperty, item));

        resources[ComboBoxItemStyleKey] = item;
        resources[ComboBoxStyleKey] = combo;
        resources[typeof(ComboBoxItem)] = item;
        resources[typeof(ComboBox)] = combo;
    }

    /// <summary>
    /// Fluent switches a System window by itself and tells nobody, so the grey text listens to the same Windows message
    /// (WM_SETTINGCHANGE "ImmersiveColorSet" arrives as category General; high contrast comes this way too). Once, from
    /// the UI thread: the handler runs on the subscriber's synchronization context. Static and never detached: it holds
    /// no window, and it lives as long as the app. Without an Application there are no windows to walk and no UI thread
    /// that outlives the call (the tests' STA threads end after each test), so nothing is watched (4d: a full test run
    /// hung once and once reported 67 tests too few after the tests first called Apply).
    /// Source: https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Win32.SystemEvents/src/Microsoft/Win32/SystemEvents.cs (SystemEventInvokeInfo)
    /// </summary>
    private static void WatchWindows()
    {
        if (_watchingWindows || Application.Current is null)
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
                ApplySelection(window.Resources, UsesDarkText(theme));
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
