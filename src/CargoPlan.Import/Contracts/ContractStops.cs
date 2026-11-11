using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Import.Contracts;

/// <summary>A contract's deliveries as the plan's stops: the way from parsed text to a mission (D39, D40).</summary>
internal static class ContractStops
{
    /// <summary>
    /// One stop per place (<see cref="DestinationNames"/>) under its preferred name, one item per commodity, in the order
    /// of the text; each item with the pickup of its commodity's rows.
    /// </summary>
    public static IReadOnlyList<MissionStop> Of(
        IReadOnlyList<ParsedDelivery> deliveries, CrateSize? max, IReadOnlyDictionary<string, IReadOnlyList<string>?> pickups) =>
    [
        .. deliveries.GroupBy(d => d.Destination, DestinationNames.Comparer)
            .Select((stop, index) => new MissionStop(stop.Select(d => d.Destination).Aggregate(DestinationNames.Preferred), index + 1,
            [
                .. stop.GroupBy(d => d.Commodity, StringComparer.OrdinalIgnoreCase)
                    .Select(item => Item(item.First().Commodity, item.Sum(d => d.Scu), max, pickups.GetValueOrDefault(item.Key))),
            ])),
    ];

    /// <summary>Without a size in the text: the largest crate that fits the SCU (T5.3), marked as assumed.</summary>
    private static MissionItem Item(string commodity, int scu, CrateSize? max, IReadOnlyList<string>? pickup) =>
        new(commodity, scu, max ?? CrateMix.Sizes.First(size => (int)size <= scu), Pickup: pickup);
}
