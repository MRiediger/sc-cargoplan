using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CommunityToolkit.Mvvm.Input;

namespace CargoPlan.App.Tests;

/// <summary>
/// Clicks on the live view (T8.3): the control's own camera, hit test and gesture rules against its own picture. The
/// click points come from WPF's mapping of the control's camera (<see cref="ScreenPoints.InPicture"/>), not from
/// <see cref="CameraMath"/>, so a second camera in the hit test can't pass. Until T8.3 no test did this (T8.1).
/// </summary>
public class ShipViewHitTests
{
    private static readonly Size Size = new(420, 420);

    // Rack 1's crate 3 (Riker Titanium) seen from above, on top in the default view (SceneHitTestTests).
    private static readonly Point3D Rack1Top = new(2.5, 2.5, 5);

    // A corner of the view, beside the ship in every camera used here.
    private static readonly Point Beside = new(3, 3);

    private static ShipViewControl Shown()
    {
        var (ship, plan) = TestPlans.Load();
        var view = new ShipViewControl { Scene = ShipViewBuilder.Build(plan, ship, LoadPlanner.Validate(plan, ship)) };

        // App.xaml's brush, which OnRender looks up; there is no App in the tests.
        view.Resources["HitTestBackgroundBrush"] = Brushes.Transparent;
        view.Measure(Size);
        view.Arrange(new Rect(Size));
        view.UpdateLayout();
        return view;
    }

    private static Crate Titanium3(ShipViewControl view) => view.Scene!.Plan.Crates.Where(c => c.Commodity == "Titanium").ElementAt(2);

    private static void Click(ShipViewControl view, MouseButton button, Point point, int clicks = 1)
    {
        view.Press(button, point, clicks);
        view.Release(button, point);
    }

    [Fact]
    public void Right_click_on_a_crate_selects_it_and_again_takes_it_out()
    {
        Sta.Run(() =>
        {
            var view = Shown();
            var at = ScreenPoints.InPicture(view, Rack1Top);

            Click(view, MouseButton.Right, at);
            view.Selection.In(view.Scene).Should().BeEquivalentTo([Titanium3(view)]);

            Click(view, MouseButton.Right, at);
            view.Selection.In(view.Scene).Should().BeEmpty();
        });
    }

    [Fact]
    public void Each_click_ends_its_own_drag_even_without_the_mouse_captured()
    {
        // Outside a window the view gets no capture, so no LostMouseCapture ends a drag: the release must (T8.4). Before,
        // the second press was ignored and the second release finished the first drag, toggling the first crate again.
        Sta.Run(() =>
        {
            var view = Shown();
            var top = view.Scene!.Racks.Single(r => r.RackId == "rack-0");
            SceneBox[] boxes = [.. top.Boxes.Where(b => b.Crate is not null && b.Bounds.Max.Z == top.Bounds.Max.Z).Take(2)];
            Point At(SceneBox box) => ScreenPoints.InPicture(view, new Point3D((box.Bounds.Min.X + box.Bounds.Max.X) / 2.0, (box.Bounds.Min.Y + box.Bounds.Max.Y) / 2.0, box.Bounds.Max.Z));

            Click(view, MouseButton.Right, At(boxes[0]));
            Click(view, MouseButton.Right, At(boxes[1]));

            view.IsDragging.Should().BeFalse();
            view.Selection.In(view.Scene).Should().BeEquivalentTo([boxes[0].Crate, boxes[1].Crate]);
        });
    }

    [Fact]
    public void Right_click_beside_the_racks_clears_the_selection()
    {
        Sta.Run(() =>
        {
            var view = Shown();
            Click(view, MouseButton.Right, ScreenPoints.InPicture(view, Rack1Top));

            Click(view, MouseButton.Right, Beside);

            view.Selection.In(view.Scene).Should().BeEmpty();
        });
    }

