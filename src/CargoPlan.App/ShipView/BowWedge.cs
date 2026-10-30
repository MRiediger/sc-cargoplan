using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>One face of the bow wedge in ship coordinates (D52): <see cref="BoxFace.Bow"/> is its tip, Port and Starboard its flanks.</summary>
/// <param name="Normal">Outward, unit length; the flanks' normals are not along an axis.</param>
public sealed record BowWedgeFace(BoxFace Face, IReadOnlyList<Point3D> Corners, (double X, double Y, double Z) Normal);

/// <summary>
/// The bow wedge's floor as the camera sees it, for the label lying on it (D52): from <paramref name="Centre"/>, one cell
/// across the ship (+y) and one cell towards the bow (+x), in screen DIPs; <paramref name="Room"/> in cells, across by
/// along.
/// </summary>
public sealed record WedgeFloor(Point Centre, Vector Across, Vector Bowwards, Size Room);

/// <summary>
/// The bow wedge (D52): a nose beyond the bow end of <c>Ship.Bounds</c>, a tenth of the ship's length but at least one
/// cell, narrowing to half the width at the full height. The racks reach the bow, so it can't taper within them. Only a
/// marker for the bow; the racks give no hull shape. The scene draws it (<see cref="ShipScene3D"/>), the fit shows it
/// (<see cref="CameraFit"/>), the "Bow" label lies on its floor (<see cref="FloorLabel"/>).
/// </summary>
public static class BowWedge
{
    private const double NoseMinLength = 1;
    private const double NoseLengthShare = 0.1;
    private const double NoseTipWidthShare = 0.5;

    // The "Bow" label on the wedge's floor keeps this share of the nose's length and of the tip's width.
    private const double LabelRoomShare = 0.7;

    /// <summary>The 5 faces of the wedge in ship coordinates: the tip, top, bottom and the two flanks; its base is the box's bow face.</summary>
    public static IReadOnlyList<BowWedgeFace> Faces(ShipBox box)
    {
        var (min, max) = (box.Min, box.Max);
        var nose = Math.Max(NoseMinLength, NoseLengthShare * (max.X - min.X));
        var tipX = max.X + nose;
        var centreY = (min.Y + max.Y) / 2.0;
        var halfTip = NoseTipWidthShare * (max.Y - min.Y) / 2;

        // Round the bow face and the tip: starboard bottom, port bottom, port top, starboard top.
        Point3D[] bow = [new(max.X, min.Y, min.Z), new(max.X, max.Y, min.Z), new(max.X, max.Y, max.Z), new(max.X, min.Y, max.Z)];
        Point3D[] tip =
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

    /// <summary>
    /// The "Bow" label's floor through <paramref name="project"/> (<see cref="CameraMath.Project"/>): its middle, the
    /// screen vectors of one cell across and bowwards there, and its room, the middle of the floor's length and of the
    /// tip's width, the narrow end of the trapezoid. In perspective the vectors hold at the floor's middle.
    /// </summary>
    public static WedgeFloor Floor(ShipBox box, Func<Point3D, Point> project)
    {
        var tip = Faces(box).Single(f => f.Face == BoxFace.Bow).Corners;
        var (nose, tipWidth) = (tip[0].X - box.Max.X, tip.Max(c => c.Y) - tip.Min(c => c.Y));
        Point3D middle = new(box.Max.X + nose / 2, (box.Min.Y + box.Max.Y) / 2.0, box.Min.Z);
        var centre = project(middle);
        return new WedgeFloor(
            centre,
            project(middle + new Vector3D(0, 1, 0)) - centre,
            project(middle + new Vector3D(1, 0, 0)) - centre,
            new Size(LabelRoomShare * tipWidth, LabelRoomShare * nose));
    }
}
