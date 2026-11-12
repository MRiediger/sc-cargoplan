using System.Diagnostics;
using System.Globalization;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Scanning;

/// <summary>
/// One scan from the key to the plan (T10.2b2, D103): read, on any doubt wait and read once more, take the better read, put
/// it into the plan, show the result, log every read. Two separate scanner calls: the one-scan lock is free while it waits,
/// so a key press meanwhile isn't lost (Martin). Triggered by <see cref="ScanRunner"/> (T10.3).
/// </summary>
/// <param name="scan">The scanner, <see cref="ContractScanner.ScanAsync"/> in the app.</param>
/// <param name="apply">Puts the contract into the plan on the UI thread (<see cref="ViewModels.OverlayViewModel.ApplyScan"/>,
/// marshalled by the caller): this class runs on the pool.</param>
/// <param name="show">The result's notice, marshalled like <paramref name="apply"/>; called once per run, failures included.</param>
/// <param name="delay">The wait before the second read, <see cref="SecondReadDelay"/> in the app (S10.0: the game types
/// a contract in within a fraction of a second).</param>
/// <param name="pictures">With <c>--keep-scans</c>: keeps each run's picture (T10.5); null = none.</param>
internal sealed class ScanIntake(
    Func<Task<ScanAttempt>> scan,
    Func<ScannedContract, Task<ScanReport>> apply,
    Func<ScanReport, Task> show,
    ScanLog log,
    Func<TimeSpan, Task> delay,
    ScanPictures? pictures = null)
{
    public static readonly TimeSpan SecondReadDelay = TimeSpan.FromMilliseconds(300);

    public async Task<ScanReport> RunAsync()
    {
        // From the key (the press starts the run in the same tick) to the raw text and to the notice: Gate 1 (T10.3).
        var clock = Stopwatch.StartNew();
        var first = await scan().ConfigureAwait(false);
        if (first.Outcome != ScanAttemptOutcome.Read)
        {
            var failed = new ScanReport(Kind(first.Outcome), Reason: first.Outcome == ScanAttemptOutcome.CaptureFailed ? Inv($"Win32 error {first.CaptureError}") : null);
            var noText = clock.Elapsed;
            await show(failed).ConfigureAwait(false);
            Finish(first.RawText, failed, first, null, noText, clock.Elapsed);
            return failed;
        }

        var (raw, read, attempt, toText) = (first.RawText, ScannedContract.Read(first.RawText), first, clock.Elapsed);
        if (read.Doubts.Count > 0)
        {
            log.Write(raw, $"doubt ({string.Join(", ", read.Doubts)}), second read | {Times(first, toText, null)}");
            await delay(SecondReadDelay).ConfigureAwait(false);
            var second = await scan().ConfigureAwait(false);
            if (second.Outcome == ScanAttemptOutcome.Read)
            {
                // The second read unless it is the one still typing in; then the first if that one is complete.
                var again = ScannedContract.Read(second.RawText);
                if (!again.InHardDoubt || read.InHardDoubt)
                {
                    (raw, read, attempt, toText) = (second.RawText, again, second, clock.Elapsed);
                }
            }
        }

        var report = await apply(read).ConfigureAwait(false);
        await show(report).ConfigureAwait(false);
        Finish(raw, report, attempt, read.View, toText, clock.Elapsed);
        return report;
    }

    /// <summary>
    /// The read and its result into scans.log, the result and the times into cargoplan.log too: scans.log keeps 50. With
    /// --keep-scans the picture of the attempt the result rests on, its name on the result line (T10.5).
    /// </summary>
    private void Finish(string raw, ScanReport report, ScanAttempt attempt, ContractView? view, TimeSpan toText, TimeSpan toNotice)
    {
        var picture = pictures?.Save(attempt.Picture, view, report);
        var result = $"{Describe(report)} | {Times(attempt, toText, toNotice)}{(picture is null ? "" : $" | picture={picture}")}";
        log.Write(raw, result);
        AppLog.Write("Scan", result);
    }

    private static ScanReportKind Kind(ScanAttemptOutcome outcome) => outcome switch
    {
        ScanAttemptOutcome.NoContractInFrame => ScanReportKind.NoContractInFrame,
        ScanAttemptOutcome.GameNotFound => ScanReportKind.GameNotFound,
        ScanAttemptOutcome.CaptureFailed => ScanReportKind.CaptureFailed,
        ScanAttemptOutcome.Unavailable => ScanReportKind.Unavailable,
        _ => ScanReportKind.Busy,
    };

    /// <summary>"Added #1", "Updated #1 quantities changed", "Refused #1: …": the result as the log line names it.</summary>
    private static string Describe(ScanReport report) => string.Concat(
        report.Kind.ToString(),
        report.ScanNo is { } no ? Inv($" #{no}") : "",
        report.QuantitiesChanged ? " quantities changed" : "",
        report.Cleared.Count > 0 ? $" ({string.Join("; ", report.Cleared)})" : "",
        report.Reason is { } reason ? $": {reason}" : "");

    /// <summary>The times as scan-probe.log names them (T10.1b), so one script reads both.</summary>
    private static string Times(ScanAttempt attempt, TimeSpan toText, TimeSpan? toNotice) => string.Join(' ', [
        Inv($"keyToTextMs={toText.TotalMilliseconds:0}"),
        .. toNotice is { } notice ? [Inv($"keyToNoticeMs={notice.TotalMilliseconds:0}")] : Array.Empty<string>(),
        Inv($"waitMs={attempt.Wait.TotalMilliseconds:0}"),
        Inv($"captureMs={attempt.Capture.TotalMilliseconds:0}"),
        Inv($"preprocessMs={Ms(attempt.Read?.Preprocess)}"),
        Inv($"ocrMs={Ms(attempt.Read?.Ocr)}"),
    ]);

    private static string Ms(TimeSpan? time) => time is { } t ? t.TotalMilliseconds.ToString("0", CultureInfo.InvariantCulture) : "-";

    private static string Inv(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
