using System.Windows;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Everything the ship view draws, already projected (<see cref="IsoProjection"/> units, not pixels). Built once per
/// plan by <see cref="ShipViewBuilder"/>; the control only transforms and paints it. A record, not a view model:
/// nothing in it changes.
/// </summary>
/// <param name="Racks">Back to front (<see cref="DrawOrder"/>).</param>
/// <param name="Frame">The 12 edges of <c>Ship.Bounds</c>.</param>
/// <param name="Extent">Encloses the frame, for fit-to-view.</param>
/// <param name="Hints">Planner hints (<c>LoadPlan.Warnings</c>), once each, word for word (D7).</param>
/// <param name="Violations">From <c>LoadPlanner.Validate</c>; these racks are red (D10).</param>
/// <param name="PaletteRepeats">More stops than colours.</param>
public sealed record ShipScene(
    IReadOnlyList<SceneRack> Racks,
    IReadOnlyList<SceneLine> Frame,
    Rect Extent,
    IReadOnlyList<LegendEntry> Legend,
    IReadOnlyList<string> Hints,
    IReadOnlyList<string> Violations,
    bool PaletteRepeats);

/// <param name="Faces">The three visible faces: top, port, bow.</param>
/// <param name="Strips">The segments on those faces; together they cover the faces.</param>
/// <param name="LabelCentre">Centre of the bow face, where the rack number goes.</param>
public sealed record SceneRack(
    int Number,
    bool IsViolated,
    IReadOnlyList<IReadOnlyList<Point>> Faces,
    IReadOnlyList<SceneStrip> Strips,
    Point LabelCentre);

/// <summary>One segment on one face. <paramref name="PaletteIndex"/> and <paramref name="ToolTip"/> are null for free space.</summary>
public sealed record SceneStrip(IReadOnlyList<Point> Polygon, SegmentFill Fill, int? PaletteIndex, string? ToolTip);

public enum SegmentFill
{
    Mission,
    Delivered,
    Free,
}

public sealed record SceneLine(Point From, Point To);

/// <summary>One stop in the legend, in route order.</summary>
public sealed record LegendEntry(int PaletteIndex, string Destination, int Scu)
{
    public string Label => Invariant($"{Destination} · {Scu} SCU");
}
