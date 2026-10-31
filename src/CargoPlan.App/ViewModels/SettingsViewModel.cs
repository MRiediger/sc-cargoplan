using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
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

    /// <summary>The selection's contour (K4.4): its size is the width on each side of the edge.</summary>
    public const string SelectionOutlineLabel = "Contour around the selection edge";

    /// <summary>Under the switch "Download ship holograms" (D63): what goes out, and what the switch doesn't take away.</summary>
    public const string DownloadShipHologramsHint =
        "Downloads one file per ship from github.com when you pick a ship. Nothing else is sent. Already downloaded holograms stay.";

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
    private readonly Action<ShipOutlineMode> _changeShipOutline;
    private readonly Action<bool> _changeDownloadShipHolograms;
    private readonly Action<double> _changeHoloOpacity;
    private readonly Action<string, string> _changeHoloColors;
    private readonly Action<double> _changeMaxZoom;
    private readonly Action<string?> _setLookNotice;

    // True while fields are loaded (constructor, reset): their change calls are one change, applied once at the end.
    private bool _loading = true;

    // True while the preset box follows the look: setting its selection then applies nothing.
    private bool _matching;

    /// <param name="windowsFontSize">The Windows message font size (WPF's SystemFonts, read by the window): the legend's size while "Use Windows text size" is ticked.</param>
    /// <param name="apply">
    /// Gets the new look and legend layout after every change; the session merges them into the one AppSettings, so
    /// bounds moved while this window is open stay (D34).
    /// </param>
    /// <param name="changeToolWindowTheme">Gets a new tool window mode; apart from <paramref name="apply"/>, because it is no part of the overlay's look (D36).</param>
    /// <param name="changeShipOutline">Gets a new bow marker mode (D62); apart from <paramref name="apply"/>, it is a setting of its own in the file.</param>
    /// <param name="changeDownloadShipHolograms">Gets the switch "Download ship holograms" (D63), like <paramref name="changeShipOutline"/>.</param>
    /// <param name="changeHoloOpacity">Gets the hologram's opacity as a share, 0.01 to 1 (D63), like <paramref name="changeShipOutline"/>.</param>
    /// <param name="changeHoloColors">Gets the hologram's hull and edge colours after either changes (D63), like <paramref name="changeShipOutline"/>.</param>
    /// <param name="changeMaxZoom">Gets the ship view's max zoom in pixels per cell (D76), like <paramref name="changeShipOutline"/>.</param>
    /// <param name="setLookNotice">The overlay's line for presets, import and export (K4.3); null clears it.</param>
    public SettingsViewModel(
        AppearanceSettings appearance,
        LegendSettings legend,
        double windowsFontSize,
        Action<AppearanceSettings, LegendSettings> apply,
        ToolWindowTheme toolWindowTheme,
        Action<ToolWindowTheme> changeToolWindowTheme,
        ShipOutlineMode shipOutline,
        Action<ShipOutlineMode> changeShipOutline,
        bool downloadShipHolograms,
        Action<bool> changeDownloadShipHolograms,
        double holoOpacity,
        Action<double> changeHoloOpacity,
        string holoHullColor,
        string holoEdgeColor,
        Action<string, string> changeHoloColors,
        double maxZoom,
        Action<double> changeMaxZoom,
        Action<string?> setLookNotice)
    {
        _windowsFontSize = Math.Clamp(windowsFontSize, AppearanceRules.MinFontSize, AppearanceRules.MaxFontSize);
        _apply = apply;
        _changeToolWindowTheme = changeToolWindowTheme;
        _changeShipOutline = changeShipOutline;
        _changeDownloadShipHolograms = changeDownloadShipHolograms;
        _changeHoloOpacity = changeHoloOpacity;
        _changeHoloColors = changeHoloColors;
        _changeMaxZoom = changeMaxZoom;
        _setLookNotice = setLookNotice;
        ToolWindowTheme = toolWindowTheme;
        MaxZoom = maxZoom;
        ShipOutline = shipOutline;
        DownloadShipHolograms = downloadShipHolograms;
        HoloOpacityPercent = ToPercent(holoOpacity);
        HoloHullColour = new(holoHullColor, minAlpha: 0, OnHoloColoursChanged, hasAlpha: false);
        HoloEdgeColour = new(holoEdgeColor, minAlpha: 0, OnHoloColoursChanged, hasAlpha: false);

        WindowBackground = new(appearance.Window.Background, AppearanceRules.MinClickableAlpha, OnChanged);
        WindowBorderColor = new(appearance.Window.BorderColor, minAlpha: 0, OnChanged);
        LegendForeground = new(appearance.Legend.Foreground, minAlpha: 0, OnChanged);
        LegendHalo = new(appearance.Legend.Halo, LegendHaloLabel, OnChanged);
        LegendCheckBox = new(appearance.Legend.CheckBoxColor, minAlpha: 0, OnChanged);
        LegendCheckMark = new(appearance.Legend.CheckMarkColor, minAlpha: 0, OnChanged);
        CratesRackNumberColor = new(appearance.Crates.RackNumberColor, minAlpha: 0, OnChanged);
        CratesHalo = new(appearance.Crates.Halo, CratesHaloLabel, OnChanged);
        CratesSelectionColor = new(appearance.Crates.SelectionColor, minAlpha: 0, OnChanged);
        CratesSelectionOutline = new(appearance.Crates.SelectionOutline, SelectionOutlineLabel, OnChanged);
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
        RefreshPreset();
    }

    // Look presets, export and import (K4.3, D77): a row above the sections.

    private static readonly PresetChoice CustomChoice = new("Custom", null);

    private static readonly IReadOnlyList<PresetChoice> Presets = [.. LookPresets.All.Select(p => new PresetChoice(p.Name, p.Look)), CustomChoice];

    /// <summary>The presets, then "Custom", which the box shows only as its selection.</summary>
    public IReadOnlyList<PresetChoice> PresetChoices => Presets;

    /// <summary>
    /// The preset the look is, or "Custom"; follows every change. Choosing a preset applies it at once (D30), with a notice;
    /// choosing "Custom" changes nothing.
    /// </summary>
    public PresetChoice SelectedPreset
    {
        get;
        set
        {
            if (!SetProperty(ref field, value) || _matching || value.Look is not { } look)
            {
                return;
            }

            ApplyLook(look);
            _setLookNotice($"Preset applied: {value.Text}.");
        }
    } = CustomChoice;

    /// <summary>What the window shows as one look: the appearance, legend layout and the ship view's settings of its own.</summary>
    public Look Look => new(
        BuildAppearance(),
        BuildLayout(),
        ToolWindowThemes.ToName(ToolWindowTheme),
        ShipOutlineModes.ToName(ShipOutline),
        HoloOpacityPercent / 100.0,
        HoloHullColour.Value,
        HoloEdgeColour.Value,
        MaxZoom);

    /// <summary>
    /// Reads a look file and applies it like a preset (D30, no undo: the reset per section is the way back, settings.json.bak
    /// keeps the file before the first save of the run, D47). A file that can't be read changes nothing and says why.
    /// </summary>
    public void ImportLook(string path)
    {
        var name = Path.GetFileName(path);
        try
        {
            Look look;
            IReadOnlyList<string> problems;
            using (var stream = File.OpenRead(path))
            {
                (look, problems) = LookFile.Read(stream, Look);
            }

            ApplyLook(look);
            _setLookNotice(string.Join(Environment.NewLine, [$"Look imported: {name}", .. problems.Select(p => $"{name}: {p}")]));
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            _setLookNotice($"Look not imported: {ex.Message}");
        }
    }

    /// <summary>Writes the look as the window shows it, every key, so the file read anywhere gives this look.</summary>
    public void ExportLook(string path)
    {
        try
        {
            var look = Look;
            AtomicFile.Write(path, stream => LookFile.Write(stream, look));
            _setLookNotice($"Look exported: {Path.GetFileName(path)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _setLookNotice($"Look not exported: {ex.Message}");
        }
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

    /// <summary>The check boxes' frame (K4.1, D69); alpha 0 is allowed, the box keeps its own hit-test background.</summary>
    public ColourField LegendCheckBox { get; }

    /// <summary>The tick (all of a line's crates selected) and the bar (part of them) inside the box.</summary>
    public ColourField LegendCheckMark { get; }

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

    /// <summary>The slider's bounds in percent, like the file's 0.2 to 0.8; double for x:Static, see <see cref="MinHoloOpacityPercent"/>.</summary>
    public static double MinLegendMaxHeightSharePercent => ToPercent(AppearanceRules.MinLegendMaxHeightShare);

    public static double MaxLegendMaxHeightSharePercent => ToPercent(AppearanceRules.MaxLegendMaxHeightShare);

    /// <summary>The stop lines' share of the overlay's height in whole percent (K7); the file and the overlay get the share.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LegendMaxHeightShareText))]
    public partial int LegendMaxHeightSharePercent { get; set; }

    public string LegendMaxHeightShareText => $"{LegendMaxHeightSharePercent} %";

    // Crates: their colours are the stop colours of a checked palette (D29, K4.2), so the palette, fill and edge alpha
    // (0–255 like the file).

    /// <summary>The palettes with their colour count, from the ship view's own list so text and colours never differ.</summary>
    public static IReadOnlyList<StopPaletteChoice> StopPaletteChoices { get; } = [.. ShipView.StopPalettes.All.Select(p => new StopPaletteChoice(p.Name, p.Text))];

    [ObservableProperty]
    public partial StopPaletteName StopPalette { get; set; }

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

    /// <summary>The selected crates' edge (D25, K4.4).</summary>
    public ColourField CratesSelectionColor { get; }

    [ObservableProperty]
    public partial double CratesSelectionWidth { get; set; }

    /// <summary>The slider's bounds, the file's range; double for x:Static like the max zoom's.</summary>
    public static double MinSelectionWidth => AppearanceRules.MinSelectionWidth;

    public static double MaxSelectionWidth => AppearanceRules.MaxSelectionWidth;

    /// <summary>The dark contour under the selection edge (K4.2, K4.4).</summary>
    public HaloEditor CratesSelectionOutline { get; }

    // Header

    /// <summary>Also the glyphs: split, merge, re-plan, pencil, undo, redo, reset view, gear, exit, padlock. Invisible, the gear is found by its tool tip (D34).</summary>
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

    // Ship view (D62, D63): what lies behind the racks and whether holograms may be downloaded. Part of what the overlay
    // shows, so it has a reset like the look's sections.

    public static IReadOnlyList<ShipOutlineChoice> ShipOutlineChoices { get; } =
    [
        new(Settings.ShipOutlineMode.Outline, "Outline"),
        new(Settings.ShipOutlineMode.HoloShaded, "Hologram – shaded"),
        new(Settings.ShipOutlineMode.HoloFlat, "Hologram – flat"),
        new(Settings.ShipOutlineMode.HoloHiddenline, "Hologram – hidden line"),
        new(Settings.ShipOutlineMode.None, "None"),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHologramLook))]
    [NotifyPropertyChangedFor(nameof(ColoursHull))]
    [NotifyPropertyChangedFor(nameof(ColoursEdges))]
    public partial ShipOutlineMode ShipOutline { get; set; }

    /// <summary>The hull colour applies to the shaded and flat looks; its row is on only for them.</summary>
    public bool ColoursHull => ShipOutline is Settings.ShipOutlineMode.HoloShaded or Settings.ShipOutlineMode.HoloFlat;

    /// <summary>The edge colour applies to the hidden-line look; the hull there stays dark, it only hides.</summary>
    public bool ColoursEdges => ShipOutline is Settings.ShipOutlineMode.HoloHiddenline;

    /// <summary>Opaque, without an alpha slider: the opacity is the slider above (D63).</summary>
    public ColourField HoloHullColour { get; }

    public ColourField HoloEdgeColour { get; }

    /// <summary>The opacity slider is on only for a hologram look; the outline keeps its own line.</summary>
    public bool IsHologramLook => ShipOutline.IsHologram();

    /// <summary>
    /// The slider's bounds in percent, like the file's 0.01 to 1. double, not int: x:Static hands the value over as it is,
    /// and an int on Slider.Minimum throws when the window opens.
    /// </summary>
    public static double MinHoloOpacityPercent => ToPercent(AppearanceRules.MinHoloOpacity);

    public static double MaxHoloOpacityPercent => ToPercent(AppearanceRules.MaxHoloOpacity);

    /// <summary>The hologram's opacity in whole percent, as the slider moves; the file and the overlay get the share.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HoloOpacityText))]
    public partial int HoloOpacityPercent { get; set; }

    public string HoloOpacityText => $"{HoloOpacityPercent} %";

    /// <summary>Off = no network access at all; holograms already downloaded still show (Martin, T7.4c-2).</summary>
    [ObservableProperty]
    public partial bool DownloadShipHolograms { get; set; }

    /// <summary>The slider's bounds, the file's range; double for x:Static like the opacity's.</summary>
    public static double MinMaxZoom => AppearanceRules.MinMaxZoom;

    public static double MaxMaxZoom => AppearanceRules.MaxMaxZoom;

    /// <summary>How far the wheel zooms in, in pixels per cell (D76); a setting of its own in the file, not part of the appearance (but of a Look, K4.3).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MaxZoomText))]
    public partial double MaxZoom { get; set; }

    public string MaxZoomText => string.Create(CultureInfo.InvariantCulture, $"{MaxZoom:0} px");

    // Settings of their own, each with its own callback: the reset changes each only if it differs.
    [RelayCommand]
    private void ResetShipView()
    {
        ShipOutline = Settings.ShipOutlineMode.Outline;
        HoloOpacityPercent = ToPercent(AppearanceRules.DefaultHoloOpacity);
        MaxZoom = AppearanceRules.DefaultMaxZoom;
        Batch(() =>
        {
            HoloHullColour.Load(AppearanceRules.DefaultHoloHullColor);
            HoloEdgeColour.Load(AppearanceRules.DefaultHoloEdgeColor);
        }, OnHoloColoursChanged);
    }

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

    partial void OnLegendMaxHeightSharePercentChanged(int value) => OnChanged();

    partial void OnStopPaletteChanged(StopPaletteName value) => OnChanged();

    partial void OnCratesFillAlphaChanged(int value) => OnChanged();

    partial void OnCratesEdgeAlphaChanged(int value) => OnChanged();

    partial void OnCratesRackNumberFontSizeChanged(double value) => OnChanged();

    partial void OnCratesSelectionWidthChanged(double value) => OnChanged();

    partial void OnButtonsBorderThicknessChanged(double value) => OnChanged();

    partial void OnButtonsCornerRadiusChanged(double value) => OnChanged();

    // Not OnChanged: the mode is no part of the overlay's appearance (it is of a Look file, K4.3). The constructor sets it
    // while loading, which calls nothing.
    partial void OnToolWindowThemeChanged(ToolWindowTheme value)
    {
        if (!_loading)
        {
            _changeToolWindowTheme(value);
            Edited();
        }
    }

    // Like the tool window mode: a setting of its own, not part of the appearance record (but of the Look, K4.3).
    partial void OnShipOutlineChanged(ShipOutlineMode value)
    {
        if (!_loading)
        {
            _changeShipOutline(value);
            Edited();
        }
    }

    // Like the ship outline mode: a setting of its own.
    partial void OnDownloadShipHologramsChanged(bool value)
    {
        if (!_loading)
        {
            _changeDownloadShipHolograms(value);
        }
    }

    // Like the ship outline mode: a setting of its own.
    partial void OnHoloOpacityPercentChanged(int value)
    {
        if (!_loading)
        {
            _changeHoloOpacity(value / 100.0);
            Edited();
        }
    }

    // Like the ship outline mode: a setting of its own.
    partial void OnMaxZoomChanged(double value)
    {
        if (!_loading)
        {
            _changeMaxZoom(value);
            Edited();
        }
    }

    private static int ToPercent(double share) => (int)Math.Round(share * 100);

    // Like the ship outline mode: settings of their own, both colours in one call.
    private void OnHoloColoursChanged()
    {
        if (!_loading)
        {
            _changeHoloColors(HoloHullColour.Value, HoloEdgeColour.Value);
            Edited();
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
        LegendCheckBox.Load(legend.CheckBoxColor);
        LegendCheckMark.Load(legend.CheckMarkColor);
    }

    private void LoadLayout(LegendSettings legend)
    {
        IsLegendVisible = legend.Visible;
        LegendPosition = legend.Position;
        LegendMaxHeightSharePercent = ToPercent(legend.MaxHeightShare);
    }

    private void LoadCrates(CrateAppearance crates)
    {
        StopPalette = StopPaletteNames.Parse(crates.StopPalette);
        CratesFillAlpha = crates.FillAlpha;
        CratesEdgeAlpha = crates.EdgeAlpha;
        CratesRackNumberColor.Load(crates.RackNumberColor);
        CratesRackNumberFontSize = crates.RackNumberFontSize;
        CratesHalo.Load(crates.Halo);
        CratesSelectionColor.Load(crates.SelectionColor);
        CratesSelectionWidth = crates.SelectionWidth;
        CratesSelectionOutline.Load(crates.SelectionOutline);
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
    /// <param name="apply">What applies the change; the look's <see cref="OnChanged"/> unless a setting of its own.</param>
    private void Batch(Action load, Action? apply = null)
    {
        var wasLoading = _loading;
        _loading = true;
        load();
        _loading = wasLoading;
        (apply ?? OnChanged)();
    }

    private void OnChanged()
    {
        if (_loading)
        {
            return;
        }

        _apply(BuildAppearance(), BuildLayout());
        Edited();
    }

    private LegendSettings BuildLayout() => new(IsLegendVisible, LegendPosition, LegendMaxHeightSharePercent / 100.0);

    private AppearanceSettings BuildAppearance() => new(
        new WindowAppearance(WindowBackground.Value, WindowBorderColor.Value, WindowBorderThickness),
        new HeaderAppearance(HeaderForeground.Value, HeaderHalo.Value),
        new LegendAppearance(
            LegendForeground.Value, UseWindowsFontSize ? null : LegendFontSize, LegendHalo.Value, LegendCheckBox.Value, LegendCheckMark.Value),
        new CrateAppearance(
            CratesFillAlpha, CratesEdgeAlpha, CratesRackNumberColor.Value, CratesRackNumberFontSize, CratesHalo.Value, StopPaletteNames.ToName(StopPalette),
            CratesSelectionColor.Value, CratesSelectionWidth, CratesSelectionOutline.Value),
        new ButtonAppearance(ButtonsBackground.Value, ButtonsBorderColor.Value, ButtonsBorderThickness, ButtonsCornerRadius, ButtonsHalo.Value));

    /// <summary>After any change of the look from the window: the last preset or import notice is out of date, the box follows.</summary>
    private void Edited()
    {
        _setLookNotice(null);
        RefreshPreset();
    }

    /// <summary>Selects the preset the look is, or "Custom", without applying it.</summary>
    private void RefreshPreset()
    {
        var look = Look;
        _matching = true;
        SelectedPreset = Presets.FirstOrDefault(c => c.Look == look) ?? CustomChoice;
        _matching = false;
    }

    /// <summary>
    /// A whole look as one change (K4.3): the fields loaded at once, then the look applied once and each setting of its own
    /// called only if it changed, so a preset that keeps the theme doesn't switch the tool windows. The download switch is
    /// no part of a look (D63) and stays.
    /// </summary>
    private void ApplyLook(Look look)
    {
        var before = Look;
        Batch(
            () =>
            {
                LoadWindow(look.Appearance.Window);
                LoadLegend(look.Appearance.Legend);
                LoadLayout(look.Legend);
                LoadCrates(look.Appearance.Crates);
                LoadHeader(look.Appearance.Header);
                LoadButtons(look.Appearance.Buttons);
                ToolWindowTheme = ToolWindowThemes.Parse(look.ToolWindowTheme);
                ShipOutline = ShipOutlineModes.Parse(look.ShipOutline);
                HoloOpacityPercent = ToPercent(look.HoloOpacity);
                HoloHullColour.Load(look.HoloHullColor);
                HoloEdgeColour.Load(look.HoloEdgeColor);
                MaxZoom = look.MaxZoomPxPerCell;
            },
            () =>
            {
                var after = Look;
                if (after.Appearance != before.Appearance || after.Legend != before.Legend)
                {
                    _apply(after.Appearance, after.Legend);
                }

                if (after.ToolWindowTheme != before.ToolWindowTheme)
                {
                    _changeToolWindowTheme(ToolWindowTheme);
                }

                if (after.ShipOutline != before.ShipOutline)
                {
                    _changeShipOutline(ShipOutline);
                }

                if (after.HoloOpacity != before.HoloOpacity)
                {
                    _changeHoloOpacity(after.HoloOpacity);
                }

                if (after.HoloHullColor != before.HoloHullColor || after.HoloEdgeColor != before.HoloEdgeColor)
                {
                    _changeHoloColors(after.HoloHullColor, after.HoloEdgeColor);
                }

                if (after.MaxZoomPxPerCell != before.MaxZoomPxPerCell)
                {
                    _changeMaxZoom(after.MaxZoomPxPerCell);
                }

                RefreshPreset();
            });
    }
}
