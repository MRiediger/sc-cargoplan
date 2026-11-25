using System.Diagnostics;
using System.Globalization;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// How long the station bar takes (T13.6 6b, D126): the pin's Everus ring for a Hull B at Senior, its five outbound
/// contracts aboard at their worst case, the bar at each stop with its ways back simulated by the load planner. The App
/// works it out on the UI thread after every change of the plan; by hand only, in Release: a measurement, not a check.
/// </summary>
[Trait("Category", "Timing")]
public class RouteProgressTimingTests(ITestOutputHelper output)
{
    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    [Fact]
    public void The_bar_at_each_stop_of_the_Everus_ring_with_its_time()
    {
        var data = Data.Value;
        var ship = FrozenShips.Load("hull_b");
        var loop = Loops.For(data.Templates, data.Positions, ObservedEdges.Empty, ship, HaulRank.Senior, null, "Everus Harbor")
            .Single(l => l.Kind == LoopKind.LocalRing && l.Start == "Everus Harbor");
        var route = Route.From(loop, "Everus Harbor ring", ObservedEdges.Empty, DateTime.UtcNow);

        // Each outbound role as the recipe checks it: every leg at its upper band to the role's places in turn.
        var plan = new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship));
        foreach (var p in route.Outbound)
        {
            MissionStop[] stops =
            [
                .. p.Legs.Select((l, i) => (Leg: l, To: p.Destinations[i % p.Destinations.Count])).GroupBy(x => x.To)
                    .Select((g, i) => new MissionStop(g.Key, i + 1, [.. g.Select(x => new MissionItem(x.Leg.Commodity, x.Leg.MaxScu, x.Leg.Container))])),
            ];
            plan = LoadPlanner.PlaceInto(plan, ship, stops, new ContractSource(new ScanIdentity(p.Id, $"KEY-{p.Id}", false), Reward: p.Reward));
            route = route with { Placeholders = [.. route.Placeholders.Select(x => x.Id == p.Id ? x with { FilledBy = $"KEY-{p.Id}" } : x)] };
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"aboard {plan.Plan.ScuOnBoard()} SCU, {plan.Plan.Crates.Count} crates, {plan.Missions.Count(m => m.Unplaced)} unplaced"));
        for (var at = 0; at <= route.Stations.Count + 1; at++)
        {
            var here = route with { At = at };
            var first = Stopwatch.StartNew();
            var progress = RouteProgress.Of(here, plan, ship, data.Positions);
            var firstMs = first.Elapsed.TotalMilliseconds;
            List<double> runs = [];
            for (var i = 0; i < 5; i++)
            {
                var watch = Stopwatch.StartNew();
                RouteProgress.Of(here, plan, ship, data.Positions);
                runs.Add(watch.Elapsed.TotalMilliseconds);
            }

            var stop = progress.Stops[at];
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{at} {PlaceNames.Short(stop.Name),-14} first {firstMs,7:0.0} ms, median {runs.Order().ElementAt(2),7:0.0} ms · unload {stop.Lines} · held {stop.Held} · ways back {string.Join(", ", stop.Returns.Select(r => $"{r.Placeholder.MaxScu} SCU {r.Fit}"))}"));
        }
    }
}
