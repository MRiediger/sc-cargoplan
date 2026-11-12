namespace CargoPlan.Core.Missions;

/// <summary>
/// One scanned contract as it comes into the plan (D103): stops in contract order and where they came from. The import
/// builds it from the scan's text; Core knows nothing of the text.
/// </summary>
public sealed record ContractCapture(IReadOnlyList<MissionStop> Stops, ContractSource Source);
