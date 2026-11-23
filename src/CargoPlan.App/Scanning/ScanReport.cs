using CargoPlan.Core.Routing;

namespace CargoPlan.App.Scanning;

/// <summary>What a scan came to (T10.2b2, D103); <see cref="ScanNotices"/> turns each into its notice (T10.3).</summary>
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

    /// <summary>An offer with Routes selected (T13.1, U3): not in the plan, an observation of <see cref="ScanReport.Observed"/>.</summary>
    Observed,

    /// <summary>An offer with Routes selected whose title or reward wasn't read: not in the plan, not observed.</summary>
    NotObserved,
}

/// <param name="ScanNo">The mission's scan number for Added, Updated, AlreadyInPlan and Refused.</param>
/// <param name="Cleared">Deliveries no longer loaded or ticked after an update.</param>
/// <param name="Reason">Why an update was refused, why the capture failed ("Win32 error 5", T10.3), or why an offer
/// wasn't observed ("title unreadable", T13.1).</param>
/// <param name="Observed">The template an offer was observed as, for <see cref="ScanReportKind.Observed"/>.</param>
public sealed record ScanReport(
    ScanReportKind Kind,
    int? ScanNo = null,
    bool QuantitiesChanged = false,
    IReadOnlyList<string>? Cleared = null,
    string? Reason = null,
    ObservationKey? Observed = null)
{
    public IReadOnlyList<string> Cleared { get; } = Cleared ?? [];

    // Records compare lists by reference; a report is equal by its parts.
    public bool Equals(ScanReport? other) =>
        other is not null
        && (Kind, ScanNo, QuantitiesChanged, Reason, Observed) == (other.Kind, other.ScanNo, other.QuantitiesChanged, other.Reason, other.Observed)
        && Cleared.SequenceEqual(other.Cleared);

    public override int GetHashCode() => HashCode.Combine(Kind, ScanNo, QuantitiesChanged, Reason, Cleared.Count, Observed);
}
