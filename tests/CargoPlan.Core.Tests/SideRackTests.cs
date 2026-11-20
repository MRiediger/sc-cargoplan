using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;

namespace CargoPlan.Core.Tests;

/// <summary>
/// Racks by side (D117, F10b): with sides every rack of the first side is filled before one of the second, racks across
/// the middle line last; within a side the planner's rules hold (D64). Destinations take the sides in the legend's order
/// (P2). With overflow a destination fills what the first side has left and crosses with the rest; without, it lies whole
/// on the first side that takes it, or its mission waits. On the Hull B both sides have eight racks of 32 SCU (256).
/// </summary>
public class SideRackTests
{
    private static readonly Ship HullB = FrozenShips.Load("hull_b");
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static readonly int[] HullBPort = [1, 2, 5, 6, 9, 10, 13, 14];
    private static readonly int[] HullBStarboard = [3, 4, 7, 8, 11, 12, 15, 16];

    private static readonly string[] Letters = ["A", "B", "C", "D", "E", "F", "G", "H"];

    /// <summary>One mission per destination, in this order: A, B, … each with one commodity in crates up to <paramref name="max"/>.</summary>
    private static Mission[] Missions(CrateSize max, params int[] scu) =>
        [.. scu.Select((amount, i) => new Mission(i + 1, [new MissionStop(Letters[i], 1, [new MissionItem("Gold", amount, max)])]))];

    private static SavedPlan Planned(
        Ship ship, Mission[] missions, Sides sides, bool overflow = true, PlanGrouping grouping = PlanGrouping.ByDestination, FillRows rows = FillRows.Crosswise) =>
        LoadPlanner.Plan(ship, missions, [.. missions.SelectMany(m => m.Stops).Select(s => s.Destination)], FillOrder.BowFirst, rows, grouping, 0, sides, overflow);

    private static SavedPlan Empty(Ship ship, Sides sides, bool overflow = true) =>
        LoadPlanner.Plan(ship, [], [], FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 0, sides, overflow);

    private static SavedPlan Scanned(SavedPlan saved, Ship ship, string destination, int scu, CrateSize max) =>
        LoadPlanner.PlaceInto(saved, ship, [new MissionStop(destination, 1, [new MissionItem("Gold", scu, max)])]);

    private static int NumberOf(Ship ship, Crate crate) => ship.Racks.Single(r => r.Id == crate.RackId).Number;

    private static IEnumerable<int> RacksOf(Ship ship, LoadPlan plan, string destination) =>
        plan.Crates.Where(c => c.Destination == destination && !c.Delivered).Select(c => NumberOf(ship, c)).Distinct();

    private static int ScuIn(Ship ship, LoadPlan plan, string destination, int[] racks) =>
        plan.Crates.Where(c => c.Destination == destination && !c.Delivered && racks.Contains(NumberOf(ship, c))).Sum(c => (int)c.CrateSize);

