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

    /// <summary>Put the placement into another rack (D20); also the parameter of <c>MovePlacementCommand</c>.</summary>
    public sealed record Move(Placement Placement, string TargetRackId) : Gesture;

    public sealed record None : Gesture;
}

/// <summary>
/// The mouse rules of the ship view as one pure table (D20). Left: orbit when dragged, anywhere; a click on cargo
/// ticks its stop. Right: dragged from cargo still on board, it moves that placement to the rack under the cursor;
/// dragged from anywhere else, it pans. The control asks on every move (highlight the target, or pan) and on release.
/// </summary>
public static class DragGesture
{
    /// <param name="down">What lay under the cursor when the button went down; null off the racks.</param>
    /// <param name="moved">The drag has left the system drag rectangle; until then a release is a click.</param>
    /// <param name="current">What lies under the cursor now, or on release.</param>
    public static Gesture Resolve(MouseButton button, SceneHit? down, bool moved, SceneHit? current) => button switch
    {
        MouseButton.Left when moved => new Gesture.Orbit(),
        MouseButton.Left when current?.Placement is { } placement => new Gesture.Click(placement.Destination),
        MouseButton.Right when !moved => new Gesture.None(),
        MouseButton.Right when DraggedPlacement(button, down) is { } placement =>
            current is { } target && target.RackId != placement.RackId ? new Gesture.Move(placement, target.RackId) : new Gesture.None(),
        MouseButton.Right => new Gesture.Pan(),
        _ => new Gesture.None(),
    };

    /// <summary>
    /// The placement a press picks up: right button on cargo still on board. Delivered crates are off the ship (D16),
    /// so a drag from them pans like one from free space.
    /// </summary>
    public static Placement? DraggedPlacement(MouseButton button, SceneHit? down) =>
        button == MouseButton.Right && down?.Placement is { Delivered: false } placement ? placement : null;
}
