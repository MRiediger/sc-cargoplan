namespace CargoPlan.Core.Models;

/// <summary>Where a crate lies in a rack's grid: its min corner (D24) and whether it is turned 90° (D71).</summary>
public readonly record struct Pose(GridPosition Position, bool Rotated = false);
