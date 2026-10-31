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
    private static readonly ShipCamera Framed = new(200, -30, Distance: 42.5, Target: new CameraTarget(1.5, -2.25, 3));

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
        OverlayViewModel viewModel = new(HullA, Catalog)
        {
            RecentShips = ["hull_a", "c2_hercules"],
            Cameras = new Dictionary<string, ShipCamera> { ["hull_a"] = Framed, ["c2_hercules"] = Turned },
        };

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

    // --- Reset view: the header's button and the resetView hotkey (K1.3) ---

    [Fact]
    public void Reset_view_fits_the_ship_again_and_forgets_its_camera()
    {
        OverlayViewModel viewModel = new(HullA, Catalog)
        {
            RecentShips = ["hull_a", "c2_hercules"],
            Cameras = new Dictionary<string, ShipCamera> { ["hull_a"] = Framed, ["c2_hercules"] = Turned },
        };

        viewModel.ResetViewCommand.Execute(null);

        viewModel.Camera.Should().BeNull();
        viewModel.Cameras.Keys.Should().Equal("c2_hercules");
    }

    [Fact]
    public void Reset_view_works_while_locked_and_without_a_plan()
    {
        // The hotkey fires in the game, where the overlay is locked (D11); the button is hidden then (its style).
        OverlayViewModel viewModel = new(HullA, Catalog) { IsManuallyLocked = true };
        viewModel.Camera = Framed;

        viewModel.ResetViewCommand.CanExecute(null).Should().BeTrue();
        viewModel.ResetViewCommand.Execute(null);

        viewModel.Camera.Should().BeNull();
    }
}
