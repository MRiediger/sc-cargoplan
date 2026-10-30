using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.Core.Models;

namespace CargoPlan.App.ShipView;

/// <summary>The overlay's theme values: the rack numbers and the selection's pens (D31, K4.4).</summary>
/// <param name="NumberBrush">null = no numbers and no "Bow" label.</param>
/// <param name="HaloBrush">The numbers' outline (D28); null = none.</param>
public sealed record OverlayLook(
    Brush? NumberBrush, double NumberFontSize, Brush? HaloBrush, double HaloSize, Pen? SelectionPen, Pen? SelectionOutlinePen, Typeface Typeface, double PixelsPerDip);

/// <summary>
/// What lies on top of the 3D scene whatever covers it, drawn in 2D through <see cref="CameraMath"/> (D83, T8.2, R1):
/// violated crates and racks in red, a drag's target rack in cyan, the selected missions' frame and the selected crates'
/// edges, a drag's ghosts (T8.4, R1), then the rack numbers and the "Bow" label. The pens are the 2D view's, in screen
/// pixels, so the settings keep their meaning (K4.4); a violation, the selection or the drop target never hides behind
/// cargo, as before. Numbers hide behind other racks (<see cref="RackLabels"/>).
/// </summary>
public static class ShipOverlay
{
    // On a long ship the rack number stands this many pixels above its rack (D61).
    public const double LabelGap = 4;

    // Drawn size of a crate: every box is this much smaller all round, as the crates in the scene (T3.11b).
    private const double Inset = 0.05;

    // The label is built at this size, then laid onto the wedge's floor, so only the proportions count (D52).
    private const string BowLabel = "Bow";
    private const double BowLabelEmSize = 100;

