using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.ShipView;

/// <summary>
/// A crate drag as Core's moves, and why Core would refuse it: one way for the preview while dragging and for the drop,
/// so the ghost's colour and the result always agree (D25). Plans nothing itself.
/// </summary>
public static class CrateDrop
{
    /// <summary>The dragged crate with its min corner at the target cell. A drag holds one crate, its anchor, until a selection can be dragged.</summary>
    public static IReadOnlyList<CrateMove> Moves(Gesture.MoveCrates move) => [new CrateMove(move.Anchor, move.RackId, move.Position)];

    /// <summary>Core's reason to refuse the drop (<see cref="LoadPlanner.MoveProblem"/>); null if it fits.</summary>
    public static string? Problem(LoadPlan plan, Ship ship, Gesture.MoveCrates move) => LoadPlanner.MoveProblem(plan, ship, Moves(move));
}
