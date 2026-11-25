using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Routing;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The station bar of T13.6 6b (D126): the stops of a route (start, stations, the hub at the end), what each unloads,
/// the contracts held and the slots free, the ways back with a slot simulation on the real plan, the bar's moves and the
/// run at the finish. Positions, routes and plans built here, the ship from FrozenShips (A10).
/// </summary>
public class RouteProgressTests
{
    private const string Everus = "Everus Harbor";
    private const string L1 = "HUR-L1 Green Glade Station";
    private const string L2 = "HUR-L2 Faithful Dream Station";
    private const string L4 = "HUR-L4 Melodic Fields Station";
    private const string Teasa = "Teasa Spaceport";

    private static readonly DateTime Monday = new(2026, 11, 25, 10, 0, 0, DateTimeKind.Utc);

    private static readonly Ship HullB = FrozenShips.Load("hull_b");

    // In Gm: Teasa lies near L4; Everus ↔ L1, L2 are short (about 1), every leg between L1, L2, L4 and Teasa but L4 ↔ Teasa is long.
    private static readonly Positions Places = new(
    [
        At(Everus, 0, 0), At(L1, 1, 0.2), At(L2, -1, 0.3), At(L4, 12, 8), At(Teasa, 13, 9),
    ]);

    private static Placeholder Out(int id, string to, int scu = 20, long reward = 50_000) =>
        new(id, RecipeKind.Small, $"t-{id}", "Junior Rank - Small Cargo Haul", reward, [new HaulLeg("Stims", 1, scu, CrateSize.Four)], [to]);

    private static Placeholder Back(int id, string acceptAt, int scu, CrateSize crate, IReadOnlyList<string>? collect = null, long reward = 60_000) =>
        new(id, RecipeKind.Return, $"b-{id}", "Junior Rank - Small Cargo Haul", reward, [new HaulLeg("Waste", 1, scu, crate)], [Everus], acceptAt, collect ?? []);

    private static Route Ring(params Placeholder[] placeholders) =>
        new("Everus ring", Monday, Everus, LoopKind.LocalRing, StarSystem.Stanton, [L1, L4, L2], placeholders, []);

    private static SavedPlan Empty() => new([], new LoadPlan(HullB.Id, [], []), CrateCap.Of(HullB));

    // A scanned contract into the plan as a scan puts it (ScanSync: PlaceInto with its identity).
    private static SavedPlan With(SavedPlan plan, int no, long reward, bool estimated, params (string To, string Commodity, int Scu, CrateSize Crate)[] items) =>
        LoadPlanner.PlaceInto(plan, HullB, [.. items.Select((x, i) => new MissionStop(x.To, i + 1, [new MissionItem(x.Commodity, x.Scu, x.Crate)]))],
            new ContractSource(new ScanIdentity(no, $"KEY-{no}", estimated), Reward: reward));

    private static SavedPlan Delivered(SavedPlan plan, string to, int mission) =>
        new(plan.Missions, LoadPlanner.SetDelivered(plan.Plan, HullB, to, mission, true), plan.CrateCap);

    [Fact]
    public void The_stops_are_the_start_the_stations_as_flown_and_the_hub_again()
    {
        var progress = RouteProgress.Of(Ring(), Empty(), HullB, Places);

        progress.Stops.Select(s => (s.Index, s.Name, s.IsHub)).Should().Equal(
            (0, Everus, true), (1, L1, false), (2, L4, false), (3, L2, false), (4, Everus, true));
        progress.Landings.Should().Be(4);
    }

    [Fact]
    public void Each_station_unloads_the_legend_lines_of_its_place_and_the_hub_at_the_end_the_ways_back()
    {
        // The card writes "Green Glade Station"; the station is the position's "HUR-L1 Green Glade Station".
        var plan = With(Empty(), 1, 300_000, false, ("Green Glade Station", "Stims", 20, CrateSize.Four), (L4, "Stims", 10, CrateSize.Four));
        plan = With(plan, 2, 40_000, false, ("Green Glade Station", "Iron", 8, CrateSize.Four));
        plan = With(plan, 3, 60_000, false, (Everus, "Waste", 12, CrateSize.Four));
        plan = Delivered(plan, "Green Glade Station", 2);

        var stops = RouteProgress.Of(Ring(), plan, HullB, Places).Stops;

        stops.Select(s => (s.Lines, s.LinesDone)).Should().Equal((0, 0), (2, 1), (1, 0), (0, 0), (1, 0));
        stops[1].Open.Should().Be(1);
    }

