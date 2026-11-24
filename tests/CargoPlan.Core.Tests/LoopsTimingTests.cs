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

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{ship.Name}, {rank}: {loops.Count} loops ({loops.Count(l => l.System == StarSystem.Stanton)} Stanton, {loops.Count(l => l.System == StarSystem.Pyro)} Pyro); first {firstMs:F0} ms, median {runs.Order().ElementAt(2):F0} ms, max {runs.Max():F0} ms"));
        loops.Should().NotBeEmpty("the LEO ring is always there");
    }
}
