using CargoPlan.Core.Missions;

namespace CargoPlan.Import.ContractTracker;

/// <summary>
/// One ContractTracker capture as a mission (D39): stops and items as entered (D23), names normalised
/// (<see cref="ContractText"/>), plus what the legend marks as uncertain (T5.4).
/// </summary>
/// <param name="ContractIds">ContractTracker's ids of the capture's rows; the candidate's identity (D39).</param>
/// <param name="CapturedAt">UTC; null if ContractTracker's text was not readable as a time.</param>
/// <param name="Stops">Order = order in the contract text; ContractTracker has no route order.</param>
/// <param name="CrateSizeAssumed">The text names no max crate size; each item has the largest crate that fits its SCU.</param>
/// <param name="UsedFallbackRows">No delivery line was readable; the stops are ContractTracker's own destination rows.</param>
/// <param name="ListedDestinationCount">Entries of the contract's drop-off list; null if it has none.</param>
/// <param name="RawText">The raw OCR text, for bug reports (CLI <c>--raw</c>).</param>
public sealed record ContractTrackerCandidate(
    IReadOnlyList<long> ContractIds,
    DateTimeOffset? CapturedAt,
    IReadOnlyList<MissionStop> Stops,
    bool CrateSizeAssumed,
    bool UsedFallbackRows,
    int? ListedDestinationCount,
    string? RawText)
{
    /// <summary>The drop-off list names more destinations than were read.</summary>
    public bool DestinationsIncomplete => ListedDestinationCount > Stops.Count;

    /// <summary>The candidate as the sync takes it into the plan (D39 addendum): stops, ids and the legend's hints.</summary>
    public ContractCapture ToCapture() => new(Stops, new ContractSource(ContractIds, CrateSizeAssumed, ListedDestinationCount));
}
