using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>
/// The view on a turned rack (D91), through <see cref="CameraMath"/>, the view's only projection. A rack of 4×2×2 cells at the
/// ship's zero, x 0..4, y 0..2, z 0..2, middle (2, 1, 1). Turned by 90° of yaw it lies x 1..3, y −1..3: the bow half to port,
/// the aft half to starboard. A point at x 0.5 lies in the straight box only, one at y 2.5 in the turned rack only.
/// </summary>
public class TurnedRackViewTests
{
    private static readonly Size View = new(800, 600);
    private static readonly RackRotation Yaw90 = new(90, 0, 0);

    private static readonly Crate Aft = new("Riker", "Gold", CrateSize.Eight, "rack-0", new GridPosition(0, 0, 0), Mission: 1);
    private static readonly Crate Bow = new("Shubin", "Iron", CrateSize.Eight, "rack-0", new GridPosition(2, 0, 0), Mission: 2);

    private static Rack RackOf(RackRotation? rotation, int z = 0, string id = "rack-0", int index = 0, int x = 0) =>
        new(id, index, 16, new Dimensions(4, 2, 2), null, new ShipPoint(x, 0, z), Rotation: rotation);

    private static Ship ShipOf(params Rack[] racks) => new("turned", "Turned", racks.Sum(r => r.CapacityScu), racks);

    private static ShipScene SceneOf(Ship ship, params Crate[] crates) => ShipViewBuilder.Build(new LoadPlan(ship.Id, crates, []), ship, []);

    private static ShipScene Turned() => SceneOf(ShipOf(RackOf(Yaw90)), Aft, Bow);

    /// <summary>Far above the ship, the lines of sight almost parallel and almost straight down.</summary>
    private static CameraMath FromAbove(Point3D target) => new(new CameraOrientation(0, CameraOrientation.MaxPitch), 200, target, View);

    private static SceneHit? HitAt(ShipScene scene, Point3D point)
    {
        var camera = FromAbove(point);
        return SceneHitTest.HitAt(scene, camera, camera.Project(point));
    }

    // --- Hit test (D21) ---

    [Fact]
    public void A_ray_hits_the_crate_of_the_turned_rack_outside_the_straight_box()
    {
        var hit = HitAt(Turned(), new Point3D(2, 2.5, 1));

        hit.Should().NotBeNull();
        hit!.Crate.Should().Be(Bow);
    }

    [Fact]
    public void A_ray_hits_the_other_crate_on_the_other_side()
    {
        HitAt(Turned(), new Point3D(2, -0.5, 1))!.Crate.Should().Be(Aft);
    }

    [Fact]
    public void A_ray_misses_where_only_the_straight_box_lies()
    {
        HitAt(Turned(), new Point3D(0.5, 1, 1)).Should().BeNull();
    }

    [Fact]
    public void The_tool_tip_names_the_crate_the_ray_hits()
    {
        var scene = Turned();
        var camera = FromAbove(new Point3D(2, 2.5, 1));

        SceneHitTest.ToolTipAt(scene, camera, camera.Project(new Point3D(2, 2.5, 1))).Should().Contain("Shubin");
        SceneHitTest.DestinationAt(scene, camera, camera.Project(new Point3D(2, 2.5, 1))).Should().Be("Shubin");
    }

    [Fact]
    public void The_first_rack_on_the_ray_decides_turned_or_not()
    {
        // Seen from above, the upper rack comes first, whichever of the two is turned; both reach (2, 1).
        var upperTurned = SceneOf(ShipOf(RackOf(null, id: "lower"), RackOf(Yaw90, z: 5, id: "upper", index: 1)));
        var lowerTurned = SceneOf(ShipOf(RackOf(Yaw90, id: "lower"), RackOf(null, z: 5, id: "upper", index: 1)));

        HitAt(upperTurned, new Point3D(2, 1, 1))!.RackId.Should().Be("upper");
        HitAt(lowerTurned, new Point3D(2, 1, 1))!.RackId.Should().Be("upper");
    }

    [Fact]
    public void A_straight_rack_answers_as_before_beside_a_turned_one()
    {
        var scene = SceneOf(ShipOf(RackOf(null, id: "straight"), RackOf(Yaw90, id: "turned", index: 1, x: 20)));

        HitAt(scene, new Point3D(0.5, 1, 1))!.RackId.Should().Be("straight");
        HitAt(scene, new Point3D(20.5, 1, 1)).Should().BeNull();
    }

    // --- Drop target (D25, D26): the ray picks the cell, the grid does the fall ---

    [Fact]
    public void A_drop_lands_on_the_cell_of_the_turned_rack_and_falls_down_its_column()
    {
        // Length 3, width 0 lies at the bow end of the rack, turned to port: x 2..3, y 2..3.
        Point3D point = new(2.5, 2.5, 1.5);
        var camera = FromAbove(point);

        DropTarget.Resolve(RackOf(Yaw90), [], camera.Unproject(camera.Project(point)))
            .Should().Be(new Drop("rack-0", new GridPosition(3, 0, 0)));
    }

