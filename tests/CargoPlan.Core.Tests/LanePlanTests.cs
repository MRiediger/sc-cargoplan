using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Lanes in plans (D116, F10a): one side full first, then the other ("side full first", P2). Racks and who waits come from
/// the planner as without lanes (F5); only the cells change. With sections a destination may cross to the second side
/// where the first is full (<c>sideOverflow</c>), or, without overflow, lies whole on one side or its mission waits.
/// On the RAFT a lane is 12 rows of 4 × 2 cells: 96 SCU, 8 SCU a row crosswise.
/// </summary>
public class LanePlanTests
{
    private static readonly Ship Raft = FrozenShips.Load("raft");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");
    private static readonly Ship C2 = FrozenShips.Load("c2_hercules");

    private static readonly string[] Letters = ["A", "B", "C", "D", "E", "F", "G", "H"];

    private static Ship Flat(int length, int width, int stack) =>
        new("flat", "Flat", length * width * stack, [new Rack("rack-0", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0))]);

    /// <summary>Two of the Hull A's plates on the port side only (y 1..3, stacking +y): no lanes, and no side groups (D117).</summary>
    private static Ship PortPlates() =>
        new("plates", "Plates", 32,
        [
            new Rack("rack-0", 0, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 1, 3), StackAxis.PlusY),
            new Rack("rack-1", 1, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 1, 0), StackAxis.PlusY),
        ]);

    /// <summary>One mission per destination, in this order: A, B, … each with one commodity in crates up to <paramref name="max"/>.</summary>
    private static Mission[] Missions(CrateSize max, params int[] scu) =>
        [.. scu.Select((amount, i) => new Mission(i + 1, [new MissionStop(Letters[i], 1, [new MissionItem("Gold", amount, max)])]))];

    private static SavedPlan Planned(
        Ship ship, Mission[] missions, Sides sides, PlanGrouping grouping = PlanGrouping.ByDestination, FillRows rows = FillRows.Crosswise,
        FillOrder order = FillOrder.BowFirst, int gap = 0, bool overflow = true) =>
        LoadPlanner.Plan(ship, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination)], order, rows, grouping, gap, sides, overflow);

    private static IEnumerable<GridPosition> CellsOf(LoadPlan plan, string destination) =>
        plan.Crates.Where(c => c.Destination == destination && !c.Delivered).SelectMany(c => c.Cells);

    private static IEnumerable<string> Notes(LoadPlan plan) => plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message);

    private static (string, string, CrateSize, GridPosition, bool)[] Lying(LoadPlan plan) =>
        [.. plan.Crates.Select(c => (c.RackId, c.Destination, c.CrateSize, c.Position, c.Rotated))];

    // --- R1: two destinations that fit one side ---

    [Fact]
    public void Port_first_lays_two_destinations_that_fit_the_port_side_both_there()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 24, 24), Sides.PortFirst).Plan;

        CellsOf(plan, "A").Should().OnlyContain(c => c.Width >= 4);
        CellsOf(plan, "B").Should().OnlyContain(c => c.Width >= 4);
        plan.Crates.SelectMany(c => c.Cells).Should().NotContain(c => c.Width < 4, "the starboard side stays empty");
    }

    [Fact]
    public void Starboard_first_lays_them_on_the_starboard_side()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 24, 24), Sides.StarboardFirst).Plan;

        plan.Crates.SelectMany(c => c.Cells).Should().OnlyContain(c => c.Width < 4);
    }

    [Fact]
    public void Without_sections_lanes_are_the_cell_order_alone()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 24, 24), Sides.PortFirst, PlanGrouping.None).Plan;

        plan.Crates.SelectMany(c => c.Cells).Should().OnlyContain(c => c.Width >= 4);
    }

    [Fact]
    public void Lanes_change_cells_not_racks_or_who_waits()
    {
        var missions = Missions(CrateSize.Four, 64, 40, 32, 16);
        var without = Planned(C2, missions, Sides.None);
        var with = Planned(C2, missions, Sides.PortFirst);

        with.Missions.Select(m => m.Unplaced).Should().Equal(without.Missions.Select(m => m.Unplaced));
        with.Plan.Crates.Select(c => (c.Destination, c.RackId)).Order().Should().Equal(without.Plan.Crates.Select(c => (c.Destination, c.RackId)).Order());
    }

    // --- R2: overflow ---

    [Fact]
    public void With_overflow_a_destination_fills_the_first_side_and_crosses_to_the_second()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 80, 24), Sides.PortFirst).Plan;

        CellsOf(plan, "A").Should().OnlyContain(c => c.Width >= 4);
        CellsOf(plan, "B").Count(c => c.Width >= 4).Should().Be(16);
        CellsOf(plan, "B").Count(c => c.Width < 4).Should().Be(8);
        SectionBlockTests.Enclosed(plan, Raft).Should().BeEmpty("each part is a block");
    }

    [Fact]
    public void Without_overflow_a_destination_that_doesnt_fit_the_rest_of_the_first_side_lies_whole_on_the_second()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 80, 24), Sides.PortFirst, overflow: false).Plan;

        CellsOf(plan, "A").Should().OnlyContain(c => c.Width >= 4);
        CellsOf(plan, "B").Should().HaveCount(24).And.OnlyContain(c => c.Width < 4);
    }

    [Fact]
    public void Without_overflow_a_later_destination_goes_back_to_the_first_side_where_it_fits()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 80, 24, 16), Sides.PortFirst, overflow: false).Plan;

        CellsOf(plan, "B").Should().OnlyContain(c => c.Width < 4);
        CellsOf(plan, "C").Should().OnlyContain(c => c.Width >= 4, "16 SCU fit the two rows left on the port side");
    }

    [Fact]
    public void Without_overflow_a_mission_that_fits_no_side_waits_though_cells_are_free()
    {
        var missions = Missions(CrateSize.Four, 88, 100);
        var crossing = Planned(Raft, missions, Sides.PortFirst);
        var saved = Planned(Raft, missions, Sides.PortFirst, overflow: false);

        crossing.Missions.Should().OnlyContain(m => !m.Unplaced, "with overflow both fit");
        saved.Missions.Single(m => m.Id == 2).Unplaced.Should().BeTrue();
        saved.Plan.Crates.Should().NotContain(c => c.Destination == "B");
        CellsOf(saved.Plan, "A").Should().OnlyContain(c => c.Width >= 4);
        Notes(saved.Plan).Should().Contain("Rack 1: lanes full – a mission waits (side overflow off)");
    }

    [Fact]
    public void Without_overflow_and_without_missions_a_destination_that_fits_no_side_crosses_with_a_note()
    {
        Stop[] stops = [new("A", 1, [new CargoItem("Gold", 88, CrateSize.Four)]), new("B", 2, [new CargoItem("Gold", 100, CrateSize.Four)])];
        var plan = LoadPlanner.Plan(Raft, stops, FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 0, Sides.PortFirst, sideOverflow: false);

        CellsOf(plan, "B").Should().HaveCount(100);
        Notes(plan).Should().Contain("Rack 1: B crosses sides – side overflow off");
    }

    // --- R4: racks without lanes ---

    [Fact]
    public void A_rack_three_wide_lies_as_without_lanes_and_says_why()
    {
        var ship = Flat(6, 3, 2);
        var missions = Missions(CrateSize.Two, 8, 6);
        var without = Planned(ship, missions, Sides.None);
        var with = Planned(ship, missions, Sides.PortFirst);

        Lying(with.Plan).Should().Equal(Lying(without.Plan));
        Notes(with.Plan).Should().Contain("Rack 1: lanes need a rack at least 4 wide");
        Notes(without.Plan).Should().NotContain(n => n.Contains("lane"));
    }

    [Fact]
    public void A_rack_stacking_sideways_lies_as_without_lanes_and_says_why()
    {
        // Racks by side would order the Hull A's plates and drop the note (D117): plates on one side only keep it.
        var ship = PortPlates();
        var missions = Missions(CrateSize.Four, 16, 16);
        var without = Planned(ship, missions, Sides.None);
        var with = Planned(ship, missions, Sides.PortFirst);

        Lying(with.Plan).Should().Equal(Lying(without.Plan));
        Notes(with.Plan).Should().Contain(n => n.EndsWith(": no lanes on a rack that stacks sideways", StringComparison.Ordinal));
    }

    [Fact]
    public void The_c2_gets_lanes_in_both_racks_and_no_note_about_sides()
    {
        var plan = Planned(C2, Missions(CrateSize.Four, 64, 40, 32, 16), Sides.PortFirst).Plan;

        foreach (var rack in C2.Racks.Where(r => plan.Crates.Any(c => c.RackId == r.Id)))
        {
            plan.Crates.Where(c => c.RackId == rack.Id).SelectMany(c => c.Cells).Should().OnlyContain(c => c.Width >= rack.Grid.Width / 2, $"rack {rack.Number} holds less than a side");
        }

        Notes(plan).Should().BeEmpty();
    }

    // --- R5, R6: goldens ---

    [Fact]
    public void Eight_destinations_four_to_a_side_without_gaps()
    {
        var plan = Planned(Raft, Missions(CrateSize.One, 24, 24, 24, 24, 24, 24, 24, 24), Sides.PortFirst).Plan;

        for (var i = 0; i < 8; i++)
        {
            var (side, first) = i < 4 ? ((Func<GridPosition, bool>)(c => c.Width >= 4), 11 - 3 * i) : (c => c.Width < 4, 11 - 3 * (i - 4));
            CellsOf(plan, Letters[i]).Should().OnlyContain(c => side(c) && c.Length <= first && c.Length > first - 3, Letters[i]);
        }
    }

    [Fact]
    public void A_gap_lies_between_sections_of_one_side_not_at_the_start_of_the_next()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 16, 16, 16, 16, 16, 16, 16, 16), Sides.PortFirst, gap: 1).Plan;

        int[] firstRow = [11, 8, 5, 2];
        for (var i = 0; i < 8; i++)
        {
            var first = firstRow[i % 4];
            var port = i < 4;
            CellsOf(plan, Letters[i]).Should().OnlyContain(c => (c.Width >= 4) == port && (c.Length == first || c.Length == first - 1), Letters[i]);
        }

        SectionBlockTests.Enclosed(plan, Raft).Should().BeEmpty();
    }

    [Fact]
    public void A_section_ending_at_the_end_of_the_first_side_leaves_no_gap_at_the_start_of_the_second()
    {
        // 32 + gap + 56 SCU of singles fill the port side's 12 rows exactly; C starts in the starboard side's first row.
        var plan = Planned(Raft, Missions(CrateSize.One, 32, 56, 16), Sides.PortFirst, gap: 1).Plan;

        CellsOf(plan, "B").Should().Contain(c => c.Length == 0 && c.Width >= 4);
        CellsOf(plan, "C").Should().OnlyContain(c => c.Width < 4 && (c.Length == 11 || c.Length == 10));
    }

    [Fact]
    public void Lengthwise_lanes_run_strips_from_the_port_edge_inwards()
    {
        var plan = Planned(Raft, Missions(CrateSize.Four, 48, 24), Sides.PortFirst, rows: FillRows.Lengthwise).Plan;

        CellsOf(plan, "A").Should().OnlyContain(c => c.Width == 7 || c.Width == 6);
        CellsOf(plan, "B").Should().OnlyContain(c => c.Width == 5 || c.Width == 4);
    }

    [Fact]
    public void Stern_first_lanes_start_at_the_stern()
    {
        var plan = Planned(Raft, Missions(CrateSize.One, 24, 24), Sides.PortFirst, order: FillOrder.SternFirst).Plan;

        CellsOf(plan, "A").Should().OnlyContain(c => c.Width >= 4 && c.Length <= 2);
        CellsOf(plan, "B").Should().OnlyContain(c => c.Width >= 4 && c.Length >= 3 && c.Length <= 5);
    }

    [Fact]
    public void A_block_ends_at_its_lane()
    {
        // X on both sides of the lane boundary with empty cells between: a gap in one rack, two blocks in lanes.
        var rack = Raft.Racks[0];
        Crate[] crates = [new("X", "Gold", CrateSize.One, rack.Id, new GridPosition(5, 2, 0)), new("X", "Gold", CrateSize.One, rack.Id, new GridPosition(5, 5, 0))];

        RackSections.Encloses(rack, crates, "X").Should().BeTrue();
        RackSections.Encloses(rack, crates, "X", Lanes.Of(rack, Sides.PortFirst)).Should().BeFalse();
    }

    // --- R8: loaded cargo stays ---

    [Fact]
    public void Turning_lanes_on_moves_planned_crates_only()
    {
        var missions = Missions(CrateSize.Four, 24, 24);
        var plain = Planned(Raft, missions, Sides.None);
        SavedPlan loaded = new(plain.Missions, LoadPlanner.SetLoaded(plain.Plan, Raft, "A", 1, true), plain.CrateCap);

        var (laned, _) = LoadPlanner.PlanKeeping(loaded, Raft, missions, ["A", "B"], FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 0, Sides.PortFirst);

        laned.Plan.Crates.Where(c => c.Destination == "A").Select(c => (c.Position, c.Rotated)).Should().BeEquivalentTo(
            loaded.Plan.Crates.Where(c => c.Destination == "A").Select(c => (c.Position, c.Rotated)));
        CellsOf(laned.Plan, "B").Should().OnlyContain(c => c.Width >= 4);
        laned.Plan.Sides.Should().Be(Sides.PortFirst);
    }

    // --- Captures and Re-plan keep the lanes ---

    [Fact]
    public void A_capture_lays_its_crates_side_first()
    {
        var saved = Planned(Raft, Missions(CrateSize.Four, 24), Sides.PortFirst, PlanGrouping.None);
        var captured = LoadPlanner.PlaceInto(saved, Raft, [new MissionStop("B", 1, [new MissionItem("Gold", 24, CrateSize.Four)])]);

        CellsOf(captured.Plan, "B").Should().HaveCount(24).And.OnlyContain(c => c.Width >= 4);
    }

    [Theory]
    [InlineData(PlanGrouping.None)]
    [InlineData(PlanGrouping.ByDestination)]
    public void A_replan_keeps_the_sides(PlanGrouping grouping)
    {
        var saved = Planned(Raft, Missions(CrateSize.Four, 24, 24), Sides.PortFirst, grouping, overflow: false);

        var replanned = LoadPlanner.Replan(saved, Raft);

        (replanned.Plan.Sides, replanned.Plan.SideOverflow).Should().Be((Sides.PortFirst, false));
        replanned.Plan.Crates.SelectMany(c => c.Cells).Should().OnlyContain(c => c.Width >= 4);
    }

    [Fact]
    public void A_replan_on_another_ship_keeps_the_sides()
    {
        var saved = Planned(Raft, Missions(CrateSize.Four, 24, 24), Sides.StarboardFirst, PlanGrouping.None);

        LoadPlanner.Replan(saved, C2).Plan.Sides.Should().Be(Sides.StarboardFirst);
    }

    [Fact]
    public void Discard_keeps_the_sides()
    {
        var saved = Planned(Raft, Missions(CrateSize.Four, 24), Sides.PortFirst, overflow: false);

        (LoadPlanner.Discard(saved, Raft).Plan.Sides, LoadPlanner.Discard(saved, Raft).Plan.SideOverflow).Should().Be((Sides.PortFirst, false));
    }

    // --- R12: notes ---

    [Theory]
    [InlineData("Rack 1: lanes need a rack at least 4 wide")]
    [InlineData("Rack 2: no lanes on a rack that stacks sideways")]
    [InlineData("Rack 1: lanes don't fit – laid as without lanes")]
    [InlineData("Rack 1: lanes full – a mission waits (side overflow off)")]
    [InlineData("Rack 1: B crosses sides – side overflow off")]
    public void Lane_notes_go_to_the_sections_line(string message) =>
        SectionNotes.IsNote(new PlanWarning("rack-0", message)).Should().BeTrue();

    [Fact]
    public void Lane_notes_stand_beside_section_notes_and_come_once()
    {
        // Two plates stacking sideways on one side (no side groups, D117); with sides each rack holding cargo says so, once.
        var ship = PortPlates();
        var saved = Planned(ship, Missions(CrateSize.Four, 16, 8, 4, 4), Sides.PortFirst);
        var again = LoadPlanner.Replan(saved, ship);

        Notes(saved.Plan).Should().OnlyHaveUniqueItems();
        Notes(again.Plan).Should().OnlyHaveUniqueItems();
        Notes(again.Plan).Count(n => n.EndsWith("stacks sideways", StringComparison.Ordinal)).Should().Be(ship.Racks.Count(r => again.Plan.Crates.Any(c => c.RackId == r.Id)));
    }

    // --- R10: property ---

    /// <summary>
    /// Seeded mixes on the RAFT with sections, gap 0, both sides, both row axes: no crate across the lane boundary; without
    /// overflow no destination on both sides; with overflow the second side only once the first has no two empty rows side
    /// by side.
    /// </summary>
    [Fact]
    public void Lanes_hold_for_random_mixes()
    {
        CrateSize[] maxima = [CrateSize.One, CrateSize.Two, CrateSize.Four, CrateSize.Eight];
        var random = new Random(116);
        var (checkedPlans, crossed) = (0, 0);
        for (var run = 0; run < 200; run++)
        {
            var count = random.Next(1, 7);
            int[] scu = [.. Enumerable.Range(0, count).Select(_ => random.Next(4, 33))];
            while (scu.Sum() > 120)
            {
                scu[Array.IndexOf(scu, scu.Max())] /= 2;
            }

            var max = maxima[random.Next(maxima.Length)];
            var sides = run % 2 == 0 ? Sides.PortFirst : Sides.StarboardFirst;
            var rows = run / 2 % 2 == 0 ? FillRows.Crosswise : FillRows.Lengthwise;
            var overflow = run / 4 % 2 == 0;
            var saved = Planned(Raft, Missions(max, scu), sides, rows: rows, overflow: overflow);
            var plan = saved.Plan;
            if (Notes(plan).Any(n => n.Contains("sections don't fit", StringComparison.Ordinal) || n.Contains("lanes don't fit", StringComparison.Ordinal)))
            {
                continue;
            }

            checkedPlans++;
            bool First(GridPosition c) => sides == Sides.PortFirst ? c.Width >= 4 : c.Width < 4;
            foreach (var crate in plan.Crates)
            {
                crate.Cells.Select(First).Distinct().Should().HaveCount(1, $"run {run}: {crate.Destination} {crate.CrateSize} at {crate.Position}");
            }

            if (!overflow)
            {
                foreach (var destination in plan.Crates.Select(c => c.Destination).Distinct())
                {
                    CellsOf(plan, destination).Select(First).Distinct().Should().HaveCount(1, $"run {run}: {destination} on one side");
                }
            }
            else if (plan.Crates.SelectMany(c => c.Cells).Any(c => !First(c)))
            {
                crossed++;

                // Crates over one cell are two long or wide and turn only where they must (D71): a single empty row may stay
                // at the end of the first side, two side by side would take any crate up to 8 SCU.
                var taken = plan.Crates.SelectMany(c => c.Cells).Where(First).ToHashSet();
                int[] rowsOfFirst = rows == FillRows.Crosswise ? [.. Enumerable.Range(0, 12)] : [.. Enumerable.Range(0, 8).Where(w => First(new GridPosition(0, w, 0))).Order()];
                bool Empty(int row) => !taken.Any(c => (rows == FillRows.Crosswise ? c.Length : c.Width) == row);
                rowsOfFirst.Zip(rowsOfFirst.Skip(1)).Any(p => Empty(p.First) && Empty(p.Second))
                    .Should().BeFalse($"run {run}: the second side is used while the first has two empty rows side by side");
            }
        }

        checkedPlans.Should().BeGreaterThan(150);
        crossed.Should().BeGreaterThan(10, "the second side is reached often enough to say something");
    }
}
