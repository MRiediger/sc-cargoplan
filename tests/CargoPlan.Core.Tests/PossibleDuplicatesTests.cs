using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>The legend's duplicate rule (D39 addendum): the net under the content key (D101).</summary>
public class PossibleDuplicatesTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    private static MissionStop Stop(string destination, string commodity = "Stims", int scu = 4, CrateSize max = CrateSize.Four) =>
        new(destination, 1, [new MissionItem(commodity, scu, max)]);

    /// <summary>A scan with its own key; the rule compares deliveries and rewards, never keys.</summary>
    private static ContractSource Scanned(int no, long? reward = null) =>
        ContractSource.Scanned(new ScanIdentity(no, $"KEY {no}", false), reward: reward);

    /// <summary>Scan <paramref name="no"/> going to these stops, in contract order.</summary>
    private static ContractCapture Scan(int no, params MissionStop[] stops) =>
        new([.. stops.Select((s, i) => s with { Order = i + 1 })], Scanned(no));

    /// <summary>The scans placed into the empty plan in this order, as missions 1, 2, …</summary>
    private static SavedPlan Placed(params ContractCapture[] scans) =>
        scans.Aggregate(Empty, (saved, scan) => LoadPlanner.PlaceInto(saved, HullA, scan.Stops, scan.Source));

    private static SavedPlan Delivered(SavedPlan saved, string destination) =>
        new(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, destination, true), saved.CrateCap);

    [Fact]
    public void Duplicates_mark_the_later_scan_whose_deliveries_are_part_of_an_earlier_one()
    {
        var saved = Placed(Scan(1, Stop("Riker"), Stop("Shubin")), Scan(2, Stop("Riker")));

        PossibleDuplicates.Of(saved).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_mark_the_later_scan_that_holds_all_of_an_earlier_one()
    {
        // A rescan after a scan that missed destinations: "⚠ 1 of 3 destinations" on the first, "duplicate?" on the second.
        var saved = Placed(Scan(1, Stop("Riker")), Scan(2, Stop("Riker"), Stop("Shubin"), Stop("Orison")));

        PossibleDuplicates.Of(saved).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_ignore_contracts_that_only_overlap()
    {
        // Two contracts that share two destinations are both real (T5.4 planning).
        var saved = Placed(
            Scan(36, Stop("Riker"), Stop("Shubin"), Stop("Orison")), Scan(37, Stop("Riker"), Stop("Shubin"), Stop("Lorville")));

        PossibleDuplicates.Of(saved).Should().BeEmpty();
    }

    [Fact]
    public void Duplicates_need_the_same_commodity()
    {
        var saved = Placed(Scan(1, Stop("Riker", "Stims")), Scan(2, Stop("Riker", "Gold")));

        PossibleDuplicates.Of(saved).Should().BeEmpty();
    }

    [Fact]
    public void Duplicates_compare_names_ignoring_case()
    {
        var saved = Placed(Scan(1, Stop("Riker", "Stims")), Scan(2, Stop("riker", "stims")));

        PossibleDuplicates.Of(saved).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_go_with_the_first_tick_on_either_and_come_back_with_ticking_back()
    {
        var saved = Placed(Scan(1, Stop("Riker"), Stop("Shubin")), Scan(2, Stop("Shubin")));

        var ticked = Delivered(saved, "Riker");
        var back = new SavedPlan(ticked.Missions, LoadPlanner.SetDelivered(ticked.Plan, HullA, "Riker", false), ticked.CrateCap);

        PossibleDuplicates.Of(ticked).Should().BeEmpty();
        PossibleDuplicates.Of(back).Should().Equal(2);
    }

    [Fact]
    public void Duplicates_ignore_contracts_with_different_rewards()
    {
        // Two real contracts, Waste to Everus Harbor, 50,250 and 72,500 (T5.3b).
        var saved = Placed(RewardScan(47, 50_250, Stop("Everus Harbor", "Waste")), RewardScan(48, 72_500, Stop("Everus Harbor", "Waste")));

        PossibleDuplicates.Of(saved).Should().BeEmpty();
    }

    [Theory]
    [InlineData(50_250L, 50_250L)]
    [InlineData(50_250L, null)]
    [InlineData(null, 50_250L)]
    [InlineData(null, null)]
    public void Duplicates_mark_a_subset_with_the_same_or_an_unknown_reward(long? first, long? second)
    {
        var saved = Placed(RewardScan(1, first, Stop("Everus Harbor", "Waste")), RewardScan(2, second, Stop("Everus Harbor", "Waste")));

        PossibleDuplicates.Of(saved).Should().Equal(2);
    }

    private static ContractCapture RewardScan(int no, long? reward, MissionStop stop) => new([stop], Scanned(no, reward));
}
