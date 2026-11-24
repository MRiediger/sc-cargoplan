using System.Globalization;
using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;
using Xunit.Abstractions;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The loops on the pinned release's boards against design §3 and §4.7 (T13.4): a Hull B at Senior, Everus saved, the
/// default times, no scans. The structure is exact (landings, long jumps, order); the aUEC per loop are printed with
/// their difference to §3, a figure for TASKS.md, not a check: §3 came from scans, these are templates at their worst.
/// Where the shortest round trip isn't §3's (§3 is the players' habit, not a measurement), the test's name gives both
/// distances. A data test of the baseline (A10): ships from FrozenShips.
/// </summary>
public class LoopsBaselineTests(ITestOutputHelper output)
{
    private const string Everus = "Everus Harbor";

    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    private static readonly Lazy<IReadOnlyList<LoopCandidate>> HullB = new(() =>
        Loops.For(Data.Value.Templates, Data.Value.Positions, ObservedEdges.Empty, FrozenShips.Load("hull_b"), HaulRank.Senior, null, Everus));

    [Theory]
    [InlineData(Everus, 6, 4, 1_010_000)]
    [InlineData("Baijini Point", 6, 4, 1_080_000)]
    [InlineData("Seraphim Station", 4, 3, 875_000)]
    [InlineData("Port Tressler", 6, 4, 1_000_000)]
    public void A_rings_landings_and_long_jumps_are_design_3s(string hub, int landings, int longJumps, long designReward)
    {
        var ring = Ring(hub);

        output.WriteLine(string.Create(CultureInfo.InvariantCulture,
            $"{hub}: {string.Join(" → ", ring.Stations.Select(PlaceNames.Short))} ({TripGm(ring):0.00} Gm); {ring.Reward:N0} aUEC against {designReward:N0} ({(ring.Reward!.Value - designReward) * 100.0 / designReward:+0;-0} %), {ring.Crates} crates, {ring.Contracts.Count} contracts"));
        foreach (var c in ring.Contracts)
        {
            output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"  {c.Kind,-6} {c.Template.Title,-44} {c.Template.Reward,9:N0} {c.WorstScu,4} SCU → {string.Join(", ", c.Destinations.Select(PlaceNames.Short))}{(c.AcceptAt is { } at ? " from " + PlaceNames.Short(at) : "")}"));
        }

        (ring.Landings, ring.LongJumps).Should().Be((landings, longJumps));
    }

    [Fact]
    public void Everus_ring_is_L1_L4_L3_L5_L2_72_88_Gm_not_design_3s_L1_L2_L4_L3_L5_74_76_Gm()
    {
        var ring = Ring(Everus);

        ring.Stations.Select(PlaceNames.Short).Should().Equal("HUR-L1", "HUR-L4", "HUR-L3", "HUR-L5", "HUR-L2");
        TripGm(ring).Should().BeApproximately(72.88, 0.01);
        TripGm(ring with { Stations = Short("HUR", "L1", "L2", "L4", "L3", "L5") }).Should().BeApproximately(74.76, 0.01);
    }

    [Fact]
    public void Seraphim_ring_is_design_3s_L1_L4_L5_72_48_Gm_tied_with_L1_L5_L4_within_0_1_Gm()
    {
        var ring = Ring("Seraphim Station");

        ring.Stations.Select(PlaceNames.Short).Should().Equal("CRU-L1", "CRU-L4", "CRU-L5");
        TripGm(ring with { Stations = Short("CRU", "L1", "L5", "L4") }).Should().BeApproximately(TripGm(ring), Loops.TieGm, "a tie goes to the place order");
    }

    [Fact]
    public void Baijini_ring_is_L1_L4_L3_L5_L2_not_design_3s_L1_L2_L4_L3_L5_166_71_Gm()
    {
        var ring = Ring("Baijini Point");
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{string.Join(" → ", ring.Stations.Select(PlaceNames.Short))}: {TripGm(ring):0.00} Gm"));

        ring.Stations.Select(PlaceNames.Short).Should().Equal("ARC-L1", "ARC-L4", "ARC-L3", "ARC-L5", "ARC-L2");
        TripGm(ring with { Stations = Short("ARC", "L1", "L2", "L4", "L3", "L5") }).Should().BeApproximately(166.71, 0.01);
        TripGm(ring).Should().BeLessThan(166.71 - 1);
    }

    [Fact]
    public void The_experienced_direct_medium_from_Everus_lands_twice_and_both_its_jumps_are_long()
    {
        var run = HullB.Value.Where(c => c.Kind == LoopKind.Direct && c.Start == Everus && c.Contracts[0].Template.Title == "Experienced Rank - Direct Medium Cargo Haul")
            .MaxBy(c => c.Reward)!;
        var gm = Positions.DistanceGm(Data.Value.Positions.Find(StarSystem.Stanton, Everus)!, Data.Value.Positions.Find(StarSystem.Stanton, run.Stations[0])!)!.Value;
        output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Everus → {PlaceNames.Short(run.Stations[0])} {gm:0.00} Gm, {run.Reward:N0} aUEC with {run.Contracts.Count} contracts"));

        (run.Landings, run.Legs).Should().Be((2, 2));
        run.LongJumps.Should().Be(gm > Loops.LongJumpGm ? 2 : 0);
    }

    [Fact]
    public void Pyro_rings_show_structure_without_aUEC_and_the_LEO_ring_needs_scans_at_Everus()
    {
        HullB.Value.Where(c => c.System == StarSystem.Pyro).Should().NotBeEmpty()
            .And.OnlyContain(c => c.Reward == null && c.Stations.Count <= Loops.MaxRingStations && c.TravelGm == null);
        HullB.Value.Single(c => c.Kind == LoopKind.SystemWide).Should().Match<LoopCandidate>(c => !c.HasNumbers && c.NeedsScansAt == Everus);
    }

    [Fact]
    public void Ranking_at_10_and_at_5_and_15_minutes_per_station()
    {
        foreach (var minutes in new[] { 10, 5, 15 })
        {
            var times = LoopTimes.Default with { StationMinutes = minutes };
            output.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{minutes} min per station:"));
            foreach (var c in Loops.Rank(HullB.Value.Where(c => c.System == StarSystem.Stanton), LoopSort.PerHour, times).Take(8))
            {
                output.WriteLine(string.Create(CultureInfo.InvariantCulture,
                    $"  {c.PerHour(times),9:N0}/h {c.Kind,-10} {c.Start,-18} {string.Join(">", c.Stations.Select(PlaceNames.Short)),-36} {c.Reward,10:N0} L{c.Landings} C{c.Crates} J{c.LongJumps}{(c.Kind == LoopKind.Direct ? " " + c.Contracts[0].Template.Title : "")}"));
            }
        }
    }

    private static LoopCandidate Ring(string hub) => HullB.Value.Single(c => c.Kind == LoopKind.LocalRing && c.Start == hub);

    private static string[] Short(string planet, params string[] codes) =>
        [.. codes.Select(code => Data.Value.Positions.All.Single(p => p.System == StarSystem.Stanton && PlaceNames.Short(p.Name) == $"{planet}-{code}").Name)];

    private static double TripGm(LoopCandidate loop)
    {
        string[] places = [loop.Start, .. loop.Stations, loop.Start];
        return places.Zip(places.Skip(1)).Sum(p =>
            Positions.DistanceGm(Data.Value.Positions.Find(loop.System, p.First)!, Data.Value.Positions.Find(loop.System, p.Second)!)!.Value);
    }
}
