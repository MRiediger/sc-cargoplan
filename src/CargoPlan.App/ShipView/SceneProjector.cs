using System.Windows;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Projects a <see cref="ShipScene"/> for one camera: visible faces of racks and their boxes, rack numbers, frame and bow wedge.
/// Pure and cheap, so the view reprojects on every orbit step instead of the view model rebuilding the scene (D15).
/// </summary>
public static class SceneProjector
{
    // A face seen exactly edge-on has no area; it is left out instead of drawn as a line.
    private const double EdgeOn = 1e-9;

    // Same rounding as DrawOrder: faces of equal projected area stay equal, so ties fall to the order below.
    private const int AreaDecimals = 9;

    // Drawn size of a cell: every box is this much smaller all round, so the gap between boxes is the same everywhere (D21).
    private const double CellSize = 0.9;
    private const double Inset = (1 - CellSize) / 2;

    // The bow wedge (D52): a nose beyond the bow end of the frame, a tenth of the ship's length but at least one cell,
    // narrowing to half the width at the full height. The racks reach the bow, so it can't taper within the frame.
    private const double NoseMinLength = 1;
    private const double NoseLengthShare = 0.1;
    private const double NoseTipWidthShare = 0.5;

    // The "Bow" label on the wedge's floor keeps this share of the nose's length and of the tip's width.
    private const double LabelRoomShare = 0.7;

    // Also the preference on equal area: top or bottom first, then the sides, then the ends.
    private static readonly BoxFace[] AllFaces = [BoxFace.Top, BoxFace.Bottom, BoxFace.Port, BoxFace.Starboard, BoxFace.Bow, BoxFace.Aft];

    public static ProjectedScene Project(ShipScene scene, IsoProjection camera)
    {
        var visible = VisibleFaces(camera);

        ProjectedRack[] racks = [.. DrawOrder.BackToFront(scene.Racks, camera).Select(r => ProjectRack(r, visible, camera))];
        var frame = Frame(scene.Bounds, camera);
        return new ProjectedScene(racks, frame, ProjectWedge(scene.Bounds, camera), Extent(scene.Bounds, frame, camera));
    }

    /// <summary>The visible faces of one box, drawn as small as a crate of the scene: the ghost of a dragged crate (D25).</summary>
    public static IReadOnlyList<ProjectedFace> ProjectBox(ShipBox box, IsoProjection camera) =>
        Faces(Corners(box, Inset), VisibleFaces(camera), camera);

    /// <summary>The faces turned towards the camera: their outward normal points against the view direction.</summary>
    public static IReadOnlyList<BoxFace> VisibleFaces(IsoProjection camera) => [.. AllFaces.Where(f => Facing(f, camera) < -EdgeOn)];

