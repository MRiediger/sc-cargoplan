using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.App.Tests;

/// <summary>
/// Undo and redo of the player's actions on the plan (K2.4): moves, legend drags, turns, split, merge, re-plan, the
/// editor's Plan, deleting a mission, discard and, since K6.4, every change of a delivery's state (a legend line or a crate
/// clicked) are steps; syncs aren't, and carry over; a ship change clears the history. Snapshots of the whole plan, not saved: a restart starts without.
/// </summary>
public sealed class UndoRedoTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
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

    private static ContractTrackerCandidate Candidate(long id, string destination) =>
        new([id], null, [new MissionStop(destination, 1, [new MissionItem("Stims", 2, CrateSize.Two)])], false, false, null, null, null);

    private static ContractTrackerReadResult Read(params ContractTrackerCandidate[] candidates) =>
        new(candidates, null, new HashSet<long>(candidates.SelectMany(c => c.ContractIds)));

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
        var overlay = new OverlayViewModel(HullA, Catalog);
        overlay.ApplyContractTracker(Read(Candidate(36, "Riker"), Candidate(37, "Shubin")));
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
    public void Syncs_are_no_steps_and_a_tick_is_one()
    {
        var overlay = new OverlayViewModel(HullA, Catalog);
        overlay.ApplyContractTracker(Read(Candidate(36, "Riker")));
        overlay.ApplyContractTracker(Read(Candidate(36, "Riker"), Candidate(37, "Shubin")));
        overlay.CanUndo.Should().BeFalse();

        overlay.ToggleDeliveredCommand.Execute("Riker");

        overlay.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void A_contract_synced_after_a_move_stays_through_the_undo()
    {
        var overlay = new OverlayViewModel(HullA, Catalog);
        overlay.ApplyContractTracker(Read(Candidate(36, "Riker")));
        Move(overlay);
        overlay.ApplyContractTracker(Read(Candidate(36, "Riker"), Candidate(37, "Shubin")));

        overlay.UndoCommand.Execute(null);

        overlay.Missions.Select(m => m.Contract!.ContractIds.Single()).Should().Equal(36, 37);
        overlay.Watermark.Should().Be(37);
        overlay.CanRedo.Should().BeTrue();
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
