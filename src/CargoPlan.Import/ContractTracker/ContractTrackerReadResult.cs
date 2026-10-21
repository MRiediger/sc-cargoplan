namespace CargoPlan.Import.ContractTracker;

/// <summary>What one read found: the new candidates and which known captures are still there, or a notice why nothing was read.</summary>
/// <param name="Notice">For the player; null if the read went through or is simply to be tried again later.</param>
/// <param name="Present">The known ids still in ContractTracker; null if nothing was read, so nothing counts as deleted.</param>
/// <param name="DatabaseLooksNew">
/// The highest id the database ever gave is below the watermark (D44): a new file, whose ids started again at 1. Read from
/// the start then, and none of the known ids counts as present: they belong to the old file.
/// </param>
public sealed record ContractTrackerReadResult(
    IReadOnlyList<ContractTrackerCandidate> Candidates, string? Notice, IReadOnlySet<long>? Present = null, bool DatabaseLooksNew = false)
{
    internal static ContractTrackerReadResult Nothing { get; } = new([], null);
}
