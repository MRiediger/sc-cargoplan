using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A scanned contract into the plan (T10.2b2, D103): by its content key a new mission, an update of the one with that key,
/// or nothing new. The game rolls the amounts again on accepting (S10.0); the accepted view's amounts win.
/// </summary>
public class ScanSyncTests
{
    private const string Key = "EVERUSHARBOR | RIKER:STIMS | 4 | 50250";

    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    private static MissionStop Stop(string destination, int scu, string commodity = "Stims") =>
        new(destination, 1, [new MissionItem(commodity, scu, CrateSize.Four)]);

    /// <summary>A scan as the import hands it over (ScannedContract.ToCapture): the number is the one a new mission would get.</summary>
    private static ContractCapture Scan(bool estimated, int no = 9, string key = Key, params MissionStop[] stops) =>
        new(stops.Length == 0 ? [Stop("Riker", 8)] : stops, ContractSource.Scanned(new ScanIdentity(no, key, estimated), reward: 50_250));

    /// <summary>Orison typed, then the Riker contract as scan #2.</summary>
    private static SavedPlan WithScan(bool estimated, params MissionStop[] stops)
    {
        var typed = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Orison", 4, "Gold")]);
        return ScanSync.Apply(typed, HullA, Scan(estimated, 2, Key, stops), 2).Saved;
    }

    private static Mission Riker(SavedPlan saved) => saved.Missions.Single(m => m.Contract?.Scan is not null);

    private static IEnumerable<Crate> CratesOf(SavedPlan saved, Mission mission) => saved.Plan.Crates.Where(c => c.Mission == mission.Id);

    [Fact]
    public void A_new_key_is_a_new_mission_with_the_next_number()
    {
        var merge = ScanSync.Apply(Empty, HullA, Scan(estimated: true), nextNo: 4);

        merge.Change.Should().Be(ScanChange.Added);
        merge.ScanNo.Should().Be(4);
        Riker(merge.Saved).Contract!.Scan.Should().Be(new ScanIdentity(4, Key, true));
        CratesOf(merge.Saved, Riker(merge.Saved)).Sum(c => (int)c.CrateSize).Should().Be(8);
    }

