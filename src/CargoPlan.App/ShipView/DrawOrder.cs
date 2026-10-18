using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>Painter's algorithm: racks further back are drawn first, so the ones in front cover them.</summary>
public static class DrawOrder
{
    /// <summary>
    /// Sorted by the depth of each rack's centre, ties in rack order. Exact for racks of equal size like the Hull A's;
    /// racks of very different sizes can need a sort by separating axis (backlog, D15).
    /// </summary>
    public static IReadOnlyList<Rack> BackToFront(IEnumerable<Rack> racks) =>
        [.. racks.OrderByDescending(CentreDepth).ThenBy(r => r.Index)];

    private static double CentreDepth(Rack rack)
    {
        var (min, max) = (rack.Bounds.Min, rack.Bounds.Max);
        return IsoProjection.Depth((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0);
    }
}
