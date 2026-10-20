using CargoPlan.Core.Missions;

namespace CargoPlan.Core.Plans;

/// <summary>File format of a ContractTracker capture (D39); <see cref="ListedDestinations"/> left out without a list.</summary>
internal sealed record ContractSourceDto(IReadOnlyList<long> ContractIds, bool CrateSizeAssumed, int? ListedDestinations = null)
{
    public ContractSource ToContractSource() => new(ContractIds, CrateSizeAssumed, ListedDestinations);

    public static ContractSourceDto FromContractSource(ContractSource source) =>
        new(source.ContractIds, source.CrateSizeAssumed, source.ListedDestinations);
}
