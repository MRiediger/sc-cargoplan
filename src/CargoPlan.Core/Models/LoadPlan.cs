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
    /// The destinations in order of their first crate: route order for a planned plan (moves, split and merge keep the
    /// crate order), the order of legend and palette, and the order the stacking rule follows (D24 addendum). Derived like
    /// <see cref="Placements"/>; GroupBy keeps the order of first appearance.
    /// </summary>
    public IReadOnlyList<string> Route => [.. Crates.GroupBy(c => c.Destination).Select(g => g.Key)];

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
