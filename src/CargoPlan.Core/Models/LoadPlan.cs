namespace CargoPlan.Core.Models;

/// <summary>The result of planning: which cargo goes into which rack.</summary>
public sealed record LoadPlan(string ShipId, IReadOnlyList<Placement> Placements, IReadOnlyList<PlanWarning> Warnings)
{
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
