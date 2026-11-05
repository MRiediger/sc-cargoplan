namespace CargoPlan.Core.Models;

/// <summary>
/// Which way the rows of a rack's layout run (D88): the cell order is stack &gt; progress axis &gt; row axis, and this picks
/// the row axis. <see cref="FillOrder"/> picks the direction along the length, whichever axis that is. Width counts from
/// starboard to port in both. A rack stacking along ±y ignores it: there width stands upright (<see cref="Rack.StacksSideways"/>).
/// </summary>
public enum FillRows
{
    /// <summary>Rows along the length, stepping across the width.</summary>
    Lengthwise,

    /// <summary>Rows across the width, stepping along the length.</summary>
    Crosswise,
}
