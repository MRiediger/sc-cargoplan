namespace CargoPlan.Core.Missions;

/// <summary>
/// One ContractTracker capture as the sync takes it into the plan (D39 addendum): stops in contract order and where they
/// came from. The import builds it; Core knows nothing of ContractTracker's database.
/// </summary>
public sealed record ContractCapture(IReadOnlyList<MissionStop> Stops, ContractSource Source);
