namespace CargoPlan.Core.Models;

/// <summary>
/// The result of planning: every crate with its rack and position (D24), the planner's hints, and the fill order that
/// lays out crates moved later.
/// </summary>
public sealed record LoadPlan(
    string ShipId, IReadOnlyList<Crate> Crates, IReadOnlyList<PlanWarning> Warnings, FillOrder FillOrder = FillOrder.BowFirst)
{
    /// <summary>
    /// The crates grouped by rack, destination, commodity, crate size and delivered state, in order of first appearance:
    /// what hints, legend and text renderer work on (D24). Derived on every read, not stored: a <c>with</c> on
    /// <see cref="Crates"/> skips initializers and would leave stored placements stale. GroupBy keeps the order of first
    /// appearance (learn.microsoft.com, Enumerable.GroupBy, Remarks).
    /// </summary>
    public IReadOnlyList<Placement> Placements =>
    [
        .. Crates
            .GroupBy(c => (c.RackId, c.Destination, c.Commodity, c.CrateSize, c.Delivered))
            .Select(g => new Placement(
                g.Key.Destination, g.Key.RackId, new CargoItem(g.Key.Commodity, g.Count() * (int)g.Key.CrateSize, g.Key.CrateSize), g.Key.Delivered)),
    ];

    /// <summary>
    /// The destinations in order of their first crate, which never changes: the identity of a stop for the palette, so a
    /// stop keeps its colour. Legend and stacking rule follow <see cref="UnloadOrder"/>. Derived like
    /// <see cref="Placements"/>; GroupBy keeps the order of first appearance.
    /// </summary>
    public IReadOnlyList<string> Route => [.. Crates.GroupBy(c => c.Destination).Select(g => g.Key)];

    /// <summary>
    /// The order stops are unloaded in: the order of the legend and of the stacking rule (D24 addendum). A stop with cargo
    /// on board ranks by its first crate on board; a delivered one by the start of its last run of crates in the list, its
    /// latest visit. So ticking never moves a stop, and a new visit to a delivered stop, appended to the list, comes last
    /// (D41). While nothing is delivered it equals <see cref="Route"/>. Derived like <see cref="Placements"/>.
    /// </summary>
    public IReadOnlyList<string> UnloadOrder =>
    [
        .. Crates.Select((crate, index) => (crate, index))
            .GroupBy(p => p.crate.Destination)
            .OrderBy(g => g.Any(p => !p.crate.Delivered) ? g.First(p => !p.crate.Delivered).index : LastRunStart([.. g.Select(p => p.index)]))
            .Select(g => g.Key),
    ];

    /// <summary>The first index of the last run of consecutive indices in an ascending list.</summary>
    private static int LastRunStart(int[] indices)
    {
        var start = indices.Length - 1;
        while (start > 0 && indices[start - 1] == indices[start] - 1)
        {
            start--;
        }

        return indices[start];
    }

    public IReadOnlyList<Crate> CratesForRack(string rackId) =>
        [.. Crates.Where(c => c.RackId == rackId)];

    public IReadOnlyList<Placement> PlacementsForRack(string rackId) =>
        [.. Placements.Where(p => p.RackId == rackId)];

    public IReadOnlyList<Placement> PlacementsForStop(string destination) =>
        [.. Placements.Where(p => p.Destination == destination)];

    public IReadOnlyList<PlanWarning> WarningsForRack(string rackId) =>
        [.. Warnings.Where(w => w.RackId == rackId)];

    /// <summary>Planner, text renderer, ship view and plan file all need the plan's own ship; one check for all.</summary>
    /// <exception cref="ArgumentException">The plan belongs to another ship.</exception>
    public void EnsureBelongsTo(Ship ship)
    {
        if (ShipId != ship.Id)
        {
            throw new ArgumentException($"The plan is for ship '{ShipId}', not '{ship.Id}'.", nameof(ship));
        }
    }
}
