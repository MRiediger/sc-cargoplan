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
    /// corner at <paramref name="Position"/> of <paramref name="RackId"/>. Also the parameter of <c>MoveCratesCommand</c>;
    /// <see cref="CrateDrop"/> turns it into Core's moves.
    /// </summary>
    public sealed record MoveCrates(Crate Anchor, IReadOnlyList<Crate> Crates, string RackId, GridPosition Position) : Gesture;

    /// <summary>Put every crate of a stop on board into a rack (D25), dragged from its legend line; the parameter of <c>MoveStopCommand</c>.</summary>
    public sealed record MoveStop(string Destination, string TargetRackId) : Gesture;

    public sealed record None : Gesture;
}

/// <summary>
/// The mouse rules of the ship view as one pure table (D20, D25). Left: orbit when dragged, anywhere; a click on cargo
/// ticks its stop. Right: dragged from a crate still on board, it moves that crate to the cell under the cursor
/// (<see cref="DropTarget"/>); dragged from anywhere else, it pans. A stop line dragged from the legend moves the whole
/// stop to the rack under the cursor. The control asks on every move (preview, or pan) and on release.
/// </summary>
public static class DragGesture
{
    /// <param name="down">What lay under the cursor when the button went down; null off the racks.</param>
    /// <param name="moved">The drag has left the system drag rectangle; until then a release is a click.</param>
    /// <param name="current">What lies under the cursor now, or on release.</param>
    /// <param name="drop">The cell under the cursor for the dragged crate; null if there is none.</param>
    public static Gesture Resolve(MouseButton button, SceneHit? down, bool moved, SceneHit? current, Drop? drop) => button switch
    {
        MouseButton.Left when moved => new Gesture.Orbit(),
        MouseButton.Left when current?.Placement is { } placement => new Gesture.Click(placement.Destination),
        MouseButton.Right when !moved => new Gesture.None(),
        MouseButton.Right when DraggedCrate(button, down) is { } crate =>
            drop is { } target && (target.RackId != crate.RackId || target.Position != crate.Position)
                ? new Gesture.MoveCrates(crate, [crate], target.RackId, target.Position)
                : new Gesture.None(),
        MouseButton.Right => new Gesture.Pan(),
        _ => new Gesture.None(),
    };

    /// <summary>
    /// The crate a press picks up: right button on a crate still on board. Delivered crates are off the ship (D16), so
    /// a drag from them pans like one from free space.
    /// </summary>
    public static Crate? DraggedCrate(MouseButton button, SceneHit? down) =>
        button == MouseButton.Right && down?.Crate is { Delivered: false } crate ? crate : null;

    /// <summary>A stop line dragged from the legend: onto any face of a rack it moves the stop there; anywhere else, nothing.</summary>
    public static Gesture ResolveStop(string destination, bool moved, SceneHit? current) =>
        moved && current is { } target ? new Gesture.MoveStop(destination, target.RackId) : new Gesture.None();
}
