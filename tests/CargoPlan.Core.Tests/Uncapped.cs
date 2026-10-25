using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The crate mix of the contract alone, without a ship's cap (D70): Core asks every caller for a <see cref="CrateCap"/>, so a
/// forgotten one doesn't compile; tests of mixes, files and ships whose racks take every crate pass none.
/// </summary>
internal static class Uncapped
{
    extension(MissionStop stop)
    {
        public Stop ToStop() => stop.ToStop(CrateCap.None);
    }

    extension(IEnumerable<MissionStop> stops)
    {
        public IReadOnlyList<Stop> ToStops() => stops.ToStops(CrateCap.None);
    }

    extension(MissionItem item)
    {
        public IReadOnlyList<CrateSize> CrateList => item.CrateListUnder(CrateCap.None);

        public IReadOnlyList<CargoItem> ToCargoItems() => item.ToCargoItems(CrateCap.None);
    }
}
