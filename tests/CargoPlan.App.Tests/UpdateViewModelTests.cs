using CargoPlan.App.ViewModels;

namespace CargoPlan.App.Tests;

/// <summary>
/// The update line (T11.2, D109, U1–U6, U10): states, commands, lock and the progress throttle. Download and apply are
/// delegates here; the dispatcher is the calling thread.
/// </summary>
public class UpdateViewModelTests
{
    private readonly List<string> _skipped = [];

    private readonly List<string> _applied = [];

    private readonly ManualTime _time = new();

    private TaskCompletionSource<string?> _download = new();

    private Action<int>? _progress;

    private string? _applyResult;

    private UpdateViewModel Create() =>
        new(
            new UpdateActions(
                (version, progress) =>
                {
                    _progress = progress;
                    return _download.Task;
                },
                version =>
                {
                    _applied.Add(version);
                    return Task.FromResult(_applyResult);
                },
                _skipped.Add),
            action => action(),
            _time);

    [Fact]
    public void Without_an_offer_there_is_no_line()
    {
        var update = Create();

        update.State.Should().Be(UpdateState.None);
        update.IsVisible.Should().BeFalse();
    }

    [Fact]
    public void An_offer_shows_text_button_and_checkbox()
    {
        var update = Create();

        update.Offer("0.9.1");

        update.IsVisible.Should().BeTrue();
        update.Text.Should().Be("Update available · v0.9.1");
        update.ShowsUpdate.Should().BeTrue();
        update.ShowsSkip.Should().BeTrue();
        update.ShowsRetry.Should().BeFalse();
        update.AreControlsShown.Should().BeTrue();
    }

    [Fact]
    public void The_lock_hides_the_controls_and_keeps_the_text()
    {
        // U2: click-through, so a button would only mislead.
        var update = Create();
        update.Offer("0.9.1");

        update.IsLocked = true;

        update.AreControlsShown.Should().BeFalse();
        update.Text.Should().Be("Update available · v0.9.1");
        update.IsVisible.Should().BeTrue();

        update.IsLocked = false;
        update.AreControlsShown.Should().BeTrue();
    }

    [Fact]
    public void Skip_writes_the_version_and_removes_the_line()
    {
        // U3: the checkbox is the "away with it" too.
        var update = Create();
        update.Offer("0.9.1");

        update.SkipCommand.Execute(null);

        _skipped.Should().Equal("0.9.1");
        update.State.Should().Be(UpdateState.None);
        update.IsVisible.Should().BeFalse();
    }

    [Fact]
    public async Task Update_downloads_with_percent_then_applies()
    {
        var update = Create();
        update.Offer("0.9.1");

        var running = update.UpdateCommand.ExecuteAsync(null);

        update.State.Should().Be(new UpdateDownloading("0.9.1", 0));
        update.ShowsUpdate.Should().BeFalse();
        update.ShowsSkip.Should().BeFalse();
        _progress!(42);
        update.Text.Should().Be("Downloading update · 42 %");

        _download.SetResult(null);
        await running;

        _applied.Should().Equal("0.9.1");
        update.State.Should().Be(new UpdateApplying("0.9.1"));
        update.Text.Should().Be("Installing update · restarting");
    }

    [Fact]
    public async Task A_failed_download_shows_the_reason_and_Retry()
    {
        var update = Create();
        update.Offer("0.9.1");

        var running = update.UpdateCommand.ExecuteAsync(null);
        _download.SetResult("no network");
        await running;

        update.State.Should().Be(new UpdateFailed("0.9.1", "no network"));
        update.Text.Should().Be("Update failed · no network");
        update.ShowsRetry.Should().BeTrue();
        update.ShowsUpdate.Should().BeFalse();
        update.ShowsSkip.Should().BeFalse();
        _applied.Should().BeEmpty();
    }

    [Fact]
    public async Task A_failed_apply_shows_the_reason()
    {
        _applyResult = "see cargoplan.log";
        var update = Create();
        update.Offer("0.9.1");
        _download.SetResult(null);

        await update.UpdateCommand.ExecuteAsync(null);

        update.State.Should().Be(new UpdateFailed("0.9.1", "see cargoplan.log"));
    }

    [Fact]
    public async Task Retry_runs_the_same_way_again()
    {
        // U6: without a restart.
        var update = Create();
        update.Offer("0.9.1");
        _download.SetResult("no network");
        await update.UpdateCommand.ExecuteAsync(null);

        _download = new TaskCompletionSource<string?>();
        var retry = update.RetryCommand.ExecuteAsync(null);

        update.State.Should().Be(new UpdateDownloading("0.9.1", 0));
        _download.SetResult(null);
        await retry;
        _applied.Should().Equal("0.9.1");
    }

    [Fact]
    public void A_failed_apply_after_the_restart_offers_Retry()
    {
        // Update.exe started the old app again (UAC declined): the line says so (Freigabe, D109).
        var update = Create();

        update.ShowFailedApply("0.9.1", "cancelled");

        update.Text.Should().Be("Update failed · cancelled");
        update.ShowsRetry.Should().BeTrue();
    }

    [Fact]
    public void The_same_offer_again_doesnt_hide_a_failure()
    {
        var update = Create();
        update.ShowFailedApply("0.9.1", "cancelled");

        update.Offer("0.9.1");

        update.State.Should().Be(new UpdateFailed("0.9.1", "cancelled"));
    }

    [Fact]
    public void A_newer_offer_replaces_a_failure()
    {
        // E3: Retry would otherwise put the older release in.
        var update = Create();
        update.ShowFailedApply("0.9.1", "cancelled");

        update.Offer("0.9.2");

        update.State.Should().Be(new UpdateOffered("0.9.2"));
    }

    [Fact]
    public async Task An_offer_never_interrupts_a_download()
    {
        var update = Create();
        update.Offer("0.9.1");
        var running = update.UpdateCommand.ExecuteAsync(null);

        update.Offer("0.9.2");

        update.State.Should().Be(new UpdateDownloading("0.9.1", 0));
        await Finish(running);
    }

    [Fact]
    public async Task Progress_reaches_the_line_at_most_every_100_ms_and_100_always()
    {
        // U10: Velopack reports every even percent; the line follows ten times a second at most.
        var update = Create();
        update.Offer("0.9.1");
        var running = update.UpdateCommand.ExecuteAsync(null);

        _progress!(10);
        _time.Advance(TimeSpan.FromMilliseconds(50));
        _progress(12);
        update.State.Should().Be(new UpdateDownloading("0.9.1", 10));

        _time.Advance(TimeSpan.FromMilliseconds(60));
        _progress(14);
        update.State.Should().Be(new UpdateDownloading("0.9.1", 14));

        _progress(100);
        update.State.Should().Be(new UpdateDownloading("0.9.1", 100));
        await Finish(running);
    }

    /// <summary>
    /// Ends a download the test left running. AsyncRelayCommand watches its task in an async void (MonitorTask), and xUnit
    /// waits for every async void a test started: an unfinished download would hold the test run forever.
    /// </summary>
    private async Task Finish(Task running)
    {
        _download.TrySetResult("no network");
        await running;
    }

    /// <summary>A clock the test moves; no package for it (Microsoft.Extensions.TimeProvider.Testing).</summary>
    private sealed class ManualTime : TimeProvider
    {
        private long _ticks;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan by) => _ticks += by.Ticks;
    }
}
