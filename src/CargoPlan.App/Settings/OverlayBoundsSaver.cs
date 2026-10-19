using System.Windows;
using System.Windows.Threading;

namespace CargoPlan.App.Settings;

/// <summary>
/// Saves the overlay's bounds whenever they change, debounced, not only on exit: a Star Citizen crash often
/// takes the whole PC down, and the position would be lost every time (D13). Writes through the
/// <see cref="SettingsSession"/>, which holds the rest of the settings (D34).
/// </summary>
public sealed class OverlayBoundsSaver
{
    /// <summary>Saves once the window has rested this long; while dragging or resizing, events keep restarting it.</summary>
    public static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(500);

    private readonly Window _window;
    private readonly SettingsSession _session;
    private readonly DispatcherTimer _timer = new() { Interval = Debounce };

    public OverlayBoundsSaver(Window window, SettingsSession session)
    {
        _window = window;
        _session = session;
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

    /// <summary>Hands over only what is still pending: closing without a change leaves the file alone.</summary>
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

        // Unchanged bounds write nothing; the session compares with what it holds.
        _session.ChangeBounds(Current());
    }

    // ActualWidth/Height instead of Width/Height: the rendered size, as the lock window uses it too.
    private WindowBounds Current() => new(_window.Left, _window.Top, _window.ActualWidth, _window.ActualHeight);
}
