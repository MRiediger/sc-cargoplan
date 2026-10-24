namespace CargoPlan.Core.Planning;

/// <summary>
/// How far a move had to go to find room (D65), in the order the stages are tried; later stages move more than the
/// dragged cargo. <see cref="Pending"/> and <see cref="Refused"/> have no plan.
/// </summary>
public enum MoveStage
{
    /// <summary>Nothing to move: the cargo is where the gesture puts it.</summary>
    Unchanged,

    /// <summary>At the cell aimed at, fallen along the stack axis onto the next crate or the plate.</summary>
    Gravity,

    /// <summary>The nearest free, supported place in the target rack; nothing else moves.</summary>
    FreeSpot,

    /// <summary>The target rack laid out anew with the cargo; other crates there move within it.</summary>
    Relayout,

    /// <summary>Other destinations' cargo left the target rack to make room.</summary>
    Displace,

    /// <summary>The cargo went into another rack.</summary>
    Elsewhere,

    /// <summary>Asked to stop before <see cref="Relayout"/>, and the move needs it: the drop will tell.</summary>
    Pending,

    /// <summary>No rack takes the cargo.</summary>
    Refused,
}
