namespace CargoPlan.App.Scanning;

/// <summary>What a scan came to (T10.2b2, D103); T10.3 turns each into its notice.</summary>
public enum ScanReportKind
{
    /// <summary>A new mission, <see cref="ScanReport.ScanNo"/> its number.</summary>
    Added,

    /// <summary>The mission with the scan's key changed: amounts, or only the estimate ended.</summary>
    Updated,

    /// <summary>The mission with the scan's key is as the scan says, or an offer came after the accepted contract.</summary>
    AlreadyInPlan,

    /// <summary>The update found no room (a mission loaded in part); the plan stays.</summary>
    Refused,

    /// <summary>The text was still typing in after a second read; the plan stays.</summary>
    Incomplete,

    /// <summary>No delivery in the text; the plan stays.</summary>
    NoContract,

    /// <summary>The scanner saw no contract on screen (<see cref="ScanAttemptOutcome.NoContractInFrame"/>).</summary>
    NoContractInFrame,

    /// <summary>No Star Citizen window to scan.</summary>
    GameNotFound,

    /// <summary>The screen couldn't be copied.</summary>
    CaptureFailed,

    /// <summary>The OCR isn't there (models missing) or not ready in time.</summary>
    Unavailable,

    /// <summary>Another scan was running.</summary>
    Busy,
}

/// <param name="ScanNo">The mission's scan number for Added, Updated, AlreadyInPlan and Refused.</param>
/// <param name="Cleared">Deliveries no longer loaded or ticked after an update.</param>
/// <param name="Refusal">Why an update was refused.</param>
public sealed record ScanReport(
    ScanReportKind Kind, int? ScanNo = null, bool QuantitiesChanged = false, IReadOnlyList<string>? Cleared = null, string? Refusal = null)
{
    public IReadOnlyList<string> Cleared { get; } = Cleared ?? [];

    // Records compare lists by reference; a report is equal by its parts.
    public bool Equals(ScanReport? other) =>
        other is not null && (Kind, ScanNo, QuantitiesChanged, Refusal) == (other.Kind, other.ScanNo, other.QuantitiesChanged, other.Refusal)
        && Cleared.SequenceEqual(other.Cleared);

    public override int GetHashCode() => HashCode.Combine(Kind, ScanNo, QuantitiesChanged, Refusal, Cleared.Count);
}
