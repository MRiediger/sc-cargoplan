using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.Core.Tests;

/// <summary>
/// A capture into a plan with sections (D41, D89): nothing moves, the new crates go into their destination's rows, next
/// to them, or into rows of their own; what can't says so, and Re-plan builds the sections anew.
/// </summary>
public class RackSectionsSyncTests
{
    private static Ship OneRack(int length, int width, int stack)
    {
        Rack rack = new("rack-0", 0, length * width * stack, new Dimensions(length, width, stack), null, new ShipPoint(0, 0, 0));
        return new Ship("flat", "Flat", rack.CapacityScu, [rack]);
    }

    private static MissionStop StopOf(string destination, int scu, CrateSize size) =>
        new(destination, 1, [new MissionItem("Titanium", scu, size)]);

    /// <summary>A plan with sections crosswise from the bow, one mission per stop, the stops in this order.</summary>
    private static SavedPlan Sectioned(Ship ship, params MissionStop[] stops) =>
        LoadPlanner.Plan(ship, [.. stops.Select((s, i) => new Mission(i + 1, [s]))], [.. stops.Select(s => s.Destination)], FillOrder.BowFirst, 0, FillRows.Crosswise, PlanGrouping.ByDestination);

    private static int[] Lengths(SavedPlan saved, string destination) =>
        [.. saved.Plan.Crates.Where(c => c.Destination == destination).SelectMany(c => c.Cells).Select(c => c.Length).Distinct().OrderDescending()];

