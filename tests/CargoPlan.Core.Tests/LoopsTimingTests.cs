using System.Diagnostics;
using System.Globalization;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// How long the loop ranking takes on the pinned release (T13.4, budget 200 ms for all hubs): every Stanton hub's ring
/// and direct runs, the LEO ring, every Pyro ring, as the app computes them off the UI thread. By hand only, in Release:
/// a measurement, not a check of the mechanics (those are <see cref="LoopsTests"/>).
/// </summary>
[Trait("Category", "Timing")]
public class LoopsTimingTests(ITestOutputHelper output)
{
    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    [Theory]
    [InlineData("hull_b", HaulRank.Senior)]
    [InlineData("hull_b", HaulRank.Master)]
    [InlineData("hull_a", HaulRank.Master)]
    [InlineData("hull_c", HaulRank.Master)]
    public void All_loops_with_their_time(string shipId, HaulRank rank)
    {
        var ship = FrozenShips.Load(shipId);
        var data = Data.Value;

        var first = Stopwatch.StartNew();
        var loops = Loops.For(data.Templates, data.Positions, ObservedEdges.Empty, ship, rank, null, "Everus Harbor");
        var firstMs = first.Elapsed.TotalMilliseconds;
        List<double> runs = [];
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew();
            Loops.For(data.Templates, data.Positions, ObservedEdges.Empty, ship, rank, null, "Everus Harbor");
            runs.Add(watch.Elapsed.TotalMilliseconds);
        }

        // As in the app: the tab keeps the planner's answers, so the ranking after a scan asks almost nothing new.
        var fits = new FitCache();
        Loops.For(data.Templates, data.Positions, ObservedEdges.Empty, ship, rank, null, "Everus Harbor", fits);
        var cached = Stopwatch.StartNew();
        Loops.For(data.Templates, data.Positions, ObservedEdges.Empty, ship, rank, null, "Everus Harbor", fits);
        var cachedMs = cached.Elapsed.TotalMilliseconds;

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{ship.Name}, {rank}: {loops.Count} loops ({loops.Count(l => l.System == StarSystem.Stanton)} Stanton, {loops.Count(l => l.System == StarSystem.Pyro)} Pyro); first {firstMs:F0} ms, median {runs.Order().ElementAt(2):F0} ms, max {runs.Max():F0} ms; again with the cache {cachedMs:F0} ms ({fits.Count} answers)"));
        loops.Should().NotBeEmpty("the LEO ring is always there");
    }

    [Fact]
    public void Ranking_with_measured_times()
    {
        // T13.6c (budget ≤ 1 ms, as without): every loop of the Hull B at Senior with three runs, as many as routes.json
        // keeps (50) at most, ranked per hour by them; the tab ranks on the UI thread at each toggle and each slider.
        var ship = FrozenShips.Load("hull_b");
        var loops = Loops.For(Data.Value.Templates, Data.Value.Positions, ObservedEdges.Empty, ship, HaulRank.Senior, null, "Everus Harbor");
        var finished = new DateTime(2026, 11, 25, 18, 0, 0, DateTimeKind.Utc);
        RouteRun[] runs =
        [
            .. loops.Take(RouteRun.MaxKept / 3).SelectMany(l => new[] { 70, 80, 150 }.Select((minutes, i) => new RouteRun(
                "run", finished.AddDays(i).AddMinutes(-minutes), null, finished.AddDays(i), l.Start, l.Kind, l.System, l.Landings, l.Crates, l.LongJumps,
                l.Reward ?? 0, 1, 1, 0, ship.Id, l.Stations, l.Kind == LoopKind.Direct ? LoopKey.Of(l).Direct : null))),
        ];
        var measured = MeasuredTimes.For(runs, ship.Id);

        List<double> without = [];
        List<double> with = [];
        for (var i = 0; i < 21; i++)
        {
            var watch = Stopwatch.StartNew();
            Loops.Rank(loops, LoopSort.PerHour, LoopTimes.Default);
            without.Add(watch.Elapsed.TotalMilliseconds);
            watch.Restart();
            Loops.Rank(loops, LoopSort.PerHour, LoopTimes.Default, measured);
            with.Add(watch.Elapsed.TotalMilliseconds);
        }

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{loops.Count} loops, {loops.Count(l => measured.Usable(l) is not null)} measured from {runs.Length} runs: Rank median {without.Order().ElementAt(10):F3} ms without, {with.Order().ElementAt(10):F3} ms with (first {with[0]:F3} ms)"));
        loops.Count(l => measured.Usable(l) is not null).Should().BePositive();
    }
}
