namespace CargoPlan.Ocr;

/// <summary>What a scan found. The notices for the player come with T10.3; failures are results, not exceptions.</summary>
public enum ScanOutcome
{
    /// <summary>A contract was read; <see cref="ScanResult.RawText"/> holds it.</summary>
    Read,

    /// <summary>Neither "DETAILS" nor "PRIMARY OBJECTIVES" in the region: no contract open, or another screen.</summary>
    NoContractInFrame,
}

/// <summary>How long each stage took, for the T10.1b gates (S10.0: capture ≈ 42 ms, OCR ≈ 1000 ms, parse &lt; 1 ms).</summary>
public sealed record ScanTimings(TimeSpan Region, TimeSpan Preprocess, TimeSpan Ocr, TimeSpan Lines)
{
    public TimeSpan Total => Region + Preprocess + Ocr + Lines;
}

/// <summary>The outcome, the raw text in ContractTracker's form (lines joined with "\n", empty without a contract), the
/// stage times.</summary>
public sealed record ScanResult(ScanOutcome Outcome, string RawText, ScanTimings Timings);
