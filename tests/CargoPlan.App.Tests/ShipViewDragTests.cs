using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.Tests;

/// <summary>
/// Drags on the live view (T8.4): press, moves and release through the control's own hit test, drop target, Core's
/// preview and commands. The points come from WPF's mapping of the control's camera (<see cref="ScreenPoints.InPicture"/>);
/// the rules themselves are <see cref="DragGestureTests"/>' and <see cref="CrateDropTests"/>'. Outside a window the view
/// gets no capture, so no cursor poll runs: moves come from <c>MoveTo</c> alone.
/// </summary>
public class ShipViewDragTests
{
    private static readonly Size Size = new(420, 420);

    // Rack 3's free cell at the aft end of the outer layer, on Orison's Laranite (SceneHitTestTests): a drop there lands
    // at [0, 1, 1] of rack-2.
    private static readonly Point3D Rack3Free = new(0.5, -3.5, 5);

    // Shubin's 8 SCU crate in Rack 2 shows on the bow face.
    private static readonly Point3D ShubinBow = new(4, 1.5, 0.5);

    private static readonly Point Beside = new(3, 3);

    private sealed record Commands(List<Gesture.MoveCrates> Moves, List<Gesture.MoveLegend> Legend);

    private static (ShipViewControl View, Commands Commands) Shown(LoadPlan? plan = null)
    {
        var (ship, sample) = TestPlans.Load();
        var shown = plan ?? sample;
        var view = new ShipViewControl { Scene = ShipViewBuilder.Build(shown, ship, LoadPlanner.Validate(shown, ship)) };

        // App.xaml's brush, which OnRender looks up; there is no App in the tests.
        view.Resources["HitTestBackgroundBrush"] = Brushes.Transparent;
        Commands commands = new([], []);
        view.MoveCratesCommand = new RelayCommand<Gesture.MoveCrates>(m => commands.Moves.Add(m!));
        view.MoveLegendCommand = new RelayCommand<Gesture.MoveLegend>(m => commands.Legend.Add(m!));
        view.Measure(Size);
        view.Arrange(new Rect(Size));
        view.UpdateLayout();
        return (view, commands);
    }

    private static Crate Shubin(ShipViewControl view) => view.Scene!.Plan.Crates.Single(c => c.Destination == "Shubin");

    private static void Drag(ShipViewControl view, Point from, Point to)
    {
        view.Press(MouseButton.Right, from, 1);
        view.MoveTo(to);
        view.Release(MouseButton.Right, to);
    }

    [Fact]
    public void A_crate_dragged_onto_a_free_cell_of_another_rack_is_moved_there()
    {
        Sta.Run(() =>
        {
            var (view, commands) = Shown();
            var from = ScreenPoints.InPicture(view, ShubinBow);
            view.HitAt(from)!.Crate.Should().Be(Shubin(view));

            Drag(view, from, ScreenPoints.InPicture(view, Rack3Free));

            commands.Moves.Should().ContainSingle().Which.Should().BeEquivalentTo(
                new Gesture.MoveCrates(Shubin(view), [Shubin(view)], "rack-2", new GridPosition(0, 1, 1)));
        });
    }

    [Fact]
    public void The_preview_is_the_drop()
    {
        // What the drag shows before the release is what the release runs (D65): faded, ghost, gesture.
        Sta.Run(() =>
        {
            var (view, commands) = Shown();
            var to = ScreenPoints.InPicture(view, Rack3Free);
            view.Press(MouseButton.Right, ScreenPoints.InPicture(view, ShubinBow), 1);
            view.MoveTo(to);

            // Rack 3 has two free cells: Core makes room for the 8 SCU crate or refuses; either way the view shows Core's word.
            var pending = view.PendingGesture.Should().BeOfType<Gesture.MoveCrates>().Subject;
            var (scene, shubin) = (view.Scene!, Shubin(view));
            view.ShownPreview.Should().BeEquivalentTo(DragPreview.Of(scene.Ship, scene.Plan, [shubin], pending, CrateDrop.Outcome(scene.Plan, scene.Ship, pending)));
            view.ShownPreview.Faded.Should().Contain(shubin);
            (view.ShownPreview.Ghosts.Count + view.ShownPreview.Refused.Count).Should().BePositive();

            view.Release(MouseButton.Right, to);
            // The release resolves again at the same point: the same gesture, a new record.
            commands.Moves.Should().ContainSingle().Which.Should().BeEquivalentTo(pending);
            view.ShownPreview.Should().Be(DragPreview.None);
        });
    }

    [Fact]
    public void A_selected_crate_drags_the_whole_selection_and_another_crate_only_itself()
    {
        Sta.Run(() =>
        {
            var (view, commands) = Shown();
            var scene = view.Scene!;
            var titanium = scene.Plan.Crates.Where(c => c.Commodity == "Titanium").ToArray();
            view.Selection = CrateSelection.Of(scene, titanium.Take(2));

            Drag(view, ScreenPoints.InPicture(view, ShubinBow), ScreenPoints.InPicture(view, Rack3Free));
            commands.Moves.Should().ContainSingle().Which.Crates.Should().Equal(Shubin(view));

            view.Selection = CrateSelection.Of(scene, [Shubin(view), titanium[0]]);
            Drag(view, ScreenPoints.InPicture(view, ShubinBow), ScreenPoints.InPicture(view, Rack3Free));
            commands.Moves[^1].Crates.Should().Equal(Shubin(view), titanium[0]);
        });
    }