    [Fact]
    public void A_contract_is_held_from_its_accepted_scan_until_all_its_lines_are_unloaded_and_the_free_slots_are_ten_less()
    {
        var plan = With(Empty(), 1, 300_000, false, (L1, "Stims", 20, CrateSize.Four), (L4, "Stims", 10, CrateSize.Four));
        plan = With(plan, 2, 40_000, false, (L1, "Iron", 8, CrateSize.Four));
        plan = With(plan, 3, 30_000, estimated: true, (L2, "Gold", 8, CrateSize.Four));

        var stops = RouteProgress.Of(Ring(), plan, HullB, Places).Stops;

        stops.Select(s => s.Held).Should().Equal([2, 1, 0, 0, 0], "#2 is done at L1, #1 at L4; the offer #3 is no contract yet");
        stops.Select(s => s.FreeSlots).Should().Equal(8, 9, 10, 10, 10);
    }

    [Fact]
    public void A_way_back_fits_with_what_the_station_unloads_and_is_too_big_beyond_the_room_left()
    {
        // 480 SCU aboard a Hull B, 100 of them for L1: at L1 a way back of 72 SCU in 4s fits, one of 190 in 16s doesn't.
        var plan = With(Empty(), 1, 300_000, false, (L1, "Waste", 100, CrateSize.Sixteen));
        plan = With(plan, 2, 300_000, false, (L2, "Waste", 380, CrateSize.Sixteen));
        plan.Missions.Should().OnlyContain(m => !m.Unplaced, "the test needs 480 SCU aboard");
        var route = Ring(Out(1, L1), Back(2, L1, 72, CrateSize.Four), Back(3, L1, 190, CrateSize.Sixteen)) with { At = 1 };

        var returns = RouteProgress.Of(route, plan, HullB, Places).Stops[1].Returns;

        returns.Select(r => (r.Placeholder.Id, r.Fit)).Should().Equal((2, ReturnFit.Fits), (3, ReturnFit.TooBig));
        RouteProgress.Of(route with { At = 0 }, plan, HullB, Places).Stops[1].Returns.Should().OnlyContain(r => r.Fit == ReturnFit.Unchecked,
            "only the stop the bar stands at is simulated");
    }

    [Fact]
    public void Without_the_stations_unloading_the_72_SCU_would_not_fit_so_the_simulation_unloads_first()
    {
        var plan = With(Empty(), 1, 300_000, false, (L4, "Waste", 100, CrateSize.Sixteen));
        plan = With(plan, 2, 300_000, false, (L2, "Waste", 380, CrateSize.Sixteen));
        var route = Ring(Back(2, L1, 72, CrateSize.Four)) with { At = 1 };

        RouteProgress.Of(route, plan, HullB, Places).Stops[1].Returns.Single().Fit.Should().Be(ReturnFit.TooBig, "32 SCU free at L1, nothing unloaded there");
    }

    [Fact]
    public void A_filled_way_back_shows_its_scan_and_a_multi_pickup_one_collects_at_the_next_stations()
    {
        var back = Back(2, L1, 20, CrateSize.Four, collect: [L4]) with { FilledBy = "KEY-1" };
        var plan = With(Empty(), 1, 60_000, false, (Everus, "Waste", 20, CrateSize.Four));

        var stops = RouteProgress.Of(Ring(Out(1, L2), back) with { At = 1 }, plan, HullB, Places).Stops;

        stops[1].Returns.Single().Should().Be(new ReturnAt(back, ReturnFit.Filled, 1));
        stops[2].Collects.Should().Equal(back);
        stops[3].Collects.Should().BeEmpty();
    }

    [Fact]
    public void An_extra_to_a_ring_station_changes_nothing_one_elsewhere_joins_the_order_before_leaving_and_before_the_hub_after()
    {
        var plan = With(Empty(), 1, 40_000, false, (L1, "Iron", 8, CrateSize.Four));
        plan = With(plan, 2, 41_750, false, (Teasa, "Stims", 12, CrateSize.Four));
        var ring = Ring() with { Extras = ["KEY-1"] };
        var teasa = Ring() with { Extras = ["KEY-1", "KEY-2"] };

        RouteProgress.Of(ring, plan, HullB, Places).Stops.Select(s => s.Name).Should().Equal(Everus, L1, L4, L2, Everus);
        // Everus → L1 → L4 → Teasa → L2 → Everus is the shortest round trip with Teasa (near L4), 33.4 Gm; its reverse ties
        // and loses on the place order. After leaving, Teasa comes before the hub (K6).
        RouteProgress.Of(teasa, plan, HullB, Places).Stops.Select(s => s.Name).Should().Equal(Everus, L1, L4, Teasa, L2, Everus);
        RouteProgress.Of(teasa with { DepartedUtc = Monday }, plan, HullB, Places).Stops.Select(s => s.Name)
            .Should().Equal(Everus, L1, L4, L2, Teasa, Everus);
    }