    private static IEnumerable<string> Notes(SavedPlan saved) => saved.Plan.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message);

    private static void NothingMoved(SavedPlan before, SavedPlan after) =>
        LoadPlanner.MovedCrates(before.Plan, after.Plan).Should().Be(0);

    [Fact]
    public void A_mission_to_a_destination_on_board_goes_into_its_section()
    {
        // A's 3 SCU take two rows of two: one cell is free there, though empty rows lie behind B.
        var ship = OneRack(6, 2, 1);
        var saved = Sectioned(ship, new MissionStop("A", 1, [new MissionItem("Ice", 3, CrateSize.One)]), StopOf("B", 2, CrateSize.One));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("A", 1, CrateSize.One)]);

        NothingMoved(saved, placed);
        Lengths(placed, "A").Should().Equal(5, 4);
        Notes(placed).Should().BeEmpty();
    }

    [Fact]
    public void A_full_section_grows_into_the_empty_row_next_to_it()
    {
        var ship = OneRack(6, 2, 1);
        var saved = Sectioned(ship, StopOf("A", 4, CrateSize.Four), StopOf("B", 4, CrateSize.Four));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("B", 2, CrateSize.One)]);

        NothingMoved(saved, placed);
        Lengths(placed, "B").Should().Equal(3, 2, 1);
        Notes(placed).Should().BeEmpty();
    }

    [Fact]
    public void A_full_section_with_no_empty_row_beside_it_says_so()
    {
        var ship = OneRack(6, 2, 1);
        var saved = Sectioned(ship, StopOf("A", 4, CrateSize.Four), StopOf("B", 4, CrateSize.Four));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("A", 2, CrateSize.One)]);

        NothingMoved(saved, placed);
        placed.Missions.Should().OnlyContain(m => !m.Unplaced);
        Notes(placed).Should().Equal("Rack 1: A placed outside sections (capacity)");
    }

    [Fact]
    public void A_new_destination_gets_rows_of_its_own()
    {
        var ship = OneRack(6, 2, 1);
        var saved = Sectioned(ship, StopOf("A", 4, CrateSize.Four), StopOf("B", 4, CrateSize.Four));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("C", 2, CrateSize.Two)]);

        NothingMoved(saved, placed);
        Lengths(placed, "C").Should().Equal(1, 0);
        Notes(placed).Should().BeEmpty();
    }

    [Fact]
    public void A_new_destination_without_a_free_row_lies_in_free_cells_and_says_so()
    {
        // Each of A and B takes a row of four, with cells free in both.
        var ship = OneRack(2, 4, 1);
        var saved = Sectioned(ship, StopOf("A", 1, CrateSize.One), StopOf("B", 1, CrateSize.One));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("C", 1, CrateSize.One)]);

        NothingMoved(saved, placed);
        placed.Missions.Should().OnlyContain(m => !m.Unplaced);
        Notes(placed).Should().Equal("Rack 1: C: no free section – Re-plan");
    }

    [Fact]
    public void A_mission_that_finds_no_cell_waits_and_Re_plan_lays_it_in()
    {
        // Holes in sections hold no 4 (D89, decision 2): the capture waits where a plan without sections has room.
        var ship = OneRack(4, 2, 1);
        var saved = Sectioned(ship, StopOf("A", 1, CrateSize.One), StopOf("B", 1, CrateSize.One), StopOf("C", 1, CrateSize.One));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("D", 4, CrateSize.Four)]);
        placed.Missions.Single(m => m.Stops[0].Destination == "D").Unplaced.Should().BeTrue();

        var replanned = LoadPlanner.Replan(placed, ship);

        replanned.Missions.Should().OnlyContain(m => !m.Unplaced);
        Lengths(replanned, "D").Should().HaveCount(2);
    }

    [Fact]
    public void A_rack_with_one_destination_takes_more_of_it_as_it_comes()
    {
        var ship = OneRack(6, 2, 1);
        var saved = Sectioned(ship, StopOf("A", 2, CrateSize.One));

        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("A", 2, CrateSize.One)]);

        placed.Plan.Crates.Select(c => c.Position).Should().Equal(
            LoadPlanner.Plan(ship, [new Stop("A", 1, [new CargoItem("Titanium", 4, CrateSize.One)])], FillOrder.BowFirst, FillRows.Crosswise).Crates.Select(c => c.Position));
    }

    [Fact]
    public void Without_grouping_the_capture_lies_as_it_always_did()
    {
        var ship = OneRack(6, 2, 1);
        var plain = LoadPlanner.Plan(ship, [new Mission(1, [StopOf("A", 4, CrateSize.Four)]), new Mission(2, [StopOf("B", 4, CrateSize.Four)])], ["A", "B"]);

        var placed = LoadPlanner.PlaceInto(plain, ship, [StopOf("A", 2, CrateSize.One)]);

        Notes(placed).Should().BeEmpty();
    }

    [Fact]
    public void A_re_plan_clears_the_notes_of_the_sync()
    {
        var ship = OneRack(6, 2, 1);
        var saved = Sectioned(ship, StopOf("A", 4, CrateSize.Four), StopOf("B", 4, CrateSize.Four));
        var placed = LoadPlanner.PlaceInto(saved, ship, [StopOf("A", 2, CrateSize.One)]);

        var replanned = LoadPlanner.Replan(placed, ship);

        Notes(replanned).Should().BeEmpty();
        Lengths(replanned, "A").Should().Equal(5, 4, 3);
    }

    // --- Notes follow the crates ---

    /// <summary>Three destinations in two rows of two: C shares with B.</summary>
    private static (Ship Ship, SavedPlan Saved) Sharing()
    {
        var ship = OneRack(2, 2, 1);
        return (ship, Sectioned(ship, StopOf("A", 1, CrateSize.One), StopOf("B", 1, CrateSize.One), StopOf("C", 1, CrateSize.One)));
    }

    [Fact]
    public void Moving_a_crate_of_a_named_destination_clears_the_note()
    {
        var (ship, saved) = Sharing();
        Notes(saved).Should().Equal("Rack 1: C shares a section with B");
        var crate = saved.Plan.Crates.First(c => c.Destination == "C");

        var free = new[] { new GridPosition(0, 0, 0), new GridPosition(0, 1, 0), new GridPosition(1, 0, 0), new GridPosition(1, 1, 0) }
            .First(p => !saved.Plan.Crates.Any(c => c.Position == p));
        var moved = LoadPlanner.MoveCrate(saved.Plan, ship, crate, crate.RackId, free);

        moved.Warnings.Where(SectionNotes.IsNote).Should().BeEmpty();
    }

    [Fact]
    public void Moving_another_destinations_crate_keeps_the_note()
    {
        var (ship, saved) = Sharing();
        var crate = saved.Plan.Crates.First(c => c.Destination == "A");

        var free = new[] { new GridPosition(0, 0, 0), new GridPosition(0, 1, 0), new GridPosition(1, 0, 0), new GridPosition(1, 1, 0) }
            .First(p => !saved.Plan.Crates.Any(c => c.Position == p));
        var moved = LoadPlanner.MoveCrate(saved.Plan, ship, crate, crate.RackId, free);

        moved.Warnings.Where(SectionNotes.IsNote).Select(w => w.Message).Should().Equal("Rack 1: C shares a section with B");
    }

    [Fact]
    public void A_note_goes_once_a_destination_it_names_has_left_the_ship()
    {
        var (ship, saved) = Sharing();

        var ticked = LoadPlanner.SetDelivered(saved.Plan, ship, "C", true);

        ticked.Warnings.Where(SectionNotes.IsNote).Should().BeEmpty();
    }
}
