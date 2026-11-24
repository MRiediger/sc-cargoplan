using CargoPlan.Core.Routing;
using CargoPlan.Core.ShipData;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Routes from the pinned release's loops (T13.6, D125): a Hull B at Senior, Everus saved, no scans – the figures of
/// T13.4 (TASKS.md) carry over to the placeholders. A data test of the baseline (A10): ships from FrozenShips.
/// </summary>
public class RouteBaselineTests
{
    private const string Everus = "Everus Harbor";

    private static readonly DateTime Monday = new(2026, 11, 24, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Lazy<HaulingData> Data = new(() =>
        ((HaulingLoad.Loaded)HaulingFiles.Load(ActiveShipData.ReadBaseline(ActiveShipData.BaselineFolder))).Data);

    private static readonly Lazy<IReadOnlyList<LoopCandidate>> HullB = new(() =>
        Loops.For(Data.Value.Templates, Data.Value.Positions, ObservedEdges.Empty, FrozenShips.Load("hull_b"), HaulRank.Senior, null, Everus));

    [Fact]
    public void The_Everus_ring_is_ten_placeholders_594_500_out_and_282_500_back_along_L1_L4_L3_L5_L2()
    {
        var route = From(HullB.Value.Single(c => c.Kind == LoopKind.LocalRing && c.Start == Everus));

        route.Stations.Select(PlaceNames.Short).Should().Equal("HUR-L1", "HUR-L4", "HUR-L3", "HUR-L5", "HUR-L2");
        route.Placeholders.Should().HaveCount(10);
        (route.Outbound.Count(), route.OutboundReward, route.BackReward).Should().Be((5, 594_500L, 282_500L));
        route.Back.Select(p => (PlaceNames.Short(p.AcceptAt!), p.Reward)).Should().Equal(
            ("HUR-L1", 72_500L), ("HUR-L4", 60_750L), ("HUR-L3", 50_250L), ("HUR-L5", 48_500L), ("HUR-L2", 50_500L));
        route.Back.First().CollectAt!.Select(PlaceNames.Short).Should().Equal("HUR-L4", "HUR-L3", "HUR-L5");
        route.Outbound.First().Kind.Should().Be(RecipeKind.Bulk);
    }

    [Fact]
    public void The_experienced_direct_from_Everus_to_HUR_L3_is_one_station_and_a_one_pickup_way_back_211_750()
    {
        var run = HullB.Value.Where(c => c.Kind == LoopKind.Direct && c.Start == Everus && c.Contracts[0].Template.Title == "Experienced Rank - Direct Medium Cargo Haul")
            .MaxBy(c => c.Reward)!;

        var route = From(run);

        route.Stations.Select(PlaceNames.Short).Should().Equal("HUR-L3");
        route.Outbound.Single().Kind.Should().Be(RecipeKind.Direct);
        route.Back.Single().CollectAt.Should().BeEmpty();
        (route.OutboundReward + route.BackReward).Should().Be(211_750);
    }

    [Fact]
    public void The_Port_Tressler_ring_is_1_028_000()
    {
        var route = From(HullB.Value.Single(c => c.Kind == LoopKind.LocalRing && c.Start == "Port Tressler"));

        (route.OutboundReward + route.BackReward).Should().Be(1_028_000);
    }

    private static Route From(LoopCandidate loop) => Route.From(loop, "test", ObservedEdges.Empty, Monday);
}
