namespace CargoPlan.Core.Models;

/// <summary>A single crate position inside a rack.</summary>
public sealed record Slot(string Id, IReadOnlySet<CrateSize> AllowedSizes);
