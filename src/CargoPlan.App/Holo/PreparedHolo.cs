using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;

namespace CargoPlan.App.Holo;

/// <summary>
/// A hologram ready to draw for one ship and look (D63): placed in cells, its triangles as a frozen
/// <see cref="MeshGeometry3D"/>. Made off the UI thread (<see cref="HoloLoader"/>); a frozen Freezable may be used by any
/// thread, so the UI thread only hands it to the backdrop.
/// Source: https://learn.microsoft.com/dotnet/desktop/wpf/advanced/freezable-objects-overview
/// </summary>
/// <param name="Points">The mesh's vertices in cells, for the hidden-line strips.</param>
/// <param name="Contour">The contour's candidates; only for the hidden-line look.</param>
public sealed record PreparedHolo(
    string ShipId, ShipOutlineMode Look, HoloMesh Mesh, HoloPlacement Placement, MeshGeometry3D Surface, Point3D[] Points, HoloContour? Contour)
{
    /// <param name="look">One of the hologram looks (<see cref="ShipOutlineModes.IsHologram"/>).</param>
    public static PreparedHolo Prepare(Ship ship, HoloMesh mesh, ShipOutlineMode look)
    {
        var placement = HoloPlacement.Place(mesh, ship.Bounds, ship.HoloOffset);
        var surface = look switch
        {
            ShipOutlineMode.HoloFlat => HoloSurface.Flat(mesh),
            ShipOutlineMode.HoloShaded => HoloSurface.Smooth(mesh),
            ShipOutlineMode.HoloHiddenline => HoloSurface.Opaque(mesh),
            _ => throw new ArgumentOutOfRangeException(nameof(look), look, "Not a hologram look."),
        };

        return new PreparedHolo(
            ship.Id,
            look,
            mesh,
            placement,
            Geometry(surface, placement),
            [.. Enumerable.Range(0, mesh.VertexCount).Select(v => placement.ToCells(mesh.Vertices[3 * v], mesh.Vertices[3 * v + 1], mesh.Vertices[3 * v + 2]))],
            look == ShipOutlineMode.HoloHiddenline ? HoloContour.Build(mesh) : null);
    }

    private static MeshGeometry3D Geometry(HoloSurface surface, HoloPlacement placement)
    {
        var count = surface.Positions.Length / 3;
        Point3DCollection positions = new(count);
        for (var v = 0; v < count; v++)
        {
            positions.Add(placement.ToCells(surface.Positions[3 * v], surface.Positions[3 * v + 1], surface.Positions[3 * v + 2]));
        }

        // Scaling is uniform, so the normals stay as they are; without normals (hidden line) WPF makes its own.
        Vector3DCollection normals = new(surface.Normals.Length / 3);
        for (var v = 0; v < surface.Normals.Length / 3; v++)
        {
            normals.Add(new Vector3D(surface.Normals[3 * v], surface.Normals[3 * v + 1], surface.Normals[3 * v + 2]));
        }

        MeshGeometry3D geometry = new() { Positions = positions, Normals = normals, TriangleIndices = new Int32Collection(surface.Indices) };
        geometry.Freeze();
        return geometry;
    }
}
