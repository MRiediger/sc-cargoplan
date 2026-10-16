using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>File format of a stop. The order is not stored; it comes from the position in the array.</summary>
internal sealed record StopDto(string Destination, IReadOnlyList<CargoItemDto> Items)
{
    public Stop ToStop(int order)
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

        return new Stop(Destination, order, [.. Items.Select(i => i.ToCargoItem(Destination))]);
    }

    public static StopDto FromStop(Stop stop) =>
        new(stop.Destination, [.. stop.Items.Select(CargoItemDto.FromCargoItem)]);
}
