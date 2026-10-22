using System.Windows;
using System.Windows.Media;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Lays text flat on the bow wedge's floor (D52), like a marking painted on it: it turns and tilts with the ship. Seen
/// from below it reads mirrored, as paint on a floor would.
/// </summary>
public static class FloorLabel
{
    /// <summary>
    /// <paramref name="glyphs"/> in text space (x along the line, y down) on <paramref name="floor"/>, their ink centred:
    /// the line runs across the ship and the letters' tops point aft, so it reads from in front of the bow.
    /// <paramref name="sizeShare"/> of the largest size its room allows, at most all of it. Flattened into screen pixels,
    /// so a pen strokes it in pixels instead of skewed with it.
    /// </summary>
    public static PathGeometry Lay(Geometry glyphs, WedgeFloor floor, ViewTransform transform, double sizeShare)
    {
        var ink = glyphs.Bounds;
        var cellsPerUnit = Math.Min(sizeShare, 1) * Math.Min(floor.Room.Width / ink.Width, floor.Room.Height / ink.Height);
        var across = floor.Across * (transform.Scale * cellsPerUnit);
        var down = floor.Bowwards * (transform.Scale * cellsPerUnit);

        // Text space: the ink's centre goes to the floor's centre.
        var origin = transform.Apply(floor.Centre) - across * (ink.X + ink.Width / 2) - down * (ink.Y + ink.Height / 2);

        var laid = glyphs.Clone();
        laid.Transform = new MatrixTransform(across.X, across.Y, down.X, down.Y, origin.X, origin.Y);
        return laid.GetFlattenedPathGeometry();
    }
}
