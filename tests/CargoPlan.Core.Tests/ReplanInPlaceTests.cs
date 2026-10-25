using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// The player's Re-plan (B4, D66): the plan on its own ship laid out anew, as a fresh Plan of the same missions would lay
/// it; ticked crates stay where they lay and take no room (D24), split and typed crates keep their sizes.
/// </summary>
public class ReplanInPlaceTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max) =>
        new(destination, 1, [new MissionItem(commodity, scu, max)]);

    /// <summary>Each stop a mission of its own, placed one after the other as captures are: the layout grows (D41).</summary>
    private static SavedPlan Grown(params MissionStop[] stops) =>
        stops.Aggregate(new SavedPlan([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA)), (saved, stop) => LoadPlanner.PlaceInto(saved, HullA, [stop]));

    private static SavedPlan Ticked(SavedPlan saved, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, true), saved.CrateCap, saved.Watermark);

    private static string[] Route(SavedPlan saved) => [.. saved.Stops.Select(s => s.Destination)];

    private static SavedPlan Fresh(SavedPlan saved) =>
        LoadPlanner.Plan(HullA, saved.Missions, Route(saved), saved.Plan.FillOrder, saved.Watermark);

    /// <summary>A fresh plan of the missions of <paramref name="saved"/> that <paramref name="keep"/> picks, in its route.</summary>
    private static SavedPlan Fresh(SavedPlan saved, Func<Mission, bool> keep)
    {
        Mission[] missions = [.. saved.Missions.Where(keep)];
        HashSet<string> destinations = [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination)];
        return LoadPlanner.Plan(HullA, missions, [.. Route(saved).Where(destinations.Contains)], saved.Plan.FillOrder);
    }

    private static IEnumerable<(string, string, CrateSize, string, int, int, int)> Lying(IEnumerable<Crate> crates) =>
        crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId, c.Position.Length, c.Position.Width, c.Position.Stack)).Order();

    private static IEnumerable<(string, string, CrateSize)> Kinds(SavedPlan saved) =>
        saved.Plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize)).Order();

    public static TheoryData<string> Fixtures =>
        ["example_missions", "split_and_shared", "split_required", "crate_mix", "hull_a_eight_stops", "hull_a_hurston_2026-10-24"];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Replan_on_its_own_ship_is_the_fresh_plan_of_the_same_missions(string fixture)
    {
        var grown = Grown([.. MissionFixtures.LoadMissions(fixture)]);

        var after = LoadPlanner.Replan(grown, HullA);

        var fresh = Fresh(grown);
        after.Plan.Crates.Should().Equal(fresh.Plan.Crates);
        after.Plan.Warnings.Should().Equal(fresh.Plan.Warnings);
        after.Watermark.Should().Be(grown.Watermark);
    }

    [Fact]
    public void Replan_keeps_fill_order_and_watermark()
    {
        var grown = Grown(Stop("Riker", "Titanium", 12, CrateSize.Four), Stop("Shubin", "Quartz", 8, CrateSize.Four));
        SavedPlan stern = new(grown.Missions, grown.Plan with { FillOrder = FillOrder.SternFirst }, grown.CrateCap, 41);

        var after = LoadPlanner.Replan(stern, HullA);

        after.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        after.Watermark.Should().Be(41);
        after.Plan.Crates.Should().Equal(Fresh(stern).Plan.Crates);
    }

    [Fact]
    public void Replan_leaves_a_ticked_stop_where_it_lay_and_plans_the_rest_as_if_it_were_gone()
    {
        var saved = Grown([.. MissionFixtures.LoadMissions("hull_a_eight_stops")]);
        var ticked = Ticked(saved, "Lorville");

        var after = LoadPlanner.Replan(ticked, HullA);

        after.Plan.Crates.Where(c => c.Delivered).Should().Equal(ticked.Plan.Crates.Where(c => c.Delivered));
        var rest = Fresh(saved, m => m.Stops.All(s => s.Destination != "Lorville"));
        Lying(after.Plan.Crates.Where(c => !c.Delivered)).Should().Equal(Lying(rest.Plan.Crates));
        after.Plan.Route.Should().Equal(ticked.Plan.Route);
    }

    [Fact]
    public void Replan_keeps_the_tick_of_a_stop_with_a_delivered_visit_and_new_cargo()
    {
        // D41: Riker's 8 delivered, then a new contract for Riker; the stop is half ticked.
        var saved = Ticked(Grown(Stop("Riker", "Titanium", 8, CrateSize.Eight)), "Riker");
        saved = LoadPlanner.PlaceInto(saved, HullA, [Stop("Riker", "Quartz", 4, CrateSize.Four)]);

        var after = LoadPlanner.Replan(saved, HullA);

        after.Plan.Crates.Where(c => c.Delivered).Should().Equal(saved.Plan.Crates.Where(c => c.Delivered));
        after.Plan.Crates.Should().ContainSingle(c => !c.Delivered && c.Commodity == "Quartz");
    }

    [Fact]
    public void Replan_gives_ticked_cells_to_a_waiting_mission()
    {
        // 64 SCU on board, Orison waits; Shubin's 16 delivered: room for Orison only if ticked crates take none.
        var full = Grown(Stop("Riker", "Titanium", 48, CrateSize.Sixteen), Stop("Shubin", "Quartz", 16, CrateSize.Sixteen), Stop("Orison", "Gold", 16, CrateSize.Sixteen));
        full.Missions[2].Unplaced.Should().BeTrue();
        var ticked = Ticked(full, "Shubin");

        var after = LoadPlanner.Replan(ticked, HullA);

        after.Missions.Should().OnlyContain(m => !m.Unplaced);
        after.Plan.Crates.Where(c => c.Destination == "Shubin").Should().Equal(ticked.Plan.Crates.Where(c => c.Destination == "Shubin"));
        LoadPlanner.Validate(after.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Replan_keeps_split_crates_and_typed_crate_lists()
    {
        var saved = Grown([.. MissionFixtures.LoadMissions("crate_mix")]);
        var crate = saved.Plan.Crates.First(c => c.CrateSize == CrateSize.Four);
        var split = Ticked(LoadPlanner.SplitCrate(saved, HullA, crate), "Riker");

        var after = LoadPlanner.Replan(split, HullA);

        Kinds(after).Should().Equal(Kinds(split));
        after.Missions.Should().BeSameAs(split.Missions);
    }

    [Fact]
    public void Replan_places_a_third_mission_the_grown_layout_has_no_room_for()
    {
        // Two contracts of two destinations each: one destination per rack, every rack half full (D41, D64). The third
        // contract's one 16 finds no whole rack although 32 SCU are free (fragmentation).
        SavedPlan empty = new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));
        var grown = LoadPlanner.PlaceInto(empty, HullA, [Stop("A", "Titanium", 8, CrateSize.Eight), Stop("B", "Quartz", 8, CrateSize.Eight) with { Order = 2 }]);
        grown = LoadPlanner.PlaceInto(grown, HullA, [Stop("C", "Gold", 8, CrateSize.Eight), Stop("D", "Stims", 8, CrateSize.Eight) with { Order = 2 }]);
        grown = LoadPlanner.PlaceInto(grown, HullA, [Stop("E", "Agricium", 16, CrateSize.Sixteen)]);
        grown.Plan.Crates.Select(c => c.RackId).Should().OnlyHaveUniqueItems();
        grown.Missions[2].Unplaced.Should().BeTrue();
        LoadPlanner.PlaceProblem(grown, HullA, grown.Missions[2].Id).Should().Be("doesn't fit (no room for 16 SCU crates)");
        LoadPlanner.RetryUnplaced(grown, HullA).Should().BeSameAs(grown);

        var after = LoadPlanner.Replan(grown, HullA);

        after.Missions.Should().OnlyContain(m => !m.Unplaced);
        LoadPlanner.Validate(after.Plan, HullA).Should().BeEmpty();
        after.Plan.Route.Should().Equal("A", "B", "C", "D", "E");
    }

    [Fact]
    public void Replan_without_room_for_a_waiting_mission_lays_the_rest_anew_and_keeps_it_waiting()
    {
        var full = Grown(Stop("Riker", "Titanium", 48, CrateSize.Sixteen), Stop("Shubin", "Quartz", 16, CrateSize.Sixteen), Stop("Orison", "Gold", 16, CrateSize.Sixteen));

        var after = LoadPlanner.Replan(full, HullA);

        after.Missions.Should().BeSameAs(full.Missions);
        after.Plan.Crates.Should().Equal(Fresh(full, m => !m.Unplaced).Plan.Crates);
    }

    [Fact]
    public void Replan_of_a_plan_already_laid_out_moves_nothing()
    {
        var stops = MissionFixtures.LoadMissions("hull_a_eight_stops");
        var canonical = SavedPlan.FromStops(stops, LoadPlanner.Plan(HullA, stops.ToStops()), CrateCap.Of(HullA));

        var after = LoadPlanner.Replan(canonical, HullA);

        LoadPlanner.MovedCrates(canonical.Plan, after.Plan).Should().Be(0);
        Lying(after.Plan.Crates).Should().Equal(Lying(canonical.Plan.Crates));
        after.Missions.Should().BeSameAs(canonical.Missions);
    }

    [Fact]
    public void MovedCrates_counts_crates_on_board_whose_rack_or_cell_changed()
    {
        var saved = Grown(Stop("Riker", "Titanium", 4, CrateSize.One), Stop("Shubin", "Quartz", 2, CrateSize.Two));
        var crate = saved.Plan.Crates.First(c => c.Destination == "Riker");
        var moved = LoadPlanner.MoveCrate(saved.Plan, HullA, crate, "rack-3", new GridPosition(3, 1, 0));

        LoadPlanner.MovedCrates(saved.Plan, moved).Should().Be(1);
        LoadPlanner.MovedCrates(saved.Plan, saved.Plan).Should().Be(0);
    }

    [Fact]
    public void MovedCrates_ignores_delivered_crates_and_crates_new_on_board()
    {
        var saved = Grown(Stop("Riker", "Titanium", 4, CrateSize.Four));
        var ticked = Ticked(saved, "Riker");
        var added = LoadPlanner.PlaceInto(ticked, HullA, [Stop("Shubin", "Quartz", 4, CrateSize.Four)]);

        LoadPlanner.MovedCrates(saved.Plan, ticked.Plan).Should().Be(0);
        LoadPlanner.MovedCrates(ticked.Plan, added.Plan).Should().Be(0);
    }

    [Fact]
    public void A_sync_after_a_replan_finds_the_same_missions()
    {
        // B1/K2: the content key and the contract ids don't change with the layout.
        ContractCapture[] captures =
        [
            new([Stop("X", "Titanium", 24, CrateSize.Eight)], new ContractSource([36])),
            new([Stop("Y", "Quartz", 24, CrateSize.Eight)], new ContractSource([37])),
            new([Stop("Z", "Gold", 16, CrateSize.Sixteen)], new ContractSource([38])),
        ];
        var synced = ContractSync.Apply(new SavedPlan([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA)), HullA, captures, null);

        var replanned = LoadPlanner.Replan(synced, HullA);
        var again = ContractSync.Apply(replanned, HullA, captures, new HashSet<long>([36, 37, 38]));

        again.Missions.Select(m => (m.Id, m.Contract)).Should().Equal(replanned.Missions.Select(m => (m.Id, m.Contract)));
        again.Plan.Crates.Should().Equal(replanned.Plan.Crates);
        ContractSync.Conflicts(again, new HashSet<long>([36, 37, 38])).Should().BeEmpty();
    }
}
