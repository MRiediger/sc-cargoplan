using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// Discard plan (K2.3): in the overlay for the hotkey, as a button in the editor next to Re-plan. The plan empties, the
/// sync starts over (Martin's variant a), the fill order stays; a notice, no question (D66).
/// </summary>
public sealed class DiscardPlanCommandTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    /// <summary>The split-and-shared plan, stern first, Riker ticked.</summary>
    private static OverlayViewModel SternFirstWithATick()
    {
        var saved = TestPlans.SplitAndShared();
        var overlay = new OverlayViewModel(HullA);
        overlay.ShowPlan(new SavedPlan(saved.Missions, saved.Plan with { FillOrder = FillOrder.SternFirst }, saved.CrateCap, saved.Watermark));
        overlay.ToggleDeliveredCommand.Execute("Riker");
        return overlay;
    }

    // --- Overlay ---

    [Fact]
    public void Discard_is_off_without_missions()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.DiscardPlanCommand.CanExecute(null).Should().BeFalse();

        overlay.ShowPlan(TestPlans.SplitAndShared());
        overlay.DiscardPlanCommand.CanExecute(null).Should().BeTrue();

        overlay.DiscardPlanCommand.Execute(null);
        overlay.DiscardPlanCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Discard_starts_the_scan_numbers_again()
    {
        // A fresh plan counts its scans from #1 (D102, Martin); an undo of the discard brings its scans back, and the next
        // number then goes above them (T10.2b2 counts from the plan's highest too).
        var overlay = new OverlayViewModel(HullA) { LastScanNo = 5 };
        overlay.ShowPlan(TestPlans.SplitAndShared());

        overlay.DiscardPlanCommand.Execute(null);

        overlay.LastScanNo.Should().Be(0);
    }

    [Fact]
    public void Discard_shows_the_empty_state_and_says_so_and_keeps_ship_and_fill_order()
    {
        var overlay = SternFirstWithATick();

        overlay.DiscardPlanCommand.Execute(null);

        overlay.Missions.Should().BeEmpty();
        overlay.HasPlan.Should().BeFalse("the empty state of a first start shows (T3.8a)");
        overlay.Plan!.Crates.Should().BeEmpty();
        overlay.Plan.FillOrder.Should().Be(FillOrder.SternFirst);
        overlay.Ship.Should().BeSameAs(HullA);
        overlay.Scene.Legend.Should().BeEmpty();
        overlay.Notices.Should().Contain(OverlayViewModel.DiscardedText);
    }

    [Fact]
    public void Without_missions_there_is_no_plan_to_show()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ShowPlan(TestPlans.SplitAndShared());

        foreach (var mission in overlay.Missions.ToList())
        {
            overlay.RemoveMission(mission.Id);
        }

        overlay.HasPlan.Should().BeFalse();
    }

    // --- Editor ---

    [Fact]
    public void The_editors_button_follows_the_overlay()
    {
        var overlay = new OverlayViewModel(HullA);
        var editor = new MissionEditorViewModel(overlay);
        editor.DiscardPlanCommand.CanExecute(null).Should().BeFalse();

        overlay.ShowPlan(TestPlans.SplitAndShared());

        editor.DiscardPlanCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Discard_from_a_clean_editor_leaves_one_blank_row_and_the_fill_order()
    {
        var overlay = SternFirstWithATick();
        var editor = new MissionEditorViewModel(overlay);

        editor.DiscardPlanCommand.Execute(null);

        overlay.Missions.Should().BeEmpty();
        editor.Rows.Should().ContainSingle().Which.Destination.Should().BeEmpty();
        editor.IsDirty.Should().BeFalse();
        editor.Status.Should().BeNull();
        editor.HasPlan.Should().BeFalse();
        editor.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Fact]
    public void Discard_keeps_a_dirty_draft_as_it_is()
    {
        var overlay = SternFirstWithATick();
        var editor = new MissionEditorViewModel(overlay);
        editor.Rows[0].Scu = "4";

        editor.DiscardPlanCommand.Execute(null);

        overlay.Missions.Should().BeEmpty();
        editor.IsDirty.Should().BeTrue();
        editor.Status.Should().BeNull();
        editor.Rows[0].Scu.Should().Be("4");
    }
}
