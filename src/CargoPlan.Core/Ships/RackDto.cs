using CargoPlan.Core.Models;

namespace CargoPlan.Core.Ships;

/// <summary>File format of a rack. The index is not stored; it comes from the position in the array.</summary>
internal sealed record RackDto(string Id, int CapacityScu, IReadOnlyList<SlotDto> Slots)
{
    public Rack ToRack(int index)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));

        // Rack validates this too, but without naming the rack.
        if (CapacityScu <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CapacityScu), CapacityScu, $"Rack '{Id}' needs a positive capacityScu.");
        }

        ThrowOnDuplicateIds(Slots.Select(s => s.Id), $"rack '{Id}'");

        return new Rack(Id, index, CapacityScu, [.. Slots.Select(s => s.ToSlot())]);
    }

    internal static void ThrowOnDuplicateIds(IEnumerable<string> ids, string owner)
    {
        var duplicate = ids.GroupBy(id => id, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException($"Id '{duplicate.Key}' is used more than once in {owner}.");
        }
    }
}
