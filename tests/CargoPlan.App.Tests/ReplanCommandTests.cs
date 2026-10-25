using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>The player's Re-plan (B4, D66): in the overlay for the hotkey, as a button in the editor.</summary>
public sealed class ReplanCommandTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static MissionStop Stop(string destination, string commodity, int scu, CrateSize max, int order = 1) =>
        new(destination, order, [new MissionItem(commodity, scu, max)]);

    private static SavedPlan Empty => new([], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

    /// <summary>
    /// Two contracts of two destinations each, one per rack; the third contract's 16 finds no whole rack and waits,
    /// although 32 SCU are free (fragmentation). A re-plan places it.
    /// </summary>
    private static SavedPlan Fragmented()
    {
        var saved = LoadPlanner.PlaceInto(Empty, HullA, [Stop("A", "Titanium", 8, CrateSize.Eight), Stop("B", "Quartz", 8, CrateSize.Eight, 2)]);
        saved = LoadPlanner.PlaceInto(saved, HullA, [Stop("C", "Gold", 8, CrateSize.Eight), Stop("D", "Stims", 8, CrateSize.Eight, 2)]);
        return LoadPlanner.PlaceInto(saved, HullA, [Stop("E", "Agricium", 16, CrateSize.Sixteen)]);
    }

    /// <summary>64 SCU on board, Orison's 16 waits and fits nowhere, also after a re-plan.</summary>
    private static SavedPlan Full()
    {
        var saved = LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker", "Titanium", 48, CrateSize.Sixteen)]);
        saved = LoadPlanner.PlaceInto(saved, HullA, [Stop("Shubin", "Quartz", 16, CrateSize.Sixteen)]);
        return LoadPlanner.PlaceInto(saved, HullA, [Stop("Orison", "Gold", 16, CrateSize.Sixteen)]);
    }

    /// <summary>The overlay with the plan; every re-plan question answered yes, as the window's message box would.</summary>
    private static OverlayViewModel Showing(SavedPlan saved)
    {
        var overlay = new OverlayViewModel(HullA) { ConfirmReplan = _ => true };
        overlay.ShowPlan(saved);
        return overlay;
    }

    // --- Overlay ---

    [Fact]
    public void Replan_is_off_without_a_plan()
    {
        new OverlayViewModel(HullA).ReplanCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Replan_is_off_once_every_crate_is_ticked_and_says_so()
    {
        var overlay = Showing(LoadPlanner.PlaceInto(Empty, HullA, [Stop("Riker", "Titanium", 8, CrateSize.Eight)]));
        overlay.ReplanCommand.CanExecute(null).Should().BeTrue();
        var raised = 0;
        overlay.ReplanCommand.CanExecuteChanged += (_, _) => raised++;

        overlay.ToggleDeliveredCommand.Execute("Riker");

        overlay.ReplanCommand.CanExecute(null).Should().BeFalse();
        raised.Should().BePositive();
    }

    [Fact]
    public void Replan_asks_first_and_a_no_changes_nothing()
    {
        var overlay = Showing(Fragmented());
        var saved = overlay.Saved;
        List<string> asked = [];
        overlay.ConfirmReplan = question =>
        {
            asked.Add(question);
            return false;
        };

        overlay.ReplanCommand.Execute(null);

        asked.Should().Equal(OverlayViewModel.ReplanConfirmText);
        overlay.Saved.Should().BeSameAs(saved);
        overlay.Notices.Should().NotContain(n => n.StartsWith("Plan ", StringComparison.Ordinal));
    }

    [Fact]
    public void Without_a_way_to_ask_Replan_does_nothing()
    {
        // Never without confirmation (Martin, B4): the view model's default answer is no.
        var overlay = new OverlayViewModel(HullA);
        overlay.ShowPlan(Fragmented());
        var saved = overlay.Saved;

        overlay.ReplanCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(saved);
    }

    [Fact]
    public void The_header_buttons_tool_tip_says_what_it_does_or_why_it_cant()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ReplanToolTip.Should().Be(OverlayViewModel.NothingToReplanText);

        var changed = new List<string?>();
        overlay.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        overlay.ShowPlan(Fragmented());

        overlay.ReplanToolTip.Should().Be(OverlayViewModel.ReplanToolTipText);
        changed.Should().Contain(nameof(OverlayViewModel.ReplanToolTip));
    }

    [Fact]
    public void Replan_places_the_waiting_mission_and_says_how_many_crates_moved()
    {
        var before = Fragmented();
        var overlay = Showing(before);

        overlay.ReplanCommand.Execute(null);

        overlay.Missions.Should().OnlyContain(m => !m.Unplaced);
        var moved = LoadPlanner.MovedCrates(before.Plan, overlay.Plan!);
        moved.Should().BePositive();
        overlay.Notices.Should().Contain(OverlayViewModel.ReplanText(changed: true, moved, []));
    }

    [Fact]
    public void The_header_capacity_names_the_waiting_mission_until_a_replan_places_it()
    {
        // The case the "waiting" part is for (D67): 32 SCU free, the 16 SCU mission still waits.
        var overlay = Showing(Fragmented());
        overlay.Scene.CapacityText.Should().Be("32/64 · 32 free · 16 waiting");

        overlay.ReplanCommand.Execute(null);

        overlay.Scene.CapacityText.Should().Be("48/64 · 16 free");
    }

    [Fact]
    public void A_second_replan_changes_nothing_and_says_so()
    {
        var overlay = Showing(Fragmented());
        overlay.ReplanCommand.Execute(null);
        var saved = overlay.Saved;

        overlay.ReplanCommand.Execute(null);

        overlay.Saved.Should().BeSameAs(saved);
        overlay.Notices.Should().Contain("Plan unchanged");
    }

    [Fact]
    public void Replan_keeps_the_ticks()
    {
        var overlay = Showing(Fragmented());
        overlay.ToggleDeliveredCommand.Execute("A");
        var ticked = overlay.Plan!.Crates.Where(c => c.Delivered).ToArray();

        overlay.ReplanCommand.Execute(null);

        overlay.Plan!.Crates.Where(c => c.Delivered).Should().Equal(ticked);
    }

    [Fact]
    public void Replan_names_a_mission_that_still_doesnt_fit()
    {
        var overlay = Showing(Full());

        overlay.ReplanCommand.Execute(null);

        overlay.Notices.Should().ContainSingle(n => n.StartsWith("Plan ", StringComparison.Ordinal))
            .Which.Should().EndWith(" – Orison still doesn't fit");
    }

    [Fact]
    public void A_move_clears_the_replan_notice()
    {
        var overlay = Showing(Fragmented());
        overlay.ReplanCommand.Execute(null);
        var crate = overlay.Plan!.Crates[0];

        overlay.MoveCratesCommand.Execute(new ShipView.Gesture.MoveCrates(crate, [crate], crate.RackId, crate.Position));

        overlay.Notices.Should().NotContain(n => n.StartsWith("Plan ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, 0, new string[0], "Plan unchanged")]
    [InlineData(true, 0, new string[0], "Plan rebuilt – 0 crates moved")]
    [InlineData(true, 1, new string[0], "Plan rebuilt – 1 crate moved")]
    [InlineData(true, 5, new string[0], "Plan rebuilt – 5 crates moved")]
    [InlineData(false, 0, new[] { "Orison" }, "Plan unchanged – Orison still doesn't fit")]
    [InlineData(true, 3, new[] { "Orison", "Hurston + 1" }, "Plan rebuilt – 3 crates moved – Orison, Hurston + 1 still don't fit")]
    public void ReplanText_says_what_happened(bool changed, int moved, string[] waiting, string expected)
    {
        OverlayViewModel.ReplanText(changed, moved, waiting).Should().Be(expected);
    }

    // --- Editor ---

    [Fact]
    public void The_editors_button_follows_the_overlay()
    {
        var overlay = new OverlayViewModel(HullA);
        var editor = new MissionEditorViewModel(overlay);
        editor.ReplanCommand.CanExecute(null).Should().BeFalse();

        overlay.ShowPlan(Fragmented());

        editor.ReplanCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Replan_from_a_clean_editor_shows_the_rows_anew_without_a_change_outside()
    {
        var overlay = Showing(Fragmented());
        var editor = new MissionEditorViewModel(overlay);

        editor.ReplanCommand.Execute(null);

        overlay.Missions.Should().OnlyContain(m => !m.Unplaced);
        editor.IsDirty.Should().BeFalse();
        editor.Status.Should().BeNull();
        editor.Rows.Select(r => r.Destination).Should().Equal("A", "B", "C", "D", "E");
    }

    [Fact]
    public void Replan_keeps_a_dirty_draft_as_it_is()
    {
        var overlay = Showing(Fragmented());
        var editor = new MissionEditorViewModel(overlay);
        editor.Rows[0].Scu = "4";

        editor.ReplanCommand.Execute(null);

        overlay.Missions.Should().OnlyContain(m => !m.Unplaced);
        editor.IsDirty.Should().BeTrue();
        editor.Status.Should().BeNull();
        editor.Rows[0].Scu.Should().Be("4");
    }
}
