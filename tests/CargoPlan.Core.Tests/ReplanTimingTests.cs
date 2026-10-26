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
/// What a re-plan costs on the UI thread (B4, D66): <see cref="LoadPlanner.Replan"/> of every fixture on its ship, the
/// first run (after the plan it starts from) and the median of three after it. No time is asserted (the machines differ,
/// T7.1d); the table goes to the test output: <c>dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class ReplanTimingTests(ITestOutputHelper output)
{
    [Fact]
    public void Every_fixture_on_its_ship()
    {
        output.WriteLine("| Case | first ms | median ms |");
        List<string> unplaced = [];
        foreach (var (name, ship, stops) in Cases())
        {
            SavedPlan saved;
            try
            {
                saved = SavedPlan.FromStops(stops, LoadPlanner.Plan(ship, stops.ToStops()), CrateCap.Of(ship));
            }
            catch (PlanException ex)
            {
                output.WriteLine($"| {name} | refused: {ex.Message} | |");
                unplaced.Add(name);
                continue;
            }

            var watch = Stopwatch.StartNew();
            var replanned = LoadPlanner.Replan(saved, ship);
            var first = watch.Elapsed.TotalMilliseconds;
            var median = Median(() => LoadPlanner.Replan(saved, ship));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| {name} | {first:F1} | {median:F1} |"));

            if (replanned.Missions.Any(m => m.Unplaced))
            {
                unplaced.Add(name);
            }
        }

        unplaced.Should().BeEmpty();
    }

    /// <summary>
    /// What a re-plan costs with loaded cargo (K6.4, D79): every fixture with its first stop in the legend loaded, laid in
    /// around it (<c>PlanKeeping</c>). Fewer crates to lay, but no exact assignment: the table says how much waits, whether
    /// the old layout was kept (E2) and how many racks' stacking searches gave up (D58), which must stay 0.
    /// </summary>
    [Fact]
    public void Every_fixture_with_its_first_stop_loaded()
    {
        output.WriteLine("| Case | first ms | median ms | waiting SCU | kept old layout | gave up |");
        var gaveUp = 0;
        foreach (var (name, ship, stops) in Cases())
        {
            SavedPlan plain;
            try
            {
                plain = SavedPlan.FromStops(stops, LoadPlanner.Plan(ship, stops.ToStops()), CrateCap.Of(ship));
            }
            catch (PlanException)
            {
                continue;
            }

            var first = plain.Plan.Route[0];
            var mission = plain.Plan.Crates.First(c => c.Destination == first).Mission;
            var saved = new SavedPlan(plain.Missions, LoadPlanner.SetLoaded(plain.Plan, ship, first, mission, true), plain.CrateCap, plain.Watermark);

            var watch = Stopwatch.StartNew();
            var replanned = LoadPlanner.Replan(saved, ship);
            var ms = watch.Elapsed.TotalMilliseconds;
            var median = Median(() => LoadPlanner.Replan(saved, ship));
            var (around, _) = LoadPlanner.PlanKeeping(saved, ship, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], saved.Plan.FillOrder);
            var racks = around.Plan.Warnings.Count(w => w.Message.Contains("too many crates to rearrange", StringComparison.Ordinal));
            gaveUp += racks;
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {name} | {ms:F1} | {median:F1} | {around.Missions.Where(m => m.Unplaced).Sum(m => m.TotalScu)} | {ReferenceEquals(replanned, saved)} | {racks} |"));
        }

        gaveUp.Should().Be(0);
    }

    /// <summary>
    /// What the searches count, machine-independent (K3.2, D71): the nodes of the rack assignment (D64) and, per loaded rack,
    /// the steps of its final layout search and whether it gave up (D58). Measured before and after crates turn.
    /// </summary>
    [Fact]
    public void Search_counts_of_every_fixture()
    {
        output.WriteLine("| Case | nodes | layout steps | gave up |");
        foreach (var (name, ship, stops) in Cases())
        {
            Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
            Stop[] input = [.. stops.ToStops(CrateCap.Of(ship)).OrderBy(s => s.Order)];
            var result = RackAssignment.Assign(racks, input, FillOrder.BowFirst);
            long steps = 0;
            var gaveUp = 0;
            foreach (var load in result?.Racks ?? [])
            {
                var laying = CrateLayout.Lay(load.Rack, [], [.. load.Crates.Select(c => (c.Piece.Size, input[c.Piece.Stop].Destination))], FillOrder.BowFirst)!;
                steps += laying.Steps;
                gaveUp += laying.GaveUp ? 1 : 0;
            }

            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {name} | {(result is null ? "no assignment" : result.Nodes.ToString(CultureInfo.InvariantCulture))} | {steps} | {gaveUp} |"));
        }
    }

    /// <summary>
    /// What a ship change costs (D59, K2.2): the eight stops from the Hull A to the Freelancer and back, without ticks and
    /// with the first two stops of the legend ticked. Measured before and after ticks survive the change.
    /// </summary>
    [Fact]
    public void Ship_change_with_and_without_ticks()
    {
        var hullA = ShipLoader.Load("hull_a");
        var freelancer = ShipLoader.Load("freelancer");
        var stops = MissionFixtures.LoadMissions("hull_a_eight_stops");
        var plain = SavedPlan.FromStops(stops, LoadPlanner.Plan(hullA, stops.ToStops()), CrateCap.Of(hullA));
        var ticked = plain.Plan.Route.Take(2).Aggregate(plain, (saved, destination) =>
            new SavedPlan(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, hullA, destination, true), saved.CrateCap, saved.Watermark));

        output.WriteLine("| Case | first ms | median ms |");
        foreach (var (name, saved) in new[] { ("no ticks", plain), ("two stops ticked", ticked) })
        {
            var watch = Stopwatch.StartNew();
            var there = LoadPlanner.Replan(saved, freelancer);
            var first = watch.Elapsed.TotalMilliseconds;
            var median = Median(() => LoadPlanner.Replan(saved, freelancer));
            var back = Median(() => LoadPlanner.Replan(there, hullA));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| Hull A → Freelancer, {name} | {first:F1} | {median:F1} |"));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| Freelancer → Hull A, {name} | | {back:F1} |"));
        }
    }

    private static (string Name, Ship Ship, IReadOnlyList<MissionStop> Stops)[] Cases()
    {
        var hullA = ShipLoader.Load("hull_a");
        return
        [
            ("example_missions", hullA, MissionFixtures.LoadMissions("example_missions")),
            ("split_and_shared", hullA, MissionFixtures.LoadMissions("split_and_shared")),
            ("split_required", hullA, MissionFixtures.LoadMissions("split_required")),
            ("crate_mix", hullA, MissionFixtures.LoadMissions("crate_mix")),
            ("Hull A eight stops", hullA, MissionFixtures.LoadMissions("hull_a_eight_stops")),
            ("Hull A Hurston 24 Oct", hullA, MissionFixtures.LoadMissions("hull_a_hurston_2026-10-24")),
            ("Freelancer eight stops", ShipLoader.Load("freelancer"), MissionFixtures.LoadMissions("hull_a_eight_stops")),
            ("Perseus eight stops", ShipLoader.Load("perseus"), MissionFixtures.LoadMissions("hull_a_eight_stops")),
            ("Stress load (not a ship)", StackingSearchLimitTests.Huge,
                [.. StackingSearchLimitTests.Stress.Select(s => new MissionStop(s.Destination, s.Order, [.. s.Items.Select(i => new MissionItem(i.Commodity, i.Scu, i.CrateSize))]))]),
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
