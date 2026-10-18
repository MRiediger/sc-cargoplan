using System.Diagnostics;
using System.Windows.Threading;
using CargoPlan.App.ViewModels;
using CargoPlan.App.Win32;

namespace CargoPlan.App;

/// <summary>
/// Polls the foreground process and the cursor state and sets <see cref="OverlayViewModel.IsAutoLocked"/>;
/// the decision itself is <see cref="AutoLockRule"/>. The DispatcherTimer ticks on the UI thread, so the
/// view model is only touched there. No thread, no hook: Windows has no event for the cursor state,
/// so polling is needed anyway (D11, addendum T3.3).
/// </summary>
public sealed class CursorStateMonitor
{
    /// <summary>Well below the time it takes to release a crate after the cursor hides.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(150);

    private readonly OverlayViewModel _viewModel;
    private readonly DispatcherTimer _timer = new() { Interval = Interval };

    // GetForegroundWindow is cheap, resolving the process name is not: only on a window change.
    private nint _lastWindow;
    private string? _lastProcessName;

    public CursorStateMonitor(OverlayViewModel viewModel)
    {
        _viewModel = viewModel;
        _timer.Tick += OnTick;
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window != _lastWindow)
        {
            _lastWindow = window;
            _lastProcessName = ProcessNameOf(window);
        }

        // Unchanged values raise no PropertyChanged, so setting it every tick is free.
        _viewModel.IsAutoLocked = AutoLockRule.ShouldLock(_lastProcessName, NativeMethods.IsCursorShowing());
    }

    /// <summary>null if there is no window or its process is gone by now: that is "not the game", not an error.</summary>
    private static string? ProcessNameOf(nint window)
    {
        if (NativeMethods.GetWindowProcessId(window) is not { } processId)
        {
            return null;
        }

        // The process can exit between GetWindowProcessId and here.
        // Sources: https://learn.microsoft.com/dotnet/api/system.diagnostics.process.getprocessbyid (ArgumentException)
        //          https://learn.microsoft.com/dotnet/api/system.diagnostics.process.processname (InvalidOperationException)
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
