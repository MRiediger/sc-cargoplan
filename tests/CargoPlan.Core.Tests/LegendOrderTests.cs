using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>The legend's order lives in Core (B6, D69), so the sections of a plan can follow what the player sees (D89).</summary>
public class LegendOrderTests
{
    private static Crate Of(string destination, int mission) =>
        new(destination, "Titanium", CrateSize.One, "rack-0", new GridPosition(0, 0, 0), Mission: mission);

    [Fact]
    public void Martins_RAFT_plan_reads_in_the_order_of_his_legend()
    {
        var raft = FrozenShips.Load("raft");
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");
        var saved = PlanFile.Read(stream, "raft", raft);

        saved.Plan.LegendDestinations([.. saved.Missions.Select(m => m.Id)]).Should().Equal(
            "Green Glade Station", "High Course Station", "Thundering Express Station", "Faithful Dream Station", "Melodic Fields Station");
    }

    [Fact]
    public void A_mission_brings_all_its_destinations_along()
    {
        // Route A, B, C, D; mission 1 goes to A and D, so D reads before B, as the legend shows it.
        LoadPlan plan = new("raft", [Of("A", 1), Of("B", 2), Of("C", 2), Of("D", 1)], []);

        plan.Route.Should().Equal("A", "B", "C", "D");
        plan.LegendDestinations([1, 2]).Should().Equal("A", "D", "B", "C");
    }

    [Fact]
    public void Missions_starting_at_one_destination_stand_in_mission_order()
    {
        LoadPlan plan = new("raft", [Of("A", 2), Of("A", 1), Of("B", 1), Of("C", 2)], []);

        plan.LegendMissions([1, 2]).Select(m => m.Mission).Should().Equal(1, 2);
        plan.LegendDestinations([1, 2]).Should().Equal("A", "B", "C");
        plan.LegendDestinations([2, 1]).Should().Equal("A", "C", "B");
    }

    [Fact]
    public void A_line_holds_its_crates_for_one_destination()
    {
        LoadPlan plan = new("raft", [Of("A", 1), Of("B", 1), Of("A", 1)], []);

        plan.LegendMissions([1]).Single().Lines.Select(l => (l.Destination, l.Crates.Count)).Should().Equal(("A", 2), ("B", 1));
    }

    [Fact]
    public void Missions_stand_in_the_given_order_and_those_it_does_not_name_behind_in_route_order()
    {
        // B16: the order is the caller's; a plan without missions (empty order) keeps the route rule of D69.
        LoadPlan plan = new("raft", [Of("A", 3), Of("B", 1), Of("C", 2)], []);

        plan.LegendMissions([2]).Select(m => m.Mission).Should().Equal(2, 3, 1);
        plan.LegendMissions([]).Select(m => m.Mission).Should().Equal(3, 1, 2);
    }

    // --- Scan order (B16): new missions at the bottom, stable through everything that isn't a new scan ---

    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan EmptyOn(Ship ship, PlanGrouping grouping = PlanGrouping.None, FillRows rows = FillRows.Lengthwise) =>
        new([], new LoadPlan(ship.Id, [], [], FillRows: rows, Grouping: grouping), CrateCap.Of(ship));

    private static MissionStop To(string destination, int order = 1) => new(destination, order, [new MissionItem("Stims", 4, CrateSize.Four)]);

    private static SavedPlan Scan(SavedPlan saved, Ship ship, int no, params string[] destinations) => LoadPlanner.PlaceInto(
        saved, ship, [.. destinations.Select((d, i) => To(d, i + 1))], new ContractSource(new ScanIdentity(no, $"KEY {no}", false)));

    private static SavedPlan Typed(SavedPlan saved, string destination) => LoadPlanner.PlaceInto(saved, HullA, [To(destination)]);

    /// <summary>The legend top to bottom by scan number, 0 for a typed mission.</summary>
    private static int[] Legend(SavedPlan saved) =>
        [.. saved.Plan.LegendMissions(saved.LegendOrder).Select(m => saved.MissionOf(m.Mission).Contract?.Scan.No ?? 0)];

    /// <summary>The case of the report: scan #3 goes where #1 goes, so before B16 it stood above #2.</summary>
    private static SavedPlan ThreeScans(Ship ship, SavedPlan? empty = null) =>
        Scan(Scan(Scan(empty ?? EmptyOn(ship), ship, 1, "A"), ship, 2, "B"), ship, 3, "A", "C");

    private static int MissionOfScan(SavedPlan saved, int no) => saved.Missions.Single(m => m.Contract?.Scan.No == no).Id;

    [Fact]
    public void Three_scans_read_in_scan_order_also_when_a_later_one_goes_where_an_earlier_one_goes()
    {
        Legend(ThreeScans(HullA)).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Re_plan_tick_plan_file_and_the_editors_plan_keep_the_order()
    {
        var saved = ThreeScans(HullA);

        Legend(LoadPlanner.Replan(saved, HullA)).Should().Equal(1, 2, 3);

        var ticked = new SavedPlan(saved.Missions, LoadPlanner.SetDelivered(saved.Plan, HullA, "B", MissionOfScan(saved, 2), true), saved.CrateCap);
        Legend(ticked).Should().Equal(1, 2, 3);

        using var stream = new MemoryStream();
        PlanFile.Write(stream, ticked, 3);
        stream.Position = 0;
        Legend(PlanFile.Read(stream, "plan.json", HullA)).Should().Equal(1, 2, 3);

        // The editor's Plan hands the missions over in route order (MissionRows.ToMissions); the legend doesn't follow it.
        Mission[] inRouteOrder = [.. saved.Missions.OrderBy(m => m.Contract!.Scan.No == 2)];
        Legend(LoadPlanner.Plan(HullA, inRouteOrder, [.. saved.Stops.Select(s => s.Destination)])).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void A_removed_mission_closes_the_gap_and_the_next_scan_goes_to_the_bottom()
    {
        var removed = LoadPlanner.RemoveMission(ThreeScans(HullA), HullA, MissionOfScan(ThreeScans(HullA), 2));

        Legend(removed).Should().Equal(1, 3);
        Legend(Scan(removed, HullA, 4, "B")).Should().Equal(1, 3, 4);
    }

    [Fact]
    public void A_typed_mission_stands_behind_every_scanned_one()
    {
        var saved = Scan(Typed(Scan(EmptyOn(HullA), HullA, 1, "A"), "B"), HullA, 2, "C");

        Legend(saved).Should().Equal(1, 2, 0);
    }

    [Fact]
    public void Sections_follow_the_legend_in_scan_order()
    {
        // F1: one order. RAFT crosswise from the bow: the first section lies at the bow (highest length rows).
        var raft = FrozenShips.Load("raft");
        var scanned = ThreeScans(raft, EmptyOn(raft, PlanGrouping.ByDestination, FillRows.Crosswise));
        var saved = LoadPlanner.Plan(
            raft, scanned.Missions, [.. scanned.Stops.Select(s => s.Destination)], FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination);

        saved.Plan.LegendDestinations(saved.LegendOrder).Should().Equal("A", "B", "C");
        int[] bowmost = [.. new[] { "A", "B", "C" }.Select(d => saved.Plan.Crates.Where(c => c.Destination == d).SelectMany(c => c.Cells).Max(c => c.Length))];
        bowmost.Should().BeInDescendingOrder().And.OnlyHaveUniqueItems();
    }
}