    private static readonly Pen ViolationPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.Violation), MissionPalette.EdgeWidth));

    // Under the red (K4.2, P6): round joins, so the wider dark line has no spikes at sharp corners.
    private static readonly Pen EdgeUnderPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.EdgeUnderStroke), MissionPalette.EdgeUnderStrokeWidth)
    {
        LineJoin = PenLineJoin.Round,
    });

    private static readonly Pen MissionFramePen = Frozen(new Pen(new SolidColorBrush(MissionPalette.MissionFrame), 1.5));

    // The drop target's rack edge and the cyan ghosts' edge (D25, D65).
    private static readonly Pen DragTargetPen = Frozen(new Pen(new SolidColorBrush(MissionPalette.DragTarget), MissionPalette.EdgeWidth));

    /// <summary>The room above a long ship's racks for their numbers (D61), for the fit; 0 on a compact ship.</summary>
    public static double RoomOnTop(ShipScene scene, OverlayLook look) =>
        IsoProjection.IsLong(scene.Bounds) ? look.NumberFontSize + LabelGap + look.HaloSize : 0;

    /// <param name="drag">What a running drag shows (<see cref="DragPreview"/>); <see cref="DragPreview.None"/> without one.</param>
    public static void Draw(DrawingContext dc, ShipScene scene, CameraMath camera, CrateSelection selection, OverlayLook look, DragPreview drag)
    {
        // A faulty crate gets its own red outline over a dark one, so the player sees which (D24).
        var boxes = scene.Racks.SelectMany(r => r.Boxes).ToArray();
        foreach (var box in boxes.Where(b => b.IsViolated))
        {
            foreach (var face in Faces(box.Bounds, Inset, camera))
            {
                dc.DrawGeometry(null, EdgeUnderPen, face);
                dc.DrawGeometry(null, ViolationPen, face);
            }
        }

        foreach (var rack in scene.Racks.Where(r => r.IsViolated || r.RackId == drag.TargetRackId))
        {
            foreach (var face in Faces(rack.Bounds, 0, camera))
            {
                dc.DrawGeometry(null, rack.RackId == drag.TargetRackId ? DragTargetPen : ViolationPen, face);
            }
        }

        // The selected missions' crates, then the selected crates on top: edges only, the fill stays the stop colour (D25).
        var selected = selection.In(scene);
        var focus = SelectionFocus.Missions(selected);
        foreach (var box in boxes.Where(b => SelectionFocus.Frames(b.Crate, focus)))
        {
            foreach (var face in Faces(box.Bounds, Inset, camera))
            {
                dc.DrawGeometry(null, MissionFramePen, face);
            }
        }

        if (look.SelectionPen is { } selectionPen)
        {
            foreach (var box in boxes.Where(b => b.Crate is { } crate && selected.Contains(crate)))
            {
                foreach (var face in Faces(box.Bounds, Inset, camera))
                {
                    if (look.SelectionOutlinePen is { } outline)
                    {
                        dc.DrawGeometry(null, outline, face);
                    }

                    dc.DrawGeometry(null, selectionPen, face);
                }
            }
        }

        // The ghosts: cyan where Core puts every crate that changes place, red at the cells aimed at where Core refuses
        // (D25, D65); the whole cells, as in the 2D view, filled and edged over the dark under-stroke (K4.2).
        foreach (var (ghosts, fill, pen) in new[] { (drag.Ghosts, MissionPalette.DropBrush, DragTargetPen), (drag.Refused, MissionPalette.RefusedDropBrush, ViolationPen) })
        {
            foreach (var crate in ghosts)
            {
                foreach (var face in Faces(scene.Ship.Racks.Single(r => r.Id == crate.RackId).CellBox(crate), 0, camera, filled: true))
                {
                    dc.DrawGeometry(fill, EdgeUnderPen, face);
                    dc.DrawGeometry(null, pen, face);
                }
            }
        }

        if (look.NumberBrush is not { } brush)
        {
            return;
        }

        // One size for the rack numbers and the bow label (D32, D52).
        var size = RackNumberSize.For(look.NumberFontSize, camera.PxPerCellAtTarget, scene.Racks);
        var halo = look.HaloBrush is { } haloBrush ? Frozen(new Pen(haloBrush, look.HaloSize) { LineJoin = PenLineJoin.Round }) : null;
        if (scene.Marker is BowMarker.Wedge)
        {
            DrawBowLabel(dc, scene, camera, look, brush, halo, size);
        }

        foreach (var label in RackLabels.Place(scene, camera).Where(l => !l.Hidden))
        {
            FormattedText text = new(
                label.Number.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, look.Typeface, size, brush, look.PixelsPerDip);

            // Above its rack the number stands on its baseline (D61); on its face it is centred.
            var top = label.Above ? label.Anchor.Y - LabelGap - text.Baseline : label.Anchor.Y - text.Height / 2;
            DrawLabel(dc, text.BuildGeometry(new Point(label.Anchor.X - text.Width / 2, top)), brush, halo);
        }
    }

    /// <summary>The label lies on the wedge's floor (D52), linearised at its middle; hidden where a rack covers that middle.</summary>
    private static void DrawBowLabel(DrawingContext dc, ShipScene scene, CameraMath camera, OverlayLook look, Brush brush, Pen? halo, double size)
    {
        var floor = BowWedge.Floor(scene.Bounds, camera.Project);
        var bounds = scene.Bounds;
        var tip = BowWedge.Faces(bounds).Single(f => f.Face == BoxFace.Bow).Corners[0].X;
        Point3D middle = new((bounds.Max.X + tip) / 2, (bounds.Min.Y + bounds.Max.Y) / 2.0, bounds.Min.Z);
        if (RackLabels.Covered(scene, null, middle, camera))
        {
            return;
        }

        FormattedText text = new(BowLabel, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, look.Typeface, BowLabelEmSize, brush, look.PixelsPerDip);

        var glyphs = FloorLabel.Lay(text.BuildGeometry(new Point(0, 0)), BowLabelEmSize, floor, size);
        DrawLabel(dc, glyphs, brush, halo);
    }

    // Outline first, fill on top: the outline only shows outside the glyphs.
    private static void DrawLabel(DrawingContext dc, Geometry glyphs, Brush brush, Pen? halo)
    {
        if (halo is not null)
        {
            dc.DrawGeometry(null, halo, glyphs);
        }

        dc.DrawGeometry(brush, null, glyphs);
    }

    /// <summary>The visible faces of a box, inset by <paramref name="inset"/>, as screen polygons; faces reaching behind the camera are left out.</summary>
    private static IEnumerable<Geometry> Faces(ShipBox box, double inset, CameraMath camera, bool filled = false)
    {
        Point3D min = new(box.Min.X + inset, box.Min.Y + inset, box.Min.Z + inset);
        Point3D max = new(box.Max.X - inset, box.Max.Y - inset, box.Max.Z - inset);
        foreach (var face in BoxFaces.Visible(min, max, camera))
        {
            if (face.Corners.Any(c => Vector3D.DotProduct(c - camera.Position, camera.LookDirection) <= camera.Near))
            {
                continue;
            }

            Point[] points = [.. face.Corners.Select(camera.Project)];
            StreamGeometry geometry = new();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], isFilled: filled, isClosed: true);
                context.PolyLineTo(points[1..], isStroked: true, isSmoothJoin: false);
            }

            yield return Frozen(geometry);
        }
    }

    private static T Frozen<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
