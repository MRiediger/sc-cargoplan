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
    /// <paramref name="glyphs"/>, built at <paramref name="emSize"/>, laid by <see cref="Placement"/> and flattened into
    /// screen pixels, so a pen strokes it in pixels instead of skewed with it.
    /// </summary>
    public static PathGeometry Lay(Geometry glyphs, double emSize, WedgeFloor floor, ViewTransform transform, double screenEmSize)
    {
        var laid = glyphs.Clone();
        laid.Transform = new MatrixTransform(Placement(glyphs.Bounds, emSize, floor, transform, screenEmSize));
        return laid.GetFlattenedPathGeometry();
    }

    /// <summary>
    /// From text space (x along the line, y down, built at <paramref name="emSize"/>) to screen pixels on
    /// <paramref name="floor"/>, the <paramref name="ink"/> centred: the line runs across the ship and the letters' tops
    /// point aft, so it reads from in front of the bow. Along the letters' upright axis one em is
    /// <paramref name="screenEmSize"/> pixels, so a capital stands as tall on the screen as a digit of that size; at
    /// most as large as the floor's room, but never smaller than the smallest rack number for it (D32, D61): a floor
    /// too small for that is left by the label, as rack numbers below it overlap.
    /// </summary>
    public static Matrix Placement(Rect ink, double emSize, WedgeFloor floor, ViewTransform transform, double screenEmSize)
    {
        // The upright axis lies along the ship's x: one cell of it is this many pixels at the current camera and zoom.
        var uprightPixelsPerCell = floor.Bowwards.Length * transform.Scale;
        double CellsPerUnitAt(double pixels) => pixels / (emSize * uprightPixelsPerCell);

        // A setting below the minimum is kept, as for the rack numbers.
        var room = Math.Min(floor.Room.Width / ink.Width, floor.Room.Height / ink.Height);
        var cellsPerUnit = Math.Min(
            CellsPerUnitAt(screenEmSize),
            Math.Max(room, CellsPerUnitAt(Math.Min(screenEmSize, RackNumberSize.MinSize))));
        var across = floor.Across * (transform.Scale * cellsPerUnit);
        var down = floor.Bowwards * (transform.Scale * cellsPerUnit);

        // Text space: the ink's centre goes to the floor's centre.
        var origin = transform.Apply(floor.Centre) - across * (ink.X + ink.Width / 2) - down * (ink.Y + ink.Height / 2);
        return new Matrix(across.X, across.Y, down.X, down.Y, origin.X, origin.Y);
    }
}
