namespace CargoPlan.Core.Models;

/// <summary>The result of planning: which cargo goes into which rack.</summary>
public sealed record LoadPlan(string ShipId, IReadOnlyList<Placement> Placements, IReadOnlyList<string> Warnings)
{
    public IReadOnlyList<Placement> PlacementsForRack(string rackId) =>
        [.. Placements.Where(p => p.RackId == rackId)];

    public IReadOnlyList<Placement> PlacementsForStop(string destination) =>
        [.. Placements.Where(p => p.Destination == destination)];
}
