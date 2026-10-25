using System.Diagnostics;
using System.Globalization;
using System.Text;
using CargoPlan.App.ShipView;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using Xunit.Abstractions;

namespace CargoPlan.App.Tests;

/// <summary>
/// What an undo costs (K2.4): the memory a snapshot holds, a whole independent plan and what a move adds to the one
/// before, and the time to show a plan again (what setting <c>Saved</c> runs: validation and scene). No time is asserted;
/// the table goes to the test output:
/// <c>dotnet test tests/CargoPlan.App.Tests -c Release -p:RunSettingsFilePath= --filter Category=Timing --logger "console;verbosity=detailed"</c>.
/// </summary>
[Trait("Category", "Timing")]
public class UndoTimingTests(ITestOutputHelper output)
{
    [Fact]
    public void Snapshot_memory_and_show_time()
    {
        output.WriteLine("| Case | crates | plan alone KB | a move adds KB | show ms (median) |");
        foreach (var (name, ship, saved) in Cases())
        {
            var whole = Retained(() => Copy(saved, ship));
            var crate = saved.Plan.Crates[0];
            var target = ship.Racks[^1];
            var move = new Gesture.MoveCrates(crate, [crate], target.Id, new GridPosition(0, 0, 0));
            var added = Retained(() => CrateDrop.Outcome(saved.Plan, ship, move).Plan is { } moved
                ? new SavedPlan(saved.Missions, moved, saved.CrateCap, saved.Watermark)
                : saved);
            var show = Median(() => ShipViewBuilder.Build(saved, ship, LoadPlanner.Validate(saved.Plan, ship)));
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {name} | {saved.Plan.Crates.Count} | {whole / 1024.0:F1} | {added / 1024.0:F1} | {show:F2} |"));
        }
    }

    /// <summary>
    /// Undo and redo with everything they do (K2.4): after a move a sync brought a contract and a stop was ticked, so every
    /// undo places the contract again (Resync) and carries the tick (CarryTicks), and sets the plan (validation, scene).
    /// Martin's bound: under 16 ms, else a sync clears the history.
    /// </summary>
    [Fact]
    public void Undo_and_redo_time()
    {
        output.WriteLine("| Case | undo ms (median) | redo ms (median) |");
        foreach (var (name, ship, saved) in Cases())
        {
            // Built anew every round: only the first undo after the sync places the contract again.
            List<double> undo = [], redo = [];
            for (var i = 0; i < 6; i++)
            {
                var overlay = new ViewModels.OverlayViewModel(ship);
                overlay.ShowPlan(saved);
                Move(overlay, ship);
                var stop = new MissionStop("Area18", 1, [new MissionItem("Stims", 2, CrateSize.Two)]);
                overlay.ApplyContractTracker(new(
                    [new([overlay.Watermark + 100], null, [stop], false, false, null, null, null)], null, null));
                overlay.ToggleDeliveredCommand.Execute(overlay.Plan!.Route[0]);
                var missions = overlay.Missions.Count;

                var watch = Stopwatch.StartNew();
                overlay.UndoCommand.Execute(null);
                undo.Add(watch.Elapsed.TotalMilliseconds);
                overlay.Missions.Should().HaveCount(missions, "the undo places the synced contract again");
                watch.Restart();
                overlay.RedoCommand.Execute(null);
                redo.Add(watch.Elapsed.TotalMilliseconds);
            }

            // The first round warms up.
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"| {name} | {undo.Skip(1).Order().ElementAt(2):F2} | {redo.Skip(1).Order().ElementAt(2):F2} |"));
        }
    }

    private static void Move(ViewModels.OverlayViewModel overlay, Ship ship)
    {
        var plan = overlay.Plan!;
        foreach (var crate in plan.Crates.Where(c => !c.Delivered))
        {
            foreach (var rack in ship.Racks.Where(r => r.Id != crate.RackId))
            {
                var move = new Gesture.MoveCrates(crate, [crate], rack.Id, new GridPosition(0, 0, 0));
                if (CrateDrop.Outcome(plan, ship, move).Plan is { } moved && !moved.Crates.SequenceEqual(plan.Crates))
                {
                    overlay.MoveCratesCommand.Execute(move);
                    return;
                }
            }
        }

        throw new InvalidOperationException("No move changes the plan.");
    }

    private static (string Name, Ship Ship, SavedPlan Saved)[] Cases()
    {
        var hullA = ShipLoader.Load("hull_a");
        var c2 = ShipLoader.Load("c2_hercules");
        var stops = EightStops();
        return
        [
            ("Hull A eight stops", hullA, SavedPlan.FromStops(stops, LoadPlanner.Plan(hullA, stops.ToStops()), CrateCap.Of(hullA))),
            ("Hurston 25 Oct (real plan)", hullA, TestPlans.HurstonPickups()),
            ("C2 eight stops", c2, SavedPlan.FromStops(stops, LoadPlanner.Plan(c2, stops.ToStops(CrateCap.Of(c2))), CrateCap.Of(c2))),
        ];
    }

    private static IReadOnlyList<MissionStop> EightStops()
    {
        const string name = "missions/hull_a_eight_stops.json";
        using var stream = typeof(UndoTimingTests).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Test resource '{name}' not found.");
        return MissionFileReader.Read(stream, name).Stops;
    }

    /// <summary>An independent copy, nothing shared with <paramref name="saved"/>: written and read back.</summary>
    private static SavedPlan Copy(SavedPlan saved, Ship ship)
    {
        using var stream = new MemoryStream();
        PlanFile.Write(stream, saved);
        stream.Position = 0;
        return PlanFile.Read(stream, "copy", ship);
    }

    /// <summary>
    /// Bytes still held once <paramref name="make"/>'s result is kept and everything else is collected. Run once before,
    /// so first-call initialisation (static caches, JIT) isn't counted as the snapshot's.
    /// </summary>
    private static long Retained(Func<object> make)
    {
        GC.KeepAlive(make());
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var kept = make();
        var after = GC.GetTotalMemory(forceFullCollection: true);
        GC.KeepAlive(kept);
        return after - before;
    }

    private static double Median(Action action)
    {
        action();
        double[] runs = new double[5];
        for (var i = 0; i < runs.Length; i++)
        {
            var watch = Stopwatch.StartNew();
            action();
            runs[i] = watch.Elapsed.TotalMilliseconds;
        }

        return runs.Order().ElementAt(2);
    }
}
