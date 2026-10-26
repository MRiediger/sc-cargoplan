using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Input;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// K6.2 (D25 addendum): at the elevator the lock is the normal state and the selection is what is needed there, so locking
/// keeps it; a drag running when the lock comes ends without a move.
/// </summary>
public class ShipViewLockTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();

    /// <summary>The ship view of Martin's Hurston plan, two crates of the first line selected.</summary>
    private static ShipViewControl SelectedView()
    {
        var hullA = Catalog.Get("hull_a");
        var saved = TestPlans.HurstonPickups();
        var scene = ShipViewBuilder.Build(saved, hullA, LoadPlanner.Validate(saved.Plan, hullA));
        var crates = scene.Legend[0].Crates;
        var view = new ShipViewControl { Scene = scene };
        view.Selection = crates.Take(2).Aggregate(CrateSelection.Empty, (selection, crate) => selection.Toggle(scene, crate));
        return view;
    }

    [Fact]
    public void Locking_keeps_the_selection()
    {
        OnSta(() =>
        {
            var view = SelectedView();
            var selection = view.Selection;

            view.IsNavigationEnabled = false;

            view.Selection.Should().BeSameAs(selection);
            view.Selection.In(view.Scene!).Should().HaveCount(2);
        });
    }

    [Fact]
    public void Unlocking_keeps_the_selection()
    {
        OnSta(() =>
        {
            var view = SelectedView();
            var selection = view.Selection;

            view.IsNavigationEnabled = false;
            view.IsNavigationEnabled = true;

            view.Selection.Should().BeSameAs(selection);
        });
    }

    [Fact]
    public void Locking_ends_a_drag_without_a_move()
    {
        // Outside a window the view gets no capture, so no LostMouseCapture ends the drag: the case the lock must catch
        // itself. With a capture, releasing it ends the drag the same way (OnLostMouseCapture).
        OnSta(() =>
        {
            var view = SelectedView();
            var moves = 0;
            view.MoveLegendCommand = new CommunityToolkit.Mvvm.Input.RelayCommand<Gesture.MoveLegend>(_ => moves++);
            var press = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonDownEvent };
            view.StartLegendDrag(view.Scene!.Legend[0].Crates, press);
            view.IsDragging.Should().BeTrue();

            view.IsNavigationEnabled = false;

            view.IsDragging.Should().BeFalse();
            moves.Should().Be(0);
        });
    }

    // WPF controls need a single-threaded apartment; xUnit's threads aren't.
    private static void OnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
