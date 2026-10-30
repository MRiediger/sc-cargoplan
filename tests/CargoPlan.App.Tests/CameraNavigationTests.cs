using System.Numerics;
using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The perspective view's camera (D83): fit, zoom, pan and orbit as distance and target, the limits fixed to the ship.
/// The four guards of the holo pump bugfix (once OrbitScaleTests on the orthographic view, gone in T8.6) hold here
/// too: only the player changes the distance; turning, a hologram arriving or a plan step never do, also at the limit.
/// </summary>
public sealed class CameraNavigationTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();

    // The default overlay's width; the ship view is about as high.
    private static readonly Size Viewport = new(420, 420);
    private const double Margin = 8;
    private static readonly DistanceLimits Limits = DistanceLimits.Default;
    private static readonly Point Middle = new(Viewport.Width / 2, Viewport.Height / 2);

    /// <summary>A hologram box of <paramref name="length"/> × <paramref name="width"/> × <paramref name="height"/> metres.</summary>
    private static ShipScene WithHolo(ShipScene scene, float length, float width, float height)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        Vector3[] scaled = [.. vertices.Select(v => new Vector3((v.X - 0.5f) * length, (v.Y - 0.5f) * width, v.Z * height))];
        var mesh = HoloReader.Read(HoloFixtures.Write(scaled, triangles, HoloFixtures.CubeEdges(), 4, 8, length));
        var placement = HoloPlacement.Place(mesh, scene.Bounds);
        return scene with { Marker = new BowMarker.Holo(placement.Min, placement.Max) };
    }

    private static ShipScene HoloScene(string shipId) => shipId == "hull_a"
        ? WithHolo(Scene(shipId), 30, 12.5f, 10)
        : WithHolo(Scene(shipId), 125, 45, 25);

    private static ShipScene Scene(string shipId, params Crate[] crates)
    {
        var ship = Catalog.Get(shipId);
        return ShipViewBuilder.Build(new LoadPlan(ship.Id, crates, []), ship, []);
    }

    private static CameraBasis Basis(ShipScene scene, CameraNavigation navigation) =>
        CameraBasis.For(scene, navigation.Orientation, Viewport, Margin);

    private static CameraMath Drawn(CameraNavigation navigation, ShipScene scene) =>
        navigation.On(Basis(scene, navigation), Limits, Viewport);

    private static CameraNavigation Orbit(CameraNavigation navigation, ShipScene scene, double yaw, double pitch) =>
        navigation.OrbitBy(yaw, pitch, Basis(scene, navigation));

    private static IEnumerable<CameraNavigation> TurnEverywhere(CameraNavigation start, ShipScene scene)
    {
        var navigation = Orbit(start, scene, -start.Orientation.Yaw, -75 - start.Orientation.Pitch);
        yield return navigation;
        for (var yawStep = 0; yawStep < 24; yawStep++)
        {
            var direction = yawStep % 2 == 0 ? 15 : -15;
            for (var pitchStep = 0; pitchStep < 10; pitchStep++)
            {
                navigation = Orbit(navigation, scene, 0, direction);
                yield return navigation;
            }

            navigation = Orbit(navigation, scene, 15, 0);
            yield return navigation;
        }
    }

    private static CameraNavigation ZoomedOutToTheLimit(CameraNavigation navigation, ShipScene scene)
    {
        while (navigation.ZoomAt(Middle, 1 / 1.2, Basis(scene, navigation), Limits, Viewport) is var zoomed && zoomed != navigation)
        {
            navigation = zoomed;
        }

        return navigation;
    }

    private static void ShouldKeepTheDistance(CameraNavigation start, ShipScene scene)
    {
        var distance = Drawn(start, scene).Distance;
        foreach (var turned in TurnEverywhere(start, scene))
        {
            Drawn(turned, scene).Distance.Should().Be(distance, $"turned to yaw {turned.Orientation.Yaw}°, pitch {turned.Orientation.Pitch}°");
        }
    }

    // ---------- the four guards (holo pump bugfix, D76) ----------

    [Theory]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void Orbiting_from_the_fit_keeps_distance_and_target(string shipId)
    {
        var scene = HoloScene(shipId);
        var fit = CameraNavigation.DefaultFor(scene.Bounds);
        var placement = Orbit(fit, scene, 0, 0).Placement;

        placement.Should().NotBeNull();
        foreach (var turned in TurnEverywhere(fit, scene))
        {
            turned.Placement.Should().Be(placement);
            Drawn(turned, scene).PxPerCellAtTarget.Should().Be(Drawn(fit, scene).PxPerCellAtTarget, $"turned to yaw {turned.Orientation.Yaw}°, pitch {turned.Orientation.Pitch}°");
        }
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void A_hologram_arriving_after_the_placement_keeps_the_distance(string shipId)
    {
        var wedge = Scene(shipId);
        var holo = HoloScene(shipId);
        var turned = Orbit(CameraNavigation.DefaultFor(wedge.Bounds), wedge, 15, 0);

        Drawn(turned, holo).Distance.Should().Be(Drawn(turned, wedge).Distance);
        ShouldKeepTheDistance(turned, holo);
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void Orbiting_at_the_zoom_out_limit_keeps_the_distance(string shipId)
    {
        var scene = HoloScene(shipId);

        ShouldKeepTheDistance(ZoomedOutToTheLimit(CameraNavigation.DefaultFor(scene.Bounds), scene), scene);
    }

    [Fact]
    public void A_plan_step_at_the_zoom_out_limit_keeps_the_distance()
    {
        // A long ship with the wedge fits its loaded racks (D61): a plan step that empties racks changes that fit.
        var ship = Catalog.Get("c2_hercules");
        CameraOrientation.IsLong(ship.Bounds).Should().BeTrue();
        Crate[] everyRack = [.. ship.Racks.Select(r => new Crate("Riker", "Gold", CrateSize.One, r.Id, new GridPosition(0, 0, 0)))];
        var before = Scene(ship.Id, everyRack);
        var after = Scene(ship.Id, everyRack[0]);
        var limit = ZoomedOutToTheLimit(CameraNavigation.DefaultFor(ship.Bounds), before);

        Drawn(limit, after).Distance.Should().Be(Drawn(limit, before).Distance);
    }

    // ---------- fit ----------

    [Theory]
    [InlineData("utv")]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void The_fit_shows_every_fit_point_within_the_margin_and_touches_it(string shipId)
    {
        var scene = shipId == "utv" ? Scene("utv") : HoloScene(shipId);
        var camera = Drawn(CameraNavigation.DefaultFor(scene.Bounds), scene);
        Point[] points = [.. CameraFit.FitPoints(scene).Select(camera.Project)];

        points.Should().OnlyContain(p => p.X >= Margin - 1e-6 && p.X <= Viewport.Width - Margin + 1e-6
            && p.Y >= Margin - 1e-6 && p.Y <= Viewport.Height - Margin + 1e-6);
        var gap = points.Min(p => Math.Min(Math.Min(p.X, Viewport.Width - p.X), Math.Min(p.Y, Viewport.Height - p.Y)));
        gap.Should().BeApproximately(Margin, 1e-6);
    }

    [Fact]
    public void A_long_ship_with_cargo_fits_its_loaded_racks_and_the_bow()
    {
        var ship = Catalog.Get("c2_hercules");
        var bowRack = ship.Racks.MaxBy(r => r.Origin.X)!;
        var scene = Scene(ship.Id, new Crate("Riker", "Gold", CrateSize.One, bowRack.Id, new GridPosition(0, 0, 0)));

        var fitted = CameraFit.FitPoints(scene).ToArray();

        fitted.Max(p => p.X).Should().BeGreaterThan(scene.Bounds.Max.X, "the bow wedge is in the fit");
        fitted.Min(p => p.X).Should().BeGreaterThanOrEqualTo(scene.Racks.Single(r => r.RackId == bowRack.Id).Bounds.Min.X);
    }

    // ---------- zoom, pan, limits ----------

    [Fact]
    public void Zooming_keeps_the_point_of_the_target_plane_under_the_cursor()
    {
        var scene = HoloScene("hull_a");
        var navigation = CameraNavigation.DefaultFor(scene.Bounds);
        var before = Drawn(navigation, scene);
        Point cursor = new(300, 140);
        var ray = before.Unproject(cursor);
        var t = Vector3D.DotProduct(before.Target - ray.Origin, before.LookDirection) / Vector3D.DotProduct(ray.Direction, before.LookDirection);
        var onPlane = ray.Origin + t * ray.Direction;

        var after = Drawn(navigation.ZoomAt(cursor, 1.2, Basis(scene, navigation), Limits, Viewport), scene);

        after.Distance.Should().BeApproximately(before.Distance / 1.2, 1e-9);
        (after.Project(onPlane) - cursor).Length.Should().BeLessThan(1e-6);
    }

    [Fact]
    public void Panning_moves_the_target_plane_with_the_mouse()
    {
        var scene = HoloScene("hull_a");
        var navigation = CameraNavigation.DefaultFor(scene.Bounds);
        var before = Drawn(navigation, scene);
        System.Windows.Vector delta = new(25, -40);

        var after = Drawn(navigation.PanBy(delta, Basis(scene, navigation), Limits, Viewport), scene);

        after.Distance.Should().Be(before.Distance);
        (after.Project(before.Target) - (before.Project(before.Target) + delta)).Length.Should().BeLessThan(1e-6);
    }

    [Fact]
    public void Zooming_in_stops_at_the_preferred_pixels_per_cell_in_the_target_plane()
    {
        var scene = HoloScene("hull_c");
        var navigation = CameraNavigation.DefaultFor(scene.Bounds);
        for (var i = 0; i < 100; i++)
        {
            navigation = navigation.ZoomAt(Middle, 1.2, Basis(scene, navigation), Limits, Viewport);
        }

        Drawn(navigation, scene).PxPerCellAtTarget.Should().BeApproximately(Limits.MaxPxPerCell, 1e-6);
        Limits.Near(Basis(scene, navigation), Viewport)
            .Should().BeApproximately(Viewport.Width / (2 * Limits.MaxPxPerCell * Math.Tan(CameraMath.FieldOfView / 2 * Math.PI / 180)), 1e-9);
    }

    [Fact]
    public void Zooming_out_stops_at_twice_the_reference_fit_or_the_whole_scene()
    {
        var scene = HoloScene("hull_a");
        var navigation = ZoomedOutToTheLimit(CameraNavigation.DefaultFor(scene.Bounds), scene);
        var basis = Basis(scene, navigation);

        Drawn(navigation, scene).Distance.Should().BeApproximately(
            Math.Max(DistanceLimits.FarShareOfFit * basis.ReferenceDistance, basis.WholeDistance), 1e-9);
    }

    [Fact]
    public void A_small_ship_in_a_large_view_may_be_closer_than_the_preference_at_its_fit()
    {
        // The fit is always in range (D76): the near limit is the preference or the reference fit, whichever is nearer.
        var scene = Scene("utv");
        var basis = Basis(scene, CameraNavigation.DefaultFor(scene.Bounds));

        Limits.Near(basis, Viewport).Should().Be(Math.Min(basis.ReferenceDistance, Limits.DistanceFor(Limits.MaxPxPerCell, Viewport)));
    }

    [Fact]
    public void Zooming_out_reaches_the_whole_scene_where_twice_the_fit_would_not()
    {
        // A true-size outline around a small hold (K5.2c): the fit shows the racks, zooming out shows the whole hull (K5.2d).
        var hold = Scene("hull_a");
        var (min, max) = (hold.Bounds.Min, hold.Bounds.Max);
        var (dx, dy) = (5.0 * (max.X - min.X), 5.0 * (max.Y - min.Y));
        Point3D[] hull = [new(min.X - dx, min.Y - dy, 0), new(max.X + dx, min.Y - dy, 0), new(max.X + dx, max.Y + dy, 0), new(min.X - dx, max.Y + dy, 0)];
        var scene = hold with { Marker = new BowMarker.Outline(hull) };
        var basis = Basis(scene, CameraNavigation.DefaultFor(scene.Bounds));
        basis.WholeDistance.Should().BeGreaterThan(DistanceLimits.FarShareOfFit * basis.ReferenceDistance);

        var navigation = ZoomedOutToTheLimit(CameraNavigation.DefaultFor(scene.Bounds), scene);

        Drawn(navigation, scene).Distance.Should().BeApproximately(basis.WholeDistance, 1e-9);
    }

    [Fact]
    public void A_whole_scene_within_twice_the_fit_leaves_the_far_limit_at_twice_the_fit()
    {
        // The Hull A, the Hull C, a hologram or the wedge: the scene is hardly larger than the fit (D76 as before).
        var scene = HoloScene("hull_a");
        var basis = Basis(scene, CameraNavigation.DefaultFor(scene.Bounds));
        basis.WholeDistance.Should().BeLessThanOrEqualTo(DistanceLimits.FarShareOfFit * basis.ReferenceDistance);

        Limits.Far(basis).Should().Be(DistanceLimits.FarShareOfFit * basis.ReferenceDistance);
    }

    [Fact]
    public void Zooming_at_the_limit_does_not_move_the_view()
    {
        var scene = HoloScene("hull_a");
        var limit = ZoomedOutToTheLimit(CameraNavigation.DefaultFor(scene.Bounds), scene);

        limit.ZoomAt(new Point(50, 50), 1 / 1.2, Basis(scene, limit), Limits, Viewport).Should().BeSameAs(limit);
    }

    [Fact]
    public void A_lower_maximum_brings_a_closer_view_back_at_once_around_its_target()
    {
        // The preference applies at once (D30): drawing clamps, Clamped writes it back.
        var scene = HoloScene("hull_c");
        var navigation = CameraNavigation.DefaultFor(scene.Bounds);
        for (var i = 0; i < 100; i++)
        {
            navigation = navigation.ZoomAt(Middle, 1.2, Basis(scene, navigation), Limits, Viewport);
        }

        DistanceLimits lower = new(40);
        var basis = Basis(scene, navigation);

        var clamped = navigation.Clamped(basis, lower, Viewport);

        clamped.Placement!.Distance.Should().Be(lower.Near(basis, Viewport));
        clamped.Placement.Distance.Should().BeGreaterThan(navigation.Placement!.Distance);
        clamped.Placement.Target.Should().Be(navigation.Placement.Target);
        navigation.On(basis, lower, Viewport).Distance.Should().Be(clamped.Placement.Distance);
        clamped.Clamped(basis, lower, Viewport).Should().BeSameAs(clamped);
    }

    [Fact]
    public void Panning_adds_up()
    {
        var scene = HoloScene("hull_a");
        var navigation = CameraNavigation.DefaultFor(scene.Bounds);
        var once = navigation.PanBy(new System.Windows.Vector(25, -40), Basis(scene, navigation), Limits, Viewport);
        var twice = once.PanBy(new System.Windows.Vector(-10, 15), Basis(scene, once), Limits, Viewport);
        var direct = navigation.PanBy(new System.Windows.Vector(15, -25), Basis(scene, navigation), Limits, Viewport);

        (twice.Placement!.Target - direct.Placement!.Target).Length.Should().BeLessThan(1e-9);
        twice.Placement.Distance.Should().Be(direct.Placement.Distance);
    }

    [Fact]
    public void The_zoom_on_top_of_the_fit_is_the_fit_distance_over_the_view_s()
    {
        // What the hologram's edge levels read (D63): 1 at the fit, 2 at half its distance.
        var scene = HoloScene("hull_a");
        var fit = CameraNavigation.DefaultFor(scene.Bounds);
        var basis = Basis(scene, fit);
        var closer = fit with { Placement = new CameraPlacement(basis.FitDistance / 2, basis.FitTarget) };

        fit.ZoomOn(basis, Limits, Viewport).Should().Be(1);
        closer.ZoomOn(basis, Limits, Viewport).Should().BeApproximately(2, 1e-12);
    }

    [Fact]
    public void Zooming_and_panning_keep_the_orientation()
    {
        var scene = HoloScene("hull_a");
        var navigation = Orbit(CameraNavigation.DefaultFor(scene.Bounds), scene, 30, -20);

        var zoomed = navigation.ZoomAt(new Point(300, 140), 1.2, Basis(scene, navigation), Limits, Viewport);
        var panned = zoomed.PanBy(new System.Windows.Vector(25, -40), Basis(scene, zoomed), Limits, Viewport);

        zoomed.Orientation.Should().Be(navigation.Orientation);
        panned.Orientation.Should().Be(navigation.Orientation);
    }

    [Fact]
    public void Another_window_size_keeps_a_placed_view_s_target_in_the_middle()
    {
        // K1.2: a restart with another overlay size shows the same point in the middle; distance and target are fixed to
        // the ship (D83), so a cell's size follows the view's width.
        var scene = HoloScene("hull_a");
        var start = CameraNavigation.DefaultFor(scene.Bounds);
        var navigation = start.PanBy(new System.Windows.Vector(25, -40), Basis(scene, start), Limits, Viewport);
        Size other = new(800, 300);

        var drawn = navigation.On(CameraBasis.For(scene, navigation.Orientation, other, Margin), Limits, other);

        (drawn.Project(navigation.Placement!.Target) - new Point(400, 150)).Length.Should().BeLessThan(1e-9);
        drawn.Distance.Should().Be(navigation.Placement.Distance);
    }

    [Fact]
    public void Default_navigation_looks_from_the_default_orientation_and_fits()
    {
        CameraNavigation.Default.Orientation.Should().Be(CameraOrientation.Default);
        CameraNavigation.Default.Placement.Should().BeNull();
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("caterpillar")]
    public void Default_navigation_for_a_ship_looks_from_its_default_orientation(string shipId)
    {
        var bounds = Catalog.Get(shipId).Bounds;

        CameraNavigation.DefaultFor(bounds).Should().Be(new CameraNavigation(CameraOrientation.DefaultFor(bounds)));
    }

    // ---------- the saved camera (K1.2) ----------

    [Fact]
    public void A_camera_comes_back_from_settings_as_it_was_saved()
    {
        CameraNavigation navigation = new(new CameraOrientation(200, -30), new CameraPlacement(42.5, new Point3D(1.5, -2.25, 3)));

        CameraNavigation.From(navigation.ToCamera()).Should().Be(navigation);
        navigation.ToCamera().Should().Be(new ShipCamera(200, -30, Distance: 42.5, Target: new CameraTarget(1.5, -2.25, 3)));
    }

    [Fact]
    public void A_camera_without_placement_fits()
    {
        CameraNavigation.From(new ShipCamera(120, 20)).Placement.Should().BeNull();
        CameraNavigation.From(new ShipCamera(120, 20, Distance: 30)).Placement.Should().BeNull();
        CameraNavigation.From(new ShipCamera(120, 20, Target: new CameraTarget(0, 0, 0))).Placement.Should().BeNull();
    }

    [Fact]
    public void A_saved_camera_is_read_as_the_camera_can_be()
    {
        var orientation = CameraNavigation.From(new ShipCamera(400, 100)).Orientation;

        (orientation.Yaw, orientation.Pitch).Should().Be((40, CameraOrientation.MaxPitch));
    }

    [Fact]
    public void A_saved_view_stays_on_the_same_rack_when_the_fit_changes()
    {
        // Target and distance are ship-fixed: cargo or a hologram that moves the fit doesn't move a placed view.
        var wedge = Scene("hull_a");
        var holo = HoloScene("hull_a");
        var navigation = CameraNavigation.DefaultFor(wedge.Bounds).ZoomAt(new Point(300, 250), 1.5, Basis(wedge, CameraNavigation.DefaultFor(wedge.Bounds)), Limits, Viewport);
        var rack = ScreenPoints.Centre(wedge.Racks[0].Bounds);

        var saved = CameraNavigation.From(navigation.ToCamera());

        Drawn(saved, holo).Project(rack).Should().Be(Drawn(navigation, wedge).Project(rack));
    }

    [Fact]
    public void A_saved_view_stays_on_the_same_rack_when_a_sync_loads_a_rack_further_aft()
    {
        // Martin at the K1.2 approval: save the camera, load a far rack, load the camera: the same middle on the same rack.
        var ship = Catalog.Get("caterpillar");
        Crate Gold(string rackId) => new("Riker", "Gold", CrateSize.One, rackId, new GridPosition(0, 0, 0));
        var before = Scene(ship.Id, Gold("rack-0"));
        var after = Scene(ship.Id, Gold("rack-0"), Gold("rack-13"));
        var start = CameraNavigation.DefaultFor(ship.Bounds);
        var navigation = start.ZoomAt(new Point(300, 250), 1.5, Basis(before, start), Limits, Viewport);
        var rack = ScreenPoints.Centre(before.Racks[0].Bounds);

        Basis(after, navigation).FitTarget.Should().NotBe(Basis(before, navigation).FitTarget, "the fit follows the cargo (D61)");
        Drawn(CameraNavigation.From(navigation.ToCamera()), after).Project(rack).Should().Be(Drawn(navigation, before).Project(rack));
    }
}
