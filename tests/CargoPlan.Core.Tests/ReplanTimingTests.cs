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
        var hullA = ShipLoader.Load("hull_a");
        (string Name, Ship Ship, IReadOnlyList<MissionStop> Stops)[] cases =
        [
            ("example_missions", hullA, MissionFixtures.LoadMissions("example_missions")),
            ("split_and_shared", hullA, MissionFixtures.LoadMissions("split_and_shared")),
            ("split_required", hullA, MissionFixtures.LoadMissions("split_required")),
            ("crate_mix", hullA, MissionFixtures.LoadMissions("crate_mix")),
            ("Hull A eight stops", hullA, MissionFixtures.LoadMissions("hull_a_eight_stops")),
            ("Hull A Hurston 24 Oct", hullA, MissionFixtures.LoadMissions("hull_a_hurston_2026-10-24")),
            ("Freelancer eight stops", ShipLoader.Load("freelancer"), MissionFixtures.LoadMissions("hull_a_eight_stops")),
            ("Stress load (not a ship)", StackingSearchLimitTests.Huge,
                [.. StackingSearchLimitTests.Stress.Select(s => new MissionStop(s.Destination, s.Order, [.. s.Items.Select(i => new MissionItem(i.Commodity, i.Scu, i.CrateSize))]))]),
        ];

        output.WriteLine("| Case | first ms | median ms |");
        foreach (var (name, ship, stops) in cases)
        {
            var saved = SavedPlan.FromStops(stops, LoadPlanner.Plan(ship, stops.ToStops()), CrateCap.Of(ship));
            var watch = Stopwatch.StartNew();
            var replanned = LoadPlanner.Replan(saved, ship);
            var first = watch.Elapsed.TotalMilliseconds;
            var median = Median(() => LoadPlanner.Replan(saved, ship));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| {name} | {first:F1} | {median:F1} |"));

            replanned.Missions.Should().OnlyContain(m => !m.Unplaced);
        }
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
