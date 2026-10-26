using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A ship layout. The rack capacities must add up to <see cref="TotalScu"/>. The optional data describes the ship, it
/// doesn't change the plan (D56): <see cref="Manufacturer"/> code such as "MISC", <see cref="FleetyardsSlug"/> to match
/// other Fleetyards data such as the outline, <see cref="Source"/> of the values, free <see cref="Notes"/>, and
/// <see cref="HoloOffset"/>, cells the hologram moves from its middle on the racks (K5.3b, D63; null = none).
/// </summary>
public sealed record Ship(
    string Id, string Name, int TotalScu, IReadOnlyList<Rack> Racks,
    string? Manufacturer = null, string? FleetyardsSlug = null, string? Source = null, string? Notes = null,
    ShipPoint? HoloOffset = null)
{
    public int TotalScu { get; } = TotalScu;

    // Copied so a caller can't change the racks after validation.
    public IReadOnlyList<Rack> Racks { get; } = Validated(Id, TotalScu, [.. Racks]);

    /// <summary>The box around all racks, the frame for drawing the ship. Needs at least one rack (the loader ensures that).</summary>
    public ShipBox Bounds => Racks.Select(r => r.Bounds).Aggregate((a, b) => a.Union(b));

    private static IReadOnlyList<Rack> Validated(string id, int totalScu, IReadOnlyList<Rack> racks)
    {
        var sum = racks.Sum(r => r.CapacityScu);
        if (sum != totalScu)
        {
            throw new ArgumentException(
                Invariant($"Racks of ship '{id}' sum to {sum} SCU, but TotalScu is {totalScu}."),
                nameof(Racks));
        }

        return racks;
    }
}
