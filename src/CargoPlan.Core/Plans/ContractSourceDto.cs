using System.Globalization;
using CargoPlan.Core.Missions;

namespace CargoPlan.Core.Plans;

/// <summary>
/// File format of where a mission's contract came from: a scan (schema 10, D102) with its <c>scan</c>.
/// <see cref="ListedDestinations"/> left out without a list, <see cref="Reward"/> left out when unknown. An older file's
/// ContractTracker capture (D39) has <c>contractIds</c> instead, and maybe <see cref="Gone"/> and <see cref="Detached"/>
/// (schema 8, D81): read as a scan with a legacy key (D107), never written.
/// </summary>
internal sealed record ContractSourceDto(
    bool CrateSizeAssumed,
    IReadOnlyList<long>? ContractIds = null,
    int? ListedDestinations = null,
    long? Reward = null,
    bool? Gone = null,
    bool? Detached = null,
    ScanDto? Scan = null)
{
    /// <summary>
    /// How the key of an older file's capture starts (D107): "legacy:47-48". No content key (D101) starts so: those are
    /// upper case and hold " | " between their parts.
    /// </summary>
    public const string LegacyKeyPrefix = "legacy:";

    /// <param name="legacyNo">The scan number a capture gets (D107); not used for a scan.</param>
    /// <exception cref="ArgumentException">
    /// A scan beside ContractTracker's ids, gone or detached; a capture without ids; a scan number below 1 or an empty key.
    /// </exception>
    public ContractSource ToContractSource(int legacyNo)
    {
        if (Scan is { } scan)
        {
            return ContractIds is null && Gone is null && Detached is null
                ? new ContractSource(scan.ToScanIdentity(), CrateSizeAssumed, ListedDestinations, Reward)
                : throw new ArgumentException("A scanned contract has no ContractTracker ids and is never gone or detached.", nameof(Scan));
        }

        // The ids become the key; gone and detached go, and the amounts are the ones the player works with, no estimate.
        return ContractIds is [_, ..] ids
            ? new ContractSource(new ScanIdentity(legacyNo, LegacyKey(ids), false), CrateSizeAssumed, ListedDestinations, Reward)
            : throw new ArgumentException("A contract without a scan names its ContractTracker ids (a file before v0.4.0).", nameof(ContractIds));
    }

    public static ContractSourceDto FromContractSource(ContractSource source) =>
        new(source.CrateSizeAssumed, ListedDestinations: source.ListedDestinations, Reward: source.Reward, Scan: ScanDto.FromScanIdentity(source.Scan));

    /// <summary>"legacy:" and the ids in ascending order, each once, joined by "-".</summary>
    private static string LegacyKey(IEnumerable<long> ids) =>
        LegacyKeyPrefix + string.Join('-', ids.Distinct().Order().Select(id => id.ToString(CultureInfo.InvariantCulture)));
}

/// <summary>File format of a scan's identity (schema 10, D102): <c>{ "no": 3, "key": "…", "estimated": true }</c>.</summary>
internal sealed record ScanDto(int No, string Key, bool Estimated)
{
    /// <exception cref="ArgumentException">A number below 1 or an empty key.</exception>
    public ScanIdentity ToScanIdentity() => new(No, Key, Estimated);

    public static ScanDto FromScanIdentity(ScanIdentity scan) => new(scan.No, scan.Key, scan.QuantitiesEstimated);
}
