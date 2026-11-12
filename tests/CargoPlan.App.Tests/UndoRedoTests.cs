using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// Undo and redo of the player's actions on the plan (K2.4): moves, legend drags, turns, split, merge, re-plan, the
/// editor's Plan, deleting a mission, discard and, since K6.4, every change of a delivery's state (a legend line or a crate
/// clicked) are steps; a scan is one too (D103); a ship change clears the history. Snapshots of the whole plan, not saved: a restart starts without.
/// </summary>
public sealed class UndoRedoTests
{
    private static readonly ShipCatalog Catalog = FrozenShips.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");

    private static OverlayViewModel Showing(SavedPlan saved)
    {
        var overlay = new OverlayViewModel(HullA, Catalog) { ConfirmReplan = _ => true };
        overlay.ShowPlan(saved);
        return overlay;
    }

    /// <summary>A move that changes the plan: the first crate on board that some other rack takes.</summary>
    private static void Move(OverlayViewModel overlay)
    {
        var plan = overlay.Plan!;
        foreach (var crate in plan.Crates.Where(c => !c.Delivered))
        {
            foreach (var rack in HullA.Racks.Where(r => r.Id != crate.RackId))
            {
                var move = new Gesture.MoveCrates(crate, [crate], rack.Id, new GridPosition(0, 0, 0));
                if (CrateDrop.Outcome(plan, HullA, move).Plan is { } moved && !moved.Crates.SequenceEqual(plan.Crates))
                {
                    overlay.MoveCratesCommand.Execute(move);
                    return;
                }
            }
        }

        throw new InvalidOperationException("No move changes the plan.");
    }

    /// <summary>Captures 36 to Riker and 37 to Shubin, planned.</summary>
    private static SavedPlan TwoCaptures() => LoadPlanner.Plan(
        HullA,
        [
            new Mission(1, [new MissionStop("Riker", 1, [new MissionItem("Stims", 2, CrateSize.Two)])], new ContractSource([36])),
            new Mission(2, [new MissionStop("Shubin", 1, [new MissionItem("Stims", 2, CrateSize.Two)])], new ContractSource([37])),
        ],
        ["Riker", "Shubin"]);

    // --- Start ---

    [Fact]
    public void Undo_and_redo_are_off_at_the_start_and_after_loading_a_plan()
    {
        var overlay = Showing(TestPlans.SplitAndShared());

        overlay.CanUndo.Should().BeFalse();
        overlay.CanRedo.Should().BeFalse();
        overlay.UndoCommand.CanExecute(null).Should().BeFalse();
        overlay.RedoCommand.CanExecute(null).Should().BeFalse();
    }

    // --- Martin's gate sequence ---

