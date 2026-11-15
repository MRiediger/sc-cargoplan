using CargoPlan.App.Updates;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.ViewModels;

/// <summary>Where the update line stands (T11.2, D109).</summary>
public abstract record UpdateState
{
    /// <summary>No line.</summary>
    public static UpdateState None { get; } = new UpdateNone();
}

/// <summary>No update to show.</summary>
public sealed record UpdateNone : UpdateState;

/// <summary>A newer release the player hasn't skipped: Update and Skip.</summary>
public sealed record UpdateOffered(string Version) : UpdateState;

/// <summary>The package comes down; no controls (U4).</summary>
public sealed record UpdateDownloading(string Version, int Percent) : UpdateState;

/// <summary>Update.exe has started and the app shuts down.</summary>
public sealed record UpdateApplying(string Version) : UpdateState;

/// <summary>Download or swap failed (U6): the reason until the next start, and Retry.</summary>
/// <param name="Reason">One of <see cref="UpdateFailure"/>'s texts.</param>
public sealed record UpdateFailed(string Version, string Reason) : UpdateState;

/// <summary>What the line does, outside the view model: the service and the session (App wires them).</summary>
/// <param name="Download">Version, progress (any thread) → null when the package lies there, else the reason.</param>
/// <param name="Apply">Version → null when the app now shuts down for the swap, else the reason.</param>
/// <param name="Skip"><c>updates.skippedVersion</c> through the settings session (U3).</param>
public sealed record UpdateActions(
    Func<string, Action<int>, Task<string?>> Download,
    Func<string, Task<string?>> Apply,
    Action<string> Skip);

/// <summary>
/// The update line at the bottom of the overlay (T11.2, U1–U6, U10): <c>Update available · v0.9.1 [Update] ☐ Skip this
/// version</c>, then the download in percent, then the restart, or why it failed with Retry. Its own class, not part of the
/// overlay's view model: a state machine with its own dependencies; the overlay hands in the lock.
/// </summary>
/// <param name="ui">Runs an action on the UI thread: the download's progress comes from the pool.</param>
/// <param name="time">The throttle's clock; <see cref="TimeProvider.System"/> outside tests.</param>
public sealed partial class UpdateViewModel(UpdateActions actions, Action<Action> ui, TimeProvider? time = null) : ObservableObject
{
    /// <summary>U10: ten updates a second at most; Velopack reports up to 50 even percents.</summary>
    public static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private readonly TimeProvider _time = time ?? TimeProvider.System;

    private readonly Lock _progressGate = new();

    private long? _lastProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsVisible), nameof(Text), nameof(ShowsUpdate), nameof(ShowsRetry), nameof(ShowsSkip))]
    [NotifyCanExecuteChangedFor(nameof(UpdateCommand), nameof(RetryCommand), nameof(SkipCommand))]
    public partial UpdateState State { get; private set; } = UpdateState.None;

    /// <summary>The overlay is click-through (U2): the text stays, the controls go; set by <see cref="OverlayViewModel"/>.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreControlsShown))]
    public partial bool IsLocked { get; set; }

    public bool IsVisible => State is not UpdateNone;

    public bool AreControlsShown => !IsLocked;

    public bool ShowsUpdate => State is UpdateOffered;

    public bool ShowsSkip => State is UpdateOffered;

    public bool ShowsRetry => State is UpdateFailed;

    public string Text => State switch
    {
        UpdateOffered offered => $"Update available · v{offered.Version}",
        UpdateDownloading downloading => $"Downloading update · {downloading.Percent} %",
        UpdateApplying => "Installing update · restarting",
        UpdateFailed failed => $"Update failed · {failed.Reason}",
        _ => "",
    };

    /// <summary>
    /// A check found <paramref name="version"/>. It never interrupts a download or a swap, and it replaces a failure only
    /// with a newer release (E3): Retry would otherwise put the older one in.
    /// </summary>
    public void Offer(string version)
    {
        var replaces = State switch
        {
            UpdateNone or UpdateOffered => true,
            UpdateFailed failed => IsNewer(version, failed.Version),
            _ => false,
        };
        if (replaces)
        {
            State = new UpdateOffered(version);
        }
    }

    /// <summary>Update.exe started the old app again (D109): the line stays until the next start, with Retry.</summary>
    public void ShowFailedApply(string version, string reason) => State = new UpdateFailed(version, reason);

    [RelayCommand(CanExecute = nameof(ShowsUpdate))]
    private Task Update() => State is UpdateOffered offered ? RunAsync(offered.Version) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(ShowsRetry))]
    private Task Retry() => State is UpdateFailed failed ? RunAsync(failed.Version) : Task.CompletedTask;

    /// <summary>U3: the checkbox writes the version and is the line's "away with it" too.</summary>
    [RelayCommand(CanExecute = nameof(ShowsSkip))]
    private void Skip()
    {
        if (State is UpdateOffered offered)
        {
            actions.Skip(offered.Version);
            State = UpdateState.None;
        }
    }

    private async Task RunAsync(string version)
    {
        lock (_progressGate)
        {
            _lastProgress = null;
        }

        State = new UpdateDownloading(version, 0);
        var reason = await actions.Download(version, percent => Report(version, percent));
        if (reason is null)
        {
            State = new UpdateApplying(version);
            reason = await actions.Apply(version);
            if (reason is null)
            {
                return;
            }
        }

        State = new UpdateFailed(version, reason);
    }

    /// <summary>From the download's pool thread: dropped within <see cref="ProgressInterval"/> of the last, 100 always passes.</summary>
    private void Report(string version, int percent)
    {
        lock (_progressGate)
        {
            var now = _time.GetTimestamp();
            if (percent < 100 && _lastProgress is { } last && _time.GetElapsedTime(last, now) < ProgressInterval)
            {
                return;
            }

            _lastProgress = now;
        }

        ui(() =>
        {
            if (State is UpdateDownloading downloading && downloading.Version == version)
            {
                State = downloading with { Percent = percent };
            }
        });
    }

    private static bool IsNewer(string version, string than) =>
        ReleaseVersion.TryParse(version, out var a) && ReleaseVersion.TryParse(than, out var b) && a.CompareTo(b) > 0;
}
