using System.Reflection;
using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// State of the settings window (D30, D34): every change builds the look anew and hands it to <c>apply</c> at once, no
/// OK or Cancel; "Reset to defaults" per section. WPF-free, so the tests need no window. Sections in the window's order:
/// the ones a player touches most first (T4.2b).
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    public const string LegendHaloLabel = "Halo around the text";

    public const string CratesHaloLabel = "Outline around the rack numbers and the bow label";

    public const string HeaderHaloLabel = "Halo around the ship name";

    public const string ButtonsHaloLabel = "Halo around the glyphs";

    /// <summary>
    /// The footer: product and the build's informational version, the same as <c>CargoPlan.Cli --version</c>. One line a
    /// hauler copies into a bug report; the commit hash after "+" names the exact build.
    /// Source: https://learn.microsoft.com/en-us/dotnet/core/compatibility/sdk/8.0/source-link
    /// </summary>
    public static string VersionLine { get; } =
        $"SC-CargoPlan {Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown"}";

    private readonly double _windowsFontSize;
    private readonly Action<AppearanceSettings, LegendSettings> _apply;
    private readonly Action<ToolWindowTheme> _changeToolWindowTheme;

    // True while fields are loaded (constructor, reset): their change calls are one change, applied once at the end.
    private bool _loading = true;

    /// <param name="windowsFontSize">The Windows message font size (WPF's SystemFonts, read by the window): the legend's size while "Use Windows text size" is ticked.</param>
    /// <param name="apply">
    /// Gets the new look and legend layout after every change; the session merges them into the one AppSettings, so
    /// bounds moved while this window is open stay (D34).
    /// </param>
    /// <param name="changeToolWindowTheme">Gets a new tool window mode; apart from <paramref name="apply"/>, because it is no part of the overlay's look (D36).</param>
    public SettingsViewModel(
        AppearanceSettings appearance,
        LegendSettings legend,
        double windowsFontSize,
        Action<AppearanceSettings, LegendSettings> apply,
        ToolWindowTheme toolWindowTheme,
        Action<ToolWindowTheme> changeToolWindowTheme)
    {
        _windowsFontSize = Math.Clamp(windowsFontSize, AppearanceRules.MinFontSize, AppearanceRules.MaxFontSize);
        _apply = apply;
        _changeToolWindowTheme = changeToolWindowTheme;
        ToolWindowTheme = toolWindowTheme;

        WindowBackground = new(appearance.Window.Background, AppearanceRules.MinClickableAlpha, OnChanged);
        WindowBorderColor = new(appearance.Window.BorderColor, minAlpha: 0, OnChanged);
        LegendForeground = new(appearance.Legend.Foreground, minAlpha: 0, OnChanged);
        LegendHalo = new(appearance.Legend.Halo, LegendHaloLabel, OnChanged);
        CratesRackNumberColor = new(appearance.Crates.RackNumberColor, minAlpha: 0, OnChanged);
        CratesHalo = new(appearance.Crates.Halo, CratesHaloLabel, OnChanged);
        HeaderForeground = new(appearance.Header.Foreground, minAlpha: 0, OnChanged);
        HeaderHalo = new(appearance.Header.Halo, HeaderHaloLabel, OnChanged);
        ButtonsBackground = new(appearance.Buttons.Background, AppearanceRules.MinClickableAlpha, OnChanged);
        ButtonsBorderColor = new(appearance.Buttons.BorderColor, minAlpha: 0, OnChanged);
        ButtonsHalo = new(appearance.Buttons.Halo, ButtonsHaloLabel, OnChanged);

        LoadWindow(appearance.Window);
        LoadLegend(appearance.Legend);
        LoadLayout(legend);
        LoadCrates(appearance.Crates);
        LoadHeader(appearance.Header);
        LoadButtons(appearance.Buttons);
        _loading = false;
    }

    // Window

    /// <summary>Its alpha is the overlay's transparency; at least <see cref="AppearanceRules.MinClickableAlpha"/>.</summary>
    public ColourField WindowBackground { get; }

    public ColourField WindowBorderColor { get; }

    [ObservableProperty]
    public partial double WindowBorderThickness { get; set; }

    // Legend

    public ColourField LegendForeground { get; }

    /// <summary>Ticked = <c>fontSize: null</c> (D31): the slider is disabled and shows the Windows size, unticked it starts there.</summary>
    [ObservableProperty]
    public partial bool UseWindowsFontSize { get; set; }

    [ObservableProperty]
    public partial double LegendFontSize { get; set; }

    public HaloEditor LegendHalo { get; }

    // Legend layout (D33): a section of its own, so resetting the legend's look doesn't move it.

    [ObservableProperty]
    public partial bool IsLegendVisible { get; set; }

    /// <summary>One of <see cref="LegendPositions"/>.</summary>
    public string LegendPosition
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnChanged();
            }
        }
    } = Settings.LegendPosition.Default.ToString();

    public IReadOnlyList<string> LegendPositions => Settings.LegendPosition.Names;

    // Crates: their colours are the stop colours (D29), so only fill and edge alpha, 0–255 like the file.

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CratesFillPercent))]
    public partial int CratesFillAlpha { get; set; }

    public string CratesFillPercent => ColourField.Percent(CratesFillAlpha);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CratesEdgePercent))]
    public partial int CratesEdgeAlpha { get; set; }

    public string CratesEdgePercent => ColourField.Percent(CratesEdgeAlpha);

    public ColourField CratesRackNumberColor { get; }

    [ObservableProperty]
    public partial double CratesRackNumberFontSize { get; set; }

    /// <summary>The sharp outline of the rack numbers (D31): its size is a stroke width.</summary>
    public HaloEditor CratesHalo { get; }

    // Header

    /// <summary>Also the glyphs: pencil, gear, split, merge, padlock. Invisible, the gear is found by its tool tip (D34).</summary>
    public ColourField HeaderForeground { get; }

    public HaloEditor HeaderHalo { get; }

    // Buttons

    /// <summary>At least <see cref="AppearanceRules.MinClickableAlpha"/>, or clicks pass through the buttons.</summary>
    public ColourField ButtonsBackground { get; }

    public ColourField ButtonsBorderColor { get; }

    [ObservableProperty]
    public partial double ButtonsBorderThickness { get; set; }

    [ObservableProperty]
    public partial double ButtonsCornerRadius { get; set; }

    public HaloEditor ButtonsHalo { get; }

    // Tool windows (D36): this window and the editor, not the overlay. Set once, so no reset.

    public static IReadOnlyList<ToolWindowThemeChoice> ToolWindowThemeChoices { get; } =
    [
        new(Settings.ToolWindowTheme.System, "Follow Windows"),
        new(Settings.ToolWindowTheme.Light, "Light"),
        new(Settings.ToolWindowTheme.Dark, "Dark"),
    ];

    [ObservableProperty]
    public partial ToolWindowTheme ToolWindowTheme { get; set; }

    [RelayCommand]
    private void ResetWindow() => Batch(() => LoadWindow(WindowAppearance.Default));

    [RelayCommand]
    private void ResetLegend() => Batch(() => LoadLegend(LegendAppearance.Default));

    [RelayCommand]
    private void ResetLegendLayout() => Batch(() => LoadLayout(LegendSettings.Default));

    [RelayCommand]
    private void ResetCrates() => Batch(() => LoadCrates(CrateAppearance.Default));

    [RelayCommand]
    private void ResetHeader() => Batch(() => LoadHeader(HeaderAppearance.Default));

    [RelayCommand]
    private void ResetButtons() => Batch(() => LoadButtons(ButtonAppearance.Default));

    partial void OnWindowBorderThicknessChanged(double value) => OnChanged();

    // The slider follows the switch: ticked it shows the Windows size, unticked it starts there (D31). One change.
    partial void OnUseWindowsFontSizeChanged(bool value) => Batch(() => LegendFontSize = _windowsFontSize);

    partial void OnLegendFontSizeChanged(double value) => OnChanged();

    partial void OnIsLegendVisibleChanged(bool value) => OnChanged();

    partial void OnCratesFillAlphaChanged(int value) => OnChanged();

    partial void OnCratesEdgeAlphaChanged(int value) => OnChanged();

    partial void OnCratesRackNumberFontSizeChanged(double value) => OnChanged();

    partial void OnButtonsBorderThicknessChanged(double value) => OnChanged();

    partial void OnButtonsCornerRadiusChanged(double value) => OnChanged();

    // Not OnChanged: the mode is no part of the look. The constructor sets it while loading, which calls nothing.
    partial void OnToolWindowThemeChanged(ToolWindowTheme value)
    {
        if (!_loading)
        {
            _changeToolWindowTheme(value);
        }
    }

    private void LoadWindow(WindowAppearance window)
    {
        WindowBackground.Load(window.Background);
        WindowBorderColor.Load(window.BorderColor);
        WindowBorderThickness = window.BorderThickness;
    }

    private void LoadLegend(LegendAppearance legend)
    {
        // The switch first: it sets the slider to the Windows size, a number of its own overrides that.
        UseWindowsFontSize = legend.FontSize is null;
        LegendFontSize = legend.FontSize ?? _windowsFontSize;
        LegendForeground.Load(legend.Foreground);
        LegendHalo.Load(legend.Halo);
    }

    private void LoadLayout(LegendSettings legend)
    {
        IsLegendVisible = legend.Visible;
        LegendPosition = legend.Position;
    }

    private void LoadCrates(CrateAppearance crates)
    {
        CratesFillAlpha = crates.FillAlpha;
        CratesEdgeAlpha = crates.EdgeAlpha;
        CratesRackNumberColor.Load(crates.RackNumberColor);
        CratesRackNumberFontSize = crates.RackNumberFontSize;
        CratesHalo.Load(crates.Halo);
    }

    private void LoadHeader(HeaderAppearance header)
    {
        HeaderForeground.Load(header.Foreground);
        HeaderHalo.Load(header.Halo);
    }

    private void LoadButtons(ButtonAppearance buttons)
    {
        ButtonsBackground.Load(buttons.Background);
        ButtonsBorderColor.Load(buttons.BorderColor);
        ButtonsBorderThickness = buttons.BorderThickness;
        ButtonsCornerRadius = buttons.CornerRadius;
        ButtonsHalo.Load(buttons.Halo);
    }

    /// <summary>Runs <paramref name="load"/> as one change: applied once afterwards, or not at all inside the constructor.</summary>
    private void Batch(Action load)
    {
        var wasLoading = _loading;
        _loading = true;
        load();
        _loading = wasLoading;
        OnChanged();
    }

    private void OnChanged()
    {
        if (_loading)
        {
            return;
        }

        AppearanceSettings appearance = new(
            new WindowAppearance(WindowBackground.Value, WindowBorderColor.Value, WindowBorderThickness),
            new HeaderAppearance(HeaderForeground.Value, HeaderHalo.Value),
            new LegendAppearance(LegendForeground.Value, UseWindowsFontSize ? null : LegendFontSize, LegendHalo.Value),
            new CrateAppearance(CratesFillAlpha, CratesEdgeAlpha, CratesRackNumberColor.Value, CratesRackNumberFontSize, CratesHalo.Value),
            new ButtonAppearance(ButtonsBackground.Value, ButtonsBorderColor.Value, ButtonsBorderThickness, ButtonsCornerRadius, ButtonsHalo.Value));
        _apply(appearance, new LegendSettings(IsLegendVisible, LegendPosition));
    }
}