    [Fact]
    public void Left_click_on_a_crate_ticks_its_whole_stop()
    {
        Sta.Run(() =>
        {
            var view = Shown();
            List<string> ticked = [];
            view.ToggleStopCommand = new RelayCommand<string>(d => ticked.Add(d!));

            Click(view, MouseButton.Left, ScreenPoints.InPicture(view, Rack1Top));

            ticked.Should().Equal("Riker");
        });
    }

    [Fact]
    public void Every_crate_on_top_is_hit_where_it_is_drawn_before_and_after_an_orbit()
    {
        // The upper racks' crates all reach the top (Hull A stacks sideways, D26), and racks of one height don't cover
        // each other's tops from above: the middle of each top face shows that crate, spread over the whole picture.
        Sta.Run(() =>
        {
            var view = Shown();
            var top = view.Scene!.Racks.Max(r => r.Bounds.Max.Z);
            var tops = view.Scene.Racks.Where(r => r.Bounds.Max.Z == top)
                .SelectMany(r => r.Boxes).Where(b => b.Crate is not null && b.Bounds.Max.Z == top).ToList();
            var before = ScreenPoints.InPicture(view, Rack1Top);

            tops.Should().HaveCountGreaterThan(4);
            foreach (var camera in new[] { null, new ShipCamera(130, 60) })
            {
                view.Camera = camera;
                foreach (var box in tops)
                {
                    Point3D middle = new((box.Bounds.Min.X + box.Bounds.Max.X) / 2.0, (box.Bounds.Min.Y + box.Bounds.Max.Y) / 2.0, top);
                    view.HitAt(ScreenPoints.InPicture(view, middle))!.Crate.Should().Be(box.Crate, $"camera {camera?.Yaw.ToString() ?? "default"}");
                }
            }

            (ScreenPoints.InPicture(view, Rack1Top) - before).Length.Should().BeGreaterThan(20, "the orbit moves the crates on screen");
        });
    }

    [Fact]
    public void The_tool_tip_names_the_crate_and_beside_the_racks_there_is_none()
    {
        Sta.Run(() =>
        {
            var view = Shown();

            view.ToolTipAt(ScreenPoints.InPicture(view, Rack1Top)).Should().StartWith("Riker · Rack 1 · port upper · 4 SCU Titanium crate (3 of 4)");
            view.ToolTipAt(Beside).Should().BeNull();
        });
    }

    [Fact]
    public void Outside_the_view_nothing_is_hit()
    {
        // K1.4: with the mouse captured, points over the legend or the header come here too.
        Sta.Run(() =>
        {
            var view = Shown();

            view.HitAt(new Point(Size.Width / 2, Size.Height + 40)).Should().BeNull();
        });
    }

    [Fact]
    public void A_double_click_beside_the_cargo_resets_the_view_and_on_cargo_ticks_twice()
    {
        // D16: on cargo the second click is a click of its own and ticks back; only beside it does it reset.
        Sta.Run(() =>
        {
            var view = Shown();
            List<string> ticked = [];
            view.ToggleStopCommand = new RelayCommand<string>(d => ticked.Add(d!));
            ShipCamera turned = new(130, 60);
            view.Camera = turned;
            var at = ScreenPoints.InPicture(view, Rack1Top);

            Click(view, MouseButton.Left, at);
            Click(view, MouseButton.Left, at, clicks: 2);
            view.Camera.Should().Be(turned);
            ticked.Should().Equal("Riker", "Riker");

            Click(view, MouseButton.Left, Beside);
            Click(view, MouseButton.Left, Beside, clicks: 2);
            view.Camera.Should().BeNull();
        });
    }

    [Fact]
    public void Locked_a_click_does_nothing()
    {
        Sta.Run(() =>
        {
            var view = Shown();
            List<string> ticked = [];
            view.ToggleStopCommand = new RelayCommand<string>(d => ticked.Add(d!));
            view.IsNavigationEnabled = false;
            var at = ScreenPoints.InPicture(view, Rack1Top);

            Click(view, MouseButton.Left, at);
            Click(view, MouseButton.Right, at);

            ticked.Should().BeEmpty();
            view.Selection.In(view.Scene).Should().BeEmpty();
        });
    }
}
