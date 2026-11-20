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
    public static IReadOnlyList<Rack[]>? Of(IReadOnlyList<Rack> racks, Sides sides)
    {
        if (sides == Sides.None)
        {
            return null;
        }

        Rack[] ordered = [.. racks.OrderBy(r => r.Index)];
        Rack[] port = [.. ordered.Where(r => r.Bounds.Min.Y >= 0)];
        Rack[] starboard = [.. ordered.Where(r => r.Bounds.Max.Y <= 0)];
        if (port.Length == 0 || starboard.Length == 0)
        {
            return null;
        }

        Rack[] middle = [.. ordered.Where(r => r.Bounds.Min.Y < 0 && r.Bounds.Max.Y > 0)];
        Rack[][] first = sides == Sides.PortFirst ? [port, starboard] : [starboard, port];
        return middle.Length == 0 ? first : [.. first, middle];
    }

    /// <summary>The groups of <see cref="Of"/>, or all racks in rack order as one where there are none.</summary>
    public static IReadOnlyList<Rack[]> OrAll(IReadOnlyList<Rack> racks, Sides sides) =>
        Of(racks, sides) ?? [[.. racks.OrderBy(r => r.Index)]];

    /// <summary>
    /// Where a destination's crates are tried, by the D41 order within the side order: the racks holding it, then of each
    /// group its first empty rack and the rest. With one group the D41 order itself (holding, the first empty, rack order).
    /// </summary>
    public static IEnumerable<T> Candidates<T>(IEnumerable<IEnumerable<T>> groups, Func<T, bool> holding, Func<T, bool> empty)
    {
        var all = groups.Select(g => g.ToArray()).ToArray();
        return all.SelectMany(g => g).Where(holding)
            .Concat(all.SelectMany(g => g.Where(empty).Take(1).Concat(g)))
            .Distinct();
    }
}
