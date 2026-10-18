using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>File format of a rack. The index is not stored; it comes from the position in the array.</summary>
internal sealed record RackDto(string Id, int CapacityScu, GridDto Grid, PositionDto Position, IReadOnlyList<int> Origin)
{
    private const int OriginAxes = 3;

    public Rack ToRack(int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));

        // Rack validates this too, but without naming the rack.
        if (CapacityScu <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CapacityScu), CapacityScu, $"Rack '{Id}' needs a positive capacityScu.");
        }

        return new Rack(Id, index, CapacityScu, Grid.ToDimensions(Id), Position.ToPosition(Id), ToOrigin());
    }

    // STJ already rejects null and non-integers; only the length is left.
    private ShipPoint ToOrigin() =>
        Origin is [var x, var y, var z]
            ? new ShipPoint(x, y, z)
            : throw new ArgumentException(
                Invariant($"Rack '{Id}': origin needs {OriginAxes} values [x, y, z], got {Origin.Count}."), nameof(Origin));

    internal static void ThrowOnDuplicateIds(IEnumerable<string> ids, string owner)
    {
        var duplicate = ids.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Id '{duplicate.Key}' is used more than once in {owner}.");
        }
    }
}
