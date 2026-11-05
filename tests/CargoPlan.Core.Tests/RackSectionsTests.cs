using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>Sections per destination (D89): capacity first, then each destination a block of whole rows in the legend's order.</summary>
public class RackSectionsTests
{
    private const string Green = "Green Glade Station";
    private const string High = "High Course Station";
    private const string Thunder = "Thundering Express Station";
    private const string Faithful = "Faithful Dream Station";
    private const string Melodic = "Melodic Fields Station";

    private static readonly Ship Raft = ShipLoader.Load("raft");
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static SavedPlan Fixture()
    {
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");
        return PlanFile.Read(stream, "raft", Raft);
    }

    private static SavedPlan Planned(FillRows rows, FillOrder order = FillOrder.BowFirst, PlanGrouping grouping = PlanGrouping.ByDestination)
    {
        var saved = Fixture();
        return LoadPlanner.Plan(Raft, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], order, 0, rows, grouping);
    }

    /// <summary>Rows along the length of a destination's crates.</summary>
    private static int[] Lengths(LoadPlan plan, string destination) =>
        [.. plan.Crates.Where(c => c.Destination == destination).SelectMany(c => c.Cells).Select(c => c.Length).Distinct().OrderDescending()];

    /// <summary>Rows along the width of a destination's crates.</summary>
    private static int[] Widths(LoadPlan plan, string destination) =>
        [.. plan.Crates.Where(c => c.Destination == destination).SelectMany(c => c.Cells).Select(c => c.Width).Distinct().Order()];

    private static Stop StopOf(string destination, int order, params (string Commodity, int Scu, CrateSize Size)[] items) =>
        new(destination, order, [.. items.Select(i => new CargoItem(i.Commodity, i.Scu, i.Size))]);

    private static Ship OneRack(int length, int width, int stack, StackAxis axis = StackAxis.PlusZ)
    {
        Rack rack = new("rack-0", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0), axis);
        return new Ship("flat", "Flat", rack.CapacityScu, [rack]);
    }

    private static IEnumerable<string> Notes(LoadPlan plan) => plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message);

    // --- The goldens: Martin's missions, computed from their crates ---

    [Fact]
    public void Crosswise_lays_blocks_across_the_full_width_from_the_bow()
    {
        var plan = Planned(FillRows.Crosswise).Plan;

        Lengths(plan, Green).Should().Equal(11, 10);
        Lengths(plan, High).Should().Equal(9, 8);
        Lengths(plan, Thunder).Should().Equal(7, 6);
        Lengths(plan, Faithful).Should().Equal(5, 4);
        Lengths(plan, Melodic).Should().Equal(3, 2);
        Notes(plan).Should().BeEmpty();
    }

    [Fact]
    public void Crosswise_stern_first_starts_at_the_stern()
    {
        var plan = Planned(FillRows.Crosswise, FillOrder.SternFirst).Plan;

        Lengths(plan, Green).Should().Equal(1, 0);
        Lengths(plan, Melodic).Should().Equal(9, 8);
    }

    [Fact]
    public void Lengthwise_lays_strips_bow_to_stern_from_starboard()
    {
        var plan = Planned(FillRows.Lengthwise).Plan;

        Widths(plan, Green).Should().Equal(0, 1);
        Widths(plan, High).Should().Equal(2, 3);
        Widths(plan, Thunder).Should().Equal(4, 5);
        Widths(plan, Faithful).Should().Equal(6);
        Widths(plan, Melodic).Should().Equal(7);
        Notes(plan).Should().BeEmpty();
    }

    [Theory]
    [InlineData(FillRows.Lengthwise)]
    [InlineData(FillRows.Crosswise)]
    public void The_crates_of_the_RAFT_plan_lie_unturned_and_valid(FillRows rows)
    {
        var plan = Planned(rows).Plan;

        plan.Crates.Should().OnlyContain(c => !c.Rotated);
        CrateLayout.Problems(Raft.Racks[0], plan.Crates).Should().BeEmpty();
        CrateLayout.StackedOn(Raft.Racks[0], plan.Crates).Should().BeEmpty();
    }

    [Fact]
    public void Sections_keep_missions_items_and_the_crate_list()
    {
        var plain = Planned(FillRows.Crosswise, grouping: PlanGrouping.None).Plan;
        var plan = Planned(FillRows.Crosswise).Plan;

        plan.Grouping.Should().Be(PlanGrouping.ByDestination);
        plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.Mission, c.Item)).Should()
            .Equal(plain.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.Mission, c.Item)));
    }

    // --- The invariant: capacity first ---

    public static TheoryData<string, string> Fixtures => new()
    {
        { "example_missions", "hull_a" },
        { "split_and_shared", "hull_a" },
        { "split_required", "hull_a" },
        { "crate_mix", "hull_a" },
        { "hull_a_eight_stops", "hull_a" },
        { "hull_a_hurston_2026-10-24", "hull_a" },
        { "hull_a_eight_stops", "freelancer" },
        { "hull_a_eight_stops", "raft" },
        { "crate_mix", "raft" },
    };

    private static (string, string, CrateSize, string)[] Racks(LoadPlan plan) =>
        [.. plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId)).Order()];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Sections_change_neither_the_racks_nor_who_waits_in_a_plan_or_re_plan(string fixture, string shipId)
    {
        var ship = ShipLoader.Load(shipId);
        var stops = MissionFixtures.LoadMissions(fixture);
        Mission[] missions = [.. stops.Select((s, i) => new Mission(i + 1, [s with { Order = 1 }]))];
        string[] route = [.. stops.Select(s => s.Destination)];

        foreach (var rows in Enum.GetValues<FillRows>())
        {
            var plain = LoadPlanner.Plan(ship, missions, route, FillOrder.BowFirst, 0, rows);
            var sectioned = LoadPlanner.Plan(ship, missions, route, FillOrder.BowFirst, 0, rows, PlanGrouping.ByDestination);

            Racks(sectioned.Plan).Should().Equal(Racks(plain.Plan));
            sectioned.Missions.Select(m => m.Unplaced).Should().Equal(plain.Missions.Select(m => m.Unplaced));

            var replanned = LoadPlanner.Replan(sectioned, ship);
            Racks(replanned.Plan).Should().Equal(Racks(LoadPlanner.Replan(plain, ship).Plan));
            ship.Racks.SelectMany(r => CrateLayout.Problems(r, sectioned.Plan.CratesForRack(r.Id))).Should().BeEmpty();
        }
    }

    [Fact]
    public void An_overloaded_ship_waits_for_the_same_missions_with_sections()
    {
        Mission[] missions = [.. Enumerable.Range(1, 8).Select(i => new Mission(i, [new MissionStop($"Stop {i}", 1, [new MissionItem("Titanium", 12, CrateSize.Four)])]))];
        SavedPlan empty = new([.. missions.Select(m => m with { Unplaced = true })], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

        var plain = LoadPlanner.Replan(LoadPlanner.RetryUnplaced(empty, HullA), HullA);
        var sectioned = LoadPlanner.Replan(
            new SavedPlan(empty.Missions, empty.Plan with { Grouping = PlanGrouping.ByDestination }, empty.CrateCap), HullA);

        plain.Missions.Should().Contain(m => m.Unplaced);
        sectioned.Plan.Grouping.Should().Be(PlanGrouping.ByDestination);
        sectioned.Missions.Select(m => m.Unplaced).Should().Equal(plain.Missions.Select(m => m.Unplaced));
    }

    // --- The shape of sections ---

    [Theory]
    [InlineData(FillRows.Lengthwise)]
    [InlineData(FillRows.Crosswise)]
    public void Undivided_sections_are_blocks_of_one_destination_side_by_side(FillRows rows)
    {
        var plan = Planned(rows).Plan;
        Func<LoadPlan, string, int[]> axis = rows == FillRows.Crosswise ? Lengths : Widths;

        var used = plan.Route.Select(d => axis(plan, d)).ToArray();

        used.Should().OnlyContain(r => r.Max() - r.Min() + 1 == r.Length, "a section is contiguous");
        used.SelectMany(r => r).Should().OnlyHaveUniqueItems("a row belongs to one destination");
        var all = used.SelectMany(r => r).Order().ToArray();
        (all[^1] - all[0] + 1).Should().Be(all.Length, "sections lie flush");
    }

    [Fact]
    public void A_section_that_needs_turning_at_all_turns_only_then()
    {
        // 2 long in a rack 1 long: unturned has no row, so D71 turns them and nothing else changes.
        var ship = OneRack(1, 8, 1);

        var plan = LoadPlanner.Plan(ship, [StopOf("A", 1, ("Ice", 4, CrateSize.Two)), StopOf("B", 2, ("Food", 2, CrateSize.One))], FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination);

        plan.Crates.Where(c => c.Destination == "A").Should().OnlyContain(c => c.Rotated);
    }

    [Fact]
    public void Turning_comes_before_sharing()
    {
        // Three rows of four: A and B each take two unturned, so only three rows fit if one of them turns; the later, the
        // smaller on a tie, does. No section is shared, so no note.
        var ship = OneRack(3, 4, 1);

        var plan = LoadPlanner.Plan(ship, [StopOf("A", 1, ("Ice", 4, CrateSize.Two)), StopOf("B", 2, ("Food", 4, CrateSize.Two))], FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination);

        Lengths(plan, "A").Should().Equal(2, 1);
        Lengths(plan, "B").Should().Equal(0);
        plan.Crates.Where(c => c.Destination == "A").Should().OnlyContain(c => !c.Rotated);
        plan.Crates.Where(c => c.Destination == "B").Should().OnlyContain(c => c.Rotated);
        Notes(plan).Should().BeEmpty();
    }

    [Fact]
    public void The_smallest_share_a_section_at_the_place_of_the_earlier()
    {
        // Three rows of two: A 1, B a 4 (two rows), C 1. Together they want four; A and C share A's place, ahead of B.
        var ship = OneRack(3, 2, 1);

        var plan = LoadPlanner.Plan(ship,
            [StopOf("A", 1, ("Ice", 1, CrateSize.One)), StopOf("B", 2, ("Food", 4, CrateSize.Four)), StopOf("C", 3, ("Gold", 1, CrateSize.One))],
            FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination);

        Lengths(plan, "A").Should().Equal(2);
        Lengths(plan, "C").Should().Equal(2);
        Lengths(plan, "B").Should().Equal(1, 0);
        Notes(plan).Should().Equal("Rack 1: C shares a section with A");
    }

    [Fact]
    public void All_in_one_section_is_the_rack_as_planned_with_the_note()
    {
        // Two 4s on a 2 × 2 × 2 rack: one has to lie on the other, there are no sections to speak of.
        var ship = OneRack(2, 2, 2);
        Stop[] stops = [StopOf("A", 1, ("Ice", 4, CrateSize.Four)), StopOf("B", 2, ("Food", 4, CrateSize.Four))];

        var plan = LoadPlanner.Plan(ship, stops, FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination);

        plan.Crates.Select(c => c.Position).Should().Equal(LoadPlanner.Plan(ship, stops, FillOrder.BowFirst, FillRows.Crosswise).Crates.Select(c => c.Position));
        Notes(plan).Should().Equal("Rack 1: B shares a section with A");
    }

    [Fact]
    public void The_Hull_A_runs_its_sections_along_the_length()
    {
        // Three destinations fill a rack each; D and E share the fourth, two rows each, whatever fillRows says.
        Stop[] stops =
        [
            StopOf("A", 1, ("Ice", 16, CrateSize.Sixteen)), StopOf("B", 2, ("Food", 16, CrateSize.Sixteen)), StopOf("C", 3, ("Gold", 16, CrateSize.Sixteen)),
            StopOf("D", 4, ("Iron", 8, CrateSize.Four)), StopOf("E", 5, ("Tin", 8, CrateSize.Four)),
        ];

        foreach (var rows in Enum.GetValues<FillRows>())
        {
            var plan = LoadPlanner.Plan(HullA, stops, FillOrder.BowFirst, rows, PlanGrouping.ByDestination);

            Lengths(plan, "D").Should().Equal(3, 2);
            Lengths(plan, "E").Should().Equal(1, 0);
            plan.Crates.Where(c => c.Destination is "A" or "B" or "C").Select(c => c.Position).Should()
                .Equal(LoadPlanner.Plan(HullA, stops, FillOrder.BowFirst, rows).Crates.Where(c => c.Destination is "A" or "B" or "C").Select(c => c.Position), "a rack of one destination stays");
            Notes(plan).Should().BeEmpty();
        }
    }

    // --- Crates on board and off it ---

    private static SavedPlan WithLine(SavedPlan saved, Func<LoadPlan, LoadPlan> change) =>
        new(saved.Missions, change(saved.Plan), saved.CrateCap, saved.Watermark);

    [Fact]
    public void Loaded_crates_stay_and_the_rest_lies_around_them()
    {
        var saved = Planned(FillRows.Crosswise);
        var mission = saved.Plan.Crates.First(c => c.Destination == Thunder).Mission;
        var loaded = WithLine(saved, p => LoadPlanner.SetLoaded(p, Raft, Thunder, mission, true));

        var replanned = LoadPlanner.Replan(loaded, Raft);

        replanned.Plan.Crates.Where(c => c.Loaded).Should().BeEquivalentTo(loaded.Plan.Crates.Where(c => c.Loaded));
        replanned.Plan.Grouping.Should().Be(PlanGrouping.ByDestination);
        CrateLayout.Problems(Raft.Racks[0], replanned.Plan.Crates).Should().BeEmpty();
        replanned.Missions.Should().OnlyContain(m => !m.Unplaced);
    }

    [Fact]
    public void Everything_loaded_but_one_destination_lays_that_one_around_the_rest()
    {
        var ship = OneRack(6, 2, 1);
        Mission[] missions =
        [
            new(1, [new MissionStop("A", 1, [new MissionItem("Ice", 4, CrateSize.Four)])]),
            new(2, [new MissionStop("B", 1, [new MissionItem("Food", 4, CrateSize.Four)])]),
        ];
        var saved = LoadPlanner.Plan(ship, missions, ["A", "B"], FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.ByDestination);
        var loaded = WithLine(saved, p => LoadPlanner.SetLoaded(p, ship, "A", 1, true));

        var replanned = LoadPlanner.Replan(loaded, ship);

        Lengths(replanned.Plan, "A").Should().Equal(5, 4);
        Lengths(replanned.Plan, "B").Should().Equal(3, 2);
    }

    [Fact]
    public void Delivered_crates_stay_where_they_lay_and_take_no_room()
    {
        var saved = Planned(FillRows.Crosswise);
        var mission = saved.Plan.Crates.First(c => c.Destination == Green).Mission;
        var ticked = WithLine(saved, p => LoadPlanner.SetDelivered(p, Raft, Green, mission, true));
        Crate[] delivered = [.. ticked.Plan.Crates.Where(c => c.Delivered)];

        var replanned = LoadPlanner.Replan(ticked, Raft);

        delivered.Should().NotBeEmpty();
        replanned.Plan.Crates.Where(c => c.Delivered).Should().BeEquivalentTo(delivered);

        // The rest of Green Glade comes first again, on the rows the delivered crates freed.
        Crate[] rest = [.. replanned.Plan.Crates.Where(c => c.Destination == Green && !c.Delivered)];
        rest.SelectMany(c => c.Cells).Select(c => c.Length).Should().OnlyContain(l => l >= 10);
        CrateLayout.Problems(Raft.Racks[0], replanned.Plan.Crates).Should().BeEmpty();
    }

    [Fact]
    public void Off_ship_crates_stay_off_the_ship_through_a_ship_change()
    {
        var saved = Planned(FillRows.Crosswise);
        var ticked = WithLine(saved, p => LoadPlanner.SetDelivered(p, Raft, Melodic, true));

        var there = LoadPlanner.Replan(ticked, ShipLoader.Load("freelancer"));

        there.Plan.Grouping.Should().Be(PlanGrouping.ByDestination);
        there.Plan.Crates.Where(c => c.Destination == Melodic).Should().OnlyContain(c => c.OffShip);
    }

    // --- Nothing else moves ---

    [Fact]
    public void A_re_plan_of_a_sectioned_plan_moves_nothing()
    {
        var saved = Planned(FillRows.Crosswise);

        var replanned = LoadPlanner.Replan(saved, Raft);

        LoadPlanner.MovedCrates(saved.Plan, replanned.Plan).Should().Be(0);
    }

    [Fact]
    public void Discard_keeps_the_grouping()
    {
        LoadPlanner.Discard(Planned(FillRows.Crosswise), Raft).Plan.Grouping.Should().Be(PlanGrouping.ByDestination);
    }

    [Fact]
    public void Without_grouping_the_plan_is_the_plain_one()
    {
        var plain = Planned(FillRows.Crosswise, grouping: PlanGrouping.None).Plan;
        var saved = Fixture();

        plain.Crates.Should().Equal(LoadPlanner.Plan(Raft, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], FillOrder.BowFirst, 0, FillRows.Crosswise).Plan.Crates);
        plain.Grouping.Should().Be(PlanGrouping.None);
    }

    [Fact]
    public void The_same_input_gives_the_same_plan()
    {
        Planned(FillRows.Crosswise).Plan.Crates.Should().Equal(Planned(FillRows.Crosswise).Plan.Crates);
    }

    [Fact]
    public void The_racks_are_the_same_in_any_input_order()
    {
        var stops = MissionFixtures.Load("hull_a_eight_stops");
        var first = LoadPlanner.Plan(HullA, stops, FillOrder.BowFirst, FillRows.Lengthwise, PlanGrouping.ByDestination);

        var reversed = LoadPlanner.Plan(HullA, [.. stops.Reverse().Select((s, i) => s with { Order = i + 1 })], FillOrder.BowFirst, FillRows.Lengthwise, PlanGrouping.ByDestination);

        Racks(reversed).Should().Equal(Racks(first));
    }
}
