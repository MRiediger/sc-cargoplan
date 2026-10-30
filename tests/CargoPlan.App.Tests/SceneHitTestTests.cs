using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>
/// Points are on cell faces in ship coordinates (D14); the line of sight to them comes from a camera with the default
/// orientation, far away, so the lines are almost parallel and every point keeps the meaning it had in the orthographic
/// view (T8.3). Crates lie where the planner put them, bow first: the layer at the spindle, lower row first, then the
/// outer layer (<c>CrateLayout</c>, D24); port racks stack to +y, starboard racks to −y (D26).
/// </summary>
public class SceneHitTestTests
{
    private static readonly IsoProjection Default = IsoProjection.Default;
    private static readonly ShipScene Scene = BuildSample();

    // Rack 1 (Riker Titanium, four 4 SCU crates flat against the plate), seen from above: crate 3, outer layer, bow end.
    private static readonly Point3D Rack1Top = new(2.5, 2.5, 5);

    // Rack 3 (Orison 14/16), outer layer, upper row, aft end: free. Starboard plates at y −4..−2 since K5.2 (D78).
    private static readonly Point3D Rack3Free = new(0.5, -3.5, 5);

    private static ShipScene BuildSample()
    {
        var (ship, plan) = TestPlans.Load();
        return Build(plan, ship);
    }