    [Fact]
    public void Back_onto_its_own_place_moves_nothing()
    {
        // A 1 SCU crate in Rack 1's outer layer, upper row, aft end, on another one at the spindle: while dragged it is air,
        // so the line to its top enters its own cell, and the column stops on the crate below, at its own place.
        Sta.Run(() =>
        {
            var (ship, _) = TestPlans.Load();
            Crate tin = new("Riker", "Tin", CrateSize.One, "rack-0", new GridPosition(0, 1, 1), Mission: 1);
            Crate below = new("Riker", "Tin", CrateSize.One, "rack-0", new GridPosition(0, 1, 0), Mission: 1);
            var (view, commands) = Shown(new LoadPlan(ship.Id, [tin, below], []));
            var from = ScreenPoints.InPicture(view, new Point3D(0.5, 2.5, 5));
            view.HitAt(from)!.Crate.Should().Be(tin);

            view.Press(MouseButton.Right, from, 1);
            view.MoveTo(ScreenPoints.InPicture(view, Rack3Free));
            view.MoveTo(from);
            view.Release(MouseButton.Right, from);

            commands.Moves.Should().BeEmpty();
        });
    }

    [Fact]
    public void Released_outside_the_view_a_drag_moves_nothing()
    {
        // K1.4: over the legend or the header nothing is hit, so there is no target.
        Sta.Run(() =>
        {
            var (view, commands) = Shown();

            Drag(view, ScreenPoints.InPicture(view, ShubinBow), new Point(Size.Width / 2, Size.Height + 40));

            commands.Moves.Should().BeEmpty();
        });
    }

    [Fact]
    public void A_right_drag_beside_the_crates_or_from_delivered_cargo_pans()
    {
        Sta.Run(() =>
        {
            var (ship, sample) = TestPlans.Load();
            var (view, commands) = Shown(LoadPlanner.SetDelivered(sample, ship, "Shubin", true));
            var before = view.Camera;

            Drag(view, Beside, new Point(60, 40));
            var panned = view.Camera;
            Drag(view, ScreenPoints.InPicture(view, ShubinBow), ScreenPoints.InPicture(view, ShubinBow) + new Vector(50, 30));

            panned.Should().NotBe(before);
            view.Camera.Should().NotBe(panned);
            commands.Moves.Should().BeEmpty();
        });
    }

    [Fact]
    public void The_wheel_turns_a_crate_dragged_alone_and_zooms_while_a_selection_is_dragged()
    {
        // A 2 SCU crate alone at the bow end of Rack 1's outer layer, the rest of the ship empty (K3.2, D71).
        Sta.Run(() =>
        {
            var (ship, _) = TestPlans.Load();
            Crate quartz = new("Riker", "Quartz", CrateSize.Two, "rack-0", new GridPosition(2, 1, 1), Mission: 1);
            Crate tin = new("Riker", "Tin", CrateSize.One, "rack-0", new GridPosition(0, 1, 1), Mission: 1);
            var (view, commands) = Shown(new LoadPlan(ship.Id, [quartz, tin], []));
            var from = ScreenPoints.InPicture(view, new Point3D(3, 2.5, 5));
            var to = ScreenPoints.InPicture(view, new Point3D(2, -3.5, 5));
            view.HitAt(from)!.Crate.Should().Be(quartz);

            view.Press(MouseButton.Right, from, 1);
            view.MoveTo(to);
            view.Wheel(Mouse.MouseWheelDeltaForOneLine, to);
            view.Release(MouseButton.Right, to);
            commands.Moves.Should().ContainSingle().Which.Turn.Should().BeTrue();

            view.Selection = CrateSelection.Of(view.Scene!, [quartz, tin]);
            var camera = view.Camera;
            view.Press(MouseButton.Right, from, 1);
            view.MoveTo(to);
            view.Wheel(Mouse.MouseWheelDeltaForOneLine, to);
            view.Release(MouseButton.Right, to);
            commands.Moves[^1].Turn.Should().BeFalse();
            view.Camera.Should().NotBe(camera, "the wheel zooms instead");
        });
    }

    [Fact]
    public void A_legend_line_dropped_onto_a_rack_moves_its_crates_there_and_beside_the_racks_nothing()
    {
        Sta.Run(() =>
        {
            var (view, commands) = Shown();
            var line = view.Scene!.Legend.Single(e => e.Crates.Contains(Shubin(view))).Crates;
            var rack3 = ScreenPoints.InPicture(view, Rack3Free);

            view.StartLegendDrag(line, Beside).Should().BeTrue();
            view.MoveTo(rack3);
            view.ShownPreview.TargetRackId.Should().Be("rack-2");
            view.Release(MouseButton.Right, rack3);

            view.StartLegendDrag(line, Beside);
            view.MoveTo(new Point(10, 400));
            view.Release(MouseButton.Right, new Point(10, 400));

            commands.Legend.Should().ContainSingle().Which.Should().BeEquivalentTo(new Gesture.MoveLegend(line, "rack-2"));
        });
    }

    [Fact]
    public void Locking_ends_a_drag_without_a_move_and_clears_its_preview()
    {
        Sta.Run(() =>
        {
            var (view, commands) = Shown();
            var to = ScreenPoints.InPicture(view, Rack3Free);
            view.Press(MouseButton.Right, ScreenPoints.InPicture(view, ShubinBow), 1);
            view.MoveTo(to);
            view.ShownPreview.Should().NotBe(DragPreview.None);

            view.IsNavigationEnabled = false;
            view.Release(MouseButton.Right, to);

            commands.Moves.Should().BeEmpty();
            view.ShownPreview.Should().Be(DragPreview.None);
        });
    }
}
