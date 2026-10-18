using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.Core.Plans;

/// <summary>
/// The missions as entered and the plan made from them, with the player's moves and ticks. Once the plan has either,
/// it can't be planned again from the missions, so both are kept together (D18).
/// </summary>
public sealed record SavedPlan(IReadOnlyList<Stop> Missions, LoadPlan Plan)
{
    // Get-only (no init): a `with` expression skips the constructor and must not bypass validation.
    public IReadOnlyList<Stop> Missions { get; } = Missions;
    public LoadPlan Plan { get; } = Validated(Missions, Plan);

    /// <summary>
    /// The placements cover the missions exactly: same SCU per destination, commodity and crate size. Moving and
    /// ticking never change that, so a pair that fails here was made from other missions or edited by hand.
    /// </summary>
    private static LoadPlan Validated(IReadOnlyList<Stop> missions, LoadPlan plan)
    {
        // A mission file needs a stop, so a pair without one could be written but not read back.
        if (missions.Count == 0)
        {
            throw new ArgumentException("The missions contain no stops.", nameof(Missions));
        }

        HashSet<string> destinations = [.. missions.Select(s => s.Destination)];
        if (plan.Placements.FirstOrDefault(p => !destinations.Contains(p.Destination)) is { } stray)
        {
            throw new ArgumentException($"Cargo for '{stray.Destination}' is placed, but the missions have no such stop.", nameof(Plan));
        }

        var needed = ScuBy(missions.SelectMany(s => s.Items.Select(i => (s.Destination, Item: i))));
        var placed = ScuBy(plan.Placements.Select(p => (p.Destination, p.Item)));
        foreach (var key in needed.Keys.Union(placed.Keys))
        {
            var (scuNeeded, scuPlaced) = (needed.GetValueOrDefault(key), placed.GetValueOrDefault(key));
            if (scuNeeded != scuPlaced)
            {
                throw new ArgumentException(Invariant(
                    $"{key.Destination}, {key.Commodity} ({(int)key.Size} SCU crates): {scuPlaced} of {scuNeeded} SCU placed."),
                    nameof(Plan));
            }
        }

        return plan;
    }

    private static Dictionary<(string Destination, string Commodity, CrateSize Size), int> ScuBy(
        IEnumerable<(string Destination, CargoItem Item)> cargo) =>
        cargo.GroupBy(c => (c.Destination, c.Item.Commodity, c.Item.CrateSize)).ToDictionary(g => g.Key, g => g.Sum(c => c.Item.Scu));
}
