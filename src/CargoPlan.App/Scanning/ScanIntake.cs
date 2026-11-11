using System.Globalization;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Scanning;

/// <summary>
/// One scan from the key to the plan (T10.2b2, D103): read, on any doubt wait and read once more, take the better read, put
/// it into the plan, log every read. Two separate scanner calls: the one-scan lock is free while it waits, so a key press
/// meanwhile isn't lost (Martin). Without a trigger yet: T10.3 wires it to the hotkey and the editor's button.
/// </summary>
/// <param name="scan">The scanner, <see cref="ContractScanner.ScanAsync"/> in the app.</param>
/// <param name="apply">Puts the contract into the plan on the UI thread (<see cref="ViewModels.OverlayViewModel.ApplyScan"/>,
/// marshalled by the caller): this class runs on the pool.</param>
/// <param name="delay">The wait before the second read, <see cref="SecondReadDelay"/> in the app (S10.0: the game types
/// a contract in within a fraction of a second).</param>
internal sealed class ScanIntake(Func<Task<ScanAttempt>> scan, Func<ScannedContract, ScanReport> apply, ScanLog log, Func<TimeSpan, Task> delay)
{
    public static readonly TimeSpan SecondReadDelay = TimeSpan.FromMilliseconds(300);

    public async Task<ScanReport> RunAsync()
    {
        var first = await scan().ConfigureAwait(false);
        if (first.Outcome != ScanAttemptOutcome.Read)
        {
            var failed = new ScanReport(Kind(first.Outcome));
            log.Write(first.RawText, Describe(failed));
            return failed;
        }

        var (raw, read) = (first.RawText, ScannedContract.Read(first.RawText));
        if (read.Doubts.Count > 0)
        {
            log.Write(raw, $"doubt ({string.Join(", ", read.Doubts)}), second read");
            await delay(SecondReadDelay).ConfigureAwait(false);
            var second = await scan().ConfigureAwait(false);
            if (second.Outcome == ScanAttemptOutcome.Read)
            {
                // The second read unless it is the one still typing in; then the first if that one is complete.
                var again = ScannedContract.Read(second.RawText);
                (raw, read) = !again.InHardDoubt || read.InHardDoubt ? (second.RawText, again) : (raw, read);
            }
        }

        var report = apply(read);
        log.Write(raw, Describe(report));
        return report;
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
        report.ScanNo is { } no ? string.Create(CultureInfo.InvariantCulture, $" #{no}") : "",
        report.QuantitiesChanged ? " quantities changed" : "",
        report.Cleared.Count > 0 ? $" ({string.Join("; ", report.Cleared)})" : "",
        report.Refusal is { } refusal ? $": {refusal}" : "");
}
