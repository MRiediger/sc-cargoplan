using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media.Media3D;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// What a turned rack costs the hit test (D91): rays aimed at the crates of the half-full Hull C, straight and with every
/// rack turned, in one process. The frame window's hit sweep (T8.3, M2) hits 23 of its 600 points, so it mostly times misses;
/// this one times hits. The ship is made here, no turned ship lies in the repo. Run by hand, see CLAUDE.md, Befehle.
/// </summary>
[Trait("Category", "Timing")]
public class TurnedRackTimingTests(ITestOutputHelper output)
{
    private const int Rounds = 50;
    private const double GateMilliseconds = 1;

    private static readonly RackRotation Rotation = new(30, 20, 10);

    [Fact]
    public void Hit_test_on_straight_and_turned_racks()
    {
        var straight = ShipLoader.Load("hull_c");
        Ship turned = new(straight.Id, straight.Name, straight.TotalScu, [.. straight.Racks.Select(r => r with { Rotation = Rotation })]);

        // Twice over: the first pass warms up what the second measures, in whichever order.
        Measure(straight);
        Measure(turned);
        var (straightLine, straightP95) = Measure(straight);
        var (turnedLine, turnedP95) = Measure(turned);

        output.WriteLine("| Racks | Rays | Hits | p50 ms | p95 ms | max ms |");
        output.WriteLine("|---|---|---|---|---|---|");
        output.WriteLine($"| straight | {straightLine}");
        output.WriteLine($"| turned | {turnedLine}");
        straightP95.Should().BeLessThanOrEqualTo(GateMilliseconds);
        turnedP95.Should().BeLessThanOrEqualTo(GateMilliseconds);
    }

    private static (string Line, double P95) Measure(Ship ship)
    {
        Placement[] placements = [.. ship.Racks.Select(r => new Placement("Measure", r.Id, new CargoItem("Measure", r.CapacityScu / 2 / (int)CrateSize.ThirtyTwo * (int)CrateSize.ThirtyTwo, CrateSize.ThirtyTwo)))];
        var plan = new LoadPlan(ship.Id, CrateLayout.LayOut(ship, placements, FillOrder.BowFirst), []);
        var scene = ShipViewBuilder.Build(plan, ship, []);
        CameraMath camera = new(CameraOrientation.Default, 400, ScreenPoints.Centre(scene.Bounds), new Size(800, 600));

        Ray[] rays =
        [
            .. from rack in scene.Racks
               from box in rack.Boxes.Where(b => b.Crate is not null)
               select camera.Unproject(camera.Project(rack.Transform.ToShip(ScreenPoints.Centre(box.Bounds)))),
        ];

        List<double> times = new(Rounds * rays.Length);
        var hits = 0;
        for (var round = 0; round < Rounds; round++)
        {
            foreach (var ray in rays)
            {
                var watch = Stopwatch.StartNew();
                var hit = SceneHitTest.HitAt(scene, ray);
                watch.Stop();
                times.Add(watch.Elapsed.TotalMilliseconds);
                hits += hit is null ? 0 : 1;
            }
        }

        times.Sort();
        var p95 = times[(int)(times.Count * 0.95)];
        return (string.Create(CultureInfo.InvariantCulture, $"{times.Count} | {hits} | {times[times.Count / 2]:0.0000} | {p95:0.0000} | {times[^1]:0.000} |"), p95);
    }
}