    private static ProjectedRack ProjectRack(SceneRack rack, IReadOnlyList<BoxFace> visible, IsoProjection camera)
    {
        var box = rack.Bounds;
        var faces = Faces(Corners(box, 0), visible, camera);
        ProjectedBox[] boxes = [.. DrawOrder.BoxesBackToFront(rack.Boxes, camera)
            .Select(b => new ProjectedBox(b, Faces(Corners(b.Bounds, Inset), visible, camera)))];

        // Orthographic: a face appears scaled by how squarely it faces the camera.
        var label = faces.OrderByDescending(f => Math.Round(Area(f.Face, box) * -Facing(f.Face, camera), AreaDecimals)).First();
        Point centre = new(label.Polygon.Average(p => p.X), label.Polygon.Average(p => p.Y));

        return new ProjectedRack(rack.RackId, rack.Number, rack.IsViolated, faces, boxes, centre);
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

    /// <summary>
    /// The 5 faces of the bow wedge in ship coordinates (D52): the tip, top, bottom and the two flanks; its base is the
    /// box's bow face. Only a marker for the bow; the racks give no hull shape.
    /// </summary>
    public static IReadOnlyList<BowWedgeFace> BowWedge(ShipBox box)
    {
        var (min, max) = (box.Min, box.Max);
        var nose = Math.Max(NoseMinLength, NoseLengthShare * (max.X - min.X));
        var tipX = max.X + nose;
        var centreY = (min.Y + max.Y) / 2.0;
        var halfTip = NoseTipWidthShare * (max.Y - min.Y) / 2;

        // Round the bow face and the tip: starboard bottom, port bottom, port top, starboard top.
        Corner[] bow = [new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(max.X, min.Y, max.Z)];
        Corner[] tip =
        [
            new(tipX, centreY - halfTip, min.Z), new(tipX, centreY + halfTip, min.Z),
            new(tipX, centreY + halfTip, max.Z), new(tipX, centreY - halfTip, max.Z),
        ];

        // A flank narrows by `inwards` over `nose`; its normal is at right angles to that, in the x-y plane.
        var inwards = max.Y - (centreY + halfTip);
        var length = Math.Sqrt(inwards * inwards + nose * nose);
        return
        [
            new(BoxFace.Bow, tip, (1, 0, 0)),
            new(BoxFace.Top, [bow[3], bow[2], tip[2], tip[3]], (0, 0, 1)),
            new(BoxFace.Bottom, [bow[0], bow[1], tip[1], tip[0]], (0, 0, -1)),
            new(BoxFace.Port, [bow[1], bow[2], tip[2], tip[1]], (inwards / length, nose / length, 0)),
            new(BoxFace.Starboard, [bow[0], bow[3], tip[3], tip[0]], (inwards / length, -nose / length, 0)),
        ];
    }

    private static ProjectedWedge ProjectWedge(ShipBox box, IsoProjection camera)
    {
        var d = camera.ViewDirection;
        var wedge = BowWedge(box);
        ProjectedFace[] faces = [.. from face in wedge
                                    where face.Normal.X * d.X + face.Normal.Y * d.Y + face.Normal.Z * d.Z < -EdgeOn
                                    select new ProjectedFace(face.Face, [.. face.Corners.Select(c => camera.Project(c.X, c.Y, c.Z))])];

        // The label's room: the middle of the floor's length and of the tip's width, the narrow end of the trapezoid.
        var tip = wedge.Single(f => f.Face == BoxFace.Bow).Corners;
        var (nose, tipWidth) = (tip[0].X - box.Max.X, tip.Max(c => c.Y) - tip.Min(c => c.Y));
        var origin = camera.Project(0, 0, 0);
        WedgeFloor floor = new(
            camera.Project(box.Max.X + nose / 2, (box.Min.Y + box.Max.Y) / 2.0, box.Min.Z),
            camera.Project(0, 1, 0) - origin,
            camera.Project(1, 0, 0) - origin,
            new Size(LabelRoomShare * tipWidth, LabelRoomShare * nose));

        // The camera looks against +x: the wedge, beyond every rack's bow end, is nearer.
        return new ProjectedWedge(faces, InFront: d.X < 0, floor);
    }

    /// <summary>The 12 edges of the box: every pair of corners that differ on exactly one axis.</summary>
    private static SceneLine[] Frame(ShipBox box, IsoProjection camera)
    {
        Point[] corners = [.. Enumerable.Range(0, 8).Select(i => camera.Project(
            (i & 1) == 0 ? box.Min.X : box.Max.X,
            (i & 2) == 0 ? box.Min.Y : box.Max.Y,
            (i & 4) == 0 ? box.Min.Z : box.Max.Z))];

        return [.. from i in Enumerable.Range(0, 8)
                   from axis in (int[])[1, 2, 4]
                   where (i & axis) == 0
                   select new SceneLine(corners[i], corners[i | axis])];
    }

    /// <summary>Encloses the frame and every corner of the wedge, seen or not, so the fit doesn't change as faces turn away.</summary>
    private static Rect Extent(ShipBox box, IReadOnlyList<SceneLine> frame, IsoProjection camera)
    {
        Point[] points =
        [
            .. frame.SelectMany(l => new[] { l.From, l.To }),
            .. BowWedge(box).SelectMany(f => f.Corners).Select(c => camera.Project(c.X, c.Y, c.Z)),
        ];
        return points.Aggregate(new Rect(points[0], points[0]), (rect, p) => Rect.Union(rect, p));
    }

    /// <summary>A point in cells; not a <see cref="ShipPoint"/>, a drawn box is smaller than its whole cells.</summary>
    public readonly record struct Corner(double X, double Y, double Z);
}
