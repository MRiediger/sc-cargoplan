using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Empty rows between sections (K9.2b, D89 addendum), to grip the crates. The ladder: as they are with gaps; turning where
/// that saves rows; fewer gaps, the turns stay; sharing; the rack as planned. Each test falls exactly one step further.
/// </summary>
public class SectionGapTests
{
    private const string Green = "Green Glade Station";
    private const string High = "High Course Station";
    private const string Thunder = "Thundering Express Station";
    private const string Faithful = "Faithful Dream Station";
    private const string Melodic = "Melodic Fields Station";

    private static readonly Ship Raft = FrozenShips.Load("raft");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static SavedPlan Fixture()
    {
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");
        return PlanFile.Read(stream, "raft", Raft);
    }

    private static SavedPlan Planned(FillRows rows, int gap)
    {
        var saved = Fixture();
        return LoadPlanner.Plan(Raft, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], FillOrder.BowFirst, 0, rows, PlanGrouping.ByDestination, gap);
    }

    private static Ship OneRack(int length, int width, int stack = 1)
    {
        Rack rack = new("rack-0", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0));
        return new Ship("flat", "Flat", rack.CapacityScu, [rack]);
    }

    private static Stop StopOf(string destination, int order, int scu, CrateSize size) =>
        new(destination, order, [new CargoItem("Titanium", scu, size)]);

    private static LoadPlan Crosswise(Ship ship, int gap, params Stop[] stops) =>
        LoadPlanner.Plan(ship, stops, FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination, gap);

    private static int[] Lengths(LoadPlan plan, string destination) =>
        [.. plan.Crates.Where(c => c.Destination == destination).SelectMany(c => c.Cells).Select(c => c.Length).Distinct().OrderDescending()];

    private static int[] Widths(LoadPlan plan, string destination) =>
        [.. plan.Crates.Where(c => c.Destination == destination).SelectMany(c => c.Cells).Select(c => c.Width).Distinct().Order()];

    private static bool Turned(LoadPlan plan, string destination) =>
        plan.Crates.Where(c => c.Destination == destination && c.CrateSize.Turns).All(c => c.Rotated);

    private static IEnumerable<string> Notes(LoadPlan plan) => plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message);

    // --- The goldens: Martin's missions ---

    [Fact]
    public void Crosswise_with_gap_1_turns_the_smallest_two_and_keeps_every_gap()
    {
        var plan = Planned(FillRows.Crosswise, 1).Plan;

        Lengths(plan, Green).Should().Equal(11, 10);
        Lengths(plan, High).Should().Equal(8, 7);
        Lengths(plan, Thunder).Should().Equal(5, 4);
        Lengths(plan, Faithful).Should().Equal(2);
        Lengths(plan, Melodic).Should().Equal(0);

        plan.Crates.Where(c => c.Destination is Faithful or Melodic && c.CrateSize == CrateSize.Two).Should().OnlyContain(c => c.Rotated);
        plan.Crates.Where(c => c.Destination is Green or High or Thunder).Should().OnlyContain(c => !c.Rotated);
        Notes(plan).Should().BeEmpty();
    }

    [Fact]
    public void Lengthwise_with_gap_1_has_no_row_to_spare_and_lies_as_without_gaps()
    {
        var withGap = Planned(FillRows.Lengthwise, 1).Plan;

        withGap.Crates.Select(c => (c.Position, c.Rotated)).Should().Equal(Planned(FillRows.Lengthwise, 0).Plan.Crates.Select(c => (c.Position, c.Rotated)));
        Widths(withGap, Green).Should().Equal(0, 1);
        Widths(withGap, Melodic).Should().Equal(7);
        withGap.SectionGap.Should().Be(1, "the plan keeps what the player chose");
    }

    [Fact]
    public void Three_destinations_with_gap_2_show_the_gaps_as_they_are()
    {
        var saved = Fixture();
        string[] three = [Green, High, Thunder];
        Mission[] missions = [.. saved.Stops.Where(s => three.Contains(s.Destination)).Select((s, i) => new Mission(i + 1, [s with { Order = 1 }]))];

        var plan = LoadPlanner.Plan(Raft, missions, three, FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.ByDestination, 2).Plan;

        Lengths(plan, Green).Should().Equal(11, 10);
        Lengths(plan, High).Should().Equal(7, 6);
        Lengths(plan, Thunder).Should().Equal(3, 2);
        plan.Crates.Should().OnlyContain(c => !c.Rotated);
    }

    // --- The ladder, a step at a time ---

    [Fact]
    public void Step_1_fits_with_gaps_unturned()
    {
        var plan = Crosswise(OneRack(5, 4), 1, StopOf("A", 1, 4, CrateSize.Two), StopOf("B", 2, 4, CrateSize.Two));

        Lengths(plan, "A").Should().Equal(4, 3);
        Lengths(plan, "B").Should().Equal(1, 0);
        plan.Crates.Should().OnlyContain(c => !c.Rotated);
    }

    [Fact]
    public void Step_2_turns_to_keep_the_gap_the_smaller_later_section_first()
    {
        var plan = Crosswise(OneRack(4, 4), 1, StopOf("A", 1, 4, CrateSize.Two), StopOf("B", 2, 4, CrateSize.Two));

        Lengths(plan, "A").Should().Equal(3, 2);
        Lengths(plan, "B").Should().Equal(0);
        plan.Crates.Where(c => c.Destination == "A").Should().OnlyContain(c => !c.Rotated);
        Turned(plan, "B").Should().BeTrue();
    }

    [Fact]
    public void Step_3_takes_gaps_away_one_at_a_time()
    {
        // Both turned, one row each: three rows hold one gap, not two.
        var plan = Crosswise(OneRack(3, 4), 2, StopOf("A", 1, 4, CrateSize.Two), StopOf("B", 2, 4, CrateSize.Two));

        Lengths(plan, "A").Should().Equal(2);
        Lengths(plan, "B").Should().Equal(0);
        Turned(plan, "A").Should().BeTrue();
        Turned(plan, "B").Should().BeTrue();
    }

    [Fact]
    public void Step_3_goes_to_no_gap_without_a_word()
    {
        var plan = Crosswise(OneRack(4, 2), 1, StopOf("A", 1, 4, CrateSize.Four), StopOf("B", 2, 4, CrateSize.Four));

        Lengths(plan, "A").Should().Equal(3, 2);
        Lengths(plan, "B").Should().Equal(1, 0);
        Notes(plan).Should().BeEmpty();
    }

    [Fact]
    public void A_turn_stays_when_gaps_go_afterwards()
    {
        // Unturned without gaps 2+2+2 fits six rows, but the 2 was turned trying to keep them: Martin, grip before handle.
        var plan = Crosswise(OneRack(6, 2), 1, StopOf("A", 1, 2, CrateSize.Two), StopOf("B", 2, 4, CrateSize.Four), StopOf("C", 3, 4, CrateSize.Four));

        Lengths(plan, "A").Should().Equal(5);
        Lengths(plan, "B").Should().Equal(4, 3);
        Lengths(plan, "C").Should().Equal(2, 1);
        Turned(plan, "A").Should().BeTrue();
    }

    [Fact]
    public void Step_4_shares_only_once_no_gap_is_left()
    {
        var plan = Crosswise(OneRack(3, 2), 1, StopOf("A", 1, 1, CrateSize.One), StopOf("B", 2, 4, CrateSize.Four), StopOf("C", 3, 1, CrateSize.One));

        Lengths(plan, "A").Should().Equal(2);
        Lengths(plan, "C").Should().Equal(2);
        Lengths(plan, "B").Should().Equal(1, 0);
        Notes(plan).Should().Equal("Rack 1: C shares a section with A");
    }

    [Fact]
    public void Step_5_leaves_the_rack_as_planned()
    {
        var ship = OneRack(2, 2, 2);
        Stop[] stops = [StopOf("A", 1, 4, CrateSize.Four), StopOf("B", 2, 4, CrateSize.Four)];

        var plan = Crosswise(ship, 1, stops);

        plan.Crates.Select(c => c.Position).Should().Equal(LoadPlanner.Plan(ship, stops, FillOrder.BowFirst, FillRows.Crosswise).Crates.Select(c => c.Position));
        Notes(plan).Should().Equal("Rack 1: sections don't fit – laid as with None");
    }

    // --- Around it ---

    public static TheoryData<string, string, int> Fixtures
    {
        get
        {
            TheoryData<string, string, int> cases = [];
            (string, string)[] all =
            [
                ("example_missions", "hull_a"), ("split_and_shared", "hull_a"), ("split_required", "hull_a"), ("crate_mix", "hull_a"),
                ("hull_a_eight_stops", "hull_a"), ("hull_a_hurston_2026-10-24", "hull_a"), ("hull_a_eight_stops", "freelancer"),
                ("hull_a_eight_stops", "raft"), ("crate_mix", "raft"),
            ];
            foreach (var (fixture, ship) in all)
            {
                cases.Add(fixture, ship, 1);
                cases.Add(fixture, ship, 2);
            }

            return cases;
        }
    }

    private static (string, string, CrateSize, string)[] Racks(LoadPlan plan) =>
        [.. plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId)).Order()];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Gaps_change_neither_the_racks_nor_who_waits(string fixture, string shipId, int gap)
    {
        var ship = FrozenShips.Load(shipId);
        var stops = MissionFixtures.LoadMissions(fixture);
        Mission[] missions = [.. stops.Select((s, i) => new Mission(i + 1, [s with { Order = 1 }]))];
        string[] route = [.. stops.Select(s => s.Destination)];

        foreach (var rows in Enum.GetValues<FillRows>())
        {
            var plain = LoadPlanner.Plan(ship, missions, route, FillOrder.BowFirst, 0, rows);
            var withGap = LoadPlanner.Plan(ship, missions, route, FillOrder.BowFirst, 0, rows, PlanGrouping.ByDestination, gap);

            Racks(withGap.Plan).Should().Equal(Racks(plain.Plan));
            withGap.Missions.Select(m => m.Unplaced).Should().Equal(plain.Missions.Select(m => m.Unplaced));
            Racks(LoadPlanner.Replan(withGap, ship).Plan).Should().Equal(Racks(LoadPlanner.Replan(plain, ship).Plan));
            ship.Racks.SelectMany(r => CrateLayout.Problems(r, withGap.Plan.CratesForRack(r.Id))).Should().BeEmpty();
        }
    }

    [Fact]
    public void A_row_with_a_loaded_crate_counts_as_the_gap()
    {
        // Martin (E1): no further row on top of it, or a loaded crate would push every section back, into step 3.
        var ship = OneRack(5, 4);
        Mission[] missions =
        [
            new(1, [new MissionStop("A", 1, [new MissionItem("Ice", 4, CrateSize.Two)])]),
            new(2, [new MissionStop("B", 1, [new MissionItem("Food", 4, CrateSize.Two)])]),
            new(3, [new MissionStop("C", 1, [new MissionItem("Gold", 1, CrateSize.One)])]),
        ];
        var plain = LoadPlanner.Plan(ship, missions, ["A", "B", "C"], FillOrder.BowFirst, 0, FillRows.Crosswise);
        var loaded = new SavedPlan(plain.Missions, plain.Plan with
        {
            Grouping = PlanGrouping.ByDestination,
            SectionGap = 1,
            Crates = [.. plain.Plan.Crates.Select(c => c.Destination == "C" ? (c with { Position = new GridPosition(2, 0, 0) }).AsLoaded() : c)],
        }, plain.CrateCap);

        var replanned = LoadPlanner.Replan(loaded, ship).Plan;

        replanned.Crates.Single(c => c.Destination == "C").Position.Should().Be(new GridPosition(2, 0, 0));
        Lengths(replanned, "A").Should().Equal(4, 3);
        Lengths(replanned, "B").Should().Equal(1, 0);
        replanned.Crates.Should().OnlyContain(c => !c.Rotated);
        CrateLayout.Problems(ship.Racks[0], replanned.Crates).Should().BeEmpty();
    }

    [Fact]
    public void The_Hull_A_keeps_a_gap_only_where_it_fits()
    {
        Stop[] full = [StopOf("A", 1, 16, CrateSize.Sixteen), StopOf("B", 2, 16, CrateSize.Sixteen), StopOf("C", 3, 16, CrateSize.Sixteen)];

        var fours = LoadPlanner.Plan(HullA, [.. full, StopOf("D", 4, 8, CrateSize.Four), StopOf("E", 5, 8, CrateSize.Four)], FillOrder.BowFirst, FillRows.Lengthwise, PlanGrouping.ByDestination, 1);
        Lengths(fours, "D").Should().Equal(3, 2);
        Lengths(fours, "E").Should().Equal(1, 0);

        var ones = LoadPlanner.Plan(HullA, [.. full, StopOf("D", 4, 2, CrateSize.One), StopOf("E", 5, 2, CrateSize.One)], FillOrder.BowFirst, FillRows.Lengthwise, PlanGrouping.ByDestination, 1);
        Lengths(ones, "D").Should().Equal(3);
        Lengths(ones, "E").Should().Equal(1);
    }

    [Fact]
    public void A_re_plan_keeps_the_gap_and_moves_nothing()
    {
        var saved = Planned(FillRows.Crosswise, 1);

        var replanned = LoadPlanner.Replan(saved, Raft);

        replanned.Plan.SectionGap.Should().Be(1);
        LoadPlanner.MovedCrates(saved.Plan, replanned.Plan).Should().Be(0);
    }

    [Fact]
    public void Discard_and_a_ship_change_keep_the_gap()
    {
        var saved = Planned(FillRows.Crosswise, 2);

        LoadPlanner.Discard(saved, Raft).Plan.SectionGap.Should().Be(2);
        LoadPlanner.Replan(saved, FrozenShips.Load("freelancer")).Plan.SectionGap.Should().Be(2);
    }

    [Fact]
    public void The_gap_survives_a_plan_without_grouping()
    {
        var saved = Fixture();
        var plain = LoadPlanner.Plan(Raft, saved.Missions, [.. saved.Stops.Select(s => s.Destination)], FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.None, 1);

        plain.Plan.SectionGap.Should().Be(1);
        LoadPlanner.Replan(plain, Raft).Plan.SectionGap.Should().Be(1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void A_gap_out_of_range_is_refused(int gap)
    {
        var act = () => new LoadPlan("raft", [], [], SectionGap: gap);

        act.Should().Throw<ArgumentException>().WithMessage("*sectionGap*0, 1 or 2*");
    }
}
