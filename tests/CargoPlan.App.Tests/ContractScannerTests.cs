using CargoPlan.App.Scanning;
using CargoPlan.Ocr;

namespace CargoPlan.App.Tests;

// The game window is never searched for real (find: () => null), so the tests hold whether Star Citizen runs or not.
public class ContractScannerTests
{
    private readonly List<string?> _notices = [];

    [Fact]
    public async Task Missing_models_make_the_scan_unavailable_with_a_notice_and_a_log_line_instead_of_a_crash()
    {
        var scanner = Scanner(() => throw new FileNotFoundException("The OCR model test-k1-missing.onnx is missing next to the exe (models/v5)."));
        scanner.StartWarmup();

        var attempt = await scanner.ScanAsync();

        attempt.Outcome.Should().Be(ScanAttemptOutcome.Unavailable);
        LastNotice().Should().StartWith("Contract scan unavailable:").And.Contain("test-k1-missing.onnx");
        TestLog.Text().Should().Contain("test-k1-missing.onnx");
    }

    [Fact]
    public async Task A_scan_before_the_warm_up_ends_waits_for_it_with_a_notice()
    {
        using var release = new ManualResetEventSlim();
        var scanner = Scanner(() =>
        {
            release.Wait();
            throw new FileNotFoundException("test-wait models gone");
        });
        scanner.StartWarmup();

        var scan = scanner.ScanAsync();
        await Task.Delay(100);
        scan.IsCompleted.Should().BeFalse("the scan waits for the warm-up");
        _notices.Should().Contain(ContractScanner.WarmingNotice);

        release.Set();
        var attempt = await scan;

        attempt.Outcome.Should().Be(ScanAttemptOutcome.Unavailable);
        LastNotice().Should().Contain("test-wait models gone", "it ended with the warm-up, not with the limit");
    }

    // A scan can come before the first frame (the frame measurement's loop, a hotkey in T10.3): it waits as for a warm-up.
    [Fact]
    public async Task A_scan_before_the_warm_up_started_waits_for_it()
    {
        var scanner = Scanner(() => throw new FileNotFoundException("test-early models gone"));

        var scan = scanner.ScanAsync();
        await Task.Delay(50);
        scan.IsCompleted.Should().BeFalse();
        scanner.StartWarmup();
        var attempt = await scan;

        attempt.Outcome.Should().Be(ScanAttemptOutcome.Unavailable);
        LastNotice().Should().Contain("test-early models gone");
    }

    [Fact]
    public async Task A_warm_up_that_never_ends_stops_the_wait_at_the_limit_and_frees_the_next_scan()
    {
        using var never = new ManualResetEventSlim();
        var scanner = Scanner(() =>
        {
            never.Wait();
            throw new InvalidOperationException("unreachable");
        }, limit: TimeSpan.FromMilliseconds(100));
        scanner.StartWarmup();

        var first = await scanner.ScanAsync();
        var second = await scanner.ScanAsync();

        first.Outcome.Should().Be(ScanAttemptOutcome.Unavailable);
        LastNotice().Should().Be("Contract scan unavailable: OCR did not start within 0.1 s.");
        second.Outcome.Should().Be(ScanAttemptOutcome.Unavailable, "the lock was freed, the second scan waited again");
        first.Wait.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(90));
        never.Set();
    }

    [Fact]
    public async Task A_second_scan_while_one_runs_is_ignored()
    {
        using var release = new ManualResetEventSlim();
        var scanner = Scanner(() =>
        {
            release.Wait();
            throw new FileNotFoundException("test-busy");
        });
        scanner.StartWarmup();

        var first = scanner.ScanAsync();
        var second = await scanner.ScanAsync();
        release.Set();
        await first;

        second.Outcome.Should().Be(ScanAttemptOutcome.Busy);
    }

    // The one test with the real engine (≈ 1 s): the warm-up logs its numbers, a ready scanner looks for the game.
    [Fact]
    public async Task A_warm_engine_logs_its_numbers_and_a_scan_without_the_game_says_so()
    {
        var scanner = Scanner(OcrEngine.Load);
        scanner.StartWarmup();

        var attempt = await scanner.ScanAsync();

        attempt.Outcome.Should().Be(ScanAttemptOutcome.GameNotFound);
        TestLog.Text().Should().MatchRegex(@"OCR ready: .*load \d+ ms, warm-up read \d+ ms, total \d+ ms.*working set \d+ → \d+ MB");
        LastNotice().Should().BeNull("a ready engine clears the scan's notice line");
    }

    private ContractScanner Scanner(Func<OcrEngine> load, TimeSpan? limit = null) =>
        new(load, notice => { lock (_notices) { _notices.Add(notice); } }, find: () => null, warmupLimit: limit);

    private string? LastNotice()
    {
        lock (_notices)
        {
            return _notices.Count == 0 ? null : _notices[^1];
        }
    }
}
