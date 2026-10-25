using System.Diagnostics;
using System.Globalization;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// What one drop costs per stage (B3, D65): a single crate, a selection of four and a legend drag, each chosen so that a
/// given stage takes it. Median of three runs after a warm-up, the full resolution and the preview that stops before a
/// new layout (<see cref="MoveStage.Pending"/>). No time is asserted (the machines differ, T7.1d); the table goes to the
/// test output: <c>dotnet test tests/CargoPlan.Core.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class MoveResolutionTimingTests(ITestOutputHelper output)
{
    private static readonly MoveStage[] Late = [MoveStage.Relayout, MoveStage.Displace, MoveStage.Elsewhere, MoveStage.Refused];

    /// <summary>The stress rack (12×12×7) plus a dock of 8×2×4 holding Dest5's 32, so cargo can move between racks.</summary>
    private static readonly Ship HugeWithDock = new("huge_dock", "Stress test ship with a dock", 1_072,
    [
        StackingSearchLimitTests.Huge.Racks[0],
        new Rack("rack-1", 1, 64, new Dimensions(8, 2, 4), null, new ShipPoint(0, 13, 0)),
    ]);

    [Fact]
    public void Eight_stops_on_the_Freelancer()
    {
        var ship = ShipLoader.Load("freelancer");
        Measure("Freelancer 8 stops", ship, LoadPlanner.Plan(ship, MissionFixtures.Load("hull_a_eight_stops")), Cells);
    }

    [Fact]
    public void Hurston_on_the_Hull_A()
    {
        var ship = ShipLoader.Load("hull_a");
        Measure("Hull A Hurston", ship, LoadPlanner.Plan(ship, MissionFixtures.Load("hull_a_hurston_2026-10-24")), Cells);
    }

    [Fact]
    public void Stress_load_with_a_dock()
    {
        Stop[] stops = [.. StackingSearchLimitTests.Stress, new Stop("Dest5", 5, [new CargoItem("Titanium", 32, CrateSize.ThirtyTwo)])];
        var plan = LoadPlanner.Plan(HugeWithDock, stops);

        // Every fourth cell of the 1,008: a search over all of them would take minutes.
        Measure("Stress + dock", HugeWithDock, plan, rack => rack.Grid.Volume > 100 ? Cells(rack).Where(c => c.Length % 4 == 0 && c.Width % 4 == 0 && c.Stack % 3 == 0) : Cells(rack));
    }

    private void Measure(string name, Ship ship, LoadPlan plan, Func<Rack, IEnumerable<GridPosition>> targets)
    {
        Rack[] racks = [.. ship.Racks.OrderBy(r => r.Index)];
        List<(string Gesture, MoveStage Stage, double Full, double Preview)> rows = [];

        // Single crates: one of each size and destination, onto every target cell.
        Crate[] singles = [.. plan.Crates.GroupBy(c => (c.Destination, c.CrateSize)).Select(g => g.First())];
        var single = Find(singles.SelectMany(crate => racks.SelectMany(rack => targets(rack).Select(cell =>
            ($"1 × {(int)crate.CrateSize} SCU {crate.Destination} → rack {rack.Number} {cell}",
             (Func<MoveStage, MoveOutcome>)(upTo => LoadPlanner.ResolveMove(plan, ship, [new CrateMove(crate, rack.Id, cell)], upTo)))))));
        rows.AddRange(single.Select(f => Timed(f.Value.Name, f.Key, f.Value.Run)));

        // A selection: the first four crates of the first rack holding four.
        if (racks.Select(r => plan.CratesForRack(r.Id).Where(c => !c.Delivered).Take(4).ToArray()).FirstOrDefault(s => s.Length == 4) is { } selection)
        {
            var selected = Find(racks.SelectMany(rack => targets(rack).Select(cell =>
                ($"4 crates of rack {selection[0].RackId} → rack {rack.Number} {cell}",
                 (Func<MoveStage, MoveOutcome>)(upTo => LoadPlanner.ResolveMove(plan, ship, selection, selection[0], rack.Id, cell, upTo))))));
            rows.AddRange(selected.Select(f => Timed(f.Value.Name, f.Key, f.Value.Run)));
        }

        // The legend line of the largest destination onto every rack.
        var largest = plan.Crates.Where(c => !c.Delivered).GroupBy(c => c.Destination).MaxBy(g => g.Sum(c => (int)c.CrateSize))!.Key;
        foreach (var rack in racks)
        {
            Crate[] line = [.. plan.Crates.Where(c => c.Destination == largest && !c.Delivered)];
            rows.Add(Timed($"legend {largest} → rack {rack.Number}", null, upTo => LoadPlanner.ResolveMoveToRack(plan, ship, line, rack.Id, upTo)));
        }

        output.WriteLine($"| {name} | gesture | stage | full ms | preview ms |");
        foreach (var (gesture, stage, full, preview) in rows)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"| | {gesture} | {stage} | {full:F2} | {preview:F2} |"));
        }

        single.Keys.Should().Contain([MoveStage.Gravity, MoveStage.FreeSpot]);
    }

    /// <summary>
    /// The first gesture per stage, in the given order. The preview (up to the free place) is cheap; the full resolution
    /// runs only where the preview says Pending and a late stage is still missing.
    /// </summary>
    private static Dictionary<MoveStage, (string Name, Func<MoveStage, MoveOutcome> Run)> Find(IEnumerable<(string Name, Func<MoveStage, MoveOutcome> Run)> gestures)
    {
        Dictionary<MoveStage, (string, Func<MoveStage, MoveOutcome>)> found = [];
        foreach (var gesture in gestures)
        {
            var stage = gesture.Run(MoveStage.FreeSpot).Stage;
            if (stage == MoveStage.Pending && Late.Any(s => !found.ContainsKey(s)))
            {
                stage = gesture.Run(MoveStage.Refused).Stage;
            }

            found.TryAdd(stage, gesture);
            if (found.Count == Late.Length + 3)
            {
                break;
            }
        }

        found.Remove(MoveStage.Pending);
        return found;
    }

    /// <summary>Median of three runs after a warm-up, the full resolution and the preview up to the free place.</summary>
    private static (string, MoveStage, double, double) Timed(string name, MoveStage? stage, Func<MoveStage, MoveOutcome> run)
    {
        var resolved = run(MoveStage.Refused).Stage;
        return (name, stage ?? resolved, Median(() => run(MoveStage.Refused)), Median(() => run(MoveStage.FreeSpot)));
    }

    private static double Median(Action action)
    {
        action();
        double[] runs = new double[3];
        for (var i = 0; i < runs.Length; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            runs[i] = watch.Elapsed.TotalMilliseconds;
        }

        return runs.Order().ElementAt(1);
    }

    private static IEnumerable<GridPosition> Cells(Rack rack) =>
        from l in Enumerable.Range(0, rack.Grid.Length)
        from w in Enumerable.Range(0, rack.Grid.Width)
        from s in Enumerable.Range(0, rack.Grid.Stack)
        select new GridPosition(l, w, s);
}
