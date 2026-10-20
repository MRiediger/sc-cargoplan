namespace CargoPlan.Import.ContractTracker;

/// <summary>What one read found: the new candidates and which known captures are still there, or a notice why nothing was read.</summary>
/// <param name="Notice">For the player; null if the read went through or is simply to be tried again later.</param>
/// <param name="Present">The known ids still in ContractTracker; null if nothing was read, so nothing counts as deleted.</param>
public sealed record ContractTrackerReadResult(IReadOnlyList<ContractTrackerCandidate> Candidates, string? Notice, IReadOnlySet<long>? Present = null)
{
    internal static ContractTrackerReadResult Nothing { get; } = new([], null);
}
