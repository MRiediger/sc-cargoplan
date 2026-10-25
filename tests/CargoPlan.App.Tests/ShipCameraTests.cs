using System.Windows;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>The camera per ship in settings.json (K1.2, D76): turned, zoomed and panned, the view comes back after a restart and a ship change.</summary>
public class ShipCameraTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");
    private static readonly Ship C2 = Catalog.Get("c2_hercules");

    private static readonly ShipCamera Turned = new(120, 20);
    private static readonly ShipCamera Framed = new(200, -30, 90, 1.5, -2.25);

    private static List<string> Changes(OverlayViewModel viewModel)
    {
        List<string> changes = [];
        viewModel.PropertyChanged += (_, e) => changes.Add(e.PropertyName!);
        return changes;
    }

    // --- The overlay keeps a camera per ship ---

    [Fact]
    public void A_new_overlay_has_no_camera_so_the_view_fits()
    {
        OverlayViewModel viewModel = new(HullA, Catalog);

        viewModel.Camera.Should().BeNull();
        viewModel.Cameras.Should().BeEmpty();
    }

    [Fact]
    public void The_start_reads_the_camera_of_its_ship()
    {
        OverlayViewModel viewModel = new(HullA, Catalog) { Cameras = new Dictionary<string, ShipCamera> { ["hull_a"] = Framed, ["c2_hercules"] = Turned } };

        viewModel.Camera.Should().Be(Framed);
    }

    [Fact]
    public void A_camera_is_kept_for_the_current_ship_and_tells_the_settings()
    {
        OverlayViewModel viewModel = new(HullA, Catalog);
        var changes = Changes(viewModel);

        viewModel.Camera = Framed;

        viewModel.Cameras.Should().Equal(new Dictionary<string, ShipCamera> { ["hull_a"] = Framed });
        changes.Should().Contain([nameof(OverlayViewModel.Camera), nameof(OverlayViewModel.Cameras)]);
    }

    [Fact]
    public void The_same_camera_again_tells_nothing()
    {
        // The view writes its camera on every frame of a drag; an echo must not write the settings again.
        OverlayViewModel viewModel = new(HullA, Catalog) { Cameras = new Dictionary<string, ShipCamera> { ["hull_a"] = Framed } };
        var changes = Changes(viewModel);

        viewModel.Camera = Framed with { };

        changes.Should().BeEmpty();
    }

    [Fact]
    public void No_camera_removes_the_ship_s_entry()
    {
        // The reset (double-click, K1.3's button): the ship fits again, also after a restart.
        OverlayViewModel viewModel = new(HullA, Catalog) { Cameras = new Dictionary<string, ShipCamera> { ["hull_a"] = Framed, ["c2_hercules"] = Turned } };

        viewModel.Camera = null;

        viewModel.Camera.Should().BeNull();
        viewModel.Cameras.Keys.Should().Equal("c2_hercules");
    }

    [Fact]
    public void A_ship_change_brings_the_other_ship_s_camera_and_back()
    {
        OverlayViewModel viewModel = new(HullA, Catalog);
        viewModel.Camera = Framed;
        var changes = Changes(viewModel);

        viewModel.ChangeShip(C2);

        viewModel.Camera.Should().BeNull("the C2 has none yet");
        changes.Should().Contain(nameof(OverlayViewModel.Camera));
        viewModel.Camera = Turned;
        viewModel.ChangeShip(HullA);
        viewModel.Camera.Should().Be(Framed);
        viewModel.ChangeShip(C2);
        viewModel.Camera.Should().Be(Turned);
    }

    [Fact]
    public void Only_the_recent_ships_and_the_current_one_keep_a_camera()
    {
        // Recent holds 4 ids (T7.2c): the file never collects cameras of every ship the player ever tried.
        OverlayViewModel viewModel = new(HullA, Catalog)
        {
            RecentShips = ["hull_a", "c2_hercules"],
            Cameras = new Dictionary<string, ShipCamera> { ["c2_hercules"] = Turned, ["caterpillar"] = Turned, ["gone"] = Turned },
        };

        viewModel.Camera = Framed;

        viewModel.Cameras.Keys.Should().BeEquivalentTo("hull_a", "c2_hercules");
    }

    [Fact]
    public void A_camera_is_no_step()
    {
        // K2 handover: view actions don't go through Step, so undo never turns the camera.
        OverlayViewModel viewModel = new(HullA, Catalog);
        viewModel.ShowPlan(TestPlans.SplitAndShared());

        viewModel.Camera = Framed;

        viewModel.CanUndo.Should().BeFalse();
    }

    // --- The camera is the view's navigation, written down ---

    private static readonly Size Viewport = new(800, 600);
    private static readonly ZoomLimits Limits = ZoomLimits.Default;

    [Fact]
    public void A_navigation_comes_back_from_its_camera()
    {
        var navigation = new ViewNavigation(new IsoProjection(200, -30), new Framing(90, new Vector(1.5, -2.25)));

        navigation.ToCamera().Should().Be(Framed);
        ViewNavigation.From(Framed).Should().Be(navigation);
    }

    [Fact]
    public void A_camera_without_a_framing_fits()
    {
        // Only turned: the next start fits at that camera, whatever size the overlay has then.
        ViewNavigation.From(Turned).Should().Be(new ViewNavigation(new IsoProjection(120, 20)));
        new ViewNavigation(new IsoProjection(120, 20)).ToCamera().Should().Be(Turned);
    }

    [Theory]
    [InlineData(90.0, null, 2.0)]
    [InlineData(null, 1.0, 2.0)]
    [InlineData(90.0, 1.0, null)]
    public void A_camera_with_part_of_a_framing_fits(double? pxPerCell, double? panX, double? panY)
    {
        // Edited by hand: a framing needs all three.
        ViewNavigation.From(new ShipCamera(120, 20, pxPerCell, panX, panY)).Framing.Should().BeNull();
    }

    [Fact]
    public void A_camera_out_of_range_is_taken_as_the_camera_can_be()
    {
        // The file is edited by hand: the projection wraps the yaw and clamps the pitch, the drawing clamps the zoom.
        var navigation = ViewNavigation.From(new ShipCamera(400, 100));

        navigation.Camera.Should().Be(new IsoProjection(40, IsoProjection.MaxPitch));
    }

    [Fact]
    public void A_saved_view_stays_on_the_same_rack_when_a_sync_loads_a_rack_further_aft()
    {
        // Martin at the approval: save the camera, load a far rack, load the camera: the same middle on the same rack.
        var caterpillar = Catalog.Get("caterpillar");
        var camera = IsoProjection.DefaultFor(caterpillar.Bounds);
        var before = SceneProjector.Project(ShipViewBuilder.Build(OneCrateIn(caterpillar, "rack-0"), caterpillar, []), camera);
        var after = SceneProjector.Project(ShipViewBuilder.Build(OneCrateIn(caterpillar, "rack-0", "rack-13"), caterpillar, []), camera);
        var basisBefore = ViewBasis.Of(before.FitExtent, before.Anchor, Viewport, 8);
        var basisAfter = ViewBasis.Of(after.FitExtent, after.Anchor, Viewport, 8);
        var saved = new ViewNavigation(camera).ZoomAt(new Point(300, 250), 1.5, basisBefore, Limits).ToCamera();

        var shown = ViewNavigation.From(saved);

        after.FitExtent.Should().NotBe(before.FitExtent, "the far rack widens the fit");
        var rack1Before = shown.On(basisBefore, Limits).Apply(before.Racks.Single(r => r.Number == 1).LabelCentre);
        var rack1After = shown.On(basisAfter, Limits).Apply(after.Racks.Single(r => r.Number == 1).LabelCentre);
        rack1After.X.Should().BeApproximately(rack1Before.X, 1e-9);
        rack1After.Y.Should().BeApproximately(rack1Before.Y, 1e-9);
    }

    private static LoadPlan OneCrateIn(Ship ship, params string[] rackIds) =>
        TestPlans.Of(ship, [.. rackIds.Select(id => new Placement("Riker", id, new CargoItem("Titanium", 1, CrateSize.One)))]);
}
