using CargoPlan.Core.Models;
using CargoPlan.Core.Routing;

namespace CargoPlan.App.Routing;

/// <summary>
/// What the loop ranking is computed from (T13.4, D124): the Plan tab's state when it asked. The caller runs
/// <see cref="Run"/> off the UI thread and hands the result back with these same inputs; the tab drops a result whose
/// inputs are no longer its own (<see cref="RoutesViewModel.ShowLoops"/>).
/// </summary>
/// <param name="Hub">routes.hub: where the player is; the travel is measured from it, the system-wide ring starts there.</param>
public sealed record LoopInputs(
    IReadOnlyList<HaulTemplate> Templates, Positions Positions, ObservedEdges Edges, Ship Ship, HaulRank Rank, HaulFaction? Faction, string Hub)
{
    /// <summary>Every loop candidate (<see cref="Loops.For"/>): pure, a few hundred milliseconds of load planner.</summary>
    public IReadOnlyList<LoopCandidate> Run() => Loops.For(Templates, Positions, Edges, Ship, Rank, Faction, Hub);
}
