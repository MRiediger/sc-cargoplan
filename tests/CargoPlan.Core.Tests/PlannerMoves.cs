using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// One crate put at a cell, for tests: <see cref="LoadPlanner.MoveCrates"/> with one move. The app always moves through
/// MoveCrates, so the planner's API has no single-crate wrapper (K10, F13). The App and Import tests link this file.
/// </summary>
internal static class PlannerMoves
{
    public static LoadPlan MoveCrate(LoadPlan plan, Ship ship, Crate crate, string targetRackId, GridPosition targetPosition) =>
        LoadPlanner.MoveCrates(plan, ship, [new CrateMove(crate, targetRackId, targetPosition)]);
}
