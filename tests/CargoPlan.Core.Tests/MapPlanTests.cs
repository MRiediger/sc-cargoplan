using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Routing;
using static CargoPlan.Core.Tests.MapNodesTests;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A plan as the map draws it (gate finding 7a/7b, D128 addendum): the extras of the active route from the plan's
/// missions, pickup to stop, the places resolved in the route's system. Positions and plans built here (A10).
/// </summary>
public class MapPlanTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string Baijini = "Baijini Point";

    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    private static readonly Positions Places = new([P(Everus, "Manmade", 12.849, 0.001), P(L1, "Manmade", 11.56, 0), P(Baijini, "Manmade", 18.59, -22.15)]);

    [Fact]
    public void An_extra_is_each_items_pickup_to_its_stop_with_the_missions_scan_number_and_reward()
    {
        var plan = With(With(Empty(), 1, 50_000, (L1, [Everus])), 4, 160_250, (Everus, [Baijini, L1]), (L1, [Baijini]));
        var route = Ring(extras: ["KEY-4"]);

        MapExtra.Of(route, plan, Places).Should().Equal(
            new MapExtra(Baijini, Everus, 4, 160_250), new MapExtra(L1, Everus, 4, 160_250), new MapExtra(Baijini, L1, 4, 160_250));
    }

    [Fact]
    public void A_place_it_cant_resolve_or_an_item_without_pickup_draws_nothing()
    {
        var plan = With(With(Empty(), 4, 1, ("Nowhere", [Everus]), (L1, ["Somewhere"])), 5, 1, (Baijini, []));

        MapExtra.Of(Ring(extras: ["KEY-4", "KEY-5"]), plan, Places).Should().BeEmpty();
        MapExtra.Of(Ring(extras: ["KEY-4"]), null, Places).Should().BeEmpty();
    }

    private static Route Ring(IReadOnlyList<string> extras) =>
        new("Everus Harbor ring", DateTime.UtcNow, Everus, LoopKind.LocalRing, StarSystem.Stanton, [L1], [], extras);

    private static SavedPlan Empty() => new([], new LoadPlan(HullB.Id, [], []), CrateCap.Of(HullB));

    // A scanned contract into the plan as a scan puts it; each stop one item from its pickups.
    private static SavedPlan With(SavedPlan plan, int no, long reward, params (string To, string[] Pickup)[] stops) =>
        LoadPlanner.PlaceInto(plan, HullB,
            [.. stops.Select((s, i) => new MissionStop(s.To, i + 1, [new MissionItem("Stims", 4, CrateSize.Four, Pickup: s.Pickup.Length == 0 ? null : s.Pickup)]))],
            new ContractSource(new ScanIdentity(no, $"KEY-{no}", false), Reward: reward));
}
