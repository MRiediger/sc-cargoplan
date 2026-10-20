using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>Taking ContractTracker captures into the plan (D39 addendum, D41) and the duplicate rule.</summary>
public class ContractSyncTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []));

    private static MissionStop Stop(string destination, string commodity = "Stims", int scu = 4, CrateSize max = CrateSize.Four) =>
        new(destination, 1, [new MissionItem(commodity, scu, max)]);

    /// <summary>A capture with these ids going to these stops, in contract order.</summary>
    private static ContractCapture Capture(long[] ids, params MissionStop[] stops) =>
        new([.. stops.Select((s, i) => s with { Order = i + 1 })], new ContractSource(ids));

    private static SavedPlan Applied(SavedPlan saved, params ContractCapture[] captures) =>
        ContractSync.Apply(saved, HullA, captures, null);

    private static SavedPlan Delivered(SavedPlan saved, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, true), saved.Watermark);

    private static HashSet<long> Present(params long[] ids) => [.. ids];

    // --- Apply ---

    [Fact]
    public void Apply_places_each_capture_as_a_mission_and_raises_the_watermark()
    {
        var saved = Applied(Empty, Capture([36], Stop("Riker")), Capture([37, 38], Stop("Shubin"), Stop("Orison")));

        saved.Missions.Select(m => string.Join("+", m.Contract!.ContractIds)).Should().Equal("36", "37+38");
        saved.Watermark.Should().Be(38);
    }

    [Fact]
    public void Apply_takes_captures_in_id_order()
    {
        var saved = Applied(Empty, Capture([40], Stop("Shubin")), Capture([39], Stop("Riker")));

        saved.Plan.UnloadOrder.Should().Equal("Riker", "Shubin");
    }

    [Fact]
    public void Apply_ignores_captures_at_or_below_the_watermark()
    {
        SavedPlan saved = new([], new LoadPlan(HullA.Id, [], []), 40);

        Applied(saved, Capture([40], Stop("Riker"))).Should().BeSameAs(saved);
    }

    [Fact]
    public void Apply_without_anything_new_returns_the_same_plan()
    {
        var saved = Applied(Empty, Capture([36], Stop("Riker")));

        ContractSync.Apply(saved, HullA, [], Present(36)).Should().BeSameAs(saved);
    }

    [Fact]
    public void Apply_removes_a_mission_deleted_in_ContractTracker_once_it_is_delivered()
    {
        var saved = Delivered(Applied(Empty, Capture([36], Stop("Riker")), Capture([37], Stop("Shubin"))), "Riker");

        var synced = ContractSync.Apply(saved, HullA, [], Present(37));

        synced.Missions.Select(m => m.Contract!.ContractIds[0]).Should().Equal(37);
        synced.Plan.Crates.Should().OnlyContain(c => c.Destination == "Shubin");
    }

    [Fact]
    public void Apply_keeps_a_deleted_mission_with_crates_on_board_and_names_it_a_conflict()
    {
        var saved = Applied(Empty, Capture([36], Stop("Riker")));

        var synced = ContractSync.Apply(saved, HullA, [], Present());

        synced.Should().BeSameAs(saved);
        ContractSync.Conflicts(synced, Present()).Should().Equal(1);
    }

    [Fact]
    public void Apply_removes_a_deleted_mission_that_didnt_fit()
    {
        var saved = Applied(Empty, Capture([36], Stop("Riker", "Gold", 32, CrateSize.ThirtyTwo)));

        ContractSync.Apply(saved, HullA, [], Present()).Missions.Should().BeEmpty();
    }

    [Fact]
    public void Apply_removes_nothing_when_ContractTracker_wasnt_read()
    {
        // A missing file, a busy database or another schema is "not read", not "deleted" (T5.4).
        var saved = Delivered(Applied(Empty, Capture([36], Stop("Riker"))), "Riker");

        ContractSync.Apply(saved, HullA, [], null).Should().BeSameAs(saved);
    }

    [Fact]
    public void Apply_keeps_a_capture_while_one_of_its_ids_is_left()
    {
        // The 2 + 1 split of one capture: deleting one row is no deleted contract.
        var saved = Delivered(Applied(Empty, Capture([28, 29], Stop("Riker"))), "Riker");

        ContractSync.Apply(saved, HullA, [], Present(29)).Should().BeSameAs(saved);
    }

    [Fact]
    public void Apply_never_removes_a_typed_mission()
    {
        var saved = Delivered(LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker")]), "Riker");

        ContractSync.Apply(saved, HullA, [], Present()).Should().BeSameAs(saved);
    }

    [Fact]
    public void Apply_places_a_waiting_mission_first_once_there_is_room()
    {
        var full = Applied(Empty,
            Capture([1], Stop("Riker", "Gold", 16, CrateSize.Sixteen)), Capture([2], Stop("Shubin", "Gold", 16, CrateSize.Sixteen)),
            Capture([3], Stop("Orison", "Gold", 16, CrateSize.Sixteen)), Capture([4], Stop("Lorville", "Gold", 16, CrateSize.Sixteen)),
            Capture([5], Stop("Hurston", "Gold", 16, CrateSize.Sixteen)));
        full.Missions[^1].Unplaced.Should().BeTrue();

        var synced = ContractSync.Apply(Delivered(full, "Riker"), HullA, [Capture([6], Stop("Area18", "Gold", 16, CrateSize.Sixteen))], null);

        synced.MissionOf(5).Unplaced.Should().BeFalse();
        synced.MissionOf(6).Unplaced.Should().BeTrue();
    }

    [Fact]
    public void Conflicts_ignore_missions_without_crates_on_board()
    {
        var saved = Delivered(Applied(Empty, Capture([36], Stop("Riker"))), "Riker");

        ContractSync.Conflicts(saved, Present()).Should().BeEmpty();
    }

    // --- Duplicates ---

    [Fact]
    public void Duplicates_mark_the_later_capture_whose_deliveries_are_part_of_an_earlier_one()
    {
        var saved = Applied(Empty, Capture([1], Stop("Riker"), Stop("Shubin")), Capture([2], Stop("Riker")));

        ContractSync.Duplicates(saved).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_mark_the_later_capture_that_holds_all_of_an_earlier_one()
    {
        // A retry after a capture that missed destinations: "⚠ 1 of 3 destinations" on the first, "duplicate?" on the second.
        var saved = Applied(Empty, Capture([1], Stop("Riker")), Capture([2], Stop("Riker"), Stop("Shubin"), Stop("Orison")));

        ContractSync.Duplicates(saved).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_ignore_contracts_that_only_overlap()
    {
        // Contracts 36 and 37/38 share two destinations and are both real (T5.4 planning).
        var saved = Applied(Empty,
            Capture([36], Stop("Riker"), Stop("Shubin"), Stop("Orison")), Capture([37, 38], Stop("Riker"), Stop("Shubin"), Stop("Lorville")));

        ContractSync.Duplicates(saved).Should().BeEmpty();
    }

    [Fact]
    public void Duplicates_need_the_same_commodity()
    {
        var saved = Applied(Empty, Capture([1], Stop("Riker", "Stims")), Capture([2], Stop("Riker", "Gold")));

        ContractSync.Duplicates(saved).Should().BeEmpty();
    }

    [Fact]
    public void Duplicates_compare_names_ignoring_case()
    {
        var saved = Applied(Empty, Capture([1], Stop("Riker", "Stims")), Capture([2], Stop("riker", "stims")));

        ContractSync.Duplicates(saved).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_go_with_the_first_tick_on_either_and_come_back_with_ticking_back()
    {
        var saved = Applied(Empty, Capture([1], Stop("Riker"), Stop("Shubin")), Capture([2], Stop("Shubin")));

        var ticked = Delivered(saved, "Riker");
        var back = new SavedPlan(ticked.Missions, LoadPlanner.SetDelivered(ticked.Plan, HullA, "Riker", false), ticked.Watermark);

        ContractSync.Duplicates(ticked).Should().BeEmpty();
        ContractSync.Duplicates(back).Should().Equal(2);
    }
}
