using CargoPlan.App.Scanning;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The scan from a key or the editor's button to the plan and its notice (T10.3, D104): everything the overlay sees happens
/// inside the UI delegate, a failure costs neither the plan nor the app.
/// </summary>
public sealed class ScanRunnerTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private const string Complete = "ABANDON\nDETAILS\nReward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\n"
                                    + "Deliver 0/8 SCU of Stims to Riker.\nCollect Stims from Everus Harbor.\nSHARE UNTRACK";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    private readonly OverlayViewModel _overlay = new(HullA);

    // The UI delegate of the tests: runs the action at once and says so while it runs.
    private bool _insideUi;

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private Task Ui(Action action)
    {
        _insideUi = true;
        try
        {
            action();
        }
        finally
        {
            _insideUi = false;
        }

        return Task.CompletedTask;
    }

    private ScanRunner Runner(Func<Task<ScanAttempt>> scan) =>
        new(scan, _overlay, new ScanLog(Path.Combine(_folder, "scans.log")), Ui, _ => Task.CompletedTask);

    private static ScanAttempt Attempt(ScanAttemptOutcome outcome, string raw = "") =>
        new(outcome, raw, TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero);

    [Fact]
    public async Task The_plan_and_the_notice_change_only_inside_the_ui_delegate()
    {
        // ScanIntake runs on the pool (D103): a change outside the delegate would reach WPF's bindings from a pool thread.
        List<(string? Property, bool Inside)> changes = [];
        _overlay.PropertyChanged += (_, e) => changes.Add((e.PropertyName, _insideUi));

        await Runner(async () =>
        {
            await Task.Yield();
            return Attempt(ScanAttemptOutcome.Read, Complete);
        }).RunAsync();

        _overlay.Missions.Should().ContainSingle();
        // ACCEPTED (ABANDON, no availability): amounts as accepted, no rescan hint.
        _overlay.Notices.Should().Contain("Scanned #1 · From Everus Harbor · 8 SCU Stims to Riker");
        changes.Should().Contain(c => c.Property == nameof(OverlayViewModel.Saved));
        changes.Should().OnlyContain(c => c.Inside);
    }

    [Fact]
    public async Task A_failure_is_logged_and_noticed_and_the_plan_stays()
    {
        var message = $"scanner broke {Guid.NewGuid():N}";

        var run = Runner(() => throw new InvalidOperationException(message)).RunAsync();
        await run;

        run.IsCompletedSuccessfully.Should().BeTrue("a scan never takes the app down (D82)");
        _overlay.Notices.Should().Contain($"Scan failed: {message}");
        _overlay.Saved.Should().BeNull();
        TestLog.Text().Should().Contain(message);
    }

    [Fact]
    public async Task IsScanning_is_on_while_a_scan_runs_and_off_after()
    {
        TaskCompletionSource<ScanAttempt> read = new();
        var run = Runner(() => read.Task).RunAsync();

        _overlay.IsScanning.Should().BeTrue();
        read.SetResult(Attempt(ScanAttemptOutcome.NoContractInFrame));
        await run;

        _overlay.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task IsScanning_goes_off_after_a_failure_too()
    {
        await Runner(() => Task.FromException<ScanAttempt>(new InvalidOperationException("x"))).RunAsync();

        _overlay.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task A_second_press_while_one_runs_says_scan_running()
    {
        // The scanner holds one scan at a time and answers Busy (T10.1b); the runner doesn't buffer (D104).
        TaskCompletionSource<ScanAttempt> first = new();
        var calls = 0;
        var runner = Runner(() => ++calls == 1 ? first.Task : Task.FromResult(Attempt(ScanAttemptOutcome.Busy)));

        var running = runner.RunAsync();
        await runner.RunAsync();

        _overlay.Notices.Should().Contain("Scan running");
        _overlay.IsScanning.Should().BeTrue("the first scan still runs");
        first.SetResult(Attempt(ScanAttemptOutcome.GameNotFound));
        await running;
        _overlay.Notices.Should().Contain("Star Citizen window not found").And.NotContain("Scan running");
        _overlay.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task An_undo_after_the_scan_clears_its_notice()
    {
        await Runner(() => Task.FromResult(Attempt(ScanAttemptOutcome.Read, Complete))).RunAsync();
        _overlay.Notices.Should().Contain(n => n.StartsWith("Scanned #1", StringComparison.Ordinal));

        _overlay.UndoCommand.Execute(null);

        _overlay.Missions.Should().BeEmpty();
        _overlay.Notices.Should().NotContain(n => n.StartsWith("Scanned #1", StringComparison.Ordinal));
    }
}
