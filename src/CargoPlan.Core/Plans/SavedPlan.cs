using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// The missions as entered and the plan made from them, with the player's moves and ticks. Once the plan has either,
/// it can't be planned again from the missions, so both are kept together (D18). The missions are kept as entered
/// (SCU, max crate size, override), so the editor shows them as typed (D23).
/// </summary>
public sealed record SavedPlan(IReadOnlyList<MissionStop> Missions, LoadPlan Plan)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public IReadOnlyList<MissionStop> Missions { get; } = Missions;
    public LoadPlan Plan { get; } = Validated(Missions, Plan);

    /// <summary>
    /// The crates are the crates of the expanded missions (D23, D24): per destination, commodity and crate size the same
    /// number. Moving and ticking never change that, so a pair that fails here was made from other missions or edited by
    /// hand. Where the crates lie needs the ship and is checked on reading (PlanFile).
    /// </summary>
    private static LoadPlan Validated(IReadOnlyList<MissionStop> missions, LoadPlan plan)
    {
        // A mission file needs a stop, so a pair without one could be written but not read back.
        if (missions.Count == 0)
        {
            throw new ArgumentException("The missions contain no stops.", nameof(Missions));
        }

        HashSet<string> destinations = [.. missions.Select(s => s.Destination)];
        if (plan.Crates.FirstOrDefault(c => !destinations.Contains(c.Destination)) is { } stray)
        {
            throw new ArgumentException($"Cargo for '{stray.Destination}' is placed, but the missions have no such stop.", nameof(Plan));
        }

        var needed = missions.ToStops()
            .SelectMany(s => s.Items.Select(i => (Key: (s.Destination, i.Commodity, Size: i.CrateSize), Count: i.Scu / (int)i.CrateSize)))
            .GroupBy(c => c.Key)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
        var placed = plan.Crates
            .GroupBy(c => (c.Destination, c.Commodity, Size: c.CrateSize))
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var key in needed.Keys.Union(placed.Keys))
        {
            var (countNeeded, countPlaced) = (needed.GetValueOrDefault(key), placed.GetValueOrDefault(key));
            if (countNeeded != countPlaced)
            {
                throw new ArgumentException(Invariant(
                    $"{key.Destination}, {key.Commodity}: {countPlaced} of {countNeeded} crates of {(int)key.Size} SCU placed."),
                    nameof(Plan));
            }
        }

        return plan;
    }
}
