using System.IO;
using System.Windows;
using System.Windows.Threading;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Settings;

/// <summary>
/// Saves the overlay's bounds whenever they change, debounced, not only on exit: a Star Citizen crash often
/// takes the whole PC down, and the position would be lost every time (D13).
/// </summary>
public sealed class OverlayBoundsSaver
{
    /// <summary>Saves once the window has rested this long; while dragging or resizing, events keep restarting it.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    private readonly Window _window;
    private readonly OverlayViewModel _viewModel;
    private readonly string _path;
    private readonly DispatcherTimer _timer = new() { Interval = Debounce };

    private AppSettings _settings;
    private WindowBounds? _lastSaved;

    /// <param name="settings">As loaded; other settings are written back unchanged.</param>
    public OverlayBoundsSaver(Window window, OverlayViewModel viewModel, string path, AppSettings settings)
    {
        _window = window;
        _viewModel = viewModel;
        _path = path;
        _settings = settings;
        _timer.Tick += OnTick;

        // Listens only from the first render on: positioning and first layout at startup are no change by the
        // user, and must not overwrite a broken file with defaults.
        // Source: https://learn.microsoft.com/dotnet/api/system.windows.window.contentrendered
        window.ContentRendered += OnContentRendered;
        window.Closed += OnClosed;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        _window.ContentRendered -= OnContentRendered;
        _lastSaved = Current();
        _window.LocationChanged += OnBoundsChanged;
        _window.SizeChanged += OnBoundsChanged;
    }

    private void OnBoundsChanged(object? sender, EventArgs e)
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _timer.Stop();
        Save();
    }

    /// <summary>Writes only what is still pending: closing without a change leaves the file alone.</summary>
    private void OnClosed(object? sender, EventArgs e)
    {
        _window.ContentRendered -= OnContentRendered;
        _window.LocationChanged -= OnBoundsChanged;
        _window.SizeChanged -= OnBoundsChanged;
        _window.Closed -= OnClosed;

        if (_timer.IsEnabled)
        {
            _timer.Stop();
            Save();
        }
    }

    private void Save()
    {
        // Minimized, Windows reports Left/Top = -32000; maximized is not the size the player chose.
        if (_window.WindowState != WindowState.Normal)
        {
            return;
        }

        var bounds = Current();
        if (bounds == _lastSaved)
        {
            return;
        }

        var settings = _settings with { Overlay = bounds };
        try
        {
            SettingsStore.Save(_path, settings);
            _settings = settings;
            _lastSaved = bounds;
            _viewModel.SetNotice(NoticeSource.Settings, null); // A load or save problem is solved once the file is written.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Saves only happen on changes, and an unchanged text raises no PropertyChanged: no spam.
            _viewModel.SetNotice(NoticeSource.Settings, $"Overlay position not saved ({ex.Message}).");
        }
    }

    // ActualWidth/Height instead of Width/Height: the rendered size, as the lock window uses it too.
    private WindowBounds Current() => new(_window.Left, _window.Top, _window.ActualWidth, _window.ActualHeight);
}
