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
/// <param name="Violations">From <c>LoadPlanner.Validate</c>; these racks are red (D24).</param>
/// <param name="PaletteRepeats">More stops than colours.</param>
/// <param name="Plan">The plan drawn, empty without one: a drag asks Core with it where a crate may go (D25).</param>
/// <param name="Ship">The plan's ship: racks with grid and origin for the drop target (D25).</param>
/// <param name="Unplaced">Missions that didn't fit (D41), one line each after the stops; ticking one drops it.</param>
public sealed record ShipScene(
    IReadOnlyList<SceneRack> Racks,
    ShipBox Bounds,
    IReadOnlyList<LegendEntry> Legend,
    IReadOnlyList<string> Hints,
    IReadOnlyList<string> ReservedHints,
    IReadOnlyList<string> Violations,
    bool PaletteRepeats,
    LoadPlan Plan,
    Ship Ship,
    IReadOnlyList<UnplacedEntry> Unplaced)
{
    /// <summary>Every stop is delivered; false without stops.</summary>
    public bool AllDelivered => Legend.Count > 0 && Legend.All(e => e.Delivered);
}

/// <param name="RackId">The ship's rack id, the target of a drop (<c>LoadPlanner.MoveCrates</c>, <c>MoveStop</c>).</param>
/// <param name="Boxes">
/// The rack's crates where the plan stores them (D24), by placement and in plan order, then its free cells. Overlapping
/// crates of a plan file edited by hand are all there.
/// </param>
public sealed record SceneRack(string RackId, int Number, bool IsViolated, ShipBox Bounds, IReadOnlyList<SceneBox> Boxes);

/// <summary>
/// One box of a rack in ship coordinates: a crate in its shape, or a free cell. <paramref name="PaletteIndex"/>,
/// <paramref name="ToolTip"/>, <paramref name="Placement"/> (the crate's derived placement) and <paramref name="Crate"/>
/// are null for a free cell. <paramref name="IsViolated"/>: the crate is named by a problem (<c>CrateLayout.Faulty</c>).
/// </summary>
public sealed record SceneBox(ShipBox Bounds, CellFill Fill, int? PaletteIndex, string? ToolTip, Placement? Placement, Crate? Crate, bool IsViolated);

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
/// <param name="Racks">Back to front (<see cref="DrawOrder.BackToFront"/>): the painter draws rack by rack.</param>
/// <param name="Frame">The 12 edges of <c>Ship.Bounds</c>.</param>
/// <param name="BowWedge">The nose that marks the bow (D52).</param>
/// <param name="Extent">Encloses the frame and the bow wedge, for fit-to-view.</param>
public sealed record ProjectedScene(IReadOnlyList<ProjectedRack> Racks, IReadOnlyList<SceneLine> Frame, ProjectedWedge BowWedge, Rect Extent);

/// <summary>One face of the bow wedge in ship coordinates (D52): <see cref="BoxFace.Bow"/> is its tip, Port and Starboard its flanks.</summary>
/// <param name="Normal">Outward, unit length; the flanks' normals are not along an axis.</param>
public sealed record BowWedgeFace(BoxFace Face, IReadOnlyList<SceneProjector.Corner> Corners, (double X, double Y, double Z) Normal);

/// <param name="Faces">The faces turned towards the camera.</param>
/// <param name="InFront">
/// The wedge lies beyond the bow end of every rack, so the x axis always separates them: seen from the bow side it is
/// drawn after the racks, otherwise before them, without a place in <see cref="DrawOrder"/>.
/// </param>
/// <param name="Floor">Where the "Bow" label lies.</param>
public sealed record ProjectedWedge(IReadOnlyList<ProjectedFace> Faces, bool InFront, WedgeFloor Floor);

/// <summary>
/// The bow wedge's floor as the camera sees it, for the label lying on it (D52): from <paramref name="Centre"/>, one cell
/// across the ship (+y) and one cell towards the bow (+x), in projected units; <paramref name="Room"/> in cells, across
/// by along.
/// </summary>
public sealed record WedgeFloor(Point Centre, Vector Across, Vector Bowwards, Size Room);

/// <param name="Faces">The faces of the rack's box the camera sees: its edges and its area for the hit test.</param>
/// <param name="Boxes">The rack's own boxes, back to front (<see cref="DrawOrder.BoxesBackToFront"/>).</param>
/// <param name="LabelCentre">Centre of the largest visible face, where the rack number goes.</param>
public sealed record ProjectedRack(
    string RackId,
    int Number,
    bool IsViolated,
    IReadOnlyList<ProjectedFace> Faces,
    IReadOnlyList<ProjectedBox> Boxes,
    Point LabelCentre);

public sealed record ProjectedFace(BoxFace Face, IReadOnlyList<Point> Polygon);

/// <summary>A <see cref="SceneBox"/> as the camera sees it, drawn slightly smaller all round so the gap between boxes shows.</summary>
public sealed record ProjectedBox(SceneBox Box, IReadOnlyList<ProjectedFace> Faces);

public sealed record SceneLine(Point From, Point To);

/// <summary>
/// One stop in the legend, in unload order (T5.4). <paramref name="Delivered"/>: all its placements are delivered.
/// <paramref name="Markings"/>: what is uncertain about its missions ("4 SCU? · ⚠ duplicate?"), empty if nothing;
/// <paramref name="ReserveMarkings"/> the same with nothing delivered, so the line keeps its width when a tick takes a
/// marking away (D16, D33).
/// </summary>
public sealed record LegendEntry(int PaletteIndex, string Destination, int Scu, bool Delivered = false, string Markings = "", string ReserveMarkings = "")
{
    public string Label => WithMarkings(Markings);

    /// <summary>The widest the label gets: laid out hidden under it.</summary>
    public string ReserveLabel => WithMarkings(ReserveMarkings);

    private string WithMarkings(string markings) =>
        markings.Length == 0 ? Invariant($"{Destination} · {Scu} SCU") : Invariant($"{Destination} · {Scu} SCU · {markings}");
}

/// <summary>A mission that didn't fit (D41): its line in the legend, and the id a tick drops it by.</summary>
public sealed record UnplacedEntry(int MissionId, string Label);
