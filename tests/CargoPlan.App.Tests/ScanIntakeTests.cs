using CargoPlan.App.Scanning;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Tests;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Tests;

/// <summary>
/// One scan from key to plan (T10.2b2, D103): a second read only on doubt, two separate scanner calls, every read in
/// scans.log. The scanner is a fake that holds its one-scan lock while a read runs, as ContractScanner does (T10.1b).
/// </summary>
public sealed class ScanIntakeTests : IDisposable
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private const string Complete = "ABANDON\nDETAILS\nReward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\n"
                                    + "Deliver 0/8 SCU of Stims to Riker.\nCollect Stims from Everus Harbor.\nSHARE UNTRACK";

    private const string TypingIn = "ABANDON\nDETAILS\nReward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\nCollect Stims from\nSHARE UNTRACK";

    private const string NoReward = "ABANDON\nDETAILS\nReward\nContract Deadline N/A\nPRIMARY OBJECTIVES\n"
                                    + "Deliver 0/8 SCU of Stims to Riker.\nCollect Stims from Everus Harbor.\nSHARE UNTRACK";

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "CargoPlan.Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string LogPath => Path.Combine(_folder, "scans.log");

    private static ScanAttempt Attempt(ScanAttemptOutcome outcome, string raw = "") =>
        new(outcome, raw, TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero);

    /// <summary>A scanner that reads the texts in turn and answers Busy while a read runs.</summary>
    private sealed class FakeScanner(params string[] texts)
    {
        private int _next;
        private bool _running;

        public int Calls { get; private set; }

        public async Task<ScanAttempt> Scan()
        {
            if (_running)
            {
                return Attempt(ScanAttemptOutcome.Busy);
            }

            _running = true;
            Calls++;
            await Task.Yield();
            _running = false;
            return Attempt(ScanAttemptOutcome.Read, texts[Math.Min(_next++, texts.Length - 1)]);
        }
    }

    private ScanIntake Intake(Func<Task<ScanAttempt>> scan, OverlayViewModel overlay, Func<TimeSpan, Task>? delay = null) =>
        new(scan, overlay.ApplyScan, new ScanLog(LogPath), delay ?? (_ => Task.CompletedTask));

    [Fact]
    public async Task A_text_all_there_is_read_once()
    {
        var scanner = new FakeScanner(Complete);
        var overlay = new OverlayViewModel(HullA);

        var report = await Intake(scanner.Scan, overlay).RunAsync();

        report.Kind.Should().Be(ScanReportKind.Added);
        scanner.Calls.Should().Be(1, "no doubt, no second read (Gate 1, T10.1b)");
    }

    [Fact]
    public async Task A_text_typing_in_is_read_again_and_the_second_read_counts()
    {
        var scanner = new FakeScanner(TypingIn, Complete);
        var overlay = new OverlayViewModel(HullA);

        var report = await Intake(scanner.Scan, overlay).RunAsync();

        report.Kind.Should().Be(ScanReportKind.Added);
        scanner.Calls.Should().Be(2);
        overlay.Missions.Single().Stops.Single().Destination.Should().Be("Riker");
    }

    [Fact]
    public async Task Still_typing_in_after_the_second_read_is_incomplete_and_changes_nothing()
    {
        var scanner = new FakeScanner(TypingIn, TypingIn);
        var overlay = new OverlayViewModel(HullA);

        (await Intake(scanner.Scan, overlay).RunAsync()).Kind.Should().Be(ScanReportKind.Incomplete);

        overlay.Saved.Should().BeNull();
    }

    [Fact]
    public async Task A_soft_doubt_left_after_the_second_read_is_taken_in()
    {
        // A reward the OCR can't read twice must not block the contract; its key says "?" (D101).
        var scanner = new FakeScanner(NoReward, NoReward);
        var overlay = new OverlayViewModel(HullA);

        (await Intake(scanner.Scan, overlay).RunAsync()).Kind.Should().Be(ScanReportKind.Added);

        scanner.Calls.Should().Be(2);
        overlay.Missions.Single().Contract!.Reward.Should().BeNull();
    }

    [Fact]
    public async Task A_complete_first_read_wins_over_a_second_one_typing_in()
    {
        var scanner = new FakeScanner(NoReward, TypingIn);
        var overlay = new OverlayViewModel(HullA);

        (await Intake(scanner.Scan, overlay).RunAsync()).Kind.Should().Be(ScanReportKind.Added);
    }

    [Fact]
    public async Task The_scanner_is_free_while_the_intake_waits_for_the_second_read()
    {
        // Martin: the second read must not hold the one-scan lock over the wait, or a key press in T10.3 is lost for ≈ 2.6 s.
        var scanner = new FakeScanner(TypingIn, Complete, Complete);
        var overlay = new OverlayViewModel(HullA);
        ScanAttempt? duringWait = null;

        await Intake(scanner.Scan, overlay, async _ => duringWait = await scanner.Scan()).RunAsync();

        duringWait!.Outcome.Should().Be(ScanAttemptOutcome.Read);
    }

    [Fact]
    public async Task A_busy_second_read_keeps_the_first()
    {
        var overlay = new OverlayViewModel(HullA);
        var calls = 0;
        Task<ScanAttempt> Scan() => Task.FromResult(++calls == 1 ? Attempt(ScanAttemptOutcome.Read, NoReward) : Attempt(ScanAttemptOutcome.Busy));

        (await Intake(Scan, overlay).RunAsync()).Kind.Should().Be(ScanReportKind.Added);
    }

    [Theory]
    [InlineData((int)ScanAttemptOutcome.NoContractInFrame, ScanReportKind.NoContractInFrame)]
    [InlineData((int)ScanAttemptOutcome.GameNotFound, ScanReportKind.GameNotFound)]
    [InlineData((int)ScanAttemptOutcome.CaptureFailed, ScanReportKind.CaptureFailed)]
    [InlineData((int)ScanAttemptOutcome.Unavailable, ScanReportKind.Unavailable)]
    [InlineData((int)ScanAttemptOutcome.Busy, ScanReportKind.Busy)]
    public async Task What_the_scanner_couldnt_read_is_handed_on(int outcome, ScanReportKind expected)
    {
        // The scanner's outcome as a number: a public test can't take the App's internal type.
        var overlay = new OverlayViewModel(HullA);

        (await Intake(() => Task.FromResult(Attempt((ScanAttemptOutcome)outcome)), overlay).RunAsync()).Kind.Should().Be(expected);

        overlay.Saved.Should().BeNull();
    }

    [Fact]
    public async Task Every_read_goes_to_the_scan_log_with_its_result()
    {
        var scanner = new FakeScanner(TypingIn, Complete);

        await Intake(scanner.Scan, new OverlayViewModel(HullA)).RunAsync();

        var log = File.ReadAllText(LogPath);
        log.Should().Contain("Collect Stims from\n").And.Contain("Collect Stims from Everus Harbor.");
        log.Should().Contain("second read").And.Contain("Added #1");
    }
}
