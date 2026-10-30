using CargoPlan.App.ShipView;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

/// <summary>
/// What a running drag shows (D25 addendum B3, D65), the rules of the 2D view taken over (T8.4): the target rack's edge
/// for a legend drag always, for crates only while the layout is left to the drop; every crate that changes place faded
/// where it is, its ghost where it goes; red ghosts at the cells aimed at, only for a refusal. Split and shared as in
/// <see cref="CrateDropTests"/>: Orison's two Gold crates on top in Rack 3.
/// </summary>
public class DragPreviewTests
{
    private static (Ship Ship, LoadPlan Plan, Crate Bow, Crate Aft) Gold()
    {
        var (ship, plan) = TestPlans.Load();
        return (ship, plan,
            plan.Crates.Single(c => c.Commodity == "Gold" && c.Position.Length == 1),
            plan.Crates.Single(c => c.Commodity == "Gold" && c.Position.Length == 0));
    }

    private static MoveOutcome Refused(LoadPlan plan) => new(null, MoveStage.Refused, "Crates of Orison and Riker overlap in rack 2", []);

    [Fact]
    public void A_legend_drag_marks_its_target_rack_whatever_the_outcome()
    {
        var (ship, plan, bow, aft) = Gold();
        Gesture.MoveLegend move = new([bow, aft], "rack-1");

        var preview = DragPreview.Of(ship, plan, [bow, aft], move, CrateDrop.LegendOutcome(plan, ship, move));

        preview.TargetRackId.Should().Be("rack-1");
    }

    [Fact]
    public void A_crate_drag_marks_its_target_rack_only_while_the_drop_will_lay_it_out()
    {
        var (ship, plan, gold, _) = Gold();
        Gesture.MoveCrates move = new(gold, [gold], "rack-1", new GridPosition(0, 0, 1));

        DragPreview.Of(ship, plan, [gold], move, CrateDrop.Outcome(plan, ship, move)).TargetRackId.Should().BeNull("it lands at the cell aimed at");
        DragPreview.Of(ship, plan, [gold], move, new MoveOutcome(null, MoveStage.Pending, null, [])).TargetRackId.Should().Be("rack-1");
    }

    [Fact]
    public void Every_crate_that_changes_place_fades_and_shows_its_ghost_where_it_goes()
    {
        // Onto the full Rack 1: Core makes room, so Riker's crates move too (D65).
        var (ship, plan, gold, _) = Gold();
        Gesture.MoveCrates move = new(gold, [gold], "rack-0", new GridPosition(0, 0, 1));
        var outcome = CrateDrop.Outcome(plan, ship, move);
        var changed = CrateDrop.Changed(plan, outcome);

        var preview = DragPreview.Of(ship, plan, [gold], move, outcome);

        changed.Should().HaveCountGreaterThan(1);
        preview.Faded.Should().BeEquivalentTo(changed.Select(c => c.Before).Append(gold).Distinct());
        preview.Ghosts.Should().Equal(changed.Select(c => c.After));
        preview.Refused.Should().BeEmpty();
    }

    [Fact]
    public void The_crates_held_fade_even_where_nothing_changes()
    {
        var (ship, plan, gold, _) = Gold();

        var preview = DragPreview.Of(ship, plan, [gold], new Gesture.None(), null);

        preview.Faded.Should().BeEquivalentTo([gold]);
        preview.Ghosts.Should().BeEmpty();
        preview.TargetRackId.Should().BeNull();
    }

    [Fact]
    public void A_refusal_shows_red_ghosts_at_the_cells_aimed_at_and_no_others()
    {
        var (ship, plan, bow, aft) = Gold();
        Gesture.MoveCrates move = new(aft, [bow, aft], "rack-1", new GridPosition(0, 0, 1));

        var preview = DragPreview.Of(ship, plan, [aft, bow], move, Refused(plan));

        preview.Refused.Should().BeEquivalentTo(CrateDrop.Moves(ship, move)!.Select(m => m.Crate with { RackId = m.RackId, Position = m.Position, Rotated = m.Rotated }));
        preview.Ghosts.Should().BeEmpty();
        preview.Faded.Should().BeEquivalentTo([aft, bow]);
    }

    [Fact]
    public void A_turned_crate_refused_shows_its_red_ghost_turned()
    {
        // The wheel turned an oblong crate (D71): the red ghost lies as the drop would put it.
        var (ship, plan) = TestPlans.Load();
        var quartz = plan.Crates.First(c => c.Commodity == "Quartz");
        Gesture.MoveCrates move = new(quartz, [quartz], "rack-1", new GridPosition(0, 0, 1), Turn: true);

        DragPreview.Of(ship, plan, [quartz], move, Refused(plan)).Refused.Should().ContainSingle()
            .Which.Rotated.Should().Be(quartz.CrateSize.Turns && !quartz.Rotated);
    }

    [Fact]
    public void A_refused_selection_reaching_past_every_rack_shows_the_crate_held_only()
    {
        var (ship, plan, bow, aft) = Gold();
        Gesture.MoveCrates move = new(aft, [bow, aft], "rack-1", new GridPosition(3, 1, 1));

        var preview = DragPreview.Of(ship, plan, [aft, bow], move, Refused(plan));

        CrateDrop.Moves(ship, move).Should().BeNull();
        preview.Refused.Should().Equal(aft with { RackId = "rack-1", Position = new GridPosition(3, 1, 1) });
    }
}
