using System.Text.Json.Serialization;
using CargoPlan.Core.Missions;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of where a mission's contract came from: a ContractTracker capture (D39) with its <c>contractIds</c>, or a
/// scan (schema 10, D102) with its <c>scan</c>, exactly one of the two. <see cref="ListedDestinations"/> left out without a
/// list, <see cref="Reward"/> left out when unknown (older plans have none, which reads as unknown); <see cref="Gone"/> and
/// <see cref="Detached"/> written only as true (schema 8, D81), never for a scan.
/// </summary>
/// <remarks><c>contractIds</c> is optional since schema 10, so it can't stand first in the constructor; the order attribute
/// keeps it first in the file.</remarks>
internal sealed record ContractSourceDto(
    bool CrateSizeAssumed,
    [property: JsonPropertyOrder(-1)] IReadOnlyList<long>? ContractIds = null,
    int? ListedDestinations = null,
    long? Reward = null,
    bool? Gone = null,
    bool? Detached = null,
    ScanDto? Scan = null)
{
    /// <exception cref="ArgumentException">Ids and a scan or neither, detached without gone, or a scan gone.</exception>
    public ContractSource ToContractSource() =>
        new(ContractIds ?? [], CrateSizeAssumed, ListedDestinations, Reward, Gone ?? false, Detached ?? false, Scan?.ToScanIdentity());

    public static ContractSourceDto FromContractSource(ContractSource source) => new(
        source.CrateSizeAssumed,
        source.Scan is null ? source.ContractIds : null,
        source.ListedDestinations,
        source.Reward,
        source.Gone ? true : null,
        source.Detached ? true : null,
        source.Scan is { } scan ? ScanDto.FromScanIdentity(scan) : null);
}

/// <summary>File format of a scan's identity (schema 10, D102): <c>{ "no": 3, "key": "…", "estimated": true }</c>.</summary>
internal sealed record ScanDto(int No, string Key, bool Estimated)
{
    /// <exception cref="ArgumentException">A number below 1 or an empty key.</exception>
    public ScanIdentity ToScanIdentity() => new(No, Key, Estimated);

    public static ScanDto FromScanIdentity(ScanIdentity scan) => new(scan.No, scan.Key, scan.QuantitiesEstimated);
}
