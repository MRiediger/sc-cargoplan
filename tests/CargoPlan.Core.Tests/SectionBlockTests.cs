using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A section is a closed block (B17, D41 and D89 addenda): no empty cell lies between crates of one section, along the row
/// or across it. A capture to a destination on board lays its planned crates in the rack anew; loaded and delivered ones
/// stay. A single crate takes the free cell with the most neighbours of its section, on a tie the first in fill order.
/// The scene is Martin's stims scan at Everus Harbor (10 SCU, four destinations, 2+1 mixes), built here: the plan.json
/// it came from is gone.
/// </summary>
public class SectionBlockTests
{
    private const string Green = "Green Glade Station";
    private const string High = "High Course Station";
    private const string Thunder = "Thundering Express Station";
    private const string Faithful = "Faithful Dream Station";
    private const string Melodic = "Melodic Fields Station";

    private static readonly Ship Raft = FrozenShips.Load("raft");

    private static MissionStop StopOf(string destination, int order, int scu, CrateSize max) =>
        new(destination, order, [new MissionItem("Stims", scu, max, Pickup: ["Everus Harbor"])]);

    /// <summary>The scan: one contract, four deliveries in contract order; the two first with a single crate left over.</summary>
    private static MissionStop[] Scan(string mix) => mix switch
    {
        "2+1" => [StopOf(Green, 1, 3, CrateSize.Two), StopOf(High, 2, 3, CrateSize.Two), StopOf(Thunder, 3, 2, CrateSize.Two), StopOf(Melodic, 4, 2, CrateSize.Two)],
        "4+1" => [StopOf(Green, 1, 5, CrateSize.Four), StopOf(High, 2, 5, CrateSize.Four), StopOf(Thunder, 3, 2, CrateSize.Two), StopOf(Melodic, 4, 2, CrateSize.Two)],
        "4+2+1" => [StopOf(Green, 1, 7, CrateSize.Four), StopOf(High, 2, 7, CrateSize.Four), StopOf(Thunder, 3, 2, CrateSize.Two), StopOf(Melodic, 4, 2, CrateSize.Two)],
        _ => throw new ArgumentException(mix, nameof(mix)),
    };

    private static SavedPlan Planned(Ship ship, MissionStop[] scan, FillOrder order, FillRows rows, int gap) =>
        LoadPlanner.Plan(ship, [new Mission(1, scan)], [.. scan.Select(s => s.Destination)], order, rows, PlanGrouping.ByDestination, gap);

    /// <summary>Martin's RAFT plan of 5 November without its stims contract (mission 3), the rest renumbered 1–3, with sections.</summary>
    private static SavedPlan FixtureWithoutStims(FillOrder order, FillRows rows, int gap)
    {
        using var stream = MissionFixtures.Open("plans/raft_hand_2026-11-05.json");
        var saved = PlanFile.Read(stream, "raft", Raft);
        Mission[] missions = [.. saved.Missions.Where(m => m.Id != 3).Select((m, i) => new Mission(i + 1, m.Stops))];
        string[] route = [.. saved.Stops.Select(s => s.Destination).Where(d => missions.Any(m => m.Stops.Any(s => s.Destination == d)))];
        return LoadPlanner.Plan(Raft, missions, route, order, rows, PlanGrouping.ByDestination, gap);
    }

