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
public sealed record ContractSource(
    IReadOnlyList<long> ContractIds,
    bool CrateSizeAssumed = false,
    int? ListedDestinations = null,
    long? Reward = null)
{
    public IReadOnlyList<long> ContractIds { get; } =
        ContractIds.Count > 0 ? ContractIds : throw new ArgumentException("A capture has at least one ContractTracker id.", nameof(ContractIds));
}
