namespace CargoPlan.Core.Ships;

/// <summary>No ship file exists for the requested id.</summary>
public sealed class ShipNotFoundException(string shipId, IReadOnlyList<string> availableShipIds)
    : Exception($"Ship '{shipId}' not found. Available ships: {string.Join(", ", availableShipIds)}.")
{
    public string ShipId { get; } = shipId;
    public IReadOnlyList<string> AvailableShipIds { get; } = availableShipIds;
}
