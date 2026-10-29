using CargoPlan.Core.Missions;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of a ContractTracker capture (D39); <see cref="ListedDestinations"/> left out without a list,
/// <see cref="Reward"/> left out when unknown (older plans have none, which reads as unknown); <see cref="Gone"/> and
/// <see cref="Detached"/> written only as true (schema 8, D81).
/// </summary>
internal sealed record ContractSourceDto(
    IReadOnlyList<long> ContractIds, bool CrateSizeAssumed, int? ListedDestinations = null, long? Reward = null, bool? Gone = null, bool? Detached = null)
{
    /// <exception cref="ArgumentException">Detached without gone.</exception>
    public ContractSource ToContractSource() => new(ContractIds, CrateSizeAssumed, ListedDestinations, Reward, Gone ?? false, Detached ?? false);

    public static ContractSourceDto FromContractSource(ContractSource source) =>
        new(source.ContractIds, source.CrateSizeAssumed, source.ListedDestinations, source.Reward, source.Gone ? true : null, source.Detached ? true : null);
}
