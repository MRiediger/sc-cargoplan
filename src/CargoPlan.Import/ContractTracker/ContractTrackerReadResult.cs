namespace CargoPlan.Import.ContractTracker;

/// <summary>What one read found: the new candidates, or a notice why nothing was read.</summary>
/// <param name="Notice">For the player; null if the read went through or is simply to be tried again later.</param>
public sealed record ContractTrackerReadResult(IReadOnlyList<ContractTrackerCandidate> Candidates, string? Notice)
{
    internal static ContractTrackerReadResult Nothing { get; } = new([], null);
}
