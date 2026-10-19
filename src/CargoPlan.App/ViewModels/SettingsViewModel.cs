using CargoPlan.App.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>
/// State of the settings window (D30, D34): every change builds the look anew and hands it to <c>apply</c> at once, no
/// OK or Cancel; "Reset to defaults" per section. WPF-free, so the tests need no window. Sections without fields here
/// (header, crates, buttons until T4.2b) pass through unchanged.
/// </summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly double _windowsFontSize;
    private readonly Action<AppearanceSettings, LegendSettings> _apply;
    private AppearanceSettings _appearance;

    // True while fields are loaded (constructor, reset): their change calls are one change, applied once at the end.
    private bool _loading = true;

    /// <param name="windowsFontSize">The Windows message font size (WPF's SystemFonts, read by the window): the legend's size while "Use Windows text size" is ticked.</param>
    /// <param name="apply">
    /// Gets the new look and legend layout after every change; the session merges them into the one AppSettings, so
    /// bounds moved while this window is open stay (D34).
    /// </param>
    public SettingsViewModel(AppearanceSettings appearance, LegendSettings legend, double windowsFontSize, Action<AppearanceSettings, LegendSettings> apply)
    {
        _appearance = appearance;
        _windowsFontSize = Math.Clamp(windowsFontSize, AppearanceRules.MinFontSize, AppearanceRules.MaxFontSize);
        _apply = apply;

        WindowBackground = new(appearance.Window.Background, AppearanceRules.MinClickableAlpha, OnChanged);
        WindowBorderColor = new(appearance.Window.BorderColor, minAlpha: 0, OnChanged);
        LegendForeground = new(appearance.Legend.Foreground, minAlpha: 0, OnChanged);
        LegendHalo = new(appearance.Legend.Halo, OnChanged);

        LoadWindow(appearance.Window);
        LoadLegend(appearance.Legend);
        LoadLayout(legend);
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

    [RelayCommand]
    private void ResetWindow() => Batch(() => LoadWindow(WindowAppearance.Default));

    [RelayCommand]
    private void ResetLegend() => Batch(() => LoadLegend(LegendAppearance.Default));

    [RelayCommand]
    private void ResetLegendLayout() => Batch(() => LoadLayout(LegendSettings.Default));

    partial void OnWindowBorderThicknessChanged(double value) => OnChanged();

    // The slider follows the switch: ticked it shows the Windows size, unticked it starts there (D31). One change.
    partial void OnUseWindowsFontSizeChanged(bool value) => Batch(() => LegendFontSize = _windowsFontSize);

    partial void OnLegendFontSizeChanged(double value) => OnChanged();

    partial void OnIsLegendVisibleChanged(bool value) => OnChanged();

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

        _appearance = _appearance with
        {
            Window = new(WindowBackground.Value, WindowBorderColor.Value, WindowBorderThickness),
            Legend = new(LegendForeground.Value, UseWindowsFontSize ? null : LegendFontSize, LegendHalo.Value),
        };
        _apply(_appearance, new LegendSettings(IsLegendVisible, LegendPosition));
    }
}
