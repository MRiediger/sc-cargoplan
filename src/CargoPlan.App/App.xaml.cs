using System.Windows;
using CargoPlan.App.Hotkeys;
using CargoPlan.App.Settings;
using CargoPlan.App.ViewModels;

namespace CargoPlan.App;

/// <summary>
/// Composition root. Two windows, one view model: wired by hand, a DI container would add a package
/// and indirection for nothing.
/// </summary>
public partial class App : Application
{
    /// <summary>Starts with the manual lock on (click-through); the lock window unlocks it.</summary>
    public const string LockedSwitch = "--locked";

    private CursorStateMonitor? _cursorMonitor;

    // Kept so the composition root visibly owns it; the window's events would keep it alive anyway.
    private OverlayBoundsSaver? _boundsSaver;

    // Same here: the overlay's events keep it alive, the field shows who owns it.
    private HotkeyService? _hotkeys;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var (settings, settingsProblem) = SettingsStore.Load(SettingsStore.DefaultPath);

        var (ship, plan) = SamplePlan.Load();
        var viewModel = new OverlayViewModel(ship)
        {
            Plan = plan,
            IsManuallyLocked = e.Args.Contains(LockedSwitch, StringComparer.OrdinalIgnoreCase),
            Notice = settingsProblem,
        };

        // Bounds before Show(), so the window appears in place instead of jumping there.
        var overlay = new OverlayWindow(viewModel);
        if (settings.Overlay is { } saved)
        {
            RestoreBounds(overlay, saved);
        }

        _boundsSaver = new OverlayBoundsSaver(overlay, viewModel, SettingsStore.DefaultPath, settings);

        // Before Show(): SourceInitialized, where the hotkeys get registered, is raised inside Show().
        _hotkeys = new HotkeyService(overlay, settings.Hotkeys, viewModel);

        // Overlay first: the lock window's Owner must be a window that has been shown.
        overlay.Show();
        new LockWindow(viewModel, overlay).Show();

        _cursorMonitor = new CursorStateMonitor(viewModel);
        _cursorMonitor.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _cursorMonitor?.Stop();
        base.OnExit(e);
    }

    /// <summary>Saved bounds may lie on an unplugged monitor: clamped into today's virtual desktop.</summary>
    private static void RestoreBounds(Window window, WindowBounds saved)
    {
        Rect screen = new(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);

        var bounds = WindowBoundsRestore.Clamp(saved.ToRect(), screen, new Size(window.MinWidth, window.MinHeight));
        window.Left = bounds.Left;
        window.Top = bounds.Top;
        window.Width = bounds.Width;
        window.Height = bounds.Height;
    }
}
