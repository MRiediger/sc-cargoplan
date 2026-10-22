using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>Incremental placement (D41) and the mission operations around it (D42).</summary>
public class PlaceIntoTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []));

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max) =>
        new(destination, 1, [new MissionItem(commodity, scu, max)]);

    /// <summary>Each stop as a mission of its own, placed one after the other, as captures come in.</summary>
    private static SavedPlan Placed(params MissionStop[] stops) =>
        stops.Aggregate(Empty, (saved, stop) => LoadPlanner.PlaceInto(saved, HullA, [stop]));

    private static IEnumerable<string> RacksOf(SavedPlan saved, string destination) =>
        saved.Plan.Crates.Where(c => c.Destination == destination).Select(c => c.RackId).Distinct();

    private static SavedPlan Delivered(SavedPlan saved, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, true), saved.Watermark);

    /// <summary>Four 8 SCU crates, one per rack, each leaving a 2×2×2 half of its rack free.</summary>
    private static SavedPlan HalfFull() => Placed(
        Stop("Riker", "Gold", 8, CrateSize.Eight), Stop("Shubin", "Gold", 8, CrateSize.Eight),
        Stop("Orison", "Gold", 8, CrateSize.Eight), Stop("Lorville", "Gold", 8, CrateSize.Eight));

    // --- PlaceInto ---

    [Fact]
    public void PlaceInto_an_empty_plan_takes_rack_1_and_mission_1()
    {
        var saved = Placed(Stop("Riker", "Titanium", 12, CrateSize.Four));

        saved.Missions.Should().ContainSingle().Which.Id.Should().Be(1);
        saved.Plan.Crates.Should().OnlyContain(c => c.RackId == "rack-0" && c.Mission == 1);
        LoadPlanner.Validate(saved.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void PlaceInto_puts_a_new_destination_into_the_first_empty_rack()
    {
        var saved = Placed(Stop("Riker", "Titanium", 4, CrateSize.Four), Stop("Shubin", "Quartz", 4, CrateSize.Four));

        RacksOf(saved, "Shubin").Should().Equal("rack-1");
    }

    [Fact]
    public void PlaceInto_moves_no_crate_and_keeps_ticks()
    {
        var before = Delivered(Placed(Stop("Riker", "Titanium", 12, CrateSize.Four), Stop("Shubin", "Quartz", 6, CrateSize.Two)), "Riker");

        var after = LoadPlanner.PlaceInto(before, HullA, [Stop("Orison", "Agricium", 8, CrateSize.Eight)]);

        after.Plan.Crates.Where(c => c.Destination != "Orison").Should().Equal(before.Plan.Crates);
    }

    [Fact]
    public void PlaceInto_joins_a_destination_on_board_in_its_rack_and_its_place_in_the_route()
    {
        // One handover at the elevator (D42): the new crates go behind Riker's crates, not to the end.
        var saved = Placed(
            Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Shubin", "Quartz", 8, CrateSize.Four), Stop("Riker", "Gold", 4, CrateSize.Four));

        RacksOf(saved, "Riker").Should().Equal("rack-0");
        saved.Plan.Crates.Select(c => (c.Destination, c.Mission)).Should().Equal(
            ("Riker", 1), ("Riker", 1), ("Riker", 3), ("Shubin", 2), ("Shubin", 2));
        saved.Plan.UnloadOrder.Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void PlaceInto_a_delivered_destination_is_a_new_visit_at_the_end()
    {
        var delivered = Delivered(Placed(Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Shubin", "Quartz", 8, CrateSize.Four)), "Riker");

        var saved = LoadPlanner.PlaceInto(delivered, HullA, [Stop("Riker", "Gold", 4, CrateSize.Four)]);

        saved.Plan.UnloadOrder.Should().Equal("Shubin", "Riker");
        saved.Plan.Route.Should().Equal("Riker", "Shubin");
        Delivered(saved, "Riker").Plan.UnloadOrder.Should().Equal("Shubin", "Riker");
    }

    [Fact]
    public void PlaceInto_lays_a_mission_with_two_destinations_into_two_racks()
    {
        var saved = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker", "Gold", 8, CrateSize.Eight), Stop("Shubin", "Gold", 8, CrateSize.Eight) with { Order = 2 }]);

        saved.Missions.Should().ContainSingle();
        RacksOf(saved, "Riker").Should().Equal("rack-0");
        RacksOf(saved, "Shubin").Should().Equal("rack-1");
    }

    [Fact]
    public void PlaceInto_goes_crate_by_crate_when_no_rack_takes_the_whole_stop()
    {
        var saved = LoadPlanner.PlaceInto(HalfFull(), HullA, [Stop("Hurston", "Gold", 16, CrateSize.Eight)]);

        saved.Missions[^1].Unplaced.Should().BeFalse();
        RacksOf(saved, "Hurston").Should().Equal("rack-0", "rack-1");
        LoadPlanner.Validate(saved.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void PlaceInto_takes_the_spelling_of_a_destination_already_in_the_plan()
    {
        var saved = Placed(Stop("Riker", "Titanium", 4, CrateSize.Four), Stop("riker", "Gold", 4, CrateSize.Four));

        saved.Plan.Crates.Should().OnlyContain(c => c.Destination == "Riker");
        saved.Stops.Should().ContainSingle();
    }

    [Fact]
    public void PlaceInto_adds_a_mission_that_doesnt_fit_without_crates()
    {
        var full = Placed(
            Stop("Riker", "Gold", 16, CrateSize.Sixteen), Stop("Shubin", "Gold", 16, CrateSize.Sixteen),
            Stop("Orison", "Gold", 16, CrateSize.Sixteen), Stop("Lorville", "Gold", 8, CrateSize.Eight));

        var saved = LoadPlanner.PlaceInto(full, HullA, [Stop("Hurston", "Gold", 16, CrateSize.Sixteen)]);

        saved.Missions[^1].Unplaced.Should().BeTrue();
        saved.Plan.Crates.Should().Equal(full.Plan.Crates);
        LoadPlanner.PlaceProblem(saved, HullA, saved.Missions[^1].Id).Should().Be("doesn't fit (16 SCU, 8 free)");
    }

    [Fact]
    public void PlaceProblem_names_the_shape_when_the_room_is_there_in_pieces()
    {
        // The D41 counter-argument: 4 SCU free in every rack, 16 in all, but no place for an 8.
        var pieces = Placed(
            Stop("Riker", "Gold", 12, CrateSize.Four), Stop("Shubin", "Gold", 12, CrateSize.Four),
            Stop("Orison", "Gold", 12, CrateSize.Four), Stop("Lorville", "Gold", 12, CrateSize.Four));

        var saved = LoadPlanner.PlaceInto(pieces, HullA, [Stop("Hurston", "Gold", 16, CrateSize.Eight)]);

        saved.Missions[^1].Unplaced.Should().BeTrue();
        LoadPlanner.PlaceProblem(saved, HullA, saved.Missions[^1].Id).Should().Be("doesn't fit (no room for 8 SCU crates)");
    }

    [Fact]
    public void PlaceProblem_is_null_for_a_placed_mission()
    {
        var saved = Placed(Stop("Riker", "Gold", 4, CrateSize.Four));

        LoadPlanner.PlaceProblem(saved, HullA, 1).Should().BeNull();
    }

    [Fact]
    public void PlaceInto_keeps_the_watermark()
    {
        SavedPlan saved = new([], new LoadPlan(HullA.Id, [], []), 41);

        LoadPlanner.PlaceInto(saved, HullA, [Stop("Riker", "Gold", 4, CrateSize.Four)]).Watermark.Should().Be(41);
    }

    // --- Shape-aware fallback (D54) ---

    private static readonly Ship Uneven = TestShips.Load("uneven_long");

    private static SavedPlan EmptyUneven => new([], new LoadPlan(Uneven.Id, [], []));

    /// <summary>Area18 70 × 1, then Port Tressler 2 × 32: stop by stop Area18 fills rack 1 and the second 32 finds no room.</summary>
    private static readonly MissionStop[] OnesThen32s =
        [new("Area18", 1, [new MissionItem("Quartz", 70, CrateSize.One)]), new("Port Tressler", 2, [new MissionItem("Titanium", 64, CrateSize.ThirtyTwo)])];

    [Fact]
    public void PlaceInto_lays_a_mission_largest_first_when_stop_by_stop_fails()
    {
        var saved = LoadPlanner.PlaceInto(EmptyUneven, Uneven, OnesThen32s);

        saved.Missions.Should().OnlyContain(m => !m.Unplaced);
        saved.Plan.Crates.Sum(c => (int)c.CrateSize).Should().Be(134);
        Uneven.Racks.Should().OnlyContain(r => saved.Plan.CratesForRack(r.Id).Count(c => c.CrateSize == CrateSize.ThirtyTwo) == 1);
        LoadPlanner.Validate(saved.Plan, Uneven).Should().OnlyContain(w => w.Message.EndsWith("which is unloaded first"));
    }

    [Fact]
    public void PlaceInto_on_the_Hull_A_places_a_mission_stop_by_stop_would_leave_out()
    {
        // Rack 1 has 12 SCU free, rack 2 has 4, racks 3 and 4 are full. Stop by stop Hurston's 4s take rack 1 and Area18's 8
        // finds no room; largest first the 8 goes into rack 1, a 4 on top of Riker's, the other 4 into rack 2 (D54).
        var before = Placed(
            Stop("Riker", "Gold", 4, CrateSize.Four), Stop("Shubin", "Gold", 12, CrateSize.Four),
            Stop("Orison", "Gold", 16, CrateSize.Sixteen), Stop("Lorville", "Gold", 16, CrateSize.Sixteen));

        var saved = LoadPlanner.PlaceInto(before, HullA,
            [Stop("Hurston", "Iron", 8, CrateSize.Four), Stop("Area18", "Iron", 8, CrateSize.Eight) with { Order = 2 }]);

        saved.Missions[^1].Unplaced.Should().BeFalse();
        saved.Plan.Crates.Take(before.Plan.Crates.Count).Should().Equal(before.Plan.Crates);
        RacksOf(saved, "Area18").Should().Equal("rack-0");
        RacksOf(saved, "Hurston").Should().Equal("rack-0", "rack-1");
    }

    [Fact]
    public void RetryUnplaced_places_a_waiting_mission_through_the_fallback()
    {
        SavedPlan waiting = new([new Mission(1, OnesThen32s, null, Unplaced: true)], new LoadPlan(Uneven.Id, [], []));

        LoadPlanner.PlaceProblem(waiting, Uneven, 1).Should().BeNull();
        LoadPlanner.RetryUnplaced(waiting, Uneven).Missions.Single().Unplaced.Should().BeFalse();
    }

    [Fact]
    public void PlaceProblem_names_the_crate_size_the_fallback_found_no_room_for()
    {
        // A 32 in each rack: 180 − 64 = 116 SCU free, but no gap for a third 32.
        var two = new[] { "Area18", "Port Tressler" }.Aggregate(EmptyUneven,
            (saved, destination) => LoadPlanner.PlaceInto(saved, Uneven, [new MissionStop(destination, 1, [new MissionItem("Gold", 32, CrateSize.ThirtyTwo)])]));

        var saved = LoadPlanner.PlaceInto(two, Uneven, [new MissionStop("Seraphim", 1, [new MissionItem("Gold", 32, CrateSize.ThirtyTwo)])]);

        saved.Missions[^1].Unplaced.Should().BeTrue();
        LoadPlanner.PlaceProblem(saved, Uneven, saved.Missions[^1].Id).Should().Be("doesn't fit (no room for 32 SCU crates)");
    }

    // --- RetryUnplaced ---

    [Fact]
    public void RetryUnplaced_places_a_waiting_mission_once_there_is_room()
    {
        var full = LoadPlanner.PlaceInto(
            Placed(Stop("Riker", "Gold", 16, CrateSize.Sixteen), Stop("Shubin", "Gold", 16, CrateSize.Sixteen),
                Stop("Orison", "Gold", 16, CrateSize.Sixteen), Stop("Lorville", "Gold", 16, CrateSize.Sixteen)),
            HullA, [Stop("Hurston", "Gold", 16, CrateSize.Sixteen)]);

        var retried = LoadPlanner.RetryUnplaced(Delivered(full, "Riker"), HullA);

        retried.Missions[^1].Unplaced.Should().BeFalse();
        RacksOf(retried, "Hurston").Should().Equal("rack-0");
        LoadPlanner.Validate(retried.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void RetryUnplaced_without_room_returns_the_same_plan()
    {
        var saved = Placed(Stop("Riker", "Gold", 4, CrateSize.Four));

        LoadPlanner.RetryUnplaced(saved, HullA).Should().BeSameAs(saved);
    }

    // --- RemoveMission ---

    [Fact]
    public void RemoveMission_takes_out_its_crates_and_moves_nothing_else()
    {
        var saved = Delivered(Placed(
            Stop("Riker", "Titanium", 8, CrateSize.Four), Stop("Shubin", "Quartz", 8, CrateSize.Four), Stop("Riker", "Gold", 4, CrateSize.Four)), "Shubin");

        var removed = LoadPlanner.RemoveMission(saved, HullA, 1);

        removed.Missions.Select(m => m.Id).Should().Equal(2, 3);
        removed.Plan.Crates.Should().Equal(saved.Plan.Crates.Where(c => c.Mission != 1));
    }

    [Fact]
    public void RemoveMission_drops_an_unplaced_mission()
    {
        var saved = LoadPlanner.PlaceInto(HalfFull(), HullA, [Stop("Hurston", "Gold", 32, CrateSize.ThirtyTwo)]);

        LoadPlanner.RemoveMission(saved, HullA, saved.Missions[^1].Id).Missions.Should().HaveCount(4);
    }

    [Fact]
    public void RemoveMission_rejects_an_unknown_mission()
    {
        var act = () => LoadPlanner.RemoveMission(Placed(Stop("Riker", "Gold", 4, CrateSize.Four)), HullA, 9);

        act.Should().Throw<ArgumentException>().WithMessage("*mission 9*");
    }

    // --- Plan with missions ---

    [Fact]
    public void Plan_with_missions_shares_a_destination_and_assigns_every_crate_its_mission()
    {
        Mission[] missions =
        [
            new(1, [Stop("Riker", "Titanium", 8, CrateSize.Four)]),
            new(2, [Stop("Riker", "Titanium", 4, CrateSize.Four), Stop("Shubin", "Quartz", 4, CrateSize.Two) with { Order = 2 }], Unplaced: true),
        ];

        var saved = LoadPlanner.Plan(HullA, missions, ["Riker", "Shubin"], FillOrder.BowFirst, 7);

        saved.Missions.Should().OnlyContain(m => !m.Unplaced);
        saved.Watermark.Should().Be(7);
        saved.Plan.Crates.Count(c => c.Mission == 1).Should().Be(2);
        saved.Plan.Crates.Count(c => c.Mission == 2).Should().Be(3);
        saved.Plan.UnloadOrder.Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void Plan_with_missions_follows_the_route_given()
    {
        Mission[] missions = [new(1, [Stop("Riker", "Titanium", 4, CrateSize.Four), Stop("Orison", "Gold", 4, CrateSize.Four) with { Order = 2 }]), new(2, [Stop("Shubin", "Quartz", 4, CrateSize.Four)])];

        LoadPlanner.Plan(HullA, missions, ["Riker", "Shubin", "Orison"]).Plan.UnloadOrder.Should().Equal("Riker", "Shubin", "Orison");
    }

    [Fact]
    public void Plan_with_missions_rejects_a_route_missing_a_destination()
    {
        var act = () => LoadPlanner.Plan(HullA, [new Mission(1, [Stop("Riker", "Titanium", 4, CrateSize.Four)])], ["Shubin"]);

        act.Should().Throw<ArgumentException>().WithParameterName("route");
    }
}
