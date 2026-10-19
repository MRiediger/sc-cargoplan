namespace CargoPlan.Core.Models;

/// <summary>One crate of a plan and where it should lie: rack and min corner in that rack's grid (D24, D25).</summary>
public sealed record CrateMove(Crate Crate, string RackId, GridPosition Position);
