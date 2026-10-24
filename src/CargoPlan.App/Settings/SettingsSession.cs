using System.IO;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Theme;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Settings;

/// <summary>
/// The one owner of the current <see cref="AppSettings"/> while the app runs (D34). Startup and the settings window
/// apply through the same method, and window bounds and look are saved from the same record, so neither overwrites the
/// other with an old copy.
/// </summary>
public sealed class SettingsSession
{
    /// <summary>Same rest as window bounds and plan (D13, D30): a slider drag writes once, not per step.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    private readonly ResourceDictionary _resources;
    private readonly OverlayViewModel _viewModel;
    private readonly string _path;
    private readonly DispatcherTimer _timer = new() { Interval = Debounce };

    // What the last look change had corrected: the notice stays after saving, so a wrong slider limit can't hide (D34).
    private string? _corrections;

    // Set after the first backup succeeded, not before: a failed copy writes nothing and the next save tries again (D47).
    private bool _backedUp;

    /// <summary>Applies <paramref name="settings"/> at once, before any window exists; writes nothing (D31: the file stays as the player left it until a change).</summary>
    /// <param name="settings">As loaded, checked by <see cref="AppearanceRules"/>.</param>
    public SettingsSession(ResourceDictionary resources, OverlayViewModel viewModel, string path, AppSettings settings)
    {
        _resources = resources;
        _viewModel = viewModel;
        _path = path;
        _timer.Tick += OnTick;
        Current = settings;
        ApplyLook();
        _viewModel.OutlineMode = ShipOutline;
    }

    public AppSettings Current { get; private set; }

