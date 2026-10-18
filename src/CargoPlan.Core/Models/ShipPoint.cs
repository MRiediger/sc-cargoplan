using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A corner in ship coordinates, measured in cells (1 cell = 1 SCU edge): <see cref="X"/> towards the bow,
/// <see cref="Y"/> towards port, <see cref="Z"/> up (right-handed, D14). Values may be zero or negative.
/// </summary>
public sealed record ShipPoint(int X, int Y, int Z)
{
    // Same notation as "origin" in the ship JSON, so error messages point at the line to fix.
    public override string ToString() => Invariant($"[{X}, {Y}, {Z}]");
}
