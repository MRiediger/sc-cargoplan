using System.Windows;
using System.Windows.Input;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>
/// Points are on cell faces in ship coordinates (D14), projected with the default camera. Crates lie where the planner
/// put them, bow first: the layer at the spindle, lower row first, then the outer layer (<c>CrateLayout</c>, D24); port
/// racks stack to +y, starboard racks to −y (D26).
/// </summary>
public class SceneHitTestTests
{
    private static readonly IsoProjection Default = IsoProjection.Default;
    private static readonly ShipScene Scene = BuildSample();
    private static readonly ProjectedScene Projected = SceneProjector.Project(Scene, Default);

    // Rack 1 (Riker Titanium, four 4 SCU crates flat against the plate), seen from above: crate 3, outer layer, bow end.
    private static readonly Point Rack1Top = Default.Project(2.5, 2.5, 5);

    // Rack 3 (Orison 14/16), outer layer, upper row, aft end: free.
    private static readonly Point Rack3Free = Default.Project(0.5, -1.5, 5);

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
        return Build(plan, ship);
    }

    private static ShipScene Build(LoadPlan plan, Ship ship) =>
        ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));

    [Fact]
    public void Polygon_contains_inner_points_only()
    {
        Point[] square = [new(0, 0), new(2, 0), new(2, 2), new(0, 2)];

        SceneHitTest.Contains(square, new Point(1, 1)).Should().BeTrue();
        SceneHitTest.Contains(square, new Point(3, 1)).Should().BeFalse();
        SceneHitTest.Contains(square, new Point(1, -0.5)).Should().BeFalse();
    }

    [Fact]
    public void Tool_tip_names_stop_rack_position_and_crate_then_the_rack_load()
    {
        SceneHitTest.ToolTipAt(Projected, Rack1Top).Should().Be("Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4)\nRack 1 · 16 / 16 SCU");
    }

    [Fact]
    public void Overlapping_crates_answer_as_crates()
    {
        // Shubin's crate in the full Rack 1 (a plan file edited by hand), over Riker's crate 3: both are drawn, both
        // marked; whichever the painter puts on top answers with its crate.
        var (ship, plan) = TestPlans.Load();
        var projected = SceneProjector.Project(Build(TestPlans.Overloaded(plan), ship), Default);

        SceneHitTest.HitAt(projected, Rack1Top)!.Crate.Should().NotBeNull();
        SceneHitTest.BoxAt(projected, Rack1Top)!.Box.IsViolated.Should().BeTrue();
        SceneHitTest.ToolTipAt(projected, Rack1Top).Should().MatchRegex(@" crate \(\d of \d\)\nRack 1 · 24 / 16 SCU$");
    }

    [Fact]
    public void The_rack_in_front_wins_over_the_rack_behind()
    {
        // (2, 0, 3.5) lies on Rack 3's port face, hidden behind Rack 1: same screen point as (3.5, 1.5, 5) on Rack 1's top.
        var point = Default.Project(2, 0, 3.5);

        SceneHitTest.ToolTipAt(Projected, point).Should().StartWith("Riker · Rack 1 ·");
    }

    [Fact]
    public void Free_cells_show_the_rack_load()
    {
        // The rack's tool tip, not a cell's: an empty rack shows it too (D67).
        SceneHitTest.ToolTipAt(Projected, Rack3Free).Should().Be("Rack 3 · 14 / 16 SCU");
    }

    [Fact]
    public void An_empty_rack_shows_its_load()
    {
        var (ship, _) = TestPlans.Load();
        var projected = SceneProjector.Project(Build(new LoadPlan(ship.Id, [], []), ship), Default);

        SceneHitTest.ToolTipAt(projected, Rack3Free).Should().Be("Rack 3 · 0 / 16 SCU");
    }

    [Fact]
    public void Empty_space_has_no_tool_tip()
    {
        SceneHitTest.ToolTipAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Delivered_cargo_says_so()
    {
        var (ship, plan) = TestPlans.Load();
        var delivered = plan with { Crates = [.. plan.Crates.Select(c => c with { Delivered = true })] };
        var projected = SceneProjector.Project(ShipViewBuilder.Build(delivered, ship, []), Default);

        SceneHitTest.ToolTipAt(projected, Rack1Top).Should().Be("Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4) (delivered)\nRack 1 · 0 / 16 SCU");
    }

    [Fact]
    public void Destination_of_cargo_under_the_point()
    {
        SceneHitTest.DestinationAt(Projected, Rack1Top).Should().Be("Riker");
    }

    [Fact]
    public void Delivered_cargo_still_names_its_destination_so_a_click_can_undo()
    {
        var (ship, plan) = TestPlans.Load();
        var projected = SceneProjector.Project(Build(LoadPlanner.SetDelivered(plan, ship, "Riker", true), ship), Default);

        SceneHitTest.DestinationAt(projected, Rack1Top).Should().Be("Riker");
    }

    [Fact]
    public void Free_and_empty_space_have_no_destination()
    {
        SceneHitTest.DestinationAt(Projected, Rack3Free).Should().BeNull();
        SceneHitTest.DestinationAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Destination_follows_the_rack_in_front()
    {
        // Rack 3 (Orison) lies behind Rack 1 (Riker) at this screen point.
        SceneHitTest.DestinationAt(Projected, Default.Project(2, 0, 3.5)).Should().Be("Riker");
    }

    [Fact]
    public void Cargo_seen_through_a_free_cell_of_the_same_rack_answers()
    {
        // Rack 2's outer layer is free at the aft half; through its outline Riker's Quartz crates at the spindle show,
        // one cell along the view direction (1, 1, 1) at (0.8, 1.8, 1).
        SceneHitTest.DestinationAt(Projected, Default.Project(1.8, 2.8, 2)).Should().Be("Riker");
    }

    [Fact]
    public void A_gap_between_crates_shows_the_crate_behind()
    {
        // x = 2 is the joint between two crates of Rack 1 at the spindle, seen from above; the crate behind shows through.
        SceneHitTest.DestinationAt(Projected, Default.Project(2, 1.5, 5)).Should().Be("Riker");
    }

    [Fact]
    public void Hit_on_cargo_names_its_rack_and_placement()
    {
        var (_, plan) = TestPlans.Load();
        var titanium = plan.Placements.Single(p => p.Item.Commodity == "Titanium");

        // The third Titanium crate in plan order, the one the tool tip calls "3 of 4".
        var crate = plan.Crates.Where(c => c.Commodity == "Titanium").ElementAt(2);

        SceneHitTest.HitAt(Projected, Rack1Top).Should().Be(new SceneHit("rack-0", titanium, crate));
        SceneHitTest.PlacementAt(Projected, Rack1Top).Should().Be(titanium);
    }

    [Fact]
    public void Hit_on_a_free_cell_names_the_rack_without_a_placement()
    {
        // A drop target: free cells belong to their rack.
        SceneHitTest.HitAt(Projected, Rack3Free).Should().Be(new SceneHit("rack-2", null, null));
        SceneHitTest.PlacementAt(Projected, Rack3Free).Should().BeNull();
    }

    [Fact]
    public void Hit_on_a_gap_between_free_cells_still_names_the_rack()
    {
        // x = 1: the joint between the two free cells of Rack 3, nothing behind it.
        SceneHitTest.HitAt(Projected, Default.Project(1, -1.5, 5)).Should().Be(new SceneHit("rack-2", null, null));
    }

    [Fact]
    public void Hit_off_the_racks_is_null()
    {
        SceneHitTest.HitAt(Projected, new Point(100, 100)).Should().BeNull();
    }

    [Fact]
    public void Hit_on_the_bow_wedge_is_null()
    {
        // The nose beyond the bow (x 4–5 on Hull A, D52) is drawn, not hit: off the racks in the default view.
        var nose = Default.Project(4.8, 0.5, 0);

        SceneHitTest.HitAt(Projected, nose).Should().BeNull();
        SceneHitTest.ToolTipAt(Projected, nose).Should().BeNull();
    }

    [Fact]
    public void Hit_follows_the_rack_in_front()
    {
        SceneHitTest.HitAt(Projected, Default.Project(2, 0, 3.5))!.RackId.Should().Be("rack-0");
    }

    [Fact]
    public void From_below_the_lowest_crates_answer()
    {
        // Rack 2 from below: Shubin's Agricium at the bow half, Riker's lower Quartz crate at the aft end next to the
        // spindle; the upper one shows its port face through the free outer cells.
        var below = new IsoProjection(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);
        var projected = SceneProjector.Project(Scene, below);

        const string rack2 = "\nRack 2 · 12 / 16 SCU";
        SceneHitTest.ToolTipAt(projected, below.Project(2.5, 2.5, 0)).Should().Be("Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)" + rack2);
        SceneHitTest.ToolTipAt(projected, below.Project(1, 1.5, 0)).Should().Be("Riker · Rack 2 · port lower · 2 SCU Quartz crate (1 of 2)" + rack2);
        SceneHitTest.ToolTipAt(projected, below.Project(1, 2, 1.5)).Should().Be("Riker · Rack 2 · port lower · 2 SCU Quartz crate (2 of 2)" + rack2);
    }

    [Fact]
    public void Right_drag_from_a_crate_onto_a_free_cell_of_another_rack_moves_it_there()
    {
        // Shubin's crate in Rack 2 shows on the bow face. Rack 3's free cell at the aft end of the outer layer lies on
        // Orison's Laranite, so the crate would go there (D25); whether it fits is Core's call.
        var (ship, plan) = TestPlans.Load();
        var shubin = plan.Crates.Single(c => c.Destination == "Shubin");
        var down = SceneHitTest.HitAt(Projected, Default.Project(4, 1.5, 0.5));
        var current = SceneHitTest.HitAt(Projected, Rack3Free);
        var drop = DropTarget.Resolve(ship.Racks.Single(r => r.Id == current!.RackId), plan.CratesForRack(current!.RackId), Default, Rack3Free);

        var gesture = DragGesture.Resolve(MouseButton.Right, down, moved: true, current, drop, new HashSet<Crate>());

        down.Should().Be(new SceneHit("rack-1", plan.Placements.Single(p => p.Destination == "Shubin"), shubin));
        var move = gesture.Should().BeOfType<Gesture.MoveCrates>().Subject;
        (move.Anchor, move.RackId, move.Position).Should().Be((shubin, "rack-2", new GridPosition(0, 1, 1)));
    }

    // --- Only the view's area is hit (K1.4, D33): drawn with ClipToBounds, so nothing beyond it can be seen ---

    private static readonly Size View = new(400, 300);

    /// <summary>Zoomed in so far that Rack 1 lies below the view, where the legend stands: its crate 3 at (200, 350).</summary>
    private static readonly Point OverTheLegend = new(200, 350);

    private static readonly ViewTransform Zoomed = Centred(OverTheLegend);

    /// <summary>100 pixels per cell with Rack 1's crate 3 at <paramref name="screen"/>.</summary>
    private static ViewTransform Centred(Point screen) => new(100, (Vector)screen - (Vector)Rack1Top * 100);

    [Fact]
    public void The_scene_alone_finds_the_rack_zoomed_out_of_sight()
    {
        // The number before K1.4: the hit test inverted every point, so a drop over the legend hit Rack 1 out of sight.
        SceneHitTest.HitAt(Projected, Zoomed.Invert(OverTheLegend))!.RackId.Should().Be("rack-0");
    }

    [Fact]
    public void Outside_the_view_nothing_is_hit()
    {
        SceneHitTest.HitAt(Projected, Zoomed, View, OverTheLegend).Should().BeNull();
        SceneHitTest.DestinationAt(Projected, Zoomed, View, OverTheLegend).Should().BeNull();
        SceneHitTest.ToolTipAt(Projected, Zoomed, View, OverTheLegend).Should().BeNull();
    }

    [Fact]
    public void A_legend_line_released_over_the_legend_moves_nothing()
    {
        // The legend drag runs in the view with the mouse captured (D25): its release over the legend is no drop.
        var crates = Scene.Plan.Crates.Where(c => c.Destination == "Shubin").ToList();

        DragGesture.ResolveLegend(crates, moved: true, SceneHitTest.HitAt(Projected, Zoomed, View, OverTheLegend)).Should().BeOfType<Gesture.None>();
    }

    [Theory]
    [InlineData(200, 150)]
    [InlineData(0, 0)]
    [InlineData(400, 300)]
    public void Inside_the_view_the_hit_is_the_scene_s(double x, double y)
    {
        // Edges included: the view's last pixel row is still the view.
        Point point = new(x, y);
        var transform = Centred(point);

        SceneHitTest.HitAt(Projected, transform, View, point).Should().Be(SceneHitTest.HitAt(Projected, Rack1Top));
        SceneHitTest.DestinationAt(Projected, transform, View, point).Should().Be("Riker");
        SceneHitTest.ToolTipAt(Projected, transform, View, point).Should().Be(SceneHitTest.ToolTipAt(Projected, Rack1Top));
    }
}