    [Fact]
    public void An_offer_scanned_after_the_accepted_contract_changes_nothing()
    {
        var saved = WithScan(estimated: false);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: true, stops: Stop("Riker", 12)), nextNo: 3);

        merge.Change.Should().Be(ScanChange.AlreadyInPlan);
        merge.ScanNo.Should().Be(2);
        merge.Saved.Should().BeSameAs(saved);
    }

    [Fact]
    public void The_accepted_contract_with_the_offers_amounts_only_ends_the_estimate()
    {
        var saved = WithScan(estimated: true);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: false), nextNo: 3);

        merge.Change.Should().Be(ScanChange.Updated);
        merge.QuantitiesChanged.Should().BeFalse();
        Riker(merge.Saved).Contract!.Scan!.QuantitiesEstimated.Should().BeFalse();
        merge.Saved.Plan.Crates.Should().Equal(saved.Plan.Crates);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_same_amounts_again_are_already_in_the_plan(bool estimated)
    {
        var saved = WithScan(estimated);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated), nextNo: 3);

        merge.Change.Should().Be(ScanChange.AlreadyInPlan);
        merge.Saved.Should().BeSameAs(saved);
    }

    [Fact]
    public void Other_amounts_with_nothing_on_board_lay_this_mission_anew_and_no_other()
    {
        var saved = WithScan(estimated: true);
        var orison = saved.Missions[0];

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: false, stops: Stop("Riker", 12)), nextNo: 3);

        merge.Change.Should().Be(ScanChange.Updated);
        merge.QuantitiesChanged.Should().BeTrue();
        merge.Saved.Missions.Select(m => m.Id).Should().Equal(saved.Missions.Select(m => m.Id), "same id, same place in the list");
        Riker(merge.Saved).Contract!.Scan.Should().Be(new ScanIdentity(2, Key, false));
        CratesOf(merge.Saved, Riker(merge.Saved)).Sum(c => (int)c.CrateSize).Should().Be(12);
        CratesOf(merge.Saved, orison).Should().Equal(CratesOf(saved, orison));
    }

    [Fact]
    public void A_smaller_amount_than_already_loaded_plans_like_the_editor_and_names_the_delivery()
    {
        // Martin: with loaded crates PlanKeeping decides (T10.2b2). Riker 8 loaded, the accepted contract says 4: the crates
        // no longer match the item, so they are planned again and the delivery is named.
        var scanned = WithScan(estimated: true);
        SavedPlan saved = new(scanned.Missions, LoadPlanner.SetLoaded(scanned.Plan, HullA, "Riker", Riker(scanned).Id, true), scanned.CrateCap);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: false, stops: Stop("Riker", 4)), nextNo: 3);

        merge.Change.Should().Be(ScanChange.Updated);
        merge.QuantitiesChanged.Should().BeTrue();
        merge.Cleared.Should().ContainSingle().Which.Should().StartWith("Riker");
        CratesOf(merge.Saved, Riker(merge.Saved)).Sum(c => (int)c.CrateSize).Should().Be(4);
        LoadPlanner.Validate(merge.Saved.Plan, HullA).Should().BeEmpty();
    }

    [Fact]
    public void Loaded_crates_that_still_match_stay_loaded_where_they_lie()
    {
        // Riker loaded, Shubin of the same contract grows: Riker's crates stay as they lie, Shubin is laid around them.
        var scanned = WithScan(estimated: true, Stop("Riker", 8), Stop("Shubin", 4));
        SavedPlan saved = new(scanned.Missions, LoadPlanner.SetLoaded(scanned.Plan, HullA, "Riker", Riker(scanned).Id, true), scanned.CrateCap);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: false, stops: [Stop("Riker", 8), Stop("Shubin", 8)]), nextNo: 3);

        merge.Change.Should().Be(ScanChange.Updated);
        merge.Cleared.Should().BeEmpty();
        merge.Saved.Plan.Crates.Where(c => c.Loaded).Should().Equal(saved.Plan.Crates.Where(c => c.Loaded));
    }

    [Fact]
    public void An_update_that_finds_no_room_is_refused_and_the_plan_stays()
    {
        // Riker loaded, the rest of the mission grows past the Hull A's 64 SCU: PlanKeeping can't place it (K6.4, E1).
        var scanned = WithScan(estimated: true, Stop("Riker", 8), Stop("Shubin", 4));
        SavedPlan saved = new(scanned.Missions, LoadPlanner.SetLoaded(scanned.Plan, HullA, "Riker", Riker(scanned).Id, true), scanned.CrateCap);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: false, stops: [Stop("Riker", 8), Stop("Shubin", 60)]), nextNo: 3);

        merge.Change.Should().Be(ScanChange.Refused);
        merge.Refusal.Should().NotBeNullOrWhiteSpace();
        merge.Saved.Should().BeSameAs(saved);
    }

    [Fact]
    public void An_item_added_by_hand_stays_with_the_updated_mission()
    {
        var scanned = WithScan(estimated: true);
        var riker = Riker(scanned);
        Mission withManual = new(riker.Id, [new MissionStop("Riker", 1, [.. riker.Stops[0].Items, new MissionItem("Quartz", 2, CrateSize.Two, Manual: true)])], riker.Contract);
        var saved = LoadPlanner.ReplaceMission(scanned, HullA, riker.Id, withManual.Stops, withManual.Contract);

        var merge = ScanSync.Apply(saved, HullA, Scan(estimated: false, stops: Stop("Riker", 12)), nextNo: 3);

        merge.Change.Should().Be(ScanChange.Updated);
        Riker(merge.Saved).Stops.Single().Items.Select(i => (i.Commodity, i.Scu, i.Manual)).Should().Equal(("Stims", 12, false), ("Quartz", 2, true));
    }

    [Fact]
    public void ReplaceMission_keeps_the_id_and_the_place_in_the_list()
    {
        var two = LoadPlanner.PlaceInto(LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker", 4)]), HullA, [Stop("Orison", 4, "Gold")]);

        var replaced = LoadPlanner.ReplaceMission(two, HullA, 1, [Stop("Riker", 8)], null);

        replaced.Missions.Select(m => m.Id).Should().Equal(1, 2);
        replaced.MissionOf(1).Stops.Single().Items.Single().Scu.Should().Be(8);
        replaced.Plan.Crates.Where(c => c.Mission == 2).Should().Equal(two.Plan.Crates.Where(c => c.Mission == 2));
    }
}