    [Fact]
    public void A_drop_goes_on_top_of_a_crate_in_the_turned_rack()
    {
        Point3D point = new(2.5, 2.5, 1.5);
        var camera = FromAbove(point);
        Crate below = new("Riker", "Gold", CrateSize.One, "rack-0", new GridPosition(3, 0, 0));

        DropTarget.Resolve(RackOf(Yaw90), [below], camera.Unproject(camera.Project(point)))
            .Should().Be(new Drop("rack-0", new GridPosition(3, 0, 1)));
    }

    [Fact]
    public void A_column_runs_along_the_stack_axis_of_the_rack_not_of_the_ship()
    {
        // Rolled by 90°, the rack's stack points to starboard and its width up: the plate lies at y 2, the top of the stack at
        // y 0. Seen from starboard the top comes first, and a crate falls away from the camera, to the plate. At z 1.5 it is
        // width 1; straight it would be stack 1, falling to width 0.
        Rack rolled = RackOf(new RackRotation(0, 0, 90));
        Point3D point = new(1.5, 0.5, 1.5);
        CameraMath camera = new(new CameraOrientation(270, 0), 200, point, View);
        var sight = camera.Unproject(camera.Project(point));

        DropTarget.Resolve(rolled, [], sight).Should().Be(new Drop("rack-0", new GridPosition(1, 1, 0)));

        Crate onThePlate = new("Riker", "Gold", CrateSize.One, "rack-0", new GridPosition(1, 1, 0));
        DropTarget.Resolve(rolled, [onThePlate], sight).Should().Be(new Drop("rack-0", new GridPosition(1, 1, 1)));
    }

    [Fact]
    public void A_drop_misses_where_only_the_straight_box_lies()
    {
        Point3D point = new(0.5, 1, 1);
        var camera = FromAbove(point);

        DropTarget.Resolve(RackOf(Yaw90), [], camera.Unproject(camera.Project(point))).Should().BeNull();
    }

    // --- Bounds and fit ---

    [Fact]
    public void The_scene_goes_by_the_turned_corners_and_its_boxes_stay_straight()
    {
        var scene = Turned();

        scene.Bounds.Should().Be(new ShipBox(new ShipPoint(1, -1, 0), new ShipPoint(3, 3, 2)));
        scene.Racks[0].Bounds.Should().Be(RackOf(null).Bounds);
        scene.Racks[0].Boxes.Should().OnlyContain(b => ReferenceEquals(b.Transform, scene.Racks[0].Transform));
    }

    [Fact]
    public void A_straight_ship_keeps_its_bounds_and_the_identity()
    {
        var ship = ShipOf(RackOf(null));
        var scene = SceneOf(ship, Aft);

        scene.Bounds.Should().Be(ship.Bounds);
        scene.Racks[0].Transform.Should().BeSameAs(RackTransform.Identity);
        scene.Racks[0].Boxes.Should().OnlyContain(b => b.Transform.IsIdentity);
    }

    [Fact]
    public void A_long_ship_fits_the_turned_corners_of_its_loaded_racks()
    {
        // Turned by 45° the loaded rack reaches x −1, past the box it has straight; the wedge lies at the other end.
        var scene = SceneOf(ShipOf(RackOf(new RackRotation(45, 0, 0)), RackOf(null, id: "far", index: 1, x: 40)), Aft);

        CameraOrientation.IsLong(scene.Bounds).Should().BeTrue();
        CameraFit.FitPoints(scene).Min(p => p.X).Should().Be(-1);
    }

    // --- Faces and numbers ---

    [Fact]
    public void The_visible_faces_are_those_the_turned_rack_shows()
    {
        // From the bow, level: straight the rack shows its bow end; turned to port it shows the side that was starboard.
        var rack = RackOf(Yaw90);
        CameraMath camera = new(new CameraOrientation(0, 0), 50, new Point3D(2, 1, 1), View);

        var face = BoxFaces.Visible(rack.Bounds, camera, RackTransform.Of(rack)).Should().ContainSingle().Which;

        face.Side.Should().Be(BoxFace.Starboard);
        face.Normal.X.Should().BeApproximately(1, 1e-9);
        face.Corners.Should().OnlyContain(c => Math.Abs(c.X - 3) < 1e-9);
        face.Centre.X.Should().BeApproximately(3, 1e-9);
    }

