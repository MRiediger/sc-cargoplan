using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// Only the player changes the scale (D76, K5.5): turning, a hologram arriving or a plan step never do, also at the zoom
/// limit. Guards of the holo pump bugfix: K5.5's test turned from the fit only, so the zoom limits, which followed the
/// fit at the current angle, let the pump through (Hull A +24 % looking from the bow, at the limit +148 %).
/// </summary>
public sealed class OrbitScaleTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();

    // The default overlay's width; the ship view is about as high.
    private static readonly Size Viewport = new(420, 420);
    private const double Margin = 8;
    private static readonly ZoomLimits Limits = ZoomLimits.Default;

    /// <summary>A hologram box of <paramref name="length"/> × <paramref name="width"/> × <paramref name="height"/> metres.</summary>
    private static ShipScene WithHolo(ShipScene scene, float length, float width, float height)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        Vector3[] scaled = [.. vertices.Select(v => new Vector3((v.X - 0.5f) * length, (v.Y - 0.5f) * width, v.Z * height))];
        var mesh = HoloReader.Read(HoloFixtures.Write(scaled, triangles, HoloFixtures.CubeEdges(), 4, 8, length));
        var placement = HoloPlacement.Place(mesh, scene.Bounds);
        return scene with { Marker = new BowMarker.Holo(placement.Min, placement.Max) };
    }

    // Hull A as in HoloViewTests; the Hull C as a long box around its two crosses.
    private static ShipScene HoloScene(string shipId) => shipId == "hull_a"
        ? WithHolo(Scene(shipId), 30, 12.5f, 10)
        : WithHolo(Scene(shipId), 125, 45, 25);

    private static ShipScene Scene(string shipId, params Crate[] crates)
    {
        var ship = Catalog.Get(shipId);
        return ShipViewBuilder.Build(new LoadPlan(ship.Id, crates, []), ship, []);
    }

    // The same ship at its default camera, projected once per scene as the ship view does.
    private static readonly ConditionalWeakTable<ShipScene, ProjectedScene> References = [];

    // As ShipViewControl.Basis: the scene at the camera, the same ship at its default camera.
    private static ViewBasis Basis(ShipScene scene, IsoProjection camera) => ViewBasis.For(
        SceneProjector.Project(scene, camera),
        References.GetValue(scene, s => SceneProjector.Project(s, IsoProjection.DefaultFor(s.Bounds))),
        Viewport,
        Margin,
        0);

    private static ViewTransform Drawn(ViewNavigation navigation, ShipScene scene) => navigation.On(Basis(scene, navigation.Camera), Limits);

    // As the orbit gesture: the basis of the camera the step starts from.
    private static ViewNavigation Orbit(ViewNavigation navigation, ShipScene scene, double yaw, double pitch) =>
        navigation.OrbitBy(yaw, pitch, Basis(scene, navigation.Camera));

    /// <summary>
    /// A path through every yaw and pitch in 15° steps, each step 15°: up and down the pitches from −75° to 75° at each yaw,
    /// a full turn. Starts at the ship's default camera.
    /// </summary>
    private static IEnumerable<ViewNavigation> TurnEverywhere(ViewNavigation start, ShipScene scene)
    {
        var navigation = Orbit(start, scene, -start.Camera.Yaw, -75 - start.Camera.Pitch);
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

    private static ViewNavigation ZoomedOutToTheLimit(ViewNavigation navigation, ShipScene scene)
    {
        while (navigation.ZoomAt(new Point(Viewport.Width / 2, Viewport.Height / 2), 1 / 1.2, Basis(scene, navigation.Camera), Limits) is var zoomed
            && zoomed != navigation)
        {
            navigation = zoomed;
        }

        return navigation;
    }

    private static void ShouldKeepTheScale(ViewNavigation start, ShipScene scene)
    {
        var scale = Drawn(start, scene).Scale;
        foreach (var turned in TurnEverywhere(start, scene))
        {
            Drawn(turned, scene).Scale.Should().Be(scale, $"turned to yaw {turned.Camera.Yaw}°, pitch {turned.Camera.Pitch}°");
        }
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void Orbiting_from_the_fit_keeps_scale_and_pan(string shipId)
    {
        var scene = HoloScene(shipId);
        var fit = ViewNavigation.DefaultFor(scene.Bounds);
        var framing = Orbit(fit, scene, 0, 0).Framing;

        foreach (var turned in TurnEverywhere(fit, scene))
        {
            turned.Framing.Should().Be(framing);
            Drawn(turned, scene).Scale.Should().Be(Drawn(fit, scene).Scale, $"turned to yaw {turned.Camera.Yaw}°, pitch {turned.Camera.Pitch}°");
        }
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void A_hologram_arriving_after_the_framing_keeps_the_scale(string shipId)
    {
        var wedge = Scene(shipId);
        var holo = HoloScene(shipId);
        var turned = Orbit(ViewNavigation.DefaultFor(wedge.Bounds), wedge, 15, 0);

        Drawn(turned, holo).Scale.Should().Be(Drawn(turned, wedge).Scale);
        ShouldKeepTheScale(turned, holo);
    }

    [Theory]
    [InlineData("hull_a")]
    [InlineData("hull_c")]
    public void Orbiting_at_the_zoom_out_limit_keeps_the_scale(string shipId)
    {
        var scene = HoloScene(shipId);

        ShouldKeepTheScale(ZoomedOutToTheLimit(ViewNavigation.DefaultFor(scene.Bounds), scene), scene);
    }

    [Fact]
    public void A_plan_step_at_the_zoom_out_limit_keeps_the_scale()
    {
        // A long ship with the wedge fits its loaded racks (D61): a plan step that empties racks changes that fit.
        var ship = Catalog.Get("c2_hercules");
        IsoProjection.IsLong(ship.Bounds).Should().BeTrue();
        Crate[] everyRack = [.. ship.Racks.Select(r => new Crate("Riker", "Gold", CrateSize.One, r.Id, new GridPosition(0, 0, 0)))];
        var before = Scene(ship.Id, everyRack);
        var after = Scene(ship.Id, everyRack[0]);
        var limit = ZoomedOutToTheLimit(ViewNavigation.DefaultFor(ship.Bounds), before);

        Drawn(limit, after).Scale.Should().Be(Drawn(limit, before).Scale);
    }
}
