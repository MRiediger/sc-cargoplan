using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>Room on top for the numbers above a long ship's racks (D61), kept by the perspective fit (T8.2).</summary>
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
        var orientation = IsoProjection.DefaultFor(scene.Bounds);
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

        CameraFit.Distance(IsoProjection.Default, target, points, View, Margin, top: 0)
            .Should().Be(CameraFit.Distance(IsoProjection.Default, target, points, View, Margin));
    }
}
