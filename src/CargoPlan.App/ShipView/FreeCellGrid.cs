namespace CargoPlan.App.ShipView;

/// <summary>
/// Which free cells the view draws (D61), by screen size like the rack numbers (D32): every one while a cell is at least
/// <see cref="MinCellSize"/> pixels, below that only those in a stack layer that holds a crate. Large empty racks then
/// show their edges instead of a grid that covers the crates; the layer being loaded keeps its gaps.
/// </summary>
public static class FreeCellGrid
{
    /// <summary>
    /// Pixels per cell, the zoom's unit (D76); the value of the rack numbers' minimum (D32), not tied to it any more: a
    /// font size and a cell size only happened to agree.
    /// </summary>
    public const double MinCellSize = 12;

    /// <param name="scale">Pixels per cell at the target's plane (<see cref="CameraMath.PxPerCellAtTarget"/>), fit and zoom included.</param>
    public static bool Draws(SceneBox free, double scale) => scale >= MinCellSize || free.InLoadedLayer;
}