    [Fact]
    public void Three_moves_three_undos_two_redos_then_a_new_move_drops_the_redo()
    {
        var overlay = Showing(TestPlans.SplitAndShared());
        List<LoadPlan> plans = [overlay.Plan!];
        for (var i = 0; i < 3; i++)
        {
            Move(overlay);
            plans.Add(overlay.Plan!);
        }

        for (var i = 0; i < 3; i++)
        {
            overlay.UndoCommand.Execute(null);
        }

        overlay.Plan!.Crates.Should().Equal(plans[0].Crates);
        overlay.CanUndo.Should().BeFalse();

        overlay.RedoCommand.Execute(null);
        overlay.RedoCommand.Execute(null);
        overlay.Plan!.Crates.Should().Equal(plans[2].Crates);
        overlay.CanRedo.Should().BeTrue();

        Move(overlay);
        overlay.CanRedo.Should().BeFalse();
        overlay.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void Undo_of_a_replan_brings_the_old_layout_back()
    {
        var overlay = Showing(TestPlans.HurstonPickups());
        Move(overlay);
        var before = overlay.Plan!;

        overlay.ReplanCommand.Execute(null);
        overlay.Plan!.Crates.Should().NotEqual(before.Crates, "the re-plan has to move something");
        overlay.UndoCommand.Execute(null);

        overlay.Plan!.Crates.Should().Equal(before.Crates);
    }

    [Fact]
    public void Undo_of_a_discard_brings_the_plan_back()
    {
        var overlay = Showing(TwoCaptures());
        var before = overlay.Saved!;

        overlay.DiscardPlanCommand.Execute(null);
        overlay.UndoCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(before);
        overlay.HasPlan.Should().BeTrue();
    }

    // --- A tick is a step (K6.4); a sync isn't and carries over ---

    [Fact]
    public void A_tick_after_a_move_is_undone_first_and_the_move_second()
    {
        var overlay = Showing(TestPlans.SplitAndShared());
        Move(overlay);
        var moved = overlay.Plan!;
        overlay.ToggleDeliveredCommand.Execute("Orison");

        overlay.UndoCommand.Execute(null);
        overlay.Plan!.Crates.Should().Equal(moved.Crates, "the tick was the last step");

        overlay.RedoCommand.Execute(null);
        overlay.Plan!.Crates.Where(c => c.Destination == "Orison").Should().OnlyContain(c => c.Delivered);
    }

    [Fact]
    public void Undo_and_redo_keep_captures_never_gone_and_place_a_waiting_mission_that_fits()
    {
        Mission[] captured =
        [
            new(1, [new MissionStop("Riker", 1, [new MissionItem("Stims", 16, CrateSize.Sixteen)])], new ContractSource([36])),
            new(2, [new MissionStop("Shubin", 1, [new MissionItem("Stims", 16, CrateSize.Sixteen)])], new ContractSource([37, 38])),
        ];
        var planned = LoadPlanner.Plan(HullA, captured, ["Riker", "Shubin"]);
        var waiting = new Mission(3, [new MissionStop("Orison", 1, [new MissionItem("Waste", 8, CrateSize.Eight)])], Unplaced: true);
        var overlay = Showing(new SavedPlan([.. planned.Missions, waiting], planned.Plan, planned.CrateCap));
        Move(overlay);

        overlay.UndoCommand.Execute(null);
        var afterUndo = overlay.Missions;
        overlay.RedoCommand.Execute(null);

        foreach (var missions in new[] { afterUndo, overlay.Missions })
        {
            missions.Where(m => m.Contract is not null).Select(m => (m.Contract!.ContractIds, m.Contract.Gone, m.Contract.Detached))
                .Should().BeEquivalentTo([((IReadOnlyList<long>)[36L], false, false), ([37L, 38L], false, false)]);
            missions.Single(m => m.Id == 3).Unplaced.Should().BeFalse();
        }
    }

    // --- Every player action on the plan is a step ---

    [Fact]
    public void The_editors_Plan_is_a_step()
    {
        var overlay = Showing(TestPlans.SplitAndShared());
        var before = overlay.Saved!;
        var editor = new MissionEditorViewModel(overlay);
        editor.FillOrder = FillOrder.SternFirst;

        editor.PlanCommand.Execute(null);
        overlay.Saved.Should().NotBeSameAs(before);
        overlay.UndoCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(before);
    }

    [Fact]
    public void Undo_of_a_mission_added_by_the_editors_Plan_takes_nothing_from_the_plan_it_leaves()
    {
        // The restore is the snapshot with its waiting missions placed (K2.4); nothing of the plan being left comes in,
        // since no change happens without a step any more (D107; successor of Core's UndoRestoreTests).
        var overlay = Showing(TestPlans.SplitAndShared());
        var before = overlay.Saved!;
        var editor = new MissionEditorViewModel(overlay);
        editor.AddRowCommand.Execute(null);
        var row = editor.Rows[^1];
        (row.Destination, row.Commodity, row.Scu, row.MaxCrateSize) = ("Orison", "Gold", "4", 4);

        editor.PlanCommand.Execute(null);
        overlay.Missions.Should().Contain(m => m.Stops.Any(s => s.Destination == "Orison"));
        overlay.UndoCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(before);
    }

    [Fact]
    public void Deleting_a_mission_is_a_step()
    {
        var overlay = Showing(TestPlans.SplitAndShared());
        var before = overlay.Saved!;

        overlay.RemoveMission(overlay.Missions[0].Id);
        overlay.UndoCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(before);
    }

    [Fact]
    public void A_split_is_a_step()
    {
        var overlay = Showing(TestPlans.SplitAndShared());
        var before = overlay.Saved!;
        var crate = overlay.Plan!.Crates.First(c => c.CrateSize > CrateSize.One);
        overlay.Selection = CrateSelection.Of(overlay.Scene, [crate]);

        overlay.SplitCommand.Execute(null);
        overlay.UndoCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(before);
    }

    // --- A ship change clears the history ---

    [Fact]
    public void A_ship_change_clears_both_stacks_and_says_so()
    {
        var overlay = Showing(TestPlans.SplitAndShared());
        Move(overlay);
        Move(overlay);
        overlay.UndoCommand.Execute(null);

        overlay.ChangeShip(Catalog.Get("c2_hercules"));

        overlay.CanUndo.Should().BeFalse();
        overlay.CanRedo.Should().BeFalse();
        overlay.Notices.Should().Contain(OverlayViewModel.HistoryClearedByShipChangeText);
    }

    [Fact]
    public void A_ship_change_without_history_says_nothing_about_it()
    {
        var overlay = Showing(TestPlans.SplitAndShared());

        overlay.ChangeShip(Catalog.Get("c2_hercules"));

        overlay.Notices.Should().NotContain(OverlayViewModel.HistoryClearedByShipChangeText);
    }

    // --- PlanHistory ---

    [Fact]
    public void The_history_keeps_the_last_MaxSteps_steps()
    {
        PlanHistory history = new();
        var plan = TestPlans.SplitAndShared();
        for (var i = 0; i < PlanHistory.MaxSteps + 5; i++)
        {
            history.Record(plan, plan);
        }

        var undone = 0;
        while (history.CanUndo)
        {
            history.PopUndo();
            undone++;
        }

        undone.Should().Be(PlanHistory.MaxSteps);
        PlanHistory.MaxSteps.Should().Be(100);
    }

    [Fact]
    public void A_recorded_step_drops_the_redo_and_an_undo_doesnt()
    {
        PlanHistory history = new();
        var plan = TestPlans.SplitAndShared();
        history.Record(plan, plan);
        history.Record(plan, plan);

        var entry = history.PopUndo();
        history.PushRedo(plan, plan);
        history.CanRedo.Should().BeTrue();
        history.PushUndo(entry.Target, entry.Reference);
        history.CanRedo.Should().BeTrue("going back and forth keeps both stacks");

        history.Record(plan, plan);
        history.CanRedo.Should().BeFalse();
    }
}
