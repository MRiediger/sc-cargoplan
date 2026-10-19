using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>Painter's algorithm: boxes further back are drawn first, so the ones in front cover them.</summary>
public static class DrawOrder
{
    // Sines and cosines leave noise around 1e-16; rounding keeps equal depths equal, so ties fall to the rack order.
    private const int DepthDecimals = 9;

    /// <summary>
    /// Sorted by the depth of each rack's centre along the view direction, ties in rack order. Exact for racks of
    /// equal size like the Hull A's; racks of very different sizes can need a sort by separating axis (backlog, D15).
    /// </summary>
    public static IReadOnlyList<SceneRack> BackToFront(IEnumerable<SceneRack> racks, IsoProjection camera) =>
        [.. racks.OrderByDescending(r => CentreDepth(r.Bounds, camera)).ThenBy(r => r.Number)];

    /// <summary>
    /// The cells of all racks, sorted by the depth of each cell's centre, ties by rack number, then fill order (D21).
    /// Exact here: all cells are equal boxes on one grid of whole cells.
    /// </summary>
    public static IReadOnlyList<(SceneRack Rack, SceneCell Cell)> CellsBackToFront(IEnumerable<SceneRack> racks, IsoProjection camera) =>
        [.. racks
            .SelectMany(r => r.Cells.Select((cell, index) => (Rack: r, Cell: cell, Index: index)))
            .OrderByDescending(c => CentreDepth(c.Cell.Bounds, camera))
            .ThenBy(c => c.Rack.Number)
            .ThenBy(c => c.Index)
            .Select(c => (c.Rack, c.Cell))];

    private static double CentreDepth(ShipBox box, IsoProjection camera)
    {
        var (min, max) = (box.Min, box.Max);
        var depth = camera.Depth((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
        return Math.Round(depth, DepthDecimals);
    }
}