    private static ShipScene Build(LoadPlan plan, Ship ship) =>
        ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));

    /// <summary>A camera 200 cells out with <paramref name="orientation"/>, aimed at the ship's middle.</summary>
    private static CameraMath Far(IsoProjection orientation) => new(orientation, 200, ScreenPoints.Centre(Scene.Bounds), View);

    /// <summary>The line of sight to <paramref name="point"/> from <see cref="Far"/>.</summary>
    private static Ray Sight(Point3D point, IsoProjection? orientation = null)
    {
        var camera = Far(orientation ?? Default);
        return camera.Unproject(camera.Project(point));
    }

    private static Ray Sight(double x, double y, double z) => Sight(new Point3D(x, y, z));

    // Beside the ship, which the far camera shows small in the middle of the view.
    private static readonly Ray Empty = Far(Default).Unproject(new Point(10, 10));

    [Fact]
    public void Tool_tip_names_stop_rack_position_and_crate_then_the_rack_load()
    {
        SceneHitTest.ToolTipAt(Scene, Sight(Rack1Top)).Should().Be("Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4)\nRack 1 · 16 / 16 SCU");
    }

    [Fact]
    public void Overlapping_crates_answer_as_crates()
    {
        // Shubin's crate in the full Rack 1 (a plan file edited by hand), over Riker's crate 3: both are drawn, both
        // marked; whichever comes first in the scene answers with its crate.
        var (ship, plan) = TestPlans.Load();
        var scene = Build(TestPlans.Overloaded(plan), ship);

        SceneHitTest.HitAt(scene, Sight(Rack1Top))!.Crate.Should().NotBeNull();
        SceneHitTest.BoxAt(scene, Sight(Rack1Top))!.IsViolated.Should().BeTrue();
        SceneHitTest.ToolTipAt(scene, Sight(Rack1Top)).Should().MatchRegex(@" crate \(\d of \d\)\nRack 1 · 24 / 16 SCU$");
    }

    [Fact]
    public void The_rack_in_front_wins_over_the_rack_behind()
    {
        // (2, 0, 3.5) lies on Rack 3's port face, hidden behind Rack 1: the line of sight enters Rack 1 first.
        SceneHitTest.ToolTipAt(Scene, Sight(2, 0, 3.5)).Should().StartWith("Riker · Rack 1 ·");
    }

    [Fact]
    public void An_empty_rack_in_front_covers_the_cargo_behind_it()
    {
        // Hull A from above with Rack 1 emptied: Shubin's Agricium in Rack 2 lies right below. The first rack decides
        // (D21, T8.3 R1), so the line of sight hits Rack 1 without cargo; the other rule waits in the backlog (E1).
        var (ship, plan) = TestPlans.Load();
        var scene = ShipViewBuilder.Build(plan with { Crates = [.. plan.Crates.Where(c => c.RackId != "rack-0")] }, ship, []);

        SceneHitTest.HitAt(scene, Sight(new Point3D(3, 2, 5), new IsoProjection(IsoProjection.DefaultYaw, 85))).Should().Be(new SceneHit("rack-0", null, null));
    }

    [Fact]
    public void Free_cells_show_the_rack_load()
    {
        // The rack's tool tip, not a cell's: an empty rack shows it too (D67).
        SceneHitTest.ToolTipAt(Scene, Sight(Rack3Free)).Should().Be("Rack 3 · 14 / 16 SCU");
    }

    [Fact]
    public void An_empty_rack_shows_its_load()
    {
        var (ship, _) = TestPlans.Load();
        var scene = Build(new LoadPlan(ship.Id, [], []), ship);

        SceneHitTest.ToolTipAt(scene, Sight(Rack3Free)).Should().Be("Rack 3 · 0 / 16 SCU");
    }

    [Fact]
    public void Empty_space_has_no_tool_tip()
    {
        SceneHitTest.ToolTipAt(Scene, Empty).Should().BeNull();
    }

    [Fact]
    public void Delivered_cargo_says_so()
    {
        var (ship, plan) = TestPlans.Load();
        var delivered = plan with { Crates = [.. plan.Crates.Select(c => c with { Delivered = true })] };
        var scene = ShipViewBuilder.Build(delivered, ship, []);

        SceneHitTest.ToolTipAt(scene, Sight(Rack1Top)).Should().Be("Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4) (delivered)\nRack 1 · 0 / 16 SCU");
    }

    [Fact]
    public void Destination_of_cargo_under_the_point()
    {
        SceneHitTest.DestinationAt(Scene, Sight(Rack1Top)).Should().Be("Riker");
    }

    [Fact]
    public void Delivered_cargo_still_names_its_destination_so_a_click_can_undo()
    {
        var (ship, plan) = TestPlans.Load();
        var scene = Build(LoadPlanner.SetDelivered(plan, ship, "Riker", true), ship);

        SceneHitTest.DestinationAt(scene, Sight(Rack1Top)).Should().Be("Riker");
    }

    [Fact]
    public void Free_and_empty_space_have_no_destination()
    {
        SceneHitTest.DestinationAt(Scene, Sight(Rack3Free)).Should().BeNull();
        SceneHitTest.DestinationAt(Scene, Empty).Should().BeNull();
    }

    [Fact]
    public void Destination_follows_the_rack_in_front()
    {
        // Rack 3 (Orison) lies behind Rack 1 (Riker) on this line of sight.
        SceneHitTest.DestinationAt(Scene, Sight(2, 0, 3.5)).Should().Be("Riker");
    }

    [Fact]
    public void Cargo_seen_through_a_free_cell_of_the_same_rack_answers()
    {
        // Rack 2's outer layer is free at the aft half; through its outline Riker's Quartz crates at the spindle show,
        // one cell along the view direction (1, 1, 1) at (0.8, 1.8, 1).
        SceneHitTest.DestinationAt(Scene, Sight(1.8, 2.8, 2)).Should().Be("Riker");
    }

    [Fact]
    public void A_joint_between_crates_hits_a_crate_beside_it()
    {
        // x = 2 is the joint between two crates of Rack 1 at the spindle, seen from above. The hit test takes the cells,
        // not the inset boxes drawn (T8.3, R3): the joint belongs to the crates on either side.
        SceneHitTest.DestinationAt(Scene, Sight(2, 1.5, 5)).Should().Be("Riker");
    }

    [Fact]
    public void Hit_on_cargo_names_its_rack_and_placement()
    {
        var (_, plan) = TestPlans.Load();
        var titanium = plan.Placements.Single(p => p.Item.Commodity == "Titanium");

        // The third Titanium crate in plan order, the one the tool tip calls "3 of 4".
        var crate = plan.Crates.Where(c => c.Commodity == "Titanium").ElementAt(2);

        SceneHitTest.HitAt(Scene, Sight(Rack1Top)).Should().Be(new SceneHit("rack-0", titanium, crate));
        SceneHitTest.PlacementAt(Scene, Sight(Rack1Top)).Should().Be(titanium);
    }

    [Fact]
    public void Hit_on_a_free_cell_names_the_rack_without_a_placement()
    {
        // A drop target: free cells belong to their rack.
        SceneHitTest.HitAt(Scene, Sight(Rack3Free)).Should().Be(new SceneHit("rack-2", null, null));
        SceneHitTest.PlacementAt(Scene, Sight(Rack3Free)).Should().BeNull();
    }

    [Fact]
    public void Hit_on_a_gap_between_free_cells_still_names_the_rack()
    {
        // x = 1: the joint between the two free cells of Rack 3, nothing behind it.
        SceneHitTest.HitAt(Scene, Sight(1, -3.5, 5)).Should().Be(new SceneHit("rack-2", null, null));
    }

    [Fact]
    public void Hit_off_the_racks_is_null()
    {
        SceneHitTest.HitAt(Scene, Empty).Should().BeNull();
    }

    [Fact]
    public void Hit_on_the_bow_wedge_is_null()
    {
        // The nose beyond the bow (x 4–5 on Hull A, D52) is drawn, not hit; so are the hologram and the outline: none of
        // them is scene data (D83).
        var nose = Sight(4.8, 0.5, 0);

        SceneHitTest.HitAt(Scene, nose).Should().BeNull();
        SceneHitTest.ToolTipAt(Scene, nose).Should().BeNull();
    }

    [Fact]
    public void Hit_follows_the_rack_in_front()
    {
        SceneHitTest.HitAt(Scene, Sight(2, 0, 3.5))!.RackId.Should().Be("rack-0");
    }

    [Fact]
    public void From_below_the_lowest_crates_answer()
    {
        // Rack 2 from below: Shubin's Agricium at the bow half, Riker's lower Quartz crate at the aft end next to the
        // spindle; the upper one shows its port face through the free outer cells. On that face x = 0.5, not 1: the
        // line of sight to x = 1 grazes the Agricium's edge at (2, 3), and in perspective it tips into it (T8.3).
        var below = new IsoProjection(IsoProjection.DefaultYaw, -IsoProjection.DefaultPitch);

        const string rack2 = "\nRack 2 · 12 / 16 SCU";
        SceneHitTest.ToolTipAt(Scene, Sight(new Point3D(2.5, 2.5, 0), below)).Should().Be("Shubin · Rack 2 · port lower · 8 SCU Agricium crate (1 of 1)" + rack2);
        SceneHitTest.ToolTipAt(Scene, Sight(new Point3D(1, 1.5, 0), below)).Should().Be("Riker · Rack 2 · port lower · 2 SCU Quartz crate (1 of 2)" + rack2);
        SceneHitTest.ToolTipAt(Scene, Sight(new Point3D(0.5, 2, 1.5), below)).Should().Be("Riker · Rack 2 · port lower · 2 SCU Quartz crate (2 of 2)" + rack2);
    }

    [Fact]
    public void Right_drag_from_a_crate_onto_a_free_cell_of_another_rack_moves_it_there()
    {
        // Shubin's crate in Rack 2 shows on the bow face. Rack 3's free cell at the aft end of the outer layer lies on
        // Orison's Laranite, so the crate would go there (D25); whether it fits is Core's call.
        var (ship, plan) = TestPlans.Load();
        var shubin = plan.Crates.Single(c => c.Destination == "Shubin");
        var down = SceneHitTest.HitAt(Scene, Sight(4, 1.5, 0.5));
        var current = SceneHitTest.HitAt(Scene, Sight(Rack3Free));
        var drop = DropTarget.Resolve(ship.Racks.Single(r => r.Id == current!.RackId), plan.CratesForRack(current!.RackId), Sight(Rack3Free));

        var gesture = DragGesture.Resolve(MouseButton.Right, down, moved: true, current, drop, new HashSet<Crate>());

        down.Should().Be(new SceneHit("rack-1", plan.Placements.Single(p => p.Destination == "Shubin"), shubin));
        var move = gesture.Should().BeOfType<Gesture.MoveCrates>().Subject;
        (move.Anchor, move.RackId, move.Position).Should().Be((shubin, "rack-2", new GridPosition(0, 1, 1)));
    }

    // --- Perspective (T8.3): what only a camera close by shows ---

    [Fact]
    public void Up_close_every_crate_on_a_rack_s_top_is_hit_where_it_is_drawn()
    {
        // Six cells from Rack 1 the view is strongly foreshortened: the crates' top faces lie in one plane, so the line
        // of sight to the middle of each enters that crate first, wherever it lands on screen.
        var rack = Scene.Racks.Single(r => r.RackId == "rack-0");
        var camera = new CameraMath(Default, 6, ScreenPoints.Centre(rack.Bounds), View);
        var tops = rack.Boxes.Where(b => b.Crate is not null && b.Bounds.Max.Z == rack.Bounds.Max.Z).ToList();

        tops.Should().NotBeEmpty();
        foreach (var box in tops)
        {
            Point3D middle = new((box.Bounds.Min.X + box.Bounds.Max.X) / 2.0, (box.Bounds.Min.Y + box.Bounds.Max.Y) / 2.0, box.Bounds.Max.Z);
            SceneHitTest.HitAt(Scene, camera, camera.Project(middle))!.Crate.Should().Be(box.Crate);
        }
    }

    [Fact]
    public void A_camera_inside_a_rack_hits_the_cargo_in_front_of_it()
    {
        // Zoomed into Rack 3's free cell at the aft end, looking down: Orison's Laranite lies right below. The rack
        // around the camera counts from the camera on (RayBox.Ahead), else nothing would answer.
        Point3D inside = new(0.5, -3.5, 4.5);
        var look = new CameraMath(new IsoProjection(IsoProjection.DefaultYaw, 85), 1, inside, View).LookDirection;
        var camera = new CameraMath(new IsoProjection(IsoProjection.DefaultYaw, 85), 1, inside + look, View);

        SceneHitTest.DestinationAt(Scene, camera, new Point(View.Width / 2, View.Height / 2)).Should().Be("Orison");
    }

    [Fact]
    public void Cargo_behind_the_camera_is_not_hit()
    {
        // In the same free cell, looking up: the Laranite now lies behind the camera, its entry is negative.
        Point3D inside = new(0.5, -3.5, 4.5);
        var up = new IsoProjection(IsoProjection.DefaultYaw, -85);
        var look = new CameraMath(up, 1, inside, View).LookDirection;
        var camera = new CameraMath(up, 1, inside + look, View);

        SceneHitTest.HitAt(Scene, camera, new Point(View.Width / 2, View.Height / 2)).Should().Be(new SceneHit("rack-2", null, null));
    }

    // --- Only the view's area is hit (K1.4, D33): drawn with ClipToBounds, so nothing beyond it can be seen ---

    private static readonly Size View = new(400, 300);

    /// <summary>Zoomed in so far that Rack 1 lies below the view, where the legend stands: its crate 3 at (200, 350).</summary>
    private static readonly Point OverTheLegend = new(200, 350);

    private static readonly CameraMath Zoomed = Centred(OverTheLegend);

    /// <summary>Ten cells from Rack 1's crate 3, which lands at <paramref name="screen"/>: the target moved in the view's plane.</summary>
    private static CameraMath Centred(Point screen)
    {
        var camera = new CameraMath(Default, 10, Rack1Top, View);
        var cells = camera.Distance / camera.Focal;
        return camera with { Target = Rack1Top + (View.Width / 2 - screen.X) * cells * camera.RightDirection + (screen.Y - View.Height / 2) * cells * camera.UpDirection };
    }

    [Fact]
    public void The_scene_alone_finds_the_rack_zoomed_out_of_sight()
    {
        // The number before K1.4: the hit test took every point, so a drop over the legend hit Rack 1 out of sight.
        SceneHitTest.HitAt(Scene, Zoomed.Unproject(OverTheLegend))!.RackId.Should().Be("rack-0");
    }

    [Fact]
    public void Outside_the_view_nothing_is_hit()
    {
        SceneHitTest.HitAt(Scene, Zoomed, OverTheLegend).Should().BeNull();
        SceneHitTest.DestinationAt(Scene, Zoomed, OverTheLegend).Should().BeNull();
        SceneHitTest.ToolTipAt(Scene, Zoomed, OverTheLegend).Should().BeNull();
    }

    [Fact]
    public void A_legend_line_released_over_the_legend_moves_nothing()
    {
        // The legend drag runs in the view with the mouse captured (D25): its release over the legend is no drop.
        var crates = Scene.Plan.Crates.Where(c => c.Destination == "Shubin").ToList();

        DragGesture.ResolveLegend(crates, moved: true, SceneHitTest.HitAt(Scene, Zoomed, OverTheLegend)).Should().BeOfType<Gesture.None>();
    }

    [Theory]
    [InlineData(200, 150)]
    [InlineData(0, 0)]
    [InlineData(400, 300)]
    public void Inside_the_view_the_hit_is_the_scene_s(double x, double y)
    {
        // Edges included: the view's last pixel row is still the view.
        Point point = new(x, y);
        var camera = Centred(point);

        SceneHitTest.HitAt(Scene, camera, point).Should().Be(SceneHitTest.HitAt(Scene, Sight(Rack1Top)));
        SceneHitTest.DestinationAt(Scene, camera, point).Should().Be("Riker");
        SceneHitTest.ToolTipAt(Scene, camera, point).Should().Be(SceneHitTest.ToolTipAt(Scene, Sight(Rack1Top)));
    }
}
