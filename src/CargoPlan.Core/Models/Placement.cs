namespace CargoPlan.Core.Models;

/// <summary>Assigns one cargo item of a stop to a rack.</summary>
public sealed record Placement(string Destination, string RackId, CargoItem Item, bool Delivered = false);