    [Fact]
    public void A_number_stands_on_the_largest_face_of_the_turned_rack()
    {
        // Pitched by 90°, the rack's top looks to the bow: its middle (2, 1, 2) moves to (3, 1, 1). A little from port, so
        // that isn't on one line of sight with the bow end's middle (4, 1, 1), where the number stands straight.
        var scene = SceneOf(ShipOf(RackOf(new RackRotation(0, 90, 0))));
        CameraMath camera = new(new CameraOrientation(20, 0), 50, new Point3D(2, 1, 1), View);

        var label = RackLabels.Place(scene, camera).Single();

        label.Hidden.Should().BeFalse();
        var expected = camera.Project(new Point3D(3, 1, 1));
        label.Anchor.X.Should().BeApproximately(expected.X, 1e-6);
        label.Anchor.Y.Should().BeApproximately(expected.Y, 1e-6);
    }

    [Fact]
    public void A_turned_rack_covers_what_lies_behind_it_and_nothing_else()
    {
        // From port, level. At x 0.5 only the straight box lies in the way, at x 2 the turned rack too.
        var scene = Turned();
        CameraMath camera = new(new CameraOrientation(90, 0), 200, new Point3D(2, 1, 1), View);

        RackLabels.Covered(scene, null, new Point3D(0.5, -5, 1), camera).Should().BeFalse();
        RackLabels.Covered(scene, null, new Point3D(2, -5, 1), camera).Should().BeTrue();
    }

    // --- The 3D scene ---

    private static CrateLook Look(ShipScene scene) =>
        new(StopPalettes.OkabeIto, 0xD0, Color.FromArgb(0xC0, 0xE6, 0xE6, 0xE6), SelectionFocus.Missions(CrateSelection.Empty.In(scene)));

    [Fact]
    public void The_crates_of_a_turned_rack_are_drawn_where_it_lies()
    {
        Sta.Run(() =>
        {
            var scene = Turned();
            var scene3D = new ShipScene3D();

            scene3D.Show(scene, Look(scene));

            var bounds = scene3D.Crates.Bounds;
            bounds.Y.Should().BeApproximately(-0.95, 1e-6, "the crates reach y −1 turned, y 0 straight; drawn inset all round");
            (bounds.Y + bounds.SizeY).Should().BeApproximately(2.95, 1e-6);
            bounds.X.Should().BeApproximately(1.05, 1e-6);
        });
    }

    [Fact]
    public void The_edges_of_a_turned_rack_are_drawn_where_it_lies()
    {
        Sta.Run(() =>
        {
            var scene = Turned();
            var scene3D = new ShipScene3D();
            scene3D.Show(scene, Look(scene));

            scene3D.Follow(new CameraMath(CameraOrientation.Default, 20, new Point3D(2, 1, 1), View));

            var bounds = scene3D.Edges.Bounds;
            bounds.Y.Should().BeLessThan(-0.9);
            (bounds.Y + bounds.SizeY).Should().BeGreaterThan(2.9);
            bounds.X.Should().BeInRange(0.9, 1.1, "the rack lies x 1..3 turned, x 0..4 straight");
        });
    }

    [Fact]
    public void A_crate_of_a_straight_rack_wears_no_transform()
    {
        Sta.Run(() =>
        {
            var scene = SceneOf(ShipOf(RackOf(null)), Aft);
            var scene3D = new ShipScene3D();

            scene3D.Show(scene, Look(scene));

            scene3D.Crates.Children.Cast<GeometryModel3D>().Single().Transform.Should().BeSameAs(Transform3D.Identity);
        });
    }

    // --- Preview and drop (D65): one way for both, and never two crates on one cell ---

    [Fact]
    public void Preview_and_drop_agree_on_crates_that_would_share_a_cell()
    {
        // Racks a and b take the same straight box (only a rotation lets a file say that); both crates land on one cell of c.
        var ship = ShipOf(RackOf(Yaw90, id: "a"), RackOf(Yaw90, id: "b", index: 1), RackOf(null, id: "c", index: 2, x: 10));
        Crate first = new("Riker", "Gold", CrateSize.One, "a", new GridPosition(0, 0, 0));
        Crate second = new("Riker", "Gold", CrateSize.One, "b", new GridPosition(0, 0, 0));
        var plan = new LoadPlan(ship.Id, [first, second], []);
        Gesture.MoveCrates move = new(first, [first, second], "c", new GridPosition(0, 0, 0));

        CrateDrop.Moves(ship, move).Should().BeNull();

        var outcome = CrateDrop.Outcome(plan, ship, move);
        outcome.Plan!.Crates.Should().OnlyContain(c => c.RackId == "c");
        outcome.Plan.Crates.SelectMany(c => c.Cells).Should().OnlyHaveUniqueItems();
        LoadPlanner.Validate(outcome.Plan, ship).Should().BeEmpty();
        DragPreview.Of(ship, plan, [first, second], move, outcome).Ghosts.Should().OnlyContain(c => c.RackId == "c");
    }
}
