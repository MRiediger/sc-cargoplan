using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The ship view's 3D scene (D83): lights fixed to the ship, one model per crate, the bow wedge, and the hologram as the
/// last child. WPF sorts no transparent triangles, so what is opaque goes first and the half-transparent hull after it:
/// crates inside the hull show through it, racks under the ship lie under it. The camera is set by the view
/// (<see cref="CameraMath.ApplyTo"/>); this class only builds and rebuilds models.
/// </summary>
public sealed class ShipScene3D
{
    // Every crate this much smaller all round: the gap between crates shows each one (T3.11b), as in the 2D view.
    private const double Inset = 0.05;

    // Travelling from above and the bow, a little from port (S8.0); the under light from below and astern.
    private static readonly Vector3D MainLightDirection = new(-1, -0.3, -1);
    private static readonly Vector3D UnderLightDirection = new(0.3, 0, 1);

    private static readonly Material CrateMaterial = Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(MissionPalette.SingleCrate))));
    private static readonly Material WedgeMaterial = Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(MissionPalette.BowWedgeFill))));

    private readonly Model3DGroup _crates = new();
    private readonly Model3DGroup _marker = new();
    private readonly GeometryModel3D _holoSurface = new();
    private readonly GeometryModel3D _holoLines = new();

    private PreparedHolo? _holo;
    private double _holoOpacity = AppearanceRules.DefaultHoloOpacity;
    private Color _hullColor = (Color)ColorConverter.ConvertFromString(AppearanceRules.DefaultHoloHullColor);
    private Color _edgeColor = (Color)ColorConverter.ConvertFromString(AppearanceRules.DefaultHoloEdgeColor);

    // What the hologram's edge strips were built for: they change with the camera, the scale and the edge level.
    private (IsoProjection Orientation, double PxPerCell, int Level)? _linesFor;

    public ShipScene3D()
    {
        Visuals =
        [
            new ModelVisual3D
            {
                Content = new Model3DGroup
                {
                    Children =
                    [
                        new AmbientLight(MissionPalette.SceneAmbient),
                        new DirectionalLight(MissionPalette.SceneLight, MainLightDirection),
                        new DirectionalLight(MissionPalette.SceneUnderLight, UnderLightDirection),
                    ],
                },
            },
            new ModelVisual3D { Content = _crates },
            new ModelVisual3D { Content = _marker },

            // Last: transparent, drawn over what lies behind it (D83).
            new ModelVisual3D { Content = new Model3DGroup { Children = [_holoSurface, _holoLines] } },
        ];
    }

    /// <summary>The children of the view's <c>Viewport3D</c>, in drawing order.</summary>
    public IReadOnlyList<ModelVisual3D> Visuals { get; }

    /// <summary>Crates and marker anew: a new plan, a tick, another ship or marker.</summary>
    public void Show(ShipScene? scene)
    {
        _crates.Children.Clear();
        _marker.Children.Clear();
        if (scene is null)
        {
            return;
        }

        foreach (var box in scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Fill != CellFill.Free))
        {
            _crates.Children.Add(Frozen(new GeometryModel3D(Cuboid(box.Bounds), CrateMaterial)));
        }

        // Fallback hologram → outline → wedge (D62, D63): the outline as a surface comes with T8.5, until then nothing.
        if (scene.Marker is BowMarker.Wedge)
        {
            _marker.Children.Add(Frozen(new GeometryModel3D(Wedge(scene.Bounds), WedgeMaterial) { BackMaterial = WedgeMaterial }));
        }
    }

    public void ShowHolo(PreparedHolo? holo, double opacity, Color hull, Color edge)
    {
        (_holo, _holoOpacity, _hullColor, _edgeColor) = (holo, opacity, hull, edge);
        _linesFor = null;
        _holoLines.Geometry = null;
        _holoSurface.Geometry = holo?.Surface;
        if (holo is null)
        {
            return;
        }

        // BackMaterial too: a hull with gaps shows its inside instead of holes (T7.4b). The opacity is the material's now,
        // not the element's: one scene (D83).
        var surface = holo.Look == ShipOutlineMode.HoloHiddenline
            ? Unlit(MissionPalette.HoloHiddenFill, opacity)
            : Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(hull) { Opacity = opacity })));
        _holoSurface.Material = surface;
        _holoSurface.BackMaterial = surface;
        var line = Unlit(edge, opacity);
        _holoLines.Material = line;
        _holoLines.BackMaterial = line;
    }

    /// <summary>
    /// The hidden-line look's edges for this camera: strips one pixel wide in the target's plane, nearer edges a little
    /// wider and further ones narrower (perspective); <paramref name="zoom"/> on top of the fit picks the edge level (D63).
    /// </summary>
    public void ShowHoloLines(CameraMath camera, double zoom)
    {
        if (_holo is not { Contour: { } contour } holo)
        {
            return;
        }

        var level = HoloEdges.LevelFor(zoom);
        var scale = camera.PxPerCellAtTarget;
        if (_linesFor == (camera.Orientation, scale, level))
        {
            return;
        }

        _linesFor = (camera.Orientation, scale, level);
        var look = camera.LookDirection;
        var contourEdges = contour.Visible(new System.Numerics.Vector3((float)look.X, (float)look.Y, (float)look.Z));
        int[] segments = [.. holo.Mesh.Edges.AsSpan(0, 2 * holo.Mesh.EdgeCountFor(level)), .. contourEdges];
        var (positions, indices) = HoloLines.Strips(holo.Points, segments, look, HoloLines.HalfWidthCells(scale));
        _holoLines.Geometry = Frozen(new MeshGeometry3D { Positions = new Point3DCollection(positions), TriangleIndices = new Int32Collection(indices) });
    }

    /// <summary>
    /// A box inset on every side, six faces of two triangles, each with its own normals so edges stay sharp, and texture
    /// coordinates in cells for the hatch (T8.2): u to the right and v down as the face is seen from outside with up at
    /// +z (the top and bottom with the bow up), so no face shows its hatch mirrored.
    /// </summary>
    public static MeshGeometry3D Cuboid(ShipBox box)
    {
        var (x0, y0, z0) = (box.Min.X + Inset, box.Min.Y + Inset, box.Min.Z + Inset);
        var (x1, y1, z1) = (box.Max.X - Inset, box.Max.Y - Inset, box.Max.Z - Inset);
        MeshGeometry3D mesh = new();
        Vector3D x = new(1, 0, 0), y = new(0, 1, 0), z = new(0, 0, 1);

        // Counter-clockwise seen from outside, so the front faces point out.
        Quad(mesh, new(x1, y0, z0), new(x1, y1, z0), new(x1, y1, z1), new(x1, y0, z1), x, (y, -z));
        Quad(mesh, new(x0, y1, z0), new(x0, y0, z0), new(x0, y0, z1), new(x0, y1, z1), -x, (-y, -z));
        Quad(mesh, new(x1, y1, z0), new(x0, y1, z0), new(x0, y1, z1), new(x1, y1, z1), y, (-x, -z));
        Quad(mesh, new(x0, y0, z0), new(x1, y0, z0), new(x1, y0, z1), new(x0, y0, z1), -y, (x, -z));
        Quad(mesh, new(x0, y0, z1), new(x1, y0, z1), new(x1, y1, z1), new(x0, y1, z1), z, (-y, -x));
        Quad(mesh, new(x0, y1, z0), new(x1, y1, z0), new(x1, y0, z0), new(x0, y0, z0), -z, (y, -x));
        return Frozen(mesh);
    }

    /// <summary>The bow wedge's five faces (D52), from the same corners as the 2D view drew them.</summary>
    private static MeshGeometry3D Wedge(ShipBox bounds)
    {
        MeshGeometry3D mesh = new();
        foreach (var face in SceneProjector.BowWedge(bounds))
        {
            var c = face.Corners;
            Vector3D normal = new(face.Normal.X, face.Normal.Y, face.Normal.Z);
            Quad(mesh, Point(c[0]), Point(c[1]), Point(c[2]), Point(c[3]), normal);
        }

        return Frozen(mesh);
    }

    private static Point3D Point(SceneProjector.Corner corner) => new(corner.X, corner.Y, corner.Z);

    /// <param name="texture">The face's u and v axes in the ship; a point's texture coordinates are its position along them, in cells.</param>
    private static void Quad(MeshGeometry3D mesh, Point3D a, Point3D b, Point3D c, Point3D d, Vector3D normal, (Vector3D U, Vector3D V)? texture = null)
    {
        var first = mesh.Positions.Count;
        foreach (var point in (Point3D[])[a, b, c, d])
        {
            mesh.Positions.Add(point);
            mesh.Normals.Add(normal);
            if (texture is var (u, v))
            {
                var p = (Vector3D)point;
                mesh.TextureCoordinates.Add(new Point(Vector3D.DotProduct(p, u), Vector3D.DotProduct(p, v)));
            }
        }

        foreach (var index in (int[])[0, 1, 2, 0, 2, 3])
        {
            mesh.TriangleIndices.Add(first + index);
        }
    }

    // Black diffuse under an emissive colour: the colour alone, unlit. EmissiveMaterial only adds its colour to the
    // material under it. Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.emissivematerial
    private static Material Unlit(Color color, double opacity) => Frozen(new MaterialGroup
    {
        Children =
        [
            new DiffuseMaterial(new SolidColorBrush(Colors.Black) { Opacity = opacity }),
            new EmissiveMaterial(new SolidColorBrush(color) { Opacity = opacity }),
        ],
    });

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
