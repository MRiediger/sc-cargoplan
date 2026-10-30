using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>How the crates look (T8.2): the theme's palette, fill alpha and edge colour, and the selection's missions.</summary>
/// <param name="Edge">The crates' edges (<c>crates.edgeAlpha</c>); null = crates without edges.</param>
/// <param name="Focus">The missions of the selected crates (<see cref="SelectionFocus.Missions"/>): other missions dim (D68).</param>
public sealed record CrateLook(StopPalette Palette, byte FillAlpha, Color? Edge, IReadOnlySet<int> Focus);

/// <summary>
/// The ship view's 3D scene (D83): lights fixed to the ship, one model per crate in its stop's material, the edges as
/// strips, the bow wedge, and the hologram as the last child. WPF sorts no transparent triangles, so the crates are kept
/// in order from the back to the front (they are half-transparent by default, <c>crates.fillAlpha</c>), and the
/// half-transparent hull comes after them: crates inside it show through it. The camera is set by the view
/// (<see cref="CameraMath.ApplyTo"/>); this class builds models and follows the camera (<see cref="Follow"/>).
/// What lies on top whatever covers it (violations, the selection, the numbers) is the view's 2D overlay, as before.
/// </summary>
public sealed class ShipScene3D
{
    // Every crate this much smaller all round: the gap between crates shows each one (T3.11b), as in the 2D view.
    private const double Inset = 0.05;

    // Pen widths of the 2D view, in DIPs at the target's plane.
    private const double CrateEdgeWidth = 0.5;
    private const double FreeEdgeWidth = 0.5;
    private const double RackEdgeWidth = 1;
    private const double FrameWidth = 1;

    // The frame's dashes: WPF's Dash style is 2 on, 2 off, in pen widths (DashStyles.Dash).
    private const double FrameDash = 2;

    // Travelling from above and the bow, a little from port (S8.0); the under light from below and astern.
    private static readonly Vector3D MainLightDirection = new(-1, -0.3, -1);
    private static readonly Vector3D UnderLightDirection = new(0.3, 0, 1);

    private static readonly Material WedgeMaterial = Frozen(new DiffuseMaterial(Frozen(new SolidColorBrush(MissionPalette.BowWedgeFill))));

    private readonly Model3DGroup _edges = new();
    private readonly Model3DGroup _marker = new();
    private readonly GeometryModel3D _holoSurface = new();
    private readonly GeometryModel3D _holoLines = new();
    private readonly List<(SceneBox Box, GeometryModel3D Model, Point3D Centre)> _crates = [];

    private ShipScene? _scene;
    private CrateLook? _look;
    private IReadOnlySet<Crate> _faded = new HashSet<Crate>();
    private double _tile = 1;
    private (CameraMath Camera, double Zoom)? _followed;

    private PreparedHolo? _holo;

    // What the strips were built for: they change with the orientation and the scale, not with a pan.
    private (IsoProjection Orientation, double PxPerCell)? _stripsFor;
    private (IsoProjection Orientation, double PxPerCell, int Level)? _linesFor;

