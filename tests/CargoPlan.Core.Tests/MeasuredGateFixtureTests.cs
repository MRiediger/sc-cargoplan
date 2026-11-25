using System.Globalization;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The T13.6c gate's data folder (Fixtures/Routes/MeasuredEverus): its three runs measure the pinned release's Everus
/// ring for a Hull B at Senior, so the gate shows "measured 3×" without flying it. A data test of the baseline (A10): a
/// new pin that changes the ring's stations breaks it here, not at Martin's desk. Prints the gate's figures.
/// </summary>
public class MeasuredGateFixtureTests(ITestOutputHelper output)
{
    [Fact]
    public void The_fixtures_runs_measure_the_pins_Everus_ring_for_hull_b()
    {
        var data = ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data;
        var ship = FrozenShips.Load("hull_b");
        using var stream = MissionFixtures.Open("routes/measured_everus.json");
        var runs = RoutesFile.Read(stream, "measured_everus.json").Runs;
        var loops = Loops.For(data.Templates, data.Positions, ObservedEdges.Empty, ship, HaulRank.Senior, null, "Everus Harbor");
        var ring = loops.Single(l => l.Kind == LoopKind.LocalRing && l.Start == "Everus Harbor");

        var measured = MeasuredTimes.For(runs, ship.Id);

        var everus = measured.Usable(ring);
        everus.Should().NotBeNull("the fixture's stations are the ring's: {0}", string.Join(" → ", ring.Stations));
        (everus!.Count, everus.MedianMinutes).Should().Be((3, 80));
        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"Everus ring: {ring.Reward:N0} aUEC; model {ring.PerHour(LoopTimes.Default):N0}/h, measured {Loops.PerHour(ring, LoopTimes.Default, measured):N0}/h (median {everus.MedianMinutes} min)"));
    }
}
