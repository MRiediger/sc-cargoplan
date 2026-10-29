namespace CargoPlan.Core.Missions;

/// <summary>
/// A ContractTracker capture (D39): its ids, the identity across reads, and what the legend marks as uncertain.
/// </summary>
/// <param name="ContractIds">ContractTracker's row ids of the capture, never reused (D38).</param>
/// <param name="CrateSizeAssumed">The text named no max crate size; the legend shows "4 SCU?".</param>
/// <param name="ListedDestinations">Entries of the contract's drop-off list; null if it has none. More than the
/// mission's stops: the legend shows "⚠ 1 of 3 destinations".</param>
/// <param name="Reward">The contract's reward in aUEC; null if the text had none or it wasn't readable. Two captures
/// with different rewards are two contracts, never a duplicate (D39 addendum).</param>
/// <param name="Gone">ContractTracker no longer lists any of its ids: the plan keeps it marked (D81).</param>
/// <param name="Detached">Its ids are an earlier ContractTracker database's (D81): shown, never compared again, so a new
/// contract with the same id is another one. Only when gone.</param>
public sealed record ContractSource(
    IReadOnlyList<long> ContractIds,
    bool CrateSizeAssumed = false,
    int? ListedDestinations = null,
    long? Reward = null,
    bool Gone = false,
    bool Detached = false)
{
    public IReadOnlyList<long> ContractIds { get; } =
        ContractIds.Count > 0 ? ContractIds : throw new ArgumentException("A capture has at least one ContractTracker id.", nameof(ContractIds));

    public bool Detached { get; } =
        !Detached || Gone ? Detached : throw new ArgumentException("A capture is detached only when it is gone.", nameof(Detached));

    /// <summary>Gone and detached: what a new ContractTracker database makes of a capture (D81).</summary>
    public ContractSource AsDetached() => new(ContractIds, CrateSizeAssumed, ListedDestinations, Reward, Gone: true, Detached: true);
}
