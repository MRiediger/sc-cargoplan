using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
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

    [Fact]
    public void Start_plan_at_Everus_takes_the_accepted_bulk_of_the_cargo_one_of_ten_and_the_other_contract_is_an_extra()
    {
        // Gate 6a (Martin, 25 Nov): accepted first, then Start plan - filled without a rescan.
        var route = From(HullB.Value.Single(c => c.Kind == LoopKind.LocalRing && c.Start == Everus));
        var ship = FrozenShips.Load("hull_b");
        Mission[] cargo =
        [
            Accepted(1, 325_250, ("Green Glade Station", "Hydrogen Fuel", 50), ("Faithful Dream Station", "Quantum Fuel", 100),
                ("Thundering Express Station", "Ship Ammunition", 100), ("Melodic Fields Station", "Hydrogen Fuel", 45)),
            Accepted(2, 41_750, ("Teasa Spaceport", "Stims", 12)),
        ];
        var plan = new SavedPlan(cargo, new LoadPlan(ship.Id, [], []), CrateCap.Of(ship));

        var (started, adopted) = Route.Adopt(route, plan, Data.Value.Templates, Data.Value.Positions);

        started.FilledCount(new HashSet<string> { "KEY-1", "KEY-2" }).Should().Be(1);
        started.Placeholders.Single(p => p.FilledBy == "KEY-1").Kind.Should().Be(RecipeKind.Bulk);
        started.Extras.Should().Equal("KEY-2");
        adopted.Should().Equal(new Adoption(1, 1), new Adoption(2, null));
    }

    private static Mission Accepted(int no, long reward, params (string To, string Commodity, int Scu)[] items) => new(
        no, [.. items.Select((x, i) => new MissionStop(x.To, i + 1, [new MissionItem(x.Commodity, x.Scu, CrateSize.Sixteen, Pickup: [Everus])]))],
        new ContractSource(new ScanIdentity(no, $"KEY-{no}", false), Reward: reward), Unplaced: true);

    private static Route From(LoopCandidate loop) => Route.From(loop, "test", ObservedEdges.Empty, Monday);
}
