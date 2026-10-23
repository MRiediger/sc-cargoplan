using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Puts a ship's outline (metres, D55) behind its racks (cells) as a backdrop, not to scale (D62): the racks are schematic
/// (D14), so a true scale would leave them tiny in the hull or off it. The outline shows bow, side and shape, not where a
/// rack sits.
/// </summary>
public static class OutlinePlacement
{
    // One cell is one SCU, 1.25 m on each side (D9).
    private const double MetresPerCell = 1.25;

    // The outline's box is a fifth larger than the racks' in the axis that binds: in front of the racks a tenth of their
    // length stays free, as long as the bow wedge's nose (D52).
    private const double Margin = 1.2;

    /// <summary>
    /// The outline scaled evenly so its box is <see cref="Margin"/> times the racks' in the binding axis, its origin on the
    /// middle of <paramref name="bounds"/>, on their floor; bow stays +x, port +y.
    /// </summary>
    public static SceneProjector.Corner[] Place(ShipOutline outline, ShipBox bounds)
    {
        var points = outline.Points;
        var length = (points.Max(p => p.X) - points.Min(p => p.X)) / MetresPerCell;
        var width = (points.Max(p => p.Y) - points.Min(p => p.Y)) / MetresPerCell;
        var scale = Margin * Math.Max((bounds.Max.X - bounds.Min.X) / length, (bounds.Max.Y - bounds.Min.Y) / width) / MetresPerCell;
        var (centreX, centreY) = ((bounds.Min.X + bounds.Max.X) / 2.0, (bounds.Min.Y + bounds.Max.Y) / 2.0);
        return [.. points.Select(p => new SceneProjector.Corner(centreX + scale * p.X, centreY + scale * p.Y, bounds.Min.Z))];
    }

    /// <summary>The bowmost point of a placed outline: where the bow zone ends that a long ship's fit keeps in view (D62).</summary>
    public static SceneProjector.Corner Tip(IReadOnlyList<SceneProjector.Corner> placed) => placed.MaxBy(p => p.X);
}