    public ShipScene3D()
    {
        HoloVisual = new ModelVisual3D { Content = new Model3DGroup { Children = [_holoSurface, _holoLines] } };
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
            new ModelVisual3D { Content = Crates },
            new ModelVisual3D { Content = _edges },
            new ModelVisual3D { Content = FreeGrid },
            new ModelVisual3D { Content = _marker },

            // Last: transparent, drawn over what lies behind it (D83).
            HoloVisual,
        ];
    }

    /// <summary>The children of the view's <c>Viewport3D</c>, in drawing order.</summary>
    public IReadOnlyList<ModelVisual3D> Visuals { get; }

    /// <summary>The crates, one model each, from the back to the front after <see cref="Follow"/>.</summary>
    public Model3DGroup Crates { get; } = new();

    /// <summary>The free cells' grid, one model per rack (D61: only where a cell is large enough or its layer holds cargo).</summary>
    public Model3DGroup FreeGrid { get; } = new();

    public ModelVisual3D HoloVisual { get; }

    /// <summary>Crates and marker anew: a new plan, a tick, another ship or marker, another look or selection.</summary>
    public void Show(ShipScene? scene, CrateLook look)
    {
        (_scene, _look) = (scene, look);
        _crates.Clear();
        Crates.Children.Clear();
        _marker.Children.Clear();
        _stripsFor = null;
        if (scene is null)
        {
            _edges.Children.Clear();
            FreeGrid.Children.Clear();
            return;
        }

        foreach (var box in scene.Racks.SelectMany(r => r.Boxes).Where(b => b.Fill != CellFill.Free))
        {
            var (min, max) = (box.Bounds.Min, box.Bounds.Max);
            GeometryModel3D model = new(Cuboid(box.Bounds), MaterialOf(box));
            _crates.Add((box, model, new Point3D((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, (min.Z + max.Z) / 2.0)));
            Crates.Children.Add(model);
        }

        // Fallback hologram → outline → wedge (D62, D63): the outline as a surface comes with T8.5, until then nothing.
        if (scene.Marker is BowMarker.Wedge)
        {
            _marker.Children.Add(Frozen(new GeometryModel3D(Wedge(scene.Bounds), WedgeMaterial) { BackMaterial = WedgeMaterial }));
        }

        if (_followed is var (camera, zoom))
        {
            Follow(camera, zoom);
        }
    }

    /// <summary>
    /// The scene for this camera: the hatch tile for its scale, the crates from the back to the front, the edges' strips
    /// and the hologram's edges for its orientation and scale. <paramref name="zoom"/> on top of the fit picks the
    /// hologram's edge level (D63).
    /// </summary>
    public void Follow(CameraMath camera, double zoom = 1)
    {
        _followed = (camera, zoom);
        var tile = CrateMaterials.TileCells(camera.PxPerCellAtTarget);
        if (tile != _tile)
        {
            _tile = tile;
            foreach (var (box, model, _) in _crates)
            {
                model.Material = MaterialOf(box);
            }
        }

        SortBackToFront(camera);
        if (_stripsFor != (camera.Orientation, camera.PxPerCellAtTarget))
        {
            _stripsFor = (camera.Orientation, camera.PxPerCellAtTarget);
            BuildStrips(camera);
        }

        ShowHoloLines(camera, zoom);
    }

    /// <summary>
    /// Fades <paramref name="crates"/> where they are, fill and edges (<see cref="CrateMaterials.FadedShare"/>): what a drag
    /// holds or its outcome moves (D25, T8.4). Only materials and edge strips change, and only for another set.
    /// </summary>
    public void Fade(IReadOnlySet<Crate> crates)
    {
        if (_faded.SetEquals(crates))
        {
            return;
        }

        _faded = crates;
        if (_look is null)
        {
            return;
        }

        foreach (var (box, model, _) in _crates)
        {
            model.Material = MaterialOf(box);
        }

        _stripsFor = null;
        if (_followed is var (camera, _))
        {
            _stripsFor = (camera.Orientation, camera.PxPerCellAtTarget);
            BuildStrips(camera);
        }
    }

    public void ShowHolo(PreparedHolo? holo, double opacity, Color hull, Color edge)
    {
        _holo = holo;
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
        if (_followed is var (camera, zoom))
        {
            ShowHoloLines(camera, zoom);
        }
    }

    private Material MaterialOf(SceneBox box)
    {
        var look = _look!;
        var dimmed = SelectionFocus.Dims(box.Crate, look.Focus);
        var faded = IsFaded(box);
        return box.Fill == CellFill.Delivered
            ? CrateMaterials.Delivered(look.FillAlpha, dimmed, faded)
            : CrateMaterials.Fill(look.Palette, box.PaletteIndex!.Value, look.FillAlpha, dimmed, _tile, faded);
    }

    /// <summary>Re-adds the crates only when the order changes: turning a little mostly keeps it.</summary>
    private void SortBackToFront(CameraMath camera)
    {
        var (position, look) = (camera.Position, camera.LookDirection);
        GeometryModel3D[] sorted = [.. _crates.OrderByDescending(c => Vector3D.DotProduct(c.Centre - position, look)).Select(c => c.Model)];
        if (sorted.SequenceEqual(Crates.Children.Cast<GeometryModel3D>()))
        {
            return;
        }

        Crates.Children.Clear();
        foreach (var model in sorted)
        {
            Crates.Children.Add(model);
        }
    }

    /// <summary>
    /// The edges as strips one pen wide at the target's plane (nearer a little wider, further narrower): crates in the
    /// theme's edge colour, loaded crates in the loaded edge (K6.4), dimmed with their crates; rack edges, a long ship's
    /// empty racks dimmed (D61); the dashed frame around <c>Ship.Bounds</c>; the free grid one model per rack.
    /// </summary>
    private void BuildStrips(CameraMath camera)
    {
        _edges.Children.Clear();
        FreeGrid.Children.Clear();
        if (_scene is not { } scene || _look is not { } look)
        {
            return;
        }

        var (view, scale) = (camera.LookDirection, camera.PxPerCellAtTarget);
        var isLong = IsoProjection.IsLong(scene.Bounds);
        Dictionary<(Color Colour, double Width), List<BoxEdges.Edge>> strips = [];
        void Add(Color colour, double width, IEnumerable<BoxEdges.Edge> edges)
        {
            if (!strips.TryGetValue((colour, width), out var list))
            {
                strips[(colour, width)] = list = [];
            }

            list.AddRange(edges);
        }

        foreach (var (box, _, _) in _crates)
        {
            var dimmed = SelectionFocus.Dims(box.Crate, look.Focus);
            var (colour, width) = box.Fill == CellFill.Loaded ? (MissionPalette.LoadedEdge, MissionPalette.LoadedEdgeWidth)
                : look.Edge is { } edge ? (edge, CrateEdgeWidth)
                : default;
            if (width > 0)
            {
                Add(colour with { A = CrateMaterials.Shown(colour.A, dimmed, IsFaded(box)) }, width, Inner(box.Bounds));
            }
        }

        foreach (var rack in scene.Racks)
        {
            var dimmed = isLong && !rack.IsLoaded;
            Add(Faded(MissionPalette.RackEdge, dimmed), RackEdgeWidth, BoxEdges.Of(Point(rack.Bounds.Min), Point(rack.Bounds.Max)));
            BoxEdges.Edge[] free = [.. rack.Boxes.Where(b => b.Fill == CellFill.Free && FreeCellGrid.Draws(b, scale)).SelectMany(b => Inner(b.Bounds))];
            if (Strip(free, view, FreeEdgeWidth / 2 / scale, EdgeMaterial(Faded(MissionPalette.FreeEdge, dimmed))) is { } grid)
            {
                FreeGrid.Children.Add(grid);
            }
        }

        var dash = FrameDash * FrameWidth / scale;
        Add(MissionPalette.Frame, FrameWidth, BoxEdges.Of(Point(scene.Bounds.Min), Point(scene.Bounds.Max)).SelectMany(e => BoxEdges.Dashed(e.From, e.To, dash, dash)));

        foreach (var ((colour, width), edges) in strips)
        {
            if (Strip(edges, view, width / 2 / scale, EdgeMaterial(colour)) is { } model)
            {
                _edges.Children.Add(model);
            }
        }
    }

    /// <summary>The hidden-line look's edges, one pixel wide at the target's plane; the zoom picks the edge level (D63).</summary>
    private void ShowHoloLines(CameraMath camera, double zoom)
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
        var (positions, indices) = HoloLines.Strips(holo.Points, segments, look, HoloLines.WidthPixels / 2 / scale);
        _holoLines.Geometry = Frozen(new MeshGeometry3D { Positions = new Point3DCollection(positions), TriangleIndices = new Int32Collection(indices) });
    }

    /// <summary>The edges of a box drawn as the crates are, inset all round.</summary>
    private static IReadOnlyList<BoxEdges.Edge> Inner(ShipBox box) => BoxEdges.Of(
        new Point3D(box.Min.X + Inset, box.Min.Y + Inset, box.Min.Z + Inset), new Point3D(box.Max.X - Inset, box.Max.Y - Inset, box.Max.Z - Inset));

    private static Point3D Point(ShipPoint point) => new(point.X, point.Y, point.Z);

    private static Color Faded(Color colour, bool dimmed) => colour with { A = CrateMaterials.Shown(colour.A, dimmed, faded: false) };

    private bool IsFaded(SceneBox box) => box.Crate is { } crate && _faded.Contains(crate);

    /// <summary>One model for many edges, or null without any edge to show.</summary>
    private static GeometryModel3D? Strip(IReadOnlyList<BoxEdges.Edge> edges, Vector3D look, double halfWidth, Material material)
    {
        if (edges.Count == 0)
        {
            return null;
        }

        var points = new Point3D[2 * edges.Count];
        var segments = new int[2 * edges.Count];
        for (var i = 0; i < edges.Count; i++)
        {
            (points[2 * i], points[2 * i + 1]) = (edges[i].From, edges[i].To);
            (segments[2 * i], segments[2 * i + 1]) = (2 * i, 2 * i + 1);
        }

        var (positions, indices) = HoloLines.Strips(points, segments, look, halfWidth);
        return positions.Length == 0
            ? null
            : Frozen(new GeometryModel3D(Frozen(new MeshGeometry3D { Positions = new Point3DCollection(positions), TriangleIndices = new Int32Collection(indices) }), material));
    }

    private static readonly Dictionary<Color, Material> EdgeMaterials = [];

    // Unlit, so an edge keeps its colour on every face and from every side; its alpha is the material's opacity.
    private static Material EdgeMaterial(Color colour)
    {
        if (!EdgeMaterials.TryGetValue(colour, out var material))
        {
            EdgeMaterials[colour] = material = Unlit(colour with { A = byte.MaxValue }, colour.A / 255.0);
        }

        return material;
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