    [Fact]
    public void The_bar_steps_on_its_own_when_the_last_line_of_its_station_is_ticked_and_jumps_to_a_later_station_ticked()
    {
        var plan = With(Empty(), 1, 300_000, false, (L1, "Stims", 20, CrateSize.Four), (L4, "Stims", 10, CrateSize.Four), (L2, "Stims", 10, CrateSize.Four));
        var route = Ring() with { At = 1, DepartedUtc = Monday };
        var before = RouteProgress.Of(route, plan, HullB, Places);

        var atL1 = Delivered(plan, L1, 1);
        var stepped = Route.Advance(route, before, RouteProgress.Of(route, atL1, HullB, Places), Monday.AddMinutes(5));
        var jumped = Route.Advance(route, before, RouteProgress.Of(route, Delivered(plan, L4, 1), HullB, Places), Monday.AddMinutes(5));
        var undone = Route.Advance(route with { At = 2 }, RouteProgress.Of(route, atL1, HullB, Places), before, Monday.AddMinutes(6));

        (stepped.At, jumped.At, undone.At).Should().Be((2, 3, 2), "L4 done moves on to L2; an undo moves nothing");
    }

    [Fact]
    public void Leaving_the_hub_by_hand_or_by_a_tick_sets_the_departure_once_and_the_bar_stays_within_its_stops()
    {
        var plan = With(Empty(), 1, 300_000, false, (L1, "Stims", 20, CrateSize.Four));
        var route = Ring();

        var left = route.Step(+1, 5, Monday.AddMinutes(10));
        var back = left.Step(-1, 5, Monday.AddMinutes(11)).Step(+1, 5, Monday.AddMinutes(12));
        var ticked = Route.Advance(route, RouteProgress.Of(route, plan, HullB, Places), RouteProgress.Of(route, Delivered(plan, L1, 1), HullB, Places), Monday.AddMinutes(20));

        (left.At, left.DepartedUtc).Should().Be((1, Monday.AddMinutes(10)));
        back.DepartedUtc.Should().Be(Monday.AddMinutes(10), "the first departure counts");
        (ticked.At, ticked.DepartedUtc).Should().Be((2, Monday.AddMinutes(20)), "a tick at L1 leaves the hub, and L1 is done");
        route.Step(-1, 5, Monday).At.Should().Be(0);
        (route with { At = 4 }).Step(+1, 5, Monday).At.Should().Be(4);
    }

    [Fact]
    public void The_run_sums_the_rewards_of_the_filled_and_the_extras_times_from_the_departure_and_counts_the_round()
    {
        var plan = With(Empty(), 1, 310_000, false, (L1, "Stims", 20, CrateSize.Four));
        plan = With(plan, 2, 41_750, false, (Teasa, "Stims", 12, CrateSize.Four));
        plan = With(plan, 3, 90_000, false, (L4, "Gold", 8, CrateSize.Four));
        var route = Ring(Out(1, L1, reward: 300_000) with { FilledBy = "KEY-1" }, Out(2, L4, reward: 70_000)) with
        {
            Extras = ["KEY-2"],
            DepartedUtc = Monday.AddMinutes(15),
            At = 5,
        };
        var progress = RouteProgress.Of(route, plan, HullB, Places);

        var run = Route.Finish(route, plan, progress, Places, HullB, Monday.AddMinutes(95));

        // The loop's identity for measured times (T13.6c): the ship and the stations as Start plan made them, the extra's
        // Teasa not among them.
        run.Should().BeEquivalentTo(new RouteRun("Everus ring", Monday, Monday.AddMinutes(15), Monday.AddMinutes(95), Everus, LoopKind.LocalRing, StarSystem.Stanton,
            Landings: 5, Crates: 8, LongJumps: 4, Reward: 351_750, Filled: 1, Total: 2, Extras: 1, ShipId: HullB.Id, Stations: [L1, L4, L2]),
            options => options.WithStrictOrdering());
        run.Time.Should().Be(TimeSpan.FromMinutes(80), "from the departure, not the start; #3 is no part of the route");
    }

    private static StarPosition At(string name, double xGm, double yGm) => new(name, name, StarSystem.Stanton, "Manmade", null, xGm * 1e9, yGm * 1e9, 0, []);
}
