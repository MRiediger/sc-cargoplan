namespace CargoPlan.App.ShipView;

/// <summary>
/// The screen size of the rack numbers (D32): the setting at every zoom, so it means the same at 0.5× and 4×. Only where
/// the number would be taller than the shortest rack side on screen does it shrink, down to <see cref="MinSize"/>; below
/// that the numbers overlap, as they did before T4.6, instead of turning unreadably small.
/// </summary>
public static class RackNumberSize
{
    public const double MinSize = 12;

    /// <param name="fontSize">The setting, in pixels; one below <see cref="MinSize"/> is kept as it is.</param>
    /// <param name="scale">Pixels per cell at the target's plane (<see cref="CameraMath.PxPerCellAtTarget"/>), fit and zoom included.</param>
    public static double For(double fontSize, double scale, IReadOnlyList<SceneRack> racks) =>
        Math.Min(fontSize, Math.Max(MinSize, ShortestSide(racks) * scale));

    /// <summary>
    /// The shortest edge of any rack, in cells (Hull A: 2). One value for all racks, so equal racks get equal numbers.
    /// A heuristic: how tall that edge appears depends on the camera.
    /// </summary>
    public static int ShortestSide(IReadOnlyList<SceneRack> racks) => racks.Min(r =>
    {
        var (min, max) = (r.Bounds.Min, r.Bounds.Max);
        return Math.Min(max.X - min.X, Math.Min(max.Y - min.Y, max.Z - min.Z));
    });
}
