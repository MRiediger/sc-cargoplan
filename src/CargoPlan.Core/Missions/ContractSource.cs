namespace CargoPlan.Core.Missions;

/// <summary>
/// Where a mission's contract came from: a scan by CargoPlan itself (D102), its number and content key the identity, and
/// what the legend marks as uncertain. An older plan file's ContractTracker capture is read as a scan too (D107).
/// </summary>
/// <param name="Scan">The scan that read it (D102); for an older file's capture its number and a legacy key (D107).</param>
/// <param name="CrateSizeAssumed">The text named no max crate size; the legend shows "4 SCU?".</param>
/// <param name="ListedDestinations">Entries of the contract's drop-off list; null if it has none. More than the
/// mission's stops: the legend shows "⚠ 1 of 3 destinations".</param>
/// <param name="Reward">The contract's reward in aUEC; null if the text had none or it wasn't readable. Two contracts
/// with different rewards are never a duplicate (D39 addendum).</param>
public sealed record ContractSource(ScanIdentity Scan, bool CrateSizeAssumed = false, int? ListedDestinations = null, long? Reward = null);
