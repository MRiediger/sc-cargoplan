using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Puts a ship's outline (metres, D55) in its cells: where the ship's file says (<c>outlineOrigin</c>, <c>outlineScale</c>,
/// D90), so no rack moves or scales it. A file without them keeps the older rule on the racks: at its true size, or larger
/// where the racks would not fit it (D62, K5.2c). The racks are schematic (D14), so the outline shows bow, side and shape,
/// not where a rack sits. It lies flat on about the hologram's floor (D83, T8.5).
/// </summary>
public static class OutlinePlacement
{
    // One cell is one SCU, 1.25 m on each side (D9).
    private const double MetresPerCell = 1.25;

    // Where the racks fill the hull, the outline's box is a fifth larger than the racks' in the axis that binds: in front
    // of the racks a tenth of their length stays free, as long as the bow wedge's nose (D52).
    private const double Margin = 1.2;

    // Never smaller than the ship: a hold of a few cells shrank the hull to the rack's size before (Martin, K5.2c).
    private const double TrueSize = 1;

    /// <summary>The outline of <paramref name="ship"/> where its file says; without <c>outlineOrigin</c> on the middle of its racks, on its <see cref="Floor"/>.</summary>
    public static Point3D[] Place(ShipOutline outline, Ship ship) => ship.OutlineOrigin is { } origin
        ? At(outline, ship.OutlineScale ?? TrueSize, origin.X, origin.Y, origin.Z)
        : Place(outline, ship.Bounds, Floor(outline, ship));

    /// <summary>
    /// The outline scaled evenly to its true size, or so its box is <see cref="Margin"/> times the racks' in the binding
    /// axis where that is larger; its origin on the middle of <paramref name="bounds"/>; bow stays +x, port +y.
    /// </summary>
    /// <param name="floor">The plane it lies in, in cells; null = the racks' floor.</param>
    public static Point3D[] Place(ShipOutline outline, ShipBox bounds, double? floor = null)
    {
        var (centreX, centreY) = ((bounds.Min.X + bounds.Max.X) / 2.0, (bounds.Min.Y + bounds.Max.Y) / 2.0);
        return At(outline, FitScale(outline, bounds), centreX, centreY, floor ?? bounds.Min.Z);
    }

    private static Point3D[] At(ShipOutline outline, double factor, double x, double y, double z)
    {
        var scale = factor / MetresPerCell;
        return [.. outline.Points.Select(p => new Point3D(x + scale * p.X, y + scale * p.Y, z))];
    }

    /// <summary>The factor on the outline's true size that the older rule takes from the racks; at least 1.</summary>
    public static double FitScale(ShipOutline outline, ShipBox bounds)
    {
        var points = outline.Points;
        var length = (points.Max(p => p.X) - points.Min(p => p.X)) / MetresPerCell;
        var width = (points.Max(p => p.Y) - points.Min(p => p.Y)) / MetresPerCell;
        var fill = Margin * Math.Max((bounds.Max.X - bounds.Min.X) / length, (bounds.Max.Y - bounds.Min.Y) / width);
        return Math.Max(fill, TrueSize);
    }

    /// <summary>
    /// The plane the outline lies in: <c>outlineOrigin</c>'s z where the file has one. The older rule is the hologram's
    /// floor without the hologram: its box is the hull's height, its middle on the racks' middle moved by
    /// <c>holoOffset</c>, as <see cref="HoloPlacement"/> puts it. Never above the racks' floor, so no rack reaches through
    /// it (T8.5); without a height the racks' floor.
    /// </summary>
    public static double Floor(ShipOutline outline, Ship ship)
    {
        if (ship.OutlineOrigin is { } origin)
        {
            return origin.Z;
        }

        var bounds = ship.Bounds;
        if (outline.HeightM is not { } height)
        {
            return bounds.Min.Z;
        }

        var middle = (bounds.Min.Z + bounds.Max.Z) / 2.0 + (ship.HoloOffset?.Z ?? 0);
        return Math.Min(middle - height / MetresPerCell / 2, bounds.Min.Z);
    }
}
