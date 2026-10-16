using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Ships;

/// <summary>File format of a slot. Crate sizes are written as SCU numbers (1, 2, 4, 8).</summary>
internal sealed record SlotDto(string Id, IReadOnlyList<int> AllowedSizes)
{
    public Slot ToSlot()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Id, nameof(Id));
        if (AllowedSizes.Count == 0)
        {
            throw new ArgumentException($"Slot '{Id}' allows no crate size.", nameof(AllowedSizes));
        }

        return new Slot(Id, AllowedSizes.Select(ToCrateSize).ToHashSet());
    }

    // Plain int in the DTO, because STJ would accept any number for an enum (e.g. 3).
    private CrateSize ToCrateSize(int scu) =>
        Enum.IsDefined((CrateSize)scu)
            ? (CrateSize)scu
            : throw new ArgumentOutOfRangeException(nameof(AllowedSizes), scu, Invariant($"Slot '{Id}': {scu} SCU is not a crate size."));
}
