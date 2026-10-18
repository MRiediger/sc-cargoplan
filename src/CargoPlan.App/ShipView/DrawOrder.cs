namespace CargoPlan.App.ShipView;

/// <summary>Painter's algorithm: racks further back are drawn first, so the ones in front cover them.</summary>
public static class DrawOrder
{
    // Sines and cosines leave noise around 1e-16; rounding keeps equal depths equal, so ties fall to the rack order.
    private const int DepthDecimals = 9;

    /// <summary>
    /// Sorted by the depth of each rack's centre along the view direction, ties in rack order. Exact for racks of
    /// equal size like the Hull A's; racks of very different sizes can need a sort by separating axis (backlog, D15).
    /// </summary>
    public static IReadOnlyList<SceneRack> BackToFront(IEnumerable<SceneRack> racks, IsoProjection camera) =>
        [.. racks.OrderByDescending(r => CentreDepth(r, camera)).ThenBy(r => r.Number)];

    private static double CentreDepth(SceneRack rack, IsoProjection camera)
    {
        var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);
        var depth = camera.Depth((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
        return Math.Round(depth, DepthDecimals);
    }
}
