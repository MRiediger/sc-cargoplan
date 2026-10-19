using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Projects a <see cref="ShipScene"/> for one camera: visible faces of racks and cells, rack numbers and frame.
/// Pure and cheap, so the view reprojects on every orbit step instead of the view model rebuilding the scene (D15).
/// </summary>
public static class SceneProjector
{
    // A face seen exactly edge-on has no area; it is left out instead of drawn as a line.
    private const double EdgeOn = 1e-9;

    // Same rounding as DrawOrder: faces of equal projected area stay equal, so ties fall to the order below.
    private const int AreaDecimals = 9;

    // Drawn size of a cell: the gap between neighbours shows the grid (D21).
    private const double CellSize = 0.9;

    // Also the preference on equal area: top or bottom first, then the sides, then the ends.
    private static readonly BoxFace[] AllFaces = [BoxFace.Top, BoxFace.Bottom, BoxFace.Port, BoxFace.Starboard, BoxFace.Bow, BoxFace.Aft];

    public static ProjectedScene Project(ShipScene scene, IsoProjection camera)
    {
        var visible = VisibleFaces(camera);
        var inset = (1 - CellSize) / 2;

        var cells = DrawOrder.CellsBackToFront(scene.Racks, camera)
            .Select(c => (c.Rack.RackId, Cell: new ProjectedCell(c.Cell, Faces(Corners(c.Cell.Bounds, inset), visible, camera))))
            .ToArray();
        var cellsOfRack = cells.ToLookup(c => c.RackId, c => c.Cell);

        ProjectedRack[] racks = [.. DrawOrder.BackToFront(scene.Racks, camera).Select(r => ProjectRack(r, [.. cellsOfRack[r.RackId]], visible, camera))];
        var (frame, extent) = Frame(scene.Bounds, camera);
        return new ProjectedScene(racks, [.. cells.Select(c => c.Cell)], frame, extent);
    }

    /// <summary>The faces turned towards the camera: their outward normal points against the view direction.</summary>
    public static IReadOnlyList<BoxFace> VisibleFaces(IsoProjection camera) => [.. AllFaces.Where(f => Facing(f, camera) < -EdgeOn)];

    private static ProjectedRack ProjectRack(SceneRack rack, IReadOnlyList<ProjectedCell> cells, IReadOnlyList<BoxFace> visible, IsoProjection camera)
    {
        var box = rack.Bounds;
        var faces = Faces(Corners(box, 0), visible, camera);

        // Orthographic: a face appears scaled by how squarely it faces the camera.
        var label = faces.OrderByDescending(f => Math.Round(Area(f.Face, box) * -Facing(f.Face, camera), AreaDecimals)).First();
        Point centre = new(label.Polygon.Average(p => p.X), label.Polygon.Average(p => p.Y));

        return new ProjectedRack(rack.RackId, rack.Number, rack.IsViolated, faces, cells, centre);
    }

    /// <summary>The face's outward normal · view direction: negative when it faces the camera.</summary>
    private static double Facing(BoxFace face, IsoProjection camera)
    {
        var d = camera.ViewDirection;
        return face switch
        {
            BoxFace.Top => d.Z,
            BoxFace.Bottom => -d.Z,
            BoxFace.Port => d.Y,
            BoxFace.Starboard => -d.Y,
            BoxFace.Bow => d.X,
            BoxFace.Aft => -d.X,
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
        };
    }

    private static double Area(BoxFace face, ShipBox box)
    {
        var (dx, dy, dz) = (box.Max.X - box.Min.X, box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z);
        return face switch
        {
            BoxFace.Top or BoxFace.Bottom => dx * dy,
            BoxFace.Port or BoxFace.Starboard => dx * dz,
            _ => dy * dz,
        };
    }

    /// <summary>The box's corners, moved <paramref name="inset"/> cells inwards on every side.</summary>
    private static (Corner Min, Corner Max) Corners(ShipBox box, double inset) => (
        new Corner(box.Min.X + inset, box.Min.Y + inset, box.Min.Z + inset),
        new Corner(box.Max.X - inset, box.Max.Y - inset, box.Max.Z - inset));

    private static ProjectedFace[] Faces((Corner Min, Corner Max) box, IReadOnlyList<BoxFace> visible, IsoProjection camera) =>
        [.. visible.Select(f => new ProjectedFace(f, Polygon(f, box.Min, box.Max, camera)))];

    private static Point[] Polygon(BoxFace face, Corner min, Corner max, IsoProjection camera)
    {
        var (aft, bow) = (min.X, max.X);
        return face switch
        {
            BoxFace.Top => Level(max.Z),
            BoxFace.Bottom => Level(min.Z),
            BoxFace.Port => Side(max.Y),
            BoxFace.Starboard => Side(min.Y),
            BoxFace.Bow => End(max.X),
            BoxFace.Aft => End(min.X),
            _ => throw new ArgumentOutOfRangeException(nameof(face), face, null),
        };

        Point[] Level(double z) =>
            [camera.Project(aft, min.Y, z), camera.Project(bow, min.Y, z), camera.Project(bow, max.Y, z), camera.Project(aft, max.Y, z)];

        Point[] Side(double y) =>
            [camera.Project(aft, y, min.Z), camera.Project(bow, y, min.Z), camera.Project(bow, y, max.Z), camera.Project(aft, y, max.Z)];

        Point[] End(double x) =>
            [camera.Project(x, min.Y, min.Z), camera.Project(x, max.Y, min.Z), camera.Project(x, max.Y, max.Z), camera.Project(x, min.Y, max.Z)];
    }

    /// <summary>The 12 edges of the box: every pair of corners that differ on exactly one axis.</summary>
    private static (SceneLine[] Edges, Rect Extent) Frame(ShipBox box, IsoProjection camera)
    {
        Point[] corners = [.. Enumerable.Range(0, 8).Select(i => camera.Project(
            (i & 1) == 0 ? box.Min.X : box.Max.X,
            (i & 2) == 0 ? box.Min.Y : box.Max.Y,
            (i & 4) == 0 ? box.Min.Z : box.Max.Z))];

        SceneLine[] edges = [.. from i in Enumerable.Range(0, 8)
                                from axis in (int[])[1, 2, 4]
                                where (i & axis) == 0
                                select new SceneLine(corners[i], corners[i | axis])];

        var extent = corners.Aggregate(new Rect(corners[0], corners[0]), (rect, p) => Rect.Union(rect, p));
        return (edges, extent);
    }

    /// <summary>A box corner in cells; not a <see cref="ShipPoint"/>, a drawn cell is smaller than a whole cell.</summary>
    private readonly record struct Corner(double X, double Y, double Z);
}