    /// <summary>
    /// From the settings window: the same check as loading (<see cref="AppearanceRules.Normalize(AppearanceSettings)"/>),
    /// so what the overlay shows now is what it shows after a restart; applied at once, saved after <see cref="Debounce"/>.
    /// </summary>
    public void ChangeLook(AppearanceSettings appearance, LegendSettings legend)
    {
        var (checkedAppearance, appearanceProblems) = AppearanceRules.Normalize(appearance);
        var (checkedLegend, legendProblems) = AppearanceRules.Normalize(legend);
        _corrections = SettingsStore.Corrections([.. legendProblems, .. appearanceProblems]);
        if (_corrections is not null)
        {
            _viewModel.SetNotice(NoticeSource.Settings, _corrections);
        }

        Current = Current with { Appearance = checkedAppearance, Legend = checkedLegend };
        ApplyLook();
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// From the settings window, after the hotkey was registered (D35): saved after <see cref="Debounce"/> as canonical
    /// text. null clears the action and is written as null, so the key stays visible in the file; missing reads the same.
    /// </summary>
    public void ChangeHotkey(HotkeyAction action, Hotkey? hotkey)
    {
        var hotkeys = Current.Hotkeys ?? new HotkeySettings();
        var text = hotkey?.ToString();
        Current = Current with
        {
            Hotkeys = action switch
            {
                HotkeyAction.ToggleLock => hotkeys with { ToggleLock = text },
                HotkeyAction.ToggleOverlay => hotkeys with { ToggleOverlay = text },
                HotkeyAction.SyncContractTracker => hotkeys with { SyncContractTracker = text },
                HotkeyAction.Replan => hotkeys with { Replan = text },
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null),
            },
        };
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// From the settings window: saved after <see cref="Debounce"/> like a hotkey. Applying it is the sync's job (D44), wired
    /// in the composition root like the hotkeys. Not checked again: the window only hands over full paths from the file
    /// dialog, or null (A6: checks at the file boundary).
    /// </summary>
    public void ChangeContractTracker(ContractTrackerSettings contractTracker)
    {
        Current = Current with { ContractTracker = contractTracker };
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>
    /// From a ship change in the overlay header (T7.2b): saved after <see cref="Debounce"/> like a hotkey. Applying it is the
    /// overlay's job (it replans, D59); the start's fallback ship never comes here, so the file keeps the choice.
    /// The recent ships come along: the overlay keeps them (T7.2c).
    /// </summary>
    public void ChangeShip(string shipId, IReadOnlyList<string> recentShips)
    {
        if (shipId == Current.Ship)
        {
            return;
        }

        Current = Current with { Ship = shipId, RecentShips = recentShips };
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>The tool windows' mode (D36); the overlay switches the open ones, new ones read it here.</summary>
    public ToolWindowTheme ToolWindowTheme => ToolWindowThemes.Parse(Current.ToolWindowTheme);

    /// <summary>
    /// From the settings window: saved after <see cref="Debounce"/> like a hotkey. Applying it is the overlay's job: it owns
    /// the tool windows, this session only the overlay's look.
    /// </summary>
    public void ChangeToolWindowTheme(ToolWindowTheme theme)
    {
        Current = Current with { ToolWindowTheme = ToolWindowThemes.ToName(theme) };
        _timer.Stop();
        _timer.Start();
    }

    /// <summary>What lies behind the racks in the ship view (D62, D63).</summary>
    public ShipOutlineMode ShipOutline => ShipOutlineModes.Parse(Current.ShipOutline);

    /// <summary>The switch "Download ship holograms" (D63).</summary>
    public bool DownloadShipHolograms => Current.DownloadShipHolograms;

    /// <summary>
    /// Look or download switch changed: the composition root asks the hologram loader again. An event, not a call into
    /// the loader: the session owns settings and the overlay's look, the loader belongs to the app.
    /// </summary>
    public event EventHandler? ShipViewChanged;

    /// <summary>From the settings window: the overlay redraws at once (D30), saved after <see cref="Debounce"/>.</summary>
    public void ChangeShipOutline(ShipOutlineMode mode)
    {
        _viewModel.OutlineMode = mode;
        Current = Current with { ShipOutline = ShipOutlineModes.ToName(mode) };
        _timer.Stop();
        _timer.Start();
        ShipViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>From the settings window (D63): saved after <see cref="Debounce"/>; the loader follows via <see cref="ShipViewChanged"/>.</summary>
    public void ChangeDownloadShipHolograms(bool download)
    {
        if (download == Current.DownloadShipHolograms)
        {
            return;
        }

        Current = Current with { DownloadShipHolograms = download };
        _timer.Stop();
        _timer.Start();
        ShipViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// From <see cref="OverlayBoundsSaver"/>, which has debounced already: saved at once, a pending look change with it.
    /// </summary>
    public void ChangeBounds(WindowBounds bounds)
    {
        if (bounds == Current.Overlay)
        {
            return;
        }

        Current = Current with { Overlay = bounds };
        Save();
    }

    /// <summary>Writes only what is still pending: closing without a change leaves the file alone.</summary>
    public void Flush()
    {
        if (_timer.IsEnabled)
        {
            Save();
        }
    }

    /// <summary>The one way from settings to the overlay, at startup and on every change: theme resources and legend layout.</summary>
    private void ApplyLook()
    {
        ThemeResources.Apply(_resources, Current.Appearance);
        _viewModel.IsLegendVisible = Current.Legend.Visible;
        _viewModel.LegendPosition = LegendPosition.Parse(Current.Legend.Position);
    }

    private void OnTick(object? sender, EventArgs e) => Save();

    private void Save()
    {
        _timer.Stop();
        try
        {
            if (!_backedUp)
            {
                SettingsStore.BackUp(_path);
                _backedUp = true;
            }

            SettingsStore.Save(_path, Current);

            // A load or save problem is solved once the file is written; a correction of the last change is not.
            _viewModel.SetNotice(NoticeSource.Settings, _corrections);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Saves only happen on changes, and an unchanged text raises no PropertyChanged: no spam.
            _viewModel.SetNotice(NoticeSource.Settings, $"Settings not saved ({ex.Message}).");
        }
    }
}
