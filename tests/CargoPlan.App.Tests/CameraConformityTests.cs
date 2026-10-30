using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// <see cref="CameraMath.Project"/> against WPF's own mapping of the same camera (D83): the hit test, the overlay's
/// anchors and the click points of the tests use CameraMath alone, so they match the picture only if this holds.
/// Source: https://learn.microsoft.com/dotnet/api/system.windows.media.media3d.visual3d.transformtoancestor
/// </summary>
public class CameraConformityTests
{
    private const double Tolerance = 0.5;

    public static TheoryData<string, CrateSize> Ships => new()
    {
        { "utv", CrateSize.One },
        { "hull_a", CrateSize.Sixteen },
        { "hull_c", CrateSize.ThirtyTwo },
    };

    [Theory]
    [MemberData(nameof(Ships))]
    public void Projects_rack_corners_and_crate_centres_where_WPF_draws_them(string shipId, CrateSize size)
    {
        var scene = FullScene(shipId, size);
        var bounds = scene.Bounds;
        var target = ScreenPoints.Centre(bounds);
        var span = Math.Max(bounds.Max.X - bounds.Min.X, Math.Max(bounds.Max.Y - bounds.Min.Y, bounds.Max.Z - bounds.Min.Z));
        Point3D[] samples = [.. ScreenPoints.Samples(scene)];

        (CameraOrientation Orientation, double Distance)[] cameras =
        [
            (CameraOrientation.DefaultFor(bounds), 2 * span),
            (new CameraOrientation(0, 0), 2 * span),
            (new CameraOrientation(140, 80), 1.5 * span),
            (new CameraOrientation(250, -80), 1.5 * span),
            (new CameraOrientation(45, 35.26), span / 3),
            (new CameraOrientation(300, 10), 6 * span),
        ];

        Sta.Run(() =>
        {
            foreach (var view in (Size[])[new(420, 420), new(800, 600), new(600, 300)])
            {
                foreach (var (orientation, distance) in cameras)
                {
                    var camera = new CameraMath(orientation, distance, target, view);
                    var wpf = new PerspectiveCamera();
                    camera.ApplyTo(wpf);
                    var transform = ScreenPoints.Wpf(wpf, view);

                    // Points behind or at the near plane don't project; WPF clips them, CameraMath has no picture of them.
                    foreach (var point in samples.Where(p => Vector3D.DotProduct(p - camera.Position, camera.LookDirection) > 2 * camera.Near))
                    {
                        transform.TryTransform(point, out var expected).Should().BeTrue();
                        var actual = camera.Project(point);
                        (actual - expected).Length.Should().BeLessThan(
                            Tolerance, $"{shipId} {view} yaw {orientation.Yaw} pitch {orientation.Pitch} d {distance}: {point} at {actual}, WPF {expected}");
                    }
                }
            }
        });
    }

    private static ShipScene FullScene(string shipId, CrateSize size)
    {
        var ship = ShipLoader.Load(shipId);
        Placement[] placements = [.. ship.Racks.Select(r => new Placement("Sample", r.Id, new CargoItem("Sample", r.CapacityScu, size)))];
        return ShipViewBuilder.Build(new LoadPlan(ship.Id, CrateLayout.LayOut(ship, placements, FillOrder.BowFirst), []), ship, []);
    }
}
