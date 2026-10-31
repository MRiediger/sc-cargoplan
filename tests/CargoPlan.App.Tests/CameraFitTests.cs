using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// What the perspective fit shows (D61, D62, D63, D83), and room on top for the numbers above a long ship's racks (D61),
/// kept by it (T8.2).
/// </summary>
public class CameraFitTests
{
    private static readonly Size View = new(420, 420);
    private const double Margin = 8;

    [Fact]
    public void Room_on_top_keeps_every_fit_point_below_it_and_one_touches_it()
    {
        var ship = ShipLoader.Load("c2_hercules");
        var scene = ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);
        Point3D[] points = [.. CameraFit.FitPoints(scene)];
        var orientation = CameraOrientation.DefaultFor(scene.Bounds);
        var target = CameraFit.Centre(points);
        const double top = 40;

        var distance = CameraFit.Distance(orientation, target, points, View, Margin, top);
        var camera = new CameraMath(orientation, distance, target, View);
        Point[] screen = [.. points.Select(camera.Project)];

        screen.Should().OnlyContain(p => p.Y >= top + Margin - 1e-6 && p.Y <= View.Height - Margin + 1e-6);
        screen.Min(p => Math.Min(p.Y - top, Math.Min(p.X, View.Width - p.X))).Should().BeApproximately(Margin, 1e-6);
    }

    [Fact]
    public void Without_room_on_top_the_fit_is_as_before()
    {
        var ship = ShipLoader.Load("hull_a");
        var scene = ShipViewBuilder.Build(new LoadPlan(ship.Id, [], []), ship, []);
        Point3D[] points = [.. CameraFit.FitPoints(scene)];
        var target = CameraFit.Centre(points);

        CameraFit.Distance(CameraOrientation.Default, target, points, View, Margin, top: 0)
            .Should().Be(CameraFit.Distance(CameraOrientation.Default, target, points, View, Margin));
    }

    // ---------- what the fit shows (D61, D62, D63) ----------

    private static readonly Ship Caterpillar = ShipLoader.Load("caterpillar");

    // An arrow 20 m long, 8 m wide, pointing to the bow, placed on the scene's racks as the overlay does.
    private static readonly ShipOutline Arrow = new("arrow", [new(10, 0), new(-10, 4), new(-10, -4)]);

    private static ShipScene Sample()
    {
        var (ship, plan) = TestPlans.Load();
        return ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship));
    }

    /// <summary>The Caterpillar with one 1 SCU crate in each of these racks.</summary>
    private static ShipScene CaterpillarWith(params string[] rackIds) => ShipViewBuilder.Build(
        TestPlans.Of(Caterpillar, [.. rackIds.Select(id => new Placement("Riker", id, new CargoItem("Titanium", 1, CrateSize.One)))]),
        Caterpillar,
        []);

    private static ShipScene WithOutline(ShipScene scene) =>
        scene with { Marker = new BowMarker.Outline(OutlinePlacement.Place(Arrow, scene.Bounds)) };

    private static ShipScene WithoutMarker(ShipScene scene) => scene with { Marker = new BowMarker.None() };

    private static Point3D[] Corners(ShipBox box) =>
    [
        .. from x in new double[] { box.Min.X, box.Max.X }
           from y in new double[] { box.Min.Y, box.Max.Y }
           from z in new double[] { box.Min.Z, box.Max.Z }
           select new Point3D(x, y, z),
    ];

    private static IEnumerable<Point3D> WedgeCorners(ShipBox box) => BowWedge.Faces(box).SelectMany(f => f.Corners);

    [Fact]
    public void The_whole_scene_holds_the_racks_and_the_bow_wedge()
    {
        var scene = Sample();

        CameraFit.WholePoints(scene).Should().BeEquivalentTo(Corners(scene.Bounds).Concat(WedgeCorners(scene.Bounds)));
    }

    [Fact]
    public void A_compact_ship_fits_the_whole_scene()
    {
        var scene = Sample();

        CameraFit.FitPoints(scene).Should().BeEquivalentTo(CameraFit.WholePoints(scene));
    }

    [Fact]
    public void A_long_ship_without_cargo_fits_the_whole_scene()
    {
        var scene = CaterpillarWith();

        CameraFit.FitPoints(scene).Should().BeEquivalentTo(CameraFit.WholePoints(scene));
    }

    [Fact]
    public void A_hologram_is_fitted_whole_even_on_a_long_ship_with_cargo()
    {
        // A box longer than the Caterpillar's racks: the hull may reach beyond them, and the fit shows all of it (D63).
        var bounds = Caterpillar.Bounds;
        Point3D min = new(bounds.Min.X - 10, bounds.Min.Y - 3, bounds.Min.Z - 2);
        Point3D max = new(bounds.Max.X + 12, bounds.Max.Y + 3, bounds.Max.Z + 4);
        var scene = CaterpillarWith("rack-0", "rack-6") with { Marker = new BowMarker.Holo(min, max) };

        Point3D[] fitted = [.. CameraFit.FitPoints(scene)];

        fitted.Should().BeEquivalentTo(CameraFit.WholePoints(scene));
        fitted.Should().Contain([min, max]);
    }

    [Fact]
    public void A_compact_ship_with_an_outline_fits_as_with_the_wedge()
    {
        // Since K5.2c the outline is at least true size, mostly far larger than the racks: with it a ship fits as with the
        // wedge, its racks and a short bow zone, a tenth of their length (D52, D62 addendum); the hull beyond may be cut.
        var scene = Sample();

        CameraFit.FitPoints(WithOutline(scene)).Should().BeEquivalentTo(CameraFit.FitPoints(scene));
        CameraFit.WholePoints(WithOutline(scene)).Should().Contain(((BowMarker.Outline)WithOutline(scene).Marker).Points);
    }

    [Fact]
    public void A_long_ship_with_an_outline_fits_its_loaded_racks_as_with_the_wedge_but_not_the_whole_outline()
    {
        // Racks 1 and 3 hold cargo (D61).
        var scene = CaterpillarWith("rack-0", "rack-6");
        Point3D[] fitted = [.. CameraFit.FitPoints(WithOutline(scene))];

        fitted.Should().BeEquivalentTo(CameraFit.FitPoints(scene));
        fitted.Min(p => p.X).Should().BeGreaterThan(CameraFit.WholePoints(WithOutline(scene)).Min(p => p.X));
    }

    [Fact]
    public void Without_a_marker_a_compact_ship_fits_its_racks_alone()
    {
        var scene = WithoutMarker(Sample());

        CameraFit.FitPoints(scene).Should().BeEquivalentTo(Corners(scene.Bounds));
        CameraFit.WholePoints(scene).Should().BeEquivalentTo(Corners(scene.Bounds));
    }

    [Fact]
    public void Without_a_marker_a_long_ship_fits_its_loaded_racks_only()
    {
        var scene = WithoutMarker(CaterpillarWith("rack-0", "rack-6"));
        var loaded = scene.Racks.Where(r => r.IsLoaded).Select(r => r.Bounds).Aggregate((a, b) => a.Union(b));

        CameraFit.FitPoints(scene).Should().BeEquivalentTo(Corners(loaded));
    }
}