    private static IEnumerable<string> Notes(LoadPlan plan) => plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message);

    private static (CrateSize, GridPosition, bool)[] Cells(LoadPlan plan, string destination) =>
        [.. plan.Crates.Where(c => c.Destination == destination && !c.Delivered).Select(c => (c.CrateSize, c.Position, c.Rotated))
            .OrderBy(c => c.CrateSize).ThenBy(c => c.Position.Length).ThenBy(c => c.Position.Width).ThenBy(c => c.Position.Stack)];

    /// <summary>
    /// The empty cells that lie between crates of one destination: in a layer, the nearest crate on both sides along the
    /// length, or along the width, is that destination's. As "destination @ length.width.stack", for the message.
    /// Destinations in <paramref name="except"/> are not looked at.
    /// </summary>
    internal static List<string> Enclosed(LoadPlan plan, Ship ship, IReadOnlySet<string>? except = null)
    {
        List<string> found = [];
        foreach (var rack in ship.Racks)
        {
            var grid = rack.Grid;
            var owner = new string?[grid.Length, grid.Width, grid.Stack];
            foreach (var cell in plan.Crates.Where(c => c.RackId == rack.Id && !c.Delivered).SelectMany(c => c.Cells.Select(cell => (cell, c.Destination))))
            {
                owner[cell.cell.Length, cell.cell.Width, cell.cell.Stack] = cell.Destination;
            }

            for (var l = 0; l < grid.Length; l++)
            {
                for (var w = 0; w < grid.Width; w++)
                {
                    for (var s = 0; s < grid.Stack; s++)
                    {
                        if (owner[l, w, s] is not null)
                        {
                            continue;
                        }

                        foreach (var (dl, dw) in (ReadOnlySpan<(int, int)>)[(1, 0), (0, 1)])
                        {
                            if (Nearest(owner, l, w, s, -dl, -dw) is { } before && before == Nearest(owner, l, w, s, dl, dw) && except?.Contains(before) != true)
                            {
                                found.Add(FormattableString.Invariant($"{before} @ {l}.{w}.{s}"));
                                break;
                            }
                        }
                    }
                }
            }
        }

        return found;
    }

    private static string? Nearest(string?[,,] owner, int l, int w, int s, int dl, int dw)
    {
        for (var (x, y) = (l + dl, w + dw); x >= 0 && y >= 0 && x < owner.GetLength(0) && y < owner.GetLength(1); (x, y) = (x + dl, y + dw))
        {
            if (owner[x, y, s] is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // --- R1: Martin's picture ---

    [Fact]
    public void A_capture_onto_a_destination_on_board_closes_its_section()
    {
        // Bild B: High Course's 4+2+1 lies as an L in L7–L8; the scan's 2+1 for it was hung on behind, L7/w3 stayed empty.
        var saved = FixtureWithoutStims(FillOrder.BowFirst, FillRows.Crosswise, 1);

        var placed = LoadPlanner.PlaceInto(saved, Raft, Scan("2+1")).Plan;

        Cells(placed, High).Should().Equal(
            (CrateSize.One, new GridPosition(7, 4, 0), false),
            (CrateSize.One, new GridPosition(8, 4, 0), false),
            (CrateSize.Two, new GridPosition(7, 2, 0), false),
            (CrateSize.Two, new GridPosition(7, 3, 0), false),
            (CrateSize.Four, new GridPosition(7, 0, 0), false));
        Enclosed(placed, Raft).Should().BeEmpty();
        Cells(placed, Faithful).Should().Equal(Cells(saved.Plan, Faithful), "a destination the scan doesn't name stays");
        Notes(placed).Should().BeEmpty();
    }

    [Fact]
    public void Loaded_crates_stay_and_the_new_ones_close_the_block_around_them()
    {
        var saved = FixtureWithoutStims(FillOrder.BowFirst, FillRows.Crosswise, 1);
        SavedPlan loaded = new(saved.Missions, LoadPlanner.SetLoaded(saved.Plan, Raft, High, 1, true), saved.CrateCap);

        var placed = LoadPlanner.PlaceInto(loaded, Raft, Scan("2+1")).Plan;

        Cells(placed with { Crates = [.. placed.Crates.Where(c => c.Loaded)] }, High)
            .Should().Equal(Cells(loaded.Plan with { Crates = [.. loaded.Plan.Crates.Where(c => c.Loaded)] }, High));
        Enclosed(placed, Raft).Should().BeEmpty("the new single takes L7/w3, between its loaded 2 and the new 2");
        Notes(placed).Should().BeEmpty();
    }

    [Fact]
    public void A_gap_loaded_crates_leave_stays_and_says_so()
    {
        // Loaded 4+2+1 as an L; a new 2 alone can't go into L7/w3: the gap stays, nothing loaded moves.
        var saved = FixtureWithoutStims(FillOrder.BowFirst, FillRows.Crosswise, 1);
        SavedPlan loaded = new(saved.Missions, LoadPlanner.SetLoaded(saved.Plan, Raft, High, 1, true), saved.CrateCap);

        var placed = LoadPlanner.PlaceInto(loaded, Raft, [StopOf(High, 1, 2, CrateSize.Two)]).Plan;

        Enclosed(placed, Raft).Should().Equal($"{High} @ 7.3.0");
        Notes(placed).Should().Equal($"Rack 1: {High} has a gap – loaded crates");
        SectionNotes.IsNote(placed.Warnings.Single(w => w.Message.Contains("has a gap", StringComparison.Ordinal))).Should().BeTrue();
    }

    [Fact]
    public void The_same_gap_without_loaded_crates_closes()
    {
        var saved = FixtureWithoutStims(FillOrder.BowFirst, FillRows.Crosswise, 1);

        var placed = LoadPlanner.PlaceInto(saved, Raft, [StopOf(High, 1, 2, CrateSize.Two)]).Plan;

        Enclosed(placed, Raft).Should().BeEmpty();
        Notes(placed).Should().BeEmpty();
    }

    // --- E1: the tie ---

    [Theory]
    [InlineData(FillOrder.BowFirst, 11)]
    [InlineData(FillOrder.SternFirst, 0)]
    public void On_a_tie_of_neighbours_the_single_takes_the_first_cell_in_fill_order(FillOrder order, int first)
    {
        // Green's 2 lies along L10–L11 (L0–L1): the cells next to it in both rows have one neighbour each; the single takes
        // the one its fill order reaches first, so the L of a fresh plan stays as it was (Martin: the L is no fault).
        var plan = Planned(Raft, Scan("2+1"), order, FillRows.Crosswise, 1).Plan;

        Cells(plan, Green).Should().Contain((CrateSize.One, new GridPosition(first, 1, 0), false));
    }

    [Fact]
    public void A_second_single_goes_where_it_has_two_neighbours_not_the_first_in_fill_order()
    {
        // 4, 2, 2 fill w0–w3 of L7–L8; the first single takes L8/w4 (a tie, fill order), the second L7/w4 (two neighbours)
        // rather than L8/w5 (one, but first in fill order).
        var saved = FixtureWithoutStims(FillOrder.BowFirst, FillRows.Crosswise, 1);

        var placed = LoadPlanner.PlaceInto(saved, Raft, Scan("2+1")).Plan;

        Cells(placed, High).Where(c => c.Item1 == CrateSize.One).Select(c => c.Item2)
            .Should().Equal(new GridPosition(7, 4, 0), new GridPosition(8, 4, 0));
    }

    // --- R3: the matrix ---

    public static TheoryData<string, string, FillRows, int, FillOrder> Matrix()
    {
        TheoryData<string, string, FillRows, int, FillOrder> data = [];
        foreach (var ship in (string[])["raft", "perseus"])
        {
            foreach (var mix in (string[])["2+1", "4+1", "4+2+1"])
            {
                foreach (var rows in Enum.GetValues<FillRows>())
                {
                    foreach (var gap in (int[])[0, 1, 2])
                    {
                        foreach (var order in Enum.GetValues<FillOrder>())
                        {
                            data.Add(ship, mix, rows, gap, order);
                        }
                    }
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void No_section_has_an_empty_cell_inside_in_a_plan_or_a_capture(string shipId, string mix, FillRows rows, int gap, FillOrder order)
    {
        var ship = FrozenShips.Load(shipId);
        var scan = Scan(mix);
        var fresh = Planned(ship, scan, order, rows, gap);
        var empty = LoadPlanner.Plan(ship, [], [], order, rows, PlanGrouping.ByDestination, gap);

        Enclosed(fresh.Plan, ship).Should().BeEmpty("a fresh plan");
        Enclosed(LoadPlanner.PlaceInto(empty, ship, scan).Plan, ship).Should().BeEmpty("a capture into an empty plan");
        Enclosed(LoadPlanner.PlaceInto(fresh, ship, scan).Plan, ship).Should().BeEmpty("the same scan captured again");
        if (shipId == "raft")
        {
            Enclosed(LoadPlanner.PlaceInto(FixtureWithoutStims(order, rows, gap), ship, scan).Plan, ship).Should().BeEmpty("the scan into Martin's plan");
        }
    }

    [Theory]
    [InlineData("2+1", FillRows.Crosswise, 0)]
    [InlineData("2+1", FillRows.Crosswise, 1)]
    [InlineData("2+1", FillRows.Crosswise, 2)]
    [InlineData("2+1", FillRows.Lengthwise, 0)]
    [InlineData("2+1", FillRows.Lengthwise, 1)]
    [InlineData("4+1", FillRows.Crosswise, 2)]
    [InlineData("4+2+1", FillRows.Crosswise, 2)]
    public void Between_the_sections_lie_exactly_the_gap_rows(string mix, FillRows rows, int gap)
    {
        // Lengthwise gap 2 needs 10 of the RAFT's 8 rows, so the ladder drops gaps (K9.2b): not here.
        var plan = Planned(Raft, Scan(mix), FillOrder.BowFirst, rows, gap).Plan;
        var axis = RackSections.RowAxis.Of(Raft.Racks[0], FillOrder.BowFirst, rows);

        int[][] places = [.. Scan(mix).Select(s => plan.Crates.Where(c => c.Destination == s.Destination).SelectMany(c => c.Cells)
            .Select(c => axis.Rows.ToList().IndexOf(axis.RowOf(c))).Distinct().Order().ToArray())];
        for (var i = 1; i < places.Length; i++)
        {
            (places[i][0] - places[i - 1][^1] - 1).Should().Be(gap, $"between section {i} and {i + 1}");
        }
    }

    // --- R4: any mix ---

    [Fact]
    public void Sections_of_random_mixes_have_no_empty_cell_inside()
    {
        var random = new Random(17);
        List<string> failures = [];
        for (var run = 0; run < 200; run++)
        {
            var count = random.Next(2, 7);
            MissionStop[] stops = [.. Enumerable.Range(0, count).Select(i => StopOf(
                $"Stop {i + 1}", i + 1, random.Next(1, 13), ((CrateSize[])[CrateSize.One, CrateSize.Two, CrateSize.Four])[random.Next(3)]))];
            var rows = random.Next(2) == 0 ? FillRows.Crosswise : FillRows.Lengthwise;
            var gap = random.Next(3);

            var fresh = Planned(Raft, stops, FillOrder.BowFirst, rows, gap);
            var again = LoadPlanner.PlaceInto(fresh, Raft, stops);
            foreach (var (name, plan) in (ReadOnlySpan<(string, LoadPlan)>)[("plan", fresh.Plan), ("capture twice", again.Plan)])
            {
                // A rack whose sections didn't fit lies as with None (D89). One with a destination placed outside sections
                // (capacity) can leave a gap in another section: backlog (run 135, A_destination_outside_sections_...).
                // A destination without a free section lies in free cells, not in a section of its own.
                string[] notes = [.. Notes(plan)];
                if (notes.Any(n => n.EndsWith("sections don't fit – laid as with None", StringComparison.Ordinal) || n.EndsWith(" placed outside sections (capacity)", StringComparison.Ordinal)))
                {
                    continue;
                }

                HashSet<string> free = [.. plan.Warnings.Where(w => w.Message.EndsWith(": no free section – Re-plan", StringComparison.Ordinal)).SelectMany(w => SectionNotes.Names(w)!)];
                if (Enclosed(plan, Raft, free) is [_, ..] holes)
                {
                    failures.Add($"run {run} {name} {rows} gap {gap} [{string.Join(", ", stops.Select(s => $"{s.Items[0].Scu}/{(int)s.Items[0].MaxCrateSize}"))}]: {string.Join("; ", holes)}");
                }
            }
        }

        failures.Should().BeEmpty();
    }

    [Fact]
    public void A_destination_outside_sections_can_leave_a_gap_in_another_section()
    {
        // Backlog (run 135 of the property test): B's second 9 singles find no rows of their own and lie outside sections
        // (capacity), partly in C's rows; C's second 4 can't go there, L8/w3–w4 stay empty between C's crates. The rack
        // says why. If this turns green, the backlog line "Kapazitäts-Ausweichweg erst nach den anderen Zielen" is done.
        MissionStop[] stops = [StopOf("A", 1, 1, CrateSize.Two), StopOf("B", 2, 9, CrateSize.One), StopOf("C", 3, 5, CrateSize.Four), StopOf("D", 4, 6, CrateSize.Four)];
        var fresh = Planned(Raft, stops, FillOrder.BowFirst, FillRows.Crosswise, 0);

        var again = LoadPlanner.PlaceInto(fresh, Raft, stops).Plan;

        Enclosed(again, Raft).Should().Equal("C @ 8.3.0", "C @ 8.4.0");
        Notes(again).Should().Contain("Rack 1: B placed outside sections (capacity)");
    }

    [Fact]
    public void A_capture_lays_a_shared_section_anew_whole()
    {
        // Run 103 of the property test (Martin: the whole shared section anew): F shares a section with B; a scan to F lays
        // B's planned crates anew with F's, so the block closes. B stays in the section's rows.
        MissionStop[] stops = [StopOf("A", 1, 8, CrateSize.Four), StopOf("B", 2, 8, CrateSize.Two), StopOf("C", 3, 10, CrateSize.One), StopOf("D", 4, 10, CrateSize.Four), StopOf("E", 5, 10, CrateSize.Four), StopOf("F", 6, 7, CrateSize.Four)];
        var fresh = Planned(Raft, stops, FillOrder.BowFirst, FillRows.Lengthwise, 0);
        Notes(fresh.Plan).Should().Contain("Rack 1: F shares a section with B");
        int[] sectionRows = [.. fresh.Plan.Crates.Where(c => c.Destination is "B" or "F").SelectMany(c => c.Cells).Select(c => c.Width).Distinct().Order()];

        var again = LoadPlanner.PlaceInto(fresh, Raft, stops).Plan;

        Enclosed(again, Raft).Should().BeEmpty();
        again.Crates.Where(c => c.Destination is "B" or "F").SelectMany(c => c.Cells).Select(c => c.Width).Distinct().Order().Should().Equal(sectionRows);
        Cells(again, "C").Should().Equal(Cells(LoadPlanner.PlaceInto(fresh, Raft, [stops[2]]).Plan, "C"), "C shares with nobody");
    }

    // --- R5: what a capture never changes ---

    [Fact]
    public void A_capture_moves_no_loaded_or_delivered_crate_and_changes_no_rack_or_waiting_mission()
    {
        var saved = FixtureWithoutStims(FillOrder.BowFirst, FillRows.Crosswise, 1);
        var ticked = LoadPlanner.SetLoaded(saved.Plan, Raft, Green, 1, true);
        ticked = LoadPlanner.SetDelivered(ticked, Raft, Thunder, 2, true);
        SavedPlan before = new(saved.Missions, ticked, saved.CrateCap);

        var placed = LoadPlanner.PlaceInto(before, Raft, Scan("2+1"));
        var plain = LoadPlanner.PlaceInto(new SavedPlan(before.Missions, before.Plan with { Grouping = PlanGrouping.None, SectionGap = 0 }, before.CrateCap), Raft, Scan("2+1"));

        placed.Plan.Crates.Where(c => c.Loaded || c.Delivered).Should().Equal(before.Plan.Crates.Where(c => c.Loaded || c.Delivered));
        placed.Plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId)).Order()
            .Should().Equal(plain.Plan.Crates.Select(c => (c.Destination, c.Commodity, c.CrateSize, c.RackId)).Order());
        placed.Missions.Select(m => m.Unplaced).Should().Equal(plain.Missions.Select(m => m.Unplaced));
    }
}
