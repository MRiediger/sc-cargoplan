using static System.FormattableString;

namespace CargoPlan.Core.Missions;

/// <summary>
/// One contract as accepted (D42): its stops and items as entered (D23), where it came from, and whether its crates are
/// in the plan. The unit of the crate mix, of duplicates and of removal; the stop stays the unit at the elevator, so the
/// plan merges the stops of all missions per destination (<c>SavedPlan.Stops</c>). Typed missions are the typed rows of
/// one destination.
/// </summary>
/// <param name="Id">Unique within the plan, from 1; every crate names its mission by it.</param>
/// <param name="Stops">Order = order in the contract; one stop per destination.</param>
/// <param name="Contract">ContractTracker's capture this came from; null if typed or from a file.</param>
/// <param name="Unplaced">It didn't fit when it came (D41): in the plan without crates until it fits or is dropped.</param>
public sealed record Mission(int Id, IReadOnlyList<MissionStop> Stops, ContractSource? Contract = null, bool Unplaced = false)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public int Id { get; } = Id > 0 ? Id : throw new ArgumentOutOfRangeException(nameof(Id), Id, "A mission id is 1 or more.");
    public IReadOnlyList<MissionStop> Stops { get; } = ValidatedStops(Id, Stops);

    public int TotalScu => Stops.Sum(s => s.TotalScu);

    /// <summary>Every (destination, commodity) the mission delivers: what the duplicate rule compares (D39 addendum).</summary>
    public IEnumerable<(string Destination, string Commodity)> Deliveries =>
        Stops.SelectMany(s => s.Items.Select(i => (s.Destination, i.Commodity)));

    private static IReadOnlyList<MissionStop> ValidatedStops(int id, IReadOnlyList<MissionStop> stops)
    {
        if (stops.Count == 0 || stops.Any(s => s.Items.Count == 0))
        {
            throw new ArgumentException(Invariant($"Mission {id} has a stop without items or no stop."), nameof(Stops));
        }

        if (stops.GroupBy(s => s.Destination, StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1) is { } twice)
        {
            throw new ArgumentException(Invariant($"Mission {id} goes to '{twice.Key}' twice. Put all its items into one stop."), nameof(Stops));
        }

        return stops;
    }
}
