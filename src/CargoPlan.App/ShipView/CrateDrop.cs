using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>
/// A drag as Core's move and what Core makes of it (D25, D65): one way for the preview while dragging and for the drop,
/// so the ghosts and the result always agree. Plans nothing itself.
/// </summary>
public static class CrateDrop
{
    /// <summary>
    /// How far the preview resolves a drag on every frame (D65): all stages. Set to <see cref="MoveStage.FreeSpot"/> if a
    /// new layout costs more than a frame (16 ms) on Martin's machine (B3, E3); the preview then shows
    /// <see cref="MoveStage.Pending"/> and the drop resolves fully.
    /// </summary>
    public const MoveStage PreviewStage = MoveStage.Refused;

    /// <summary>
    /// The anchor with its min corner at the target cell, the other crates at the same offset to it
    /// (<see cref="CrateMove.Shift"/>); null if one of them lands in no rack. The cells the player aims at, drawn red
    /// where Core refuses. The anchor turns if the wheel turned it (D71); every other crate keeps how it lies.
    /// </summary>
    public static IReadOnlyList<CrateMove>? Moves(Ship ship, Gesture.MoveCrates move) =>
        CrateMove.Shift(ship, move.Crates, move.Anchor, move.RackId, move.Position) is { } moves
            ? [.. moves.Select(m => m.Crate == move.Anchor ? m with { Turn = move.Turn } : m)]
            : null;

    /// <summary>
    /// Where Core puts the dragged crates: <see cref="Moves"/> resolved (<see cref="LoadPlanner.ResolveMove(LoadPlan, Ship, IReadOnlyList{CrateMove}, MoveStage)"/>),
    /// or, if one lands in no rack, the drag without cells (<see cref="LoadPlanner.ResolveMove(LoadPlan, Ship, IReadOnlyList{Crate}, Crate, string, GridPosition, MoveStage)"/>).
    /// </summary>
    public static MoveOutcome Outcome(LoadPlan plan, Ship ship, Gesture.MoveCrates move, MoveStage upTo = MoveStage.Refused) =>
        Moves(ship, move) is { } moves
            ? LoadPlanner.ResolveMove(plan, ship, moves, upTo)
            : LoadPlanner.ResolveMove(plan, ship, move.Crates, move.Anchor, move.RackId, move.Position, upTo);

    /// <summary>Where Core puts the crates of a legend line or header dragged onto a rack (<see cref="LoadPlanner.ResolveMoveToRack"/>).</summary>
    public static MoveOutcome LegendOutcome(LoadPlan plan, Ship ship, Gesture.MoveLegend move, MoveStage upTo = MoveStage.Refused) =>
        LoadPlanner.ResolveMoveToRack(plan, ship, move.Crates, move.TargetRackId, upTo);

    /// <summary>
    /// What the preview of a crate drag depends on (E2): target rack and cell, the crate held and where the others land
    /// (<see cref="Moves"/>, null if one lands in no rack). Equal keys give equal outcomes, so the preview resolves again
    /// only when the key changes, not on every mouse move.
    /// </summary>
    public static Key KeyOf(Ship ship, Gesture.MoveCrates move) => new(move.Anchor, move.RackId, move.Position, Moves(ship, move));

    /// <summary>Every crate the outcome puts into another rack or cell than <paramref name="plan"/>, or turns: the ghosts of the preview.</summary>
    public static IReadOnlyList<(Crate Before, Crate After)> Changed(LoadPlan plan, MoveOutcome outcome) =>
        outcome.Plan is { } after
            ? [.. plan.Crates.Zip(after.Crates).Where(p => p.First.RackId != p.Second.RackId || p.First.Position != p.Second.Position || p.First.Rotated != p.Second.Rotated)]
            : [];

    /// <summary>
    /// The preview's tool tip: why Core refuses, that the target rack will be laid out on release, or what moves besides
    /// the dragged cargo; null if nothing needs saying.
    /// </summary>
    public static string? Text(MoveOutcome outcome, Rack target) => outcome.Stage switch
    {
        MoveStage.Refused => outcome.Refusal,
        MoveStage.Pending => Invariant($"Rack {target.Number} will be rearranged"),
        _ => outcome.Notices.Count == 0 ? null : string.Join(Environment.NewLine, outcome.Notices),
    };

    /// <summary>See <see cref="KeyOf"/>; compares the landing cells by value.</summary>
    public sealed record Key(Crate Anchor, string RackId, GridPosition Position, IReadOnlyList<CrateMove>? Moves)
    {
        public bool Equals(Key? other) =>
            other is not null && Anchor == other.Anchor && RackId == other.RackId && Position == other.Position
            && (Moves is null ? other.Moves is null : other.Moves is not null && Moves.SequenceEqual(other.Moves));

        public override int GetHashCode() => HashCode.Combine(Anchor, RackId, Position, Moves?.Count);
    }
}
