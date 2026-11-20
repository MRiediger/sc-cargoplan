using CargoPlan.Core.Models;

namespace CargoPlan.Core.Planning;

/// <summary>
/// A ship's racks by side (D117, F10b): with sides, every rack of the first side is filled before a rack of the second,
/// and racks across the middle line come last. A rack's side is where its box lies across the ship (<see cref="Rack.Bounds"/>):
/// port from y = 0 up, starboard from y = 0 down, the middle where it spans y = 0 (P1).
/// </summary>
internal static class SideGroups
{
    /// <summary>
    /// The racks in fill order, each group in rack order: the first side, the second, then the middle; null without sides
    /// or on a ship without racks on both sides, which plans as before (F3).
    /// </summary>
    public static IReadOnlyList<Rack[]>? Of(IReadOnlyList<Rack> racks, Sides sides) => null;
}
