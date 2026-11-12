using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Scanning;

/// <summary>
/// Starts a scan for the scan hotkey and the editor's button (T10.3, D104): <see cref="ScanIntake"/> on the pool, every
/// change of the overlay through <c>ui</c> on the UI thread. Never throws: a failure goes to cargoplan.log and the notice
/// line, the plan and the app stay (D82). A second start while one runs is no queue: the
/// scanner answers Busy, the notice says "Scan running".
/// No interface: one implementation; the delegates are there for the tests (A4).
/// </summary>
/// <param name="scan">The scanner, <see cref="ContractScanner.ScanAsync"/> in the app.</param>
/// <param name="ui">Runs an action on the UI thread: <c>Dispatcher.InvokeAsync(action).Task</c> in the app.</param>
/// <param name="delay">The wait before a second read; <see cref="Task.Delay(TimeSpan)"/> in the app.</param>
/// <param name="pictures">With <c>--keep-scans</c>: keeps each run's picture (T10.5); null = none.</param>
internal sealed class ScanRunner(
    Func<Task<ScanAttempt>> scan,
    OverlayViewModel overlay,
    ScanLog log,
    Func<Action, Task> ui,
    Func<TimeSpan, Task>? delay = null,
    ScanPictures? pictures = null)
{
    private readonly ScanIntake _intake = new(
        scan,
        async contract =>
        {
            ScanReport? report = null;
            await ui(() => report = overlay.ApplyScan(contract)).ConfigureAwait(false);
            return report!; // The UI action ran when ui's task completed.
        },
        report => ui(() => overlay.ShowScanReport(report)),
        log,
        delay ?? (time => Task.Delay(time)),
        pictures);

    // Scans started and not yet ended; the editor's button is off while there is one.
    private int _running;

    /// <summary>For the hotkey and the button: fire and forget, <see cref="RunAsync"/> catches everything.</summary>
    public void Start() => _ = RunAsync();

    /// <summary>One scan with the safety net; the task never fails.</summary>
    public async Task RunAsync()
    {
        Interlocked.Increment(ref _running);
        try
        {
            await ShowRunning().ConfigureAwait(false);
            await _intake.RunAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Write("Scan", ex);
            await Guarded(() => ui(() => overlay.SetNotice(NoticeSource.Move, ScanNotices.Failed(ex)))).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.Decrement(ref _running);
            await Guarded(ShowRunning).ConfigureAwait(false);
        }
    }

    // The count read on the UI thread when it runs, not when it was queued: the last update always shows the last count,
    // in whatever order the threads got there.
    private Task ShowRunning() => ui(() => overlay.IsScanning = Volatile.Read(ref _running) > 0);

    /// <summary>The UI may be gone (the overlay closing): logged, never thrown out of a fire-and-forget task.</summary>
    private static async Task Guarded(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            AppLog.Write("Scan notice", ex);
        }
    }
}
