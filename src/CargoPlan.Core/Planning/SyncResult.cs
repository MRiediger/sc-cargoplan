using CargoPlan.Core.Missions;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Planning;

/// <summary>
/// A sync (<see cref="ContractSync.Sync"/>) and what it did with contracts gone from ContractTracker (D81), for the notice.
/// </summary>
/// <param name="Saved">The plan after the sync; the one passed in if nothing changed.</param>
/// <param name="Gone">Missions gone in this sync, or gone before and taken out now (remove), in plan order.</param>
/// <param name="Back">Missions ContractTracker lists again, as they were before the sync.</param>
/// <param name="AllAtOnce">Every contract of the plan went in one read, or the database is new: kept, whatever the policy.</param>
public sealed record SyncResult(SavedPlan Saved, IReadOnlyList<GoneReport> Gone, IReadOnlyList<Mission> Back, bool AllAtOnce);

/// <summary>
/// One mission gone from ContractTracker: the deliveries a sync took out (planned, delivered) and kept (loaded), and
/// whether the mission left the plan. All zero and not left: kept, marked gone.
/// </summary>
/// <param name="Mission">The mission as it was before the sync: its name, ids and rows added by hand.</param>
public sealed record GoneReport(Mission Mission, int Planned, int Delivered, int Loaded, bool Left);
