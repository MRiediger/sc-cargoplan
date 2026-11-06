using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A place in ship coordinates, in cells (x bow, y port, z up, D14), that needs no whole numbers, unlike a rack's
/// <see cref="ShipPoint"/>: where a hologram or an outline lies (D90).
/// </summary>
public sealed record ShipCoordinates(double X, double Y, double Z)
{
    public override string ToString() => Invariant($"[{X}, {Y}, {Z}]");
}
