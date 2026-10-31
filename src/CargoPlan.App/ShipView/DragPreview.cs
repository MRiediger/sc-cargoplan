using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.ShipView;

/// <summary>
/// What a running drag shows (D25 addendum B3, D65), the 2D view's rules in one pure place (T8.4): the scene fades
/// <paramref name="Faded"/>, the overlay draws <paramref name="TargetRackId"/>'s edge, the cyan
/// <paramref name="Ghosts"/> and the red <paramref name="Refused"/> ghosts on top.
/// </summary>
/// <param name="TargetRackId">The rack to mark: a legend drag's always, a crate drag's only while the drop will lay it out (<see cref="MoveStage.Pending"/>).</param>
/// <param name="Faded">The crates held and every crate the outcome moves, where they are now.</param>
/// <param name="Ghosts">Where Core puts every crate that changes place, dragged or moved to make room.</param>
/// <param name="Refused">The cells aimed at, only for a refusal; the crate held alone if the selection reaches past every rack.</param>
public sealed record DragPreview(string? TargetRackId, IReadOnlySet<Crate> Faded, IReadOnlyList<Crate> Ghosts, IReadOnlyList<Crate> Refused)
{
    public static DragPreview None { get; } = new(null, new HashSet<Crate>(), [], []);

    /// <param name="dragged">The crates the drag holds (<see cref="DragGesture.DraggedCrates"/>, or a legend line's).</param>
    /// <param name="outcome">Core's outcome of dropping here (<see cref="CrateDrop"/>); null for no move.</param>
    public static DragPreview Of(Ship ship, LoadPlan plan, IReadOnlyList<Crate> dragged, Gesture? gesture, MoveOutcome? outcome)
    {
        var target = gesture switch
        {
            Gesture.MoveLegend legend => legend.TargetRackId,
            Gesture.MoveCrates crates when outcome?.Stage == MoveStage.Pending => crates.RackId,
            _ => null,
        };

        var changed = outcome is { } preview ? CrateDrop.Changed(plan, preview) : [];
        IReadOnlyList<Crate> refused = gesture is Gesture.MoveCrates move && outcome?.Stage == MoveStage.Refused
            ? [.. (CrateDrop.Moves(ship, move) ?? [new CrateMove(move.Anchor, move.RackId, move.Position)])
                .Select(m => m.Crate with { RackId = m.RackId, Position = m.Position, Rotated = m.Rotated })]
            : [];

        return new DragPreview(target, new HashSet<Crate>([.. dragged, .. changed.Select(c => c.Before)]), [.. changed.Select(c => c.After)], refused);
    }
}
