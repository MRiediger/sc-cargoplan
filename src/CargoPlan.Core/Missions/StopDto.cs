using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>File format of a stop in schema version 1 (read only). The order comes from the position in the array.</summary>
internal sealed record StopDto(string Destination, IReadOnlyList<CargoItemDto> Items)
{
    public MissionStop ToMissionStop(int order)
    {
        if (string.IsNullOrWhiteSpace(Destination))
        {
            throw new ArgumentException(Invariant($"Stop {order} has no destination."), nameof(Destination));
        }

        // Stop allows no items (T1.1); a file must not, an empty stop is always a mistake.
        if (Items.Count == 0)
        {
            throw new ArgumentException($"Stop '{Destination}' has no items.", nameof(Items));
        }

        return new MissionStop(Destination, order, [.. Items.Select(i => ToMissionItem(i.ToCargoItem(Destination)))]);
    }

    /// <summary>
    /// A version 1 item names its crates exactly: SCU / crate size crates of one size. So the max is that size and the
    /// crates are an explicit list, which stays the same even if the derivation rule changes (D23).
    /// </summary>
    private static MissionItem ToMissionItem(CargoItem item) =>
        new(item.Commodity, item.Scu, item.CrateSize, [.. Enumerable.Repeat(item.CrateSize, item.Scu / (int)item.CrateSize)]);
}
