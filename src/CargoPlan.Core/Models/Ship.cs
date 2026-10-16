using static System.FormattableString;

namespace CargoPlan.Core.Models;

/// <summary>A ship layout. The rack capacities must add up to <see cref="TotalScu"/>.</summary>
public sealed record Ship(string Id, string Name, int TotalScu, IReadOnlyList<Rack> Racks)
{
    public int TotalScu { get; } = TotalScu;

    // Copied so a caller can't change the racks after validation.
    public IReadOnlyList<Rack> Racks { get; } = Validated(Id, TotalScu, [.. Racks]);

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
