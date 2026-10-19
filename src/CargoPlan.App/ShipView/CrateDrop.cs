using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.ShipView;

/// <summary>
/// A crate drag as Core's moves, and why Core would refuse it: one way for the preview while dragging and for the drop,
/// so the ghosts' colour and the result always agree (D25). Plans nothing itself.
/// </summary>
public static class CrateDrop
{
    /// <summary>The refusal when a dragged selection reaches past every rack: there is no cell to ask Core about.</summary>
    public const string OutsideRacks = "A selected crate would land outside the racks.";

    /// <summary>
    /// The anchor with its min corner at the target cell, the other crates at the same offset to it in ship coordinates
    /// (<see cref="CrateMove.Shift"/>); null if one of them lands in no rack.
    /// </summary>
    public static IReadOnlyList<CrateMove>? Moves(Ship ship, Gesture.MoveCrates move) =>
        CrateMove.Shift(ship, move.Crates, move.Anchor, move.RackId, move.Position);

    /// <summary>Core's reason to refuse the drop (<see cref="LoadPlanner.MoveProblem"/>), or <see cref="OutsideRacks"/>; null if it fits.</summary>
    public static string? Problem(LoadPlan plan, Ship ship, Gesture.MoveCrates move) =>
        Moves(ship, move) is { } moves ? LoadPlanner.MoveProblem(plan, ship, moves) : OutsideRacks;
}
