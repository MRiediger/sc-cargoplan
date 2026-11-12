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

    // What the intake handed to the notice line, in order (T10.3).
    private readonly List<ScanReport> _shown = [];

    private ScanIntake Intake(
        Func<Task<ScanAttempt>> scan, OverlayViewModel overlay, Func<TimeSpan, Task>? delay = null, ScanPictures? pictures = null) => new(
        scan,
        contract => Task.FromResult(overlay.ApplyScan(contract)),
        report =>
        {
            _shown.Add(report);
            return Task.CompletedTask;
        },
        new ScanLog(LogPath),
        delay ?? (_ => Task.CompletedTask),
        pictures);

    private string PictureFolder => Path.Combine(_folder, "scans");

    private ScanPictures Pictures() => new(PictureFolder, () => new DateTime(2026, 11, 12, 15, 50, 12));

    private static ScanAttempt Pictured(ScanAttemptOutcome outcome, string raw, params byte[] picture) =>
        Attempt(outcome, raw) with { Picture = picture };

    // --keep-scans (T10.5, D108): the picture the result rests on, named by it, and its name in scans.log.
    [Fact]
    public async Task With_keep_scans_the_picture_is_saved_with_the_result()
    {
        var read = Pictured(ScanAttemptOutcome.Read, Complete, 1, 2, 3);

        await Intake(() => Task.FromResult(read), new OverlayViewModel(HullA), pictures: Pictures()).RunAsync();

        var name = "20261112-155012-accepted-added-1.png";
        File.ReadAllBytes(Path.Combine(PictureFolder, name)).Should().Equal(1, 2, 3);
        File.ReadAllText(LogPath).Split('\n')[0].Should().EndWith($"picture={name}");
    }

    [Fact]
    public async Task On_a_second_read_the_picture_of_the_read_taken_is_saved()
    {
        var calls = 0;
        Task<ScanAttempt> Scan() => Task.FromResult(++calls == 1
            ? Pictured(ScanAttemptOutcome.Read, TypingIn, 1)
            : Pictured(ScanAttemptOutcome.Read, Complete, 2));

        await Intake(Scan, new OverlayViewModel(HullA), pictures: Pictures()).RunAsync();

        Directory.GetFiles(PictureFolder).Should().ContainSingle()
            .Which.Should().Match(path => File.ReadAllBytes(path).SequenceEqual(new byte[] { 2 }));
    }

    [Fact]
    public async Task A_frame_without_a_contract_keeps_its_picture()
    {
        // The case of an unchecked screen size (Known issue 1): the region missed the contract.
        var missed = Pictured(ScanAttemptOutcome.NoContractInFrame, "", 7);

        await Intake(() => Task.FromResult(missed), new OverlayViewModel(HullA), pictures: Pictures()).RunAsync();

        File.Exists(Path.Combine(PictureFolder, "20261112-155012-nocontractinframe.png")).Should().BeTrue();
    }

    [Fact]
    public async Task Without_keep_scans_no_picture_is_written()
    {
        var read = Pictured(ScanAttemptOutcome.Read, Complete, 1, 2, 3);

        await Intake(() => Task.FromResult(read), new OverlayViewModel(HullA)).RunAsync();

        Directory.Exists(PictureFolder).Should().BeFalse();
        File.ReadAllText(LogPath).Should().NotContain("picture=");
    }

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

    [Fact]
    public async Task The_result_goes_to_the_notice_line_once()
    {
        var report = await Intake(new FakeScanner(TypingIn, Complete).Scan, new OverlayViewModel(HullA)).RunAsync();

        _shown.Should().Equal(report);
    }

    [Fact]
    public async Task What_the_scanner_couldnt_read_goes_to_the_notice_line_too()
    {
        await Intake(() => Task.FromResult(Attempt(ScanAttemptOutcome.GameNotFound)), new OverlayViewModel(HullA)).RunAsync();

        _shown.Should().Equal(new ScanReport(ScanReportKind.GameNotFound));
    }

    [Fact]
    public async Task A_failed_capture_carries_its_win32_error()
    {
        ScanAttempt failed = new(ScanAttemptOutcome.CaptureFailed, "", TimeSpan.Zero, TimeSpan.Zero, null, TimeSpan.Zero, CaptureError: 5);

        var report = await Intake(() => Task.FromResult(failed), new OverlayViewModel(HullA)).RunAsync();

        report.Should().Be(new ScanReport(ScanReportKind.CaptureFailed, Reason: "Win32 error 5"));
    }

    [Fact]
    public async Task The_result_line_carries_the_times_from_the_key_for_gate_1()
    {
        // T10.3 Gate 1 is measured from the real path: key → raw text and key → notice, with the scanner's stages.
        ScanAttempt read = new(ScanAttemptOutcome.Read, Complete, TimeSpan.FromMilliseconds(3), TimeSpan.FromMilliseconds(22),
            new Ocr.ScanTimings(TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(41), TimeSpan.FromMilliseconds(1185), TimeSpan.Zero),
            TimeSpan.FromMilliseconds(1260));

        await Intake(() => Task.FromResult(read), new OverlayViewModel(HullA)).RunAsync();

        var result = File.ReadAllText(LogPath).Split('\n')[0];
        result.Should().Contain("Added #1").And.Contain("keyToTextMs=").And.Contain("keyToNoticeMs=")
            .And.Contain("waitMs=3").And.Contain("captureMs=22").And.Contain("preprocessMs=41").And.Contain("ocrMs=1185");
        TestLog.Text().Should().Contain("Scan: Added #1");
    }
}
