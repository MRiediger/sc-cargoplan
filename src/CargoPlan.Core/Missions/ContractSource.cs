namespace CargoPlan.Core.Missions;

/// <summary>
/// Where a mission's contract came from, and what the legend marks as uncertain: a ContractTracker capture (D39), its
/// ids the identity across reads, or a scan by CargoPlan itself (D102), its number and content key the identity. Exactly
/// one of the two; ContractTracker's goes in T10.4.
/// </summary>
/// <param name="ContractIds">ContractTracker's row ids of the capture, never reused (D38); empty for a scan.</param>
/// <param name="CrateSizeAssumed">The text named no max crate size; the legend shows "4 SCU?".</param>
/// <param name="ListedDestinations">Entries of the contract's drop-off list; null if it has none. More than the
/// mission's stops: the legend shows "⚠ 1 of 3 destinations".</param>
/// <param name="Reward">The contract's reward in aUEC; null if the text had none or it wasn't readable. Two captures
/// with different rewards are two contracts, never a duplicate (D39 addendum).</param>
/// <param name="Gone">ContractTracker no longer lists any of its ids: the plan keeps it marked (D81). Never for a scan.</param>
/// <param name="Detached">Its ids are an earlier ContractTracker database's (D81): shown, never compared again, so a new
/// contract with the same id is another one. Only when gone.</param>
/// <param name="Scan">The scan that read it (D102); null for a ContractTracker capture.</param>
public sealed record ContractSource(
    IReadOnlyList<long> ContractIds,
    bool CrateSizeAssumed = false,
    int? ListedDestinations = null,
    long? Reward = null,
    bool Gone = false,
    bool Detached = false,
    ScanIdentity? Scan = null)
{
    public IReadOnlyList<long> ContractIds { get; } = Validated(ContractIds, Scan, Gone);

    public bool Detached { get; } =
        !Detached || Gone ? Detached : throw new ArgumentException("A capture is detached only when it is gone.", nameof(Detached));

    /// <summary>A contract read by CargoPlan's own scan (D102), without ContractTracker ids.</summary>
    public static ContractSource Scanned(ScanIdentity scan, bool crateSizeAssumed = false, int? listedDestinations = null, long? reward = null) =>
        new([], crateSizeAssumed, listedDestinations, reward, Scan: scan);

    /// <summary>Gone and detached: what a new ContractTracker database makes of a capture (D81).</summary>
    public ContractSource AsDetached() => new(ContractIds, CrateSizeAssumed, ListedDestinations, Reward, Gone: true, Detached: true);

    private static IReadOnlyList<long> Validated(IReadOnlyList<long> contractIds, ScanIdentity? scan, bool gone)
    {
        if ((contractIds.Count > 0) == (scan is not null))
        {
            throw new ArgumentException("A contract has ContractTracker ids or a scan, exactly one of them.", nameof(ContractIds));
        }

        // Gone and detached are ContractTracker's (D81): a scan has no list it could leave.
        return scan is not null && gone
            ? throw new ArgumentException("A scanned contract is never gone; only a ContractTracker capture leaves its list.", nameof(Gone))
            : contractIds;
    }
}
