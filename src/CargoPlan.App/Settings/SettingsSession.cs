using System.IO;
using System.Windows;
using System.Windows.Threading;
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
