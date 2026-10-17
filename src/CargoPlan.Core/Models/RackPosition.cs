namespace CargoPlan.Core.Models;

/// <summary>Where a rack physically sits in the ship. Drawing only; the planner works in rack order.</summary>
public sealed record RackPosition(RackSide Side, RackLevel Level);
