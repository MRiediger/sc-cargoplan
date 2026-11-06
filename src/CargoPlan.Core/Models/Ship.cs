using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>
/// A ship layout. The rack capacities must add up to <see cref="TotalScu"/>. The optional data describes the ship, it
/// doesn't change the plan (D56): <see cref="Manufacturer"/> code such as "MISC", <see cref="FleetyardsSlug"/> to match
/// other Fleetyards data such as the outline, <see cref="Source"/> of the values, free <see cref="Notes"/>.
/// <see cref="HoloOrigin"/> and <see cref="OutlineOrigin"/> say where the hologram's and the outline's origins lie in
/// ship coordinates, <see cref="OutlineScale"/> the outline's factor on its true size (null = 1); no rack moves them
/// (D90). A file without them keeps the older rule on the racks' middle, with <see cref="HoloOffset"/> on top (K5.3b).
/// </summary>
public sealed record Ship(
    string Id, string Name, int TotalScu, IReadOnlyList<Rack> Racks,
    string? Manufacturer = null, string? FleetyardsSlug = null, string? Source = null, string? Notes = null,
    ShipPoint? HoloOffset = null,
    ShipCoordinates? HoloOrigin = null, ShipCoordinates? OutlineOrigin = null, double? OutlineScale = null)
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
