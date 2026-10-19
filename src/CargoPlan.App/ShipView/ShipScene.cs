using System.Windows;
using CargoPlan.Core.Models;
using static System.FormattableString;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Everything the ship view shows, in ship coordinates, independent of the camera. Built once per plan by
/// <see cref="ShipViewBuilder"/>; <see cref="SceneProjector"/> turns it into a <see cref="ProjectedScene"/> per camera.
/// A record, not a view model: nothing in it changes.
/// </summary>
/// <param name="Racks">In rack order; the draw order depends on the camera (<see cref="DrawOrder"/>).</param>
/// <param name="Bounds"><c>Ship.Bounds</c>, drawn as the frame.</param>
/// <param name="Hints">Planner hints (<c>LoadPlan.Warnings</c>), once each, word for word (D7).</param>
/// <param name="ReservedHints">
/// The hints of the same plan with nothing delivered. The legend reserves their space, so ticking a stop off never
/// resizes the ship view (D16). Never fewer or shorter than <paramref name="Hints"/>.
/// </param>
/// <param name="Violations">From <c>LoadPlanner.Validate</c>; these racks are red (D10).</param>
/// <param name="PaletteRepeats">More stops than colours.</param>
public sealed record ShipScene(
    IReadOnlyList<SceneRack> Racks,
    ShipBox Bounds,
    IReadOnlyList<LegendEntry> Legend,
    IReadOnlyList<string> Hints,
    IReadOnlyList<string> ReservedHints,
    IReadOnlyList<string> Violations,
    bool PaletteRepeats)
{
    /// <summary>Every stop is delivered; false without stops.</summary>
    public bool AllDelivered => Legend.Count > 0 && Legend.All(e => e.Delivered);
}

/// <param name="RackId">The ship's rack id, the target of a drop (<c>LoadPlanner.MovePlacement</c>).</param>
/// <param name="Cells">Every cell of the rack's grid once, in fill order (<see cref="RackCells"/>).</param>
public sealed record SceneRack(string RackId, int Number, bool IsViolated, ShipBox Bounds, IReadOnlyList<SceneCell> Cells);

/// <summary>
/// One SCU of a rack, a box of one cell in ship coordinates. <paramref name="PaletteIndex"/>, <paramref name="ToolTip"/>
/// and <paramref name="Placement"/> are null for a free cell.
/// </summary>
public sealed record SceneCell(ShipBox Bounds, CellFill Fill, int? PaletteIndex, string? ToolTip, Placement? Placement);

public enum CellFill
{
    Mission,
    Delivered,
    Free,
}

/// <summary>The six faces of an axis-aligned box, named in ship words.</summary>
public enum BoxFace
{
    Top,
    Bottom,
    Port,
    Starboard,
    Bow,
    Aft,
}

/// <summary>
/// A <see cref="ShipScene"/> seen from one camera, in projected units (not pixels); the control only transforms and
/// paints it.
/// </summary>
/// <param name="Racks">Back to front (<see cref="DrawOrder"/>).</param>
/// <param name="Cells">The cells of all racks, back to front (<see cref="DrawOrder.CellsBackToFront"/>): the painter's order.</param>
/// <param name="Frame">The 12 edges of <c>Ship.Bounds</c>.</param>
/// <param name="Extent">Encloses the frame, for fit-to-view.</param>
public sealed record ProjectedScene(IReadOnlyList<ProjectedRack> Racks, IReadOnlyList<ProjectedCell> Cells, IReadOnlyList<SceneLine> Frame, Rect Extent);

/// <param name="Faces">The faces of the rack's box the camera sees: its edges and its area for the hit test.</param>
/// <param name="Cells">The rack's own cells, back to front, the same objects as in <see cref="ProjectedScene.Cells"/>.</param>
/// <param name="LabelCentre">Centre of the largest visible face, where the rack number goes.</param>
public sealed record ProjectedRack(
    string RackId,
    int Number,
    bool IsViolated,
    IReadOnlyList<ProjectedFace> Faces,
    IReadOnlyList<ProjectedCell> Cells,
    Point LabelCentre);

public sealed record ProjectedFace(BoxFace Face, IReadOnlyList<Point> Polygon);

/// <summary>A <see cref="SceneCell"/> as the camera sees it, drawn slightly smaller than a cell so the grid shows.</summary>
public sealed record ProjectedCell(SceneCell Cell, IReadOnlyList<ProjectedFace> Faces);

public sealed record SceneLine(Point From, Point To);

/// <summary>One stop in the legend, in route order. <paramref name="Delivered"/>: all its placements are delivered.</summary>
public sealed record LegendEntry(int PaletteIndex, string Destination, int Scu, bool Delivered = false)
{
    public string Label => Invariant($"{Destination} · {Scu} SCU");
}
