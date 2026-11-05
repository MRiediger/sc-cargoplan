using System.Diagnostics;
using System.Globalization;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// What a plan costs per cell order (K9.1, D88): the first run, the median of three after it, and what the searches count,
/// which is the same on every machine. No time is asserted (T7.1d); the table goes to the test output:
/// <c>dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter FullyQualifiedName~FillOrderTimingTests --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class FillOrderTimingTests(ITestOutputHelper output)
{
    [Fact]
    public void Plan_of_each_case_in_each_order()
    {
        output.WriteLine("| Case | order | first ms | median ms | nodes | layout steps | gave up |");
        foreach (var (name, ship, missions, route) in Cases())
        {
            foreach (var order in new[] { FillOrder.BowFirst, FillOrder.SternFirst })
            {
                var watch = Stopwatch.StartNew();
                LoadPlanner.Plan(ship, missions, route, order);
                var first = watch.Elapsed.TotalMilliseconds;
                var median = Median(() => LoadPlanner.Plan(ship, missions, route, order));

                var (nodes, steps, gaveUp) = Counts(ship, missions, route, order);
                output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"| {name} | {order} | {first:F1} | {median:F1} | {nodes} | {steps} | {gaveUp} |"));
            }
        }
    }

    private static (long Nodes, long Steps, int GaveUp) Counts(Ship ship, IReadOnlyList<Mission> missions, IReadOnlyList<string> route, FillOrder order)
    {
        var cap = CrateCap.Of(ship);
        var items = missions.SelectMany(m => m.Stops).GroupBy(s => s.Destination).ToDictionary(g => g.Key, g => g.SelectMany(s => s.Items).ToArray());
        Stop[] stops = [.. route.Select((destination, index) => new MissionStop(destination, index + 1, items[destination]).ToStop(cap))];
        var result = RackAssignment.Assign([.. ship.Racks.OrderBy(r => r.Index)], stops, order);
        long steps = 0;
        var gaveUp = 0;
        foreach (var load in result?.Racks ?? [])
        {
            var laying = CrateLayout.Lay(load.Rack, [], [.. load.Crates.Select(c => (c.Piece.Size, stops[c.Piece.Stop].Destination))], order)!;
            steps += laying.Steps;
            gaveUp += laying.GaveUp ? 1 : 0;
        }

        return (result?.Nodes ?? -1, steps, gaveUp);
    }

    /// <summary>Martin's RAFT plan (66 SCU, five destinations) and eight destinations of 32s on the Hull C, about three quarters full.</summary>
    private static (string Name, Ship Ship, IReadOnlyList<Mission> Missions, IReadOnlyList<string> Route)[] Cases()
    {
        var raft = ShipLoader.Load("raft");
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");
        var saved = PlanFile.Read(stream, "raft_hand_2026-11-05.json", raft);

        string[] destinations = [.. Enumerable.Range(1, 8).Select(i => string.Create(CultureInfo.InvariantCulture, $"Stop {i}"))];
        Mission[] hullC = [.. destinations.Select((destination, index) =>
            new Mission(index + 1, [new MissionStop(destination, 1, [new MissionItem("Titanium", 448, CrateSize.ThirtyTwo)])]))];

        return
        [
            ("RAFT fixture", raft, saved.Missions, [.. saved.Stops.Select(s => s.Destination)]),
            ("Hull C eight stops", ShipLoader.Load("hull_c"), hullC, destinations),
        ];
    }

    private static double Median(Action action)
    {
        double[] runs = new double[3];
        for (var i = 0; i < runs.Length; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            runs[i] = watch.Elapsed.TotalMilliseconds;
        }

        return runs.Order().ElementAt(1);
    }
}