    private static IEnumerable<string> Notes(LoadPlan plan) => plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message);

    /// <summary>A rack of 4 × 4 × 2 on each side, as a Hermes: the port one at y = +1, the starboard one at y = −5; both get lanes.</summary>
    private static Ship TwoSides() =>
        new("two", "Two", 64,
        [
            new Rack("port", 0, 32, new Dimensions(4, 4, 2), null, new ShipPoint(0, 1, 0)),
            new Rack("starboard", 1, 32, new Dimensions(4, 4, 2), null, new ShipPoint(0, -5, 0)),
        ]);

    /// <summary>Rack 1 across the middle line (y −2..2), rack 2 on port (y 2..4), rack 3 on starboard (y −4..−2); 16 SCU each.</summary>
    private static Ship WithMiddle() =>
        new("middle", "Middle", 48,
        [
            new Rack("middle", 0, 16, new Dimensions(4, 4, 1), null, new ShipPoint(0, -2, 0)),
            new Rack("port", 1, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, 2, 0)),
            new Rack("starboard", 2, 16, new Dimensions(4, 2, 2), null, new ShipPoint(0, -4, 0)),
        ]);

    // --- Side groups from the racks (P1) ---

    [Fact]
    public void The_hull_b_has_eight_racks_a_side_in_rack_order()
    {
        var groups = SideGroups.Of(HullB.Racks, Sides.PortFirst);

        groups.Should().NotBeNull();
        groups!.Select(g => g.Select(r => r.Number).ToArray()).Should().BeEquivalentTo([HullBPort, HullBStarboard], o => o.WithStrictOrdering());
    }

    [Fact]
    public void Starboard_first_puts_the_starboard_racks_first()
    {
        var groups = SideGroups.Of(HullB.Racks, Sides.StarboardFirst);

        groups!.Select(g => g.Select(r => r.Number).ToArray()).Should().BeEquivalentTo([HullBStarboard, HullBPort], o => o.WithStrictOrdering());
    }

    [Fact]
    public void A_rack_across_the_middle_line_comes_last()
    {
        var groups = SideGroups.Of(WithMiddle().Racks, Sides.PortFirst);

        groups!.Select(g => g.Single().Id).Should().Equal("port", "starboard", "middle");
    }

    [Fact]
    public void A_rack_touching_the_middle_line_belongs_to_its_side()
    {
        // As the Railen's racks 4 and 6 (y 0..4 and −4..0) and the Hull B's rack 3 (y −2..0).
        Rack[] racks =
        [
            new("port", 0, 32, new Dimensions(4, 4, 2), null, new ShipPoint(0, 0, 0)),
            new("starboard", 1, 32, new Dimensions(4, 4, 2), null, new ShipPoint(0, -4, 0)),
        ];

        SideGroups.Of(racks, Sides.PortFirst)!.Select(g => g.Single().Id).Should().Equal("port", "starboard");
    }

    [Theory]
    [InlineData("raft")]
    [InlineData("c2_hercules")]
    [InlineData("caterpillar")]
    [InlineData("hull_c")]
    public void A_ship_without_racks_on_both_sides_has_no_side_groups(string shipId) =>
        SideGroups.Of(FrozenShips.Load(shipId).Racks, Sides.PortFirst).Should().BeNull();

    [Fact]
    public void Without_sides_there_are_no_side_groups() =>
        SideGroups.Of(HullB.Racks, Sides.None).Should().BeNull();

    // --- R1: two destinations that fit the first side ---

    [Fact]
    public void Port_first_lays_two_destinations_that_fit_the_port_racks_only_there()
    {
        var plan = Planned(HullB, Missions(CrateSize.ThirtyTwo, 96, 96), Sides.PortFirst).Plan;

        plan.Crates.Select(c => NumberOf(HullB, c)).Should().OnlyContain(n => HullBPort.Contains(n), "the starboard racks stay empty");
    }

    [Fact]
    public void Starboard_first_lays_them_on_the_starboard_racks()
    {
        var plan = Planned(HullB, Missions(CrateSize.ThirtyTwo, 96, 96), Sides.StarboardFirst).Plan;

        plan.Crates.Select(c => NumberOf(HullB, c)).Should().OnlyContain(n => HullBStarboard.Contains(n));
    }

    [Fact]
    public void Without_grouping_the_racks_still_go_by_side()
    {
        var plan = Planned(HullB, Missions(CrateSize.ThirtyTwo, 96, 96), Sides.PortFirst, grouping: PlanGrouping.None).Plan;

        plan.Crates.Select(c => NumberOf(HullB, c)).Should().OnlyContain(n => HullBPort.Contains(n));
    }

    // --- R2: more than the first side holds ---

    [Fact]
    public void With_overflow_the_third_destination_fills_the_port_side_and_crosses_to_the_first_starboard_rack()
    {
        var plan = Planned(HullB, Missions(CrateSize.ThirtyTwo, 96, 96, 96), Sides.PortFirst).Plan;

        RacksOf(HullB, plan, "A").Should().OnlyContain(n => HullBPort.Contains(n));
        RacksOf(HullB, plan, "B").Should().OnlyContain(n => HullBPort.Contains(n));
        ScuIn(HullB, plan, "C", HullBPort).Should().Be(64);
        RacksOf(HullB, plan, "C").Where(n => HullBStarboard.Contains(n)).Should().Equal(3);
    }

    [Fact]
    public void Without_overflow_the_third_destination_lies_whole_on_the_starboard_side()
    {
        var plan = Planned(HullB, Missions(CrateSize.ThirtyTwo, 96, 96, 96), Sides.PortFirst, overflow: false).Plan;

        RacksOf(HullB, plan, "A").Concat(RacksOf(HullB, plan, "B")).Should().OnlyContain(n => HullBPort.Contains(n));
        RacksOf(HullB, plan, "C").Should().OnlyContain(n => HullBStarboard.Contains(n));
    }

    [Fact]
    public void Without_overflow_a_later_destination_goes_back_to_the_first_side_where_it_fits()
    {
        var plan = Planned(HullB, Missions(CrateSize.ThirtyTwo, 192, 96, 64), Sides.PortFirst, overflow: false).Plan;

        RacksOf(HullB, plan, "B").Should().OnlyContain(n => HullBStarboard.Contains(n));
        RacksOf(HullB, plan, "C").Should().OnlyContain(n => HullBPort.Contains(n), "64 SCU fit the two port racks left");
    }

    [Fact]
    public void Without_overflow_a_mission_that_fits_no_side_waits_though_racks_are_free()
    {
        var missions = Missions(CrateSize.ThirtyTwo, 160, 160, 160);
        var crossing = Planned(HullB, missions, Sides.PortFirst);
        var saved = Planned(HullB, missions, Sides.PortFirst, overflow: false);

        crossing.Missions.Should().OnlyContain(m => !m.Unplaced, "with overflow all three fit");
        saved.Missions.Single(m => m.Id == 3).Unplaced.Should().BeTrue();
        saved.Plan.Crates.Should().NotContain(c => c.Destination == "C");
        RacksOf(HullB, saved.Plan, "A").Should().OnlyContain(n => HullBPort.Contains(n));
        RacksOf(HullB, saved.Plan, "B").Should().OnlyContain(n => HullBStarboard.Contains(n));
        Notes(saved.Plan).Should().Contain(n => n.EndsWith(": sides full – a mission waits (side overflow off)", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_overflow_and_without_missions_a_destination_that_fits_no_side_crosses_with_a_note()
    {
        Stop[] stops =
        [
            new("A", 1, [new CargoItem("Gold", 160, CrateSize.ThirtyTwo)]),
            new("B", 2, [new CargoItem("Gold", 160, CrateSize.ThirtyTwo)]),
            new("C", 3, [new CargoItem("Gold", 160, CrateSize.ThirtyTwo)]),
        ];
        var plan = LoadPlanner.Plan(HullB, stops, FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 0, Sides.PortFirst, sideOverflow: false);

        plan.Crates.Where(c => c.Destination == "C").Sum(c => (int)c.CrateSize).Should().Be(160);
        Notes(plan).Should().Contain(n => n.EndsWith(": C crosses sides – side overflow off", StringComparison.Ordinal));
    }

    // --- R3: Hull A ---

    [Fact]
    public void On_the_hull_a_two_full_destinations_take_racks_1_and_2_and_a_third_rack_3()
    {
        var plan = Planned(HullA, Missions(CrateSize.Sixteen, 16, 16, 16), Sides.PortFirst).Plan;

        RacksOf(HullA, plan, "A").Concat(RacksOf(HullA, plan, "B")).Order().Should().Equal(1, 2);
        RacksOf(HullA, plan, "C").Should().Equal(3);
    }

    [Fact]
    public void On_the_hull_a_three_small_destinations_share_the_port_racks_while_starboard_is_empty()
    {
        // P4: side full first means capacity; one destination per rack (D64) holds within a side.
        var plan = Planned(HullA, Missions(CrateSize.Eight, 8, 8, 8), Sides.PortFirst).Plan;

        plan.Crates.Select(c => NumberOf(HullA, c)).Should().OnlyContain(n => n == 1 || n == 2);
    }

    // --- R4: a rack per side with lanes: side, rack, lane ---

    [Fact]
    public void Side_then_rack_then_lane()
    {
        var ship = TwoSides();
        var plan = Planned(ship, Missions(CrateSize.Four, 16, 16, 16), Sides.PortFirst).Plan;

        plan.Crates.Where(c => c.Destination == "A").Should().OnlyContain(c => c.RackId == "port" && c.Cells.All(x => x.Width >= 2));
        plan.Crates.Where(c => c.Destination == "B").Should().OnlyContain(c => c.RackId == "port" && c.Cells.All(x => x.Width < 2));
        plan.Crates.Where(c => c.Destination == "C").Should().OnlyContain(c => c.RackId == "starboard" && c.Cells.All(x => x.Width >= 2));
    }

    // --- R5: a rack across the middle line ---

    [Fact]
    public void A_rack_across_the_middle_line_is_filled_last()
    {
        var ship = WithMiddle();
        var plan = Planned(ship, Missions(CrateSize.Four, 16, 16, 16), Sides.PortFirst).Plan;

        plan.Crates.Where(c => c.Destination == "A").Should().OnlyContain(c => c.RackId == "port");
        plan.Crates.Where(c => c.Destination == "B").Should().OnlyContain(c => c.RackId == "starboard");
        plan.Crates.Where(c => c.Destination == "C").Should().OnlyContain(c => c.RackId == "middle");
    }

    [Fact]
    public void Without_overflow_the_middle_takes_a_whole_destination()
    {
        var ship = WithMiddle();
        var plan = Planned(ship, Missions(CrateSize.Four, 12, 12, 16), Sides.PortFirst, overflow: false).Plan;

        plan.Crates.Where(c => c.Destination == "A").Should().OnlyContain(c => c.RackId == "port");
        plan.Crates.Where(c => c.Destination == "B").Should().OnlyContain(c => c.RackId == "starboard");
        plan.Crates.Where(c => c.Destination == "C").Should().OnlyContain(c => c.RackId == "middle");
    }

    // --- R6: captures ---

    [Fact]
    public void A_capture_takes_the_next_empty_port_rack_before_a_starboard_one()
    {
        var saved = Empty(HullB, Sides.PortFirst);
        foreach (var destination in (string[])["A", "B", "C"])
        {
            saved = Scanned(saved, HullB, destination, 32, CrateSize.ThirtyTwo);
        }

        RacksOf(HullB, saved.Plan, "C").Should().Equal(5);
    }

    [Fact]
    public void A_capture_too_large_for_one_rack_stays_on_the_port_side()
    {
        var saved = Scanned(Scanned(Empty(HullB, Sides.PortFirst), HullB, "A", 48, CrateSize.ThirtyTwo), HullB, "B", 48, CrateSize.ThirtyTwo);

        saved.Plan.Crates.Select(c => NumberOf(HullB, c)).Should().OnlyContain(n => HullBPort.Contains(n));
    }

    [Fact]
    public void A_capture_for_a_destination_on_board_goes_into_its_rack()
    {
        var saved = Scanned(Scanned(Empty(HullB, Sides.PortFirst), HullB, "A", 16, CrateSize.Sixteen), HullB, "B", 32, CrateSize.ThirtyTwo);
        saved = Scanned(saved, HullB, "A", 16, CrateSize.Sixteen);

        RacksOf(HullB, saved.Plan, "A").Should().Equal(1);
    }

    [Fact]
    public void A_capture_with_the_port_side_full_goes_to_starboard_and_doesnt_wait()
    {
        var saved = Empty(HullB, Sides.PortFirst);
        foreach (var destination in Letters)
        {
            saved = Scanned(saved, HullB, destination, 32, CrateSize.ThirtyTwo);
        }

        saved = Scanned(saved, HullB, "I", 32, CrateSize.ThirtyTwo);

        saved.Missions.Should().OnlyContain(m => !m.Unplaced);
        RacksOf(HullB, saved.Plan, "I").Should().Equal(3);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_capture_without_overflow_keeps_to_one_side_where_one_takes_it(bool overflow)
    {
        // 224 + 16 SCU leave 16 on the port side: C's 32 doesn't fit there. With overflow its 16 goes back to port; without, all of C to starboard.
        var saved = Scanned(Scanned(Empty(HullB, Sides.PortFirst, overflow), HullB, "A", 224, CrateSize.ThirtyTwo), HullB, "B", 16, CrateSize.Sixteen);
        saved = LoadPlanner.PlaceInto(saved, HullB, [new MissionStop("C", 1, [new MissionItem("Gold", 32, CrateSize.ThirtyTwo), new MissionItem("Silver", 16, CrateSize.Sixteen)])]);

        RacksOf(HullB, saved.Plan, "A").Concat(RacksOf(HullB, saved.Plan, "B")).Should().OnlyContain(n => HullBPort.Contains(n));
        if (overflow)
        {
            ScuIn(HullB, saved.Plan, "C", HullBPort).Should().Be(16);
        }
        else
        {
            RacksOf(HullB, saved.Plan, "C").Should().OnlyContain(n => HullBStarboard.Contains(n));
        }
    }

    // --- F5: loaded cargo stays, the rest goes by side ---

    [Fact]
    public void With_loaded_cargo_the_editors_plan_lays_the_rest_by_side_around_it()
    {
        var missions = Missions(CrateSize.ThirtyTwo, 32, 32);
        var saved = Planned(HullB, missions, Sides.PortFirst);
        var loaded = new SavedPlan(saved.Missions, LoadPlanner.SetLoaded(saved.Plan, HullB, "A", 1, loaded: true), saved.CrateCap);
        Mission[] more = [.. missions, new Mission(3, [new MissionStop("C", 1, [new MissionItem("Gold", 32, CrateSize.ThirtyTwo)])])];

        var (plan, _) = LoadPlanner.PlanKeeping(loaded, HullB, more, ["A", "B", "C"], FillOrder.BowFirst, FillRows.Crosswise, PlanGrouping.ByDestination, 0, Sides.PortFirst);

        plan.Plan.Crates.Where(c => c.Destination == "A").Should().Equal(loaded.Plan.Crates.Where(c => c.Destination == "A"), "loaded cargo stays");
        plan.Plan.Crates.Select(c => NumberOf(HullB, c)).Should().OnlyContain(n => HullBPort.Contains(n));
    }

    // --- R8: property ---

    /// <summary>
    /// Seeded mixes on the Hull B (1–8 destinations of 8s, 16s and 32s, at most the ship): with overflow no starboard crate
    /// could have gone into a port rack as the plan lies (crates of 2 × 2 × 2 multiples fill a rack of 8 × 2 × 2 to the
    /// last SCU, so free SCU is room); without overflow no destination on both sides.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void No_starboard_rack_while_a_port_rack_could_take_it(bool overflow)
    {
        var random = new Random(117);
        CrateSize[] sizes = [CrateSize.Eight, CrateSize.Sixteen, CrateSize.ThirtyTwo];
        for (var run = 0; run < 40; run++)
        {
            var count = random.Next(1, 9);
            var max = sizes[random.Next(sizes.Length)];
            int[] scu = [.. Enumerable.Range(0, count).Select(_ => (int)max * random.Next(1, 160 / (int)max + 1))];
            if (scu.Sum() > HullB.TotalScu)
            {
                continue;
            }

            var saved = Planned(HullB, Missions(max, scu), Sides.PortFirst, overflow);
            var plan = saved.Plan;
            var portFree = HullBPort.Sum(n => 32 - plan.Crates.Where(c => NumberOf(HullB, c) == n).Sum(c => (int)c.CrateSize));
            var context = $"run {run}: {string.Join(" + ", scu)} in {(int)max}s";
            if (overflow)
            {
                saved.Missions.Should().OnlyContain(m => !m.Unplaced, context);
                foreach (var crate in plan.Crates.Where(c => HullBStarboard.Contains(NumberOf(HullB, c))))
                {
                    HullBPort.Where(n => 32 - plan.Crates.Where(c => NumberOf(HullB, c) == n).Sum(c => (int)c.CrateSize) >= (int)crate.CrateSize)
                        .Should().BeEmpty($"{context}: a {(int)crate.CrateSize} of {crate.Destination} in rack {NumberOf(HullB, crate)}, {portFree} SCU free on port");
                }
            }
            else
            {
                foreach (var destination in plan.Crates.Select(c => c.Destination).Distinct())
                {
                    var racks = RacksOf(HullB, plan, destination).ToArray();
                    (racks.All(n => HullBPort.Contains(n)) || racks.All(n => HullBStarboard.Contains(n))).Should().BeTrue($"{context}: {destination} in racks {string.Join(",", racks)}");
                }
            }
        }
    }

    // --- R9: notes ---

    [Fact]
    public void A_ship_with_racks_on_both_sides_no_longer_says_lanes_apply_per_rack()
    {
        var plan = Planned(TwoSides(), Missions(CrateSize.Four, 16, 16), Sides.PortFirst).Plan;

        Notes(plan).Should().NotContain(n => n.Contains("side-first rack order", StringComparison.Ordinal));
        SectionNotes.IsNote(new PlanWarning("rack-0", "lanes apply per rack – side-first rack order is not available yet")).Should().BeFalse();
    }

    [Fact]
    public void The_sides_full_note_goes_to_the_sections_line() =>
        SectionNotes.IsNote(new PlanWarning("rack-2", "Rack 3: sides full – a mission waits (side overflow off)")).Should().BeTrue();
}
