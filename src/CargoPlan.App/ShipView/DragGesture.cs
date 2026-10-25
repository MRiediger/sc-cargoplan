using System.Windows.Input;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>What a press, drag and release in the ship view means. One case per outcome; compare with <c>is</c>.</summary>
public abstract record Gesture
{
    /// <summary>Tick the stop off or back (D16).</summary>
    public sealed record Click(string Destination) : Gesture;

    public sealed record Orbit : Gesture;

    public sealed record Pan : Gesture;

    /// <summary>
    /// Put <paramref name="Crates"/> on cells (D25): <paramref name="Anchor"/>, the crate the drag picked up, with its min
    /// corner at <paramref name="Position"/> of <paramref name="RackId"/>, turned 90° if <paramref name="Turn"/> (the wheel,
    /// only a crate dragged alone, D71). Also the parameter of <c>MoveCratesCommand</c>; <see cref="CrateDrop"/> turns it
    /// into Core's moves.
    /// </summary>
    public sealed record MoveCrates(Crate Anchor, IReadOnlyList<Crate> Crates, string RackId, GridPosition Position, bool Turn = false) : Gesture;

    /// <summary>
    /// Put <paramref name="Crates"/> into a rack without a cell (D25, B6): a legend line's or a mission header's crates on
    /// board, dragged from the legend; the parameter of <c>MoveLegendCommand</c>.
    /// </summary>
    public sealed record MoveLegend(IReadOnlyList<Crate> Crates, string TargetRackId) : Gesture;

    /// <summary>Add the crate to the selection, or take it out (D25).</summary>
    public sealed record ToggleSelect(Crate Crate) : Gesture;

    public sealed record ClearSelection : Gesture;

    public sealed record None : Gesture;
}

/// <summary>
/// The mouse rules of the ship view as one pure table (D20, D25). Left: orbit when dragged, anywhere; a click on cargo
/// ticks its stop. Right: a click on a crate still on board selects it or takes it out, on free or empty space clears
/// the selection; dragged from a crate still on board, it moves that crate (with the selection, if the crate is part of
/// it) to the cell under the cursor (<see cref="DropTarget"/>); dragged from anywhere else, it pans. A line or header
/// dragged from the legend moves its crates to the rack under the cursor. The wheel turns a crate dragged alone (D71).
/// The control asks on every move (preview, or pan) and on release.
/// </summary>
public static class DragGesture
{
    /// <param name="down">What lay under the cursor when the button went down; null off the racks.</param>
    /// <param name="moved">The drag has left the system drag rectangle; until then a release is a click.</param>
    /// <param name="current">What lies under the cursor now, or on release.</param>
    /// <param name="drop">The cell under the cursor for the crate the drag picked up; null if there is none.</param>
    /// <param name="selection">The crates selected in the current scene (<see cref="CrateSelection"/>).</param>
    /// <param name="turned">The wheel turned the crate an odd number of notches (<see cref="CanTurn"/>); a square crate looks the same.</param>
    public static Gesture Resolve(MouseButton button, SceneHit? down, bool moved, SceneHit? current, Drop? drop, IReadOnlySet<Crate> selection, bool turned = false) =>
        button switch
        {
            MouseButton.Left when moved => new Gesture.Orbit(),
            MouseButton.Left when current?.Placement is { } placement => new Gesture.Click(placement.Destination),

            // Delivered crates are off the ship (D16): they can't be picked, and a click on them keeps the selection.
            MouseButton.Right when !moved => down?.Crate switch
            {
                { Delivered: false } crate => new Gesture.ToggleSelect(crate),
                { Delivered: true } => new Gesture.None(),
                null => new Gesture.ClearSelection(),
            },
            MouseButton.Right when down?.Crate is { Delivered: false } anchor => MoveFrom(anchor, DraggedCrates(button, down, selection), drop, turned),
            MouseButton.Right => new Gesture.Pan(),
            _ => new Gesture.None(),
        };

    /// <summary>
    /// The crates a press picks up: right button on a crate still on board takes the whole selection if the crate is
    /// part of it, otherwise that crate alone. Delivered crates are off the ship (D16), so a drag from them pans like
    /// one from free space. The pressed crate comes first. Empty if the press picks nothing up.
    /// </summary>
    public static IReadOnlyList<Crate> DraggedCrates(MouseButton button, SceneHit? down, IReadOnlySet<Crate> selection) =>
        button == MouseButton.Right && down?.Crate is { Delivered: false } crate
            ? selection.Contains(crate) ? [crate, .. selection.Where(c => c != crate)] : [crate]
            : [];

    // Back onto its own cell is no move, unless the wheel turned an oblong crate dragged alone.
    private static Gesture MoveFrom(Crate anchor, IReadOnlyList<Crate> crates, Drop? drop, bool turned)
    {
        var turn = turned && anchor.CrateSize.Turns && CanTurn(crates, isLegend: false);
        return drop is { } target && (target.RackId != anchor.RackId || target.Position != anchor.Position || turn)
            ? new Gesture.MoveCrates(anchor, crates, target.RackId, target.Position, turn)
            : new Gesture.None();
    }

    /// <summary>
    /// True if the wheel turns the drag's crate instead of zooming (D71): one crate picked up in the view. A selection
    /// keeps how its crates lie (B3), a legend drag leaves it to the planner.
    /// </summary>
    public static bool CanTurn(IReadOnlyList<Crate> dragged, bool isLegend) => !isLegend && dragged.Count == 1;

    /// <summary>A legend line or header dragged: onto any face of a rack it moves its crates there; anywhere else, nothing.</summary>
    public static Gesture ResolveLegend(IReadOnlyList<Crate> crates, bool moved, SceneHit? current) =>
        moved && current is { } target ? new Gesture.MoveLegend(crates, target.RackId) : new Gesture.None();
}
