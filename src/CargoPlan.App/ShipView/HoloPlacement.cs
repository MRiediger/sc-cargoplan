using System.Windows.Media.Media3D;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Puts a hologram (metres, D63) behind the racks (cells) at true scale, unlike the outline (D62): one cell is 1.25 m.
/// The middle of its box lies on the middle of <c>Ship.Bounds</c> in all three axes. In z that is a choice (T7.4c-2):
/// cargo decks sit about mid-height in most hulls, while the keel on the racks' floor would push them through a rounded
/// bottom. A ship's <c>holoOffset</c> moves it from there by whole cells where that is wrong (Aurora Mk II, K5.3b).
/// Bow stays +x, port +y.
/// </summary>
/// <param name="Scale">Cells per metre.</param>
/// <param name="Offset">Added after scaling, in cells.</param>
/// <param name="Min">The hologram's box in cells, for the fit.</param>
public sealed record HoloPlacement(double Scale, Vector3D Offset, Point3D Min, Point3D Max)
{
    // One cell is one SCU, 1.25 m on each side (D9).
    private const double MetresPerCell = 1.25;

    /// <param name="shift">The ship's <c>holoOffset</c> in cells; null = the middle on the racks.</param>
    public static HoloPlacement Place(HoloMesh mesh, ShipBox racks, ShipPoint? shift = null)
    {
        const double scale = 1 / MetresPerCell;
        var (min, max) = (mesh.Bounds.Min, mesh.Bounds.Max);
        var (dx, dy, dz) = shift is { } s ? (s.X, s.Y, s.Z) : (0, 0, 0);
        Vector3D offset = new(
            (racks.Min.X + racks.Max.X) / 2.0 - scale * (min.X + max.X) / 2 + dx,
            (racks.Min.Y + racks.Max.Y) / 2.0 - scale * (min.Y + max.Y) / 2 + dy,
            (racks.Min.Z + racks.Max.Z) / 2.0 - scale * (min.Z + max.Z) / 2 + dz);
        return new HoloPlacement(
            scale,
            offset,
            new Point3D(offset.X + scale * min.X, offset.Y + scale * min.Y, offset.Z + scale * min.Z),
            new Point3D(offset.X + scale * max.X, offset.Y + scale * max.Y, offset.Z + scale * max.Z));
    }

    /// <summary>A point of the mesh in cells.</summary>
    public Point3D ToCells(float x, float y, float z) => new(Offset.X + Scale * x, Offset.Y + Scale * y, Offset.Z + Scale * z);
}
