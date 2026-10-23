namespace CargoPlan.App.ShipView;

/// <summary>
/// Which free cells the view draws (D61), by screen size like the rack numbers (D32): every one while a cell is at least
/// <see cref="MinCellSize"/> pixels, below that only those in a stack layer that holds a crate. Large empty racks then
/// show their edges instead of a grid that covers the crates; the layer being loaded keeps its gaps.
/// </summary>
public static class FreeCellGrid
{
    public const double MinCellSize = RackNumberSize.MinSize;

    /// <param name="scale">Pixels per cell (<see cref="ViewTransform.Scale"/>), fit and zoom included.</param>
    public static bool Draws(SceneBox free, double scale) => scale >= MinCellSize || free.InLoadedLayer;
}
