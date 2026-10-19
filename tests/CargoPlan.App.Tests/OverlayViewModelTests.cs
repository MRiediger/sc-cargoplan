using System.ComponentModel;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class OverlayViewModelTests
{
    private static OverlayViewModel WithPlan()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a"));
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        return viewModel;
    }

    /// <summary>Names of all PropertyChanged events raised while <paramref name="act"/> runs.</summary>
    private static List<string?> Changes(INotifyPropertyChanged source, Action act)
    {
        List<string?> names = [];
        PropertyChangedEventHandler handler = (_, e) => names.Add(e.PropertyName);
        source.PropertyChanged += handler;
        act();
        source.PropertyChanged -= handler;
        return names;
    }

    [Fact]
    public void IsLocked_defaults_to_false_so_the_overlay_starts_interactive()
    {
        WithPlan().IsLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void IsLocked_is_manual_or_auto(bool manual, bool auto, bool expected)
    {
        var viewModel = WithPlan();

        viewModel.IsManuallyLocked = manual;
        viewModel.IsAutoLocked = auto;

        viewModel.IsLocked.Should().Be(expected);
    }

    [Fact]
    public void IsManuallyLocked_raises_PropertyChanged_for_IsLocked_so_the_window_can_switch_click_through()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.IsManuallyLocked = true).Should().Contain(nameof(OverlayViewModel.IsLocked));
    }

    [Fact]
    public void IsAutoLocked_raises_PropertyChanged_for_IsLocked_so_the_window_can_switch_click_through()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.IsAutoLocked = true).Should().Contain(nameof(OverlayViewModel.IsLocked));
    }

    [Fact]
    public void ToggleLockCommand_flips_the_manual_lock_and_back()
    {
        var viewModel = WithPlan();

        viewModel.ToggleLockCommand.Execute(null);
        viewModel.IsManuallyLocked.Should().BeTrue();
        viewModel.IsLocked.Should().BeTrue();

        viewModel.ToggleLockCommand.Execute(null);
        viewModel.IsManuallyLocked.Should().BeFalse();
        viewModel.IsLocked.Should().BeFalse();
    }

    [Fact]
    public void ToggleLockCommand_while_auto_locked_sets_the_manual_lock_and_stays_locked()
    {
        var viewModel = WithPlan();
        viewModel.IsAutoLocked = true;

        viewModel.ToggleLockCommand.Execute(null);

        viewModel.IsManuallyLocked.Should().BeTrue();
        viewModel.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Manual_lock_survives_the_auto_lock_ending()
    {
        // Locked at the terminal, then flying (auto) and back at the terminal (auto ends): still locked.
        var viewModel = WithPlan();
        viewModel.IsManuallyLocked = true;
        viewModel.IsAutoLocked = true;

        viewModel.IsAutoLocked = false;

        viewModel.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void ToggleLockCommand_raises_PropertyChanged_for_IsLocked()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.ToggleLockCommand.Execute(null))
            .Should().Contain(nameof(OverlayViewModel.IsLocked));
    }

    [Fact]
    public void Notices_start_empty_so_no_notice_line_shows()
    {
        WithPlan().Notices.Should().BeEmpty();
    }

    [Fact]
    public void SetNotice_adds_a_line_and_raises_PropertyChanged()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.SetNotice(NoticeSource.Settings, "Settings not saved."))
            .Should().Contain(nameof(OverlayViewModel.Notices));
        viewModel.Notices.Should().Equal("Settings not saved.");
    }

    [Fact]
    public void Clearing_one_source_keeps_the_others()
    {
        // A successful bounds save clears the settings notice; the hotkey problem must stay until the next start.
        var viewModel = WithPlan();
        viewModel.SetNotice(NoticeSource.Hotkeys, "Hotkey Ctrl+Alt+L (toggleLock) is already in use.");
        viewModel.SetNotice(NoticeSource.Settings, "Overlay position not saved.");

        viewModel.SetNotice(NoticeSource.Settings, null);

        viewModel.Notices.Should().Equal("Hotkey Ctrl+Alt+L (toggleLock) is already in use.");
    }

    [Fact]
    public void Notices_follow_the_source_order_not_the_order_they_came_in()
    {
        var viewModel = WithPlan();

        viewModel.SetNotice(NoticeSource.Plan, "plan");
        viewModel.SetNotice(NoticeSource.Hotkeys, "hotkeys");
        viewModel.SetNotice(NoticeSource.Settings, "settings");

        viewModel.Notices.Should().Equal("settings", "hotkeys", "plan");
    }

    [Fact]
    public void SetNotice_replaces_the_line_of_its_source()
    {
        var viewModel = WithPlan();
        viewModel.SetNotice(NoticeSource.Plan, "Plan not loaded.");

        viewModel.SetNotice(NoticeSource.Plan, "Plan not saved.");

        viewModel.Notices.Should().Equal("Plan not saved.");
    }

    [Fact]
    public void SetNotice_without_a_change_raises_nothing()
    {
        // Every save reports its result; an unchanged line must not rebuild the list each time.
        var viewModel = WithPlan();
        viewModel.SetNotice(NoticeSource.Plan, "Plan not saved.");

        Changes(viewModel, () =>
        {
            viewModel.SetNotice(NoticeSource.Plan, "Plan not saved.");
            viewModel.SetNotice(NoticeSource.Settings, null);
        }).Should().BeEmpty();
    }

    [Fact]
    public void ShowPlan_sets_missions_and_plan_together()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a"));
        var saved = TestPlans.SplitAndShared();

        var changes = Changes(viewModel, () => viewModel.ShowPlan(saved));

        viewModel.Missions.Should().BeSameAs(saved.Missions);
        viewModel.Plan.Should().BeSameAs(saved.Plan);
        changes.Should().Contain(nameof(OverlayViewModel.Plan));
    }

    [Fact]
    public void IsOverlayHidden_defaults_to_false_so_the_overlay_starts_visible()
    {
        WithPlan().IsOverlayHidden.Should().BeFalse();
    }

    [Fact]
    public void ToggleOverlayCommand_hides_the_overlay_and_shows_it_again()
    {
        var viewModel = WithPlan();

        viewModel.ToggleOverlayCommand.Execute(null);
        viewModel.IsOverlayHidden.Should().BeTrue();

        viewModel.ToggleOverlayCommand.Execute(null);
        viewModel.IsOverlayHidden.Should().BeFalse();
    }

    [Fact]
    public void ToggleOverlayCommand_raises_PropertyChanged_for_IsOverlayHidden()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.ToggleOverlayCommand.Execute(null))
            .Should().Contain(nameof(OverlayViewModel.IsOverlayHidden));
    }

    [Fact]
    public void ToggleOverlayCommand_leaves_the_lock_alone()
    {
        // Hidden while flying: the auto-lock keeps running, so the state is right the moment the overlay shows again.
        var viewModel = WithPlan();
        viewModel.IsAutoLocked = true;

        viewModel.ToggleOverlayCommand.Execute(null);

        viewModel.IsManuallyLocked.Should().BeFalse();
        viewModel.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Scene_shows_the_plan_with_its_validation()
    {
        var viewModel = WithPlan();

        viewModel.Scene.Legend.Select(e => e.Destination).Should().Equal("Riker", "Shubin", "Orison", "Lorville");
        viewModel.Scene.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Scene_without_a_plan_shows_the_empty_ship()
    {
        var (ship, _) = TestPlans.Load();
        var viewModel = new OverlayViewModel(ship);

        viewModel.HasPlan.Should().BeFalse();
        viewModel.Scene.Racks.Should().HaveCount(ship.Racks.Count);
        viewModel.Scene.Legend.Should().BeEmpty();
    }

    [Fact]
    public void Setting_Plan_raises_PropertyChanged_for_Scene_and_HasPlan()
    {
        var viewModel = WithPlan();
        Stop riker = new("Riker", 1, [new CargoItem("Titanium", 4, CrateSize.Four)]);
        var newPlan = LoadPlanner.Plan(viewModel.Ship, [riker]);

        Changes(viewModel, () => viewModel.Plan = newPlan)
            .Should().Contain([nameof(OverlayViewModel.Scene), nameof(OverlayViewModel.HasPlan)]);
        viewModel.Scene.Legend.Should().Equal(new LegendEntry(0, "Riker", 4));
    }

    [Fact]
    public void ToggleDeliveredCommand_marks_the_stop_delivered_and_back()
    {
        var viewModel = WithPlan();
        var original = viewModel.Plan!;

        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.Plan!.PlacementsForStop("Riker").Should().OnlyContain(p => p.Delivered);
        viewModel.Scene.Legend.Single(e => e.Destination == "Riker").Delivered.Should().BeTrue();

        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.Plan!.Placements.Should().Equal(original.Placements);
        viewModel.Scene.Legend.Should().OnlyContain(e => !e.Delivered);
    }

    [Fact]
    public void ToggleDeliveredCommand_raises_PropertyChanged_for_Plan_and_Scene()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.ToggleDeliveredCommand.Execute("Shubin"))
            .Should().Contain([nameof(OverlayViewModel.Plan), nameof(OverlayViewModel.Scene)]);
    }

    private static Placement Cargo(OverlayViewModel viewModel, string commodity) =>
        viewModel.Plan!.Placements.Single(p => p.Item.Commodity == commodity);

    [Fact]
    public void MovePlacementCommand_onto_a_full_rack_overloads_it_and_drops_stale_hints()
    {
        var viewModel = WithPlan();

        // Riker's 4 SCU Quartz from rack 2 to rack 1, which holds Riker's 16 SCU Titanium already.
        viewModel.MovePlacementCommand.Execute(new Gesture.Move(Cargo(viewModel, "Quartz"), "rack-0"));

        Cargo(viewModel, "Quartz").RackId.Should().Be("rack-0");
        viewModel.Scene.Violations.Should().Equal("Rack 1 overloaded: 20/16 SCU");
        viewModel.Scene.Racks.Where(r => r.IsViolated).Select(r => r.Number).Should().Equal(1);
        viewModel.Scene.Hints.Should().BeEmpty();
    }

    [Fact]
    public void MovePlacementCommand_within_capacity_derives_hints_anew_and_stays_valid()
    {
        var viewModel = WithPlan();

        // Orison's 2 SCU Gold from rack 3 to rack 2 (12 + 2 = 14/16).
        viewModel.MovePlacementCommand.Execute(new Gesture.Move(Cargo(viewModel, "Gold"), "rack-1"));

        viewModel.Scene.Violations.Should().BeEmpty();
        viewModel.Scene.Hints.Should().Contain(["Rack 2 shared by Riker, Shubin, Orison – identify crates by count", "Orison spans racks 2+3"]);
    }

    [Fact]
    public void MovePlacementCommand_back_restores_the_plan()
    {
        var viewModel = WithPlan();
        var original = viewModel.Plan!;

        viewModel.MovePlacementCommand.Execute(new Gesture.Move(Cargo(viewModel, "Agricium"), "rack-0"));
        viewModel.MovePlacementCommand.Execute(new Gesture.Move(Cargo(viewModel, "Agricium"), "rack-1"));

        viewModel.Plan!.Placements.Should().Equal(original.Placements);
        viewModel.Plan!.Warnings.Should().Equal(original.Warnings);
    }

    [Fact]
    public void MovePlacementCommand_raises_PropertyChanged_for_Plan_so_the_plan_is_saved()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.MovePlacementCommand.Execute(new Gesture.Move(Cargo(viewModel, "Agricium"), "rack-0")))
            .Should().Contain([nameof(OverlayViewModel.Plan), nameof(OverlayViewModel.Scene)]);
    }

    [Fact]
    public void A_moved_plan_still_covers_its_missions()
    {
        // PlanSaver saves SavedPlan(Missions, Plan); its constructor checks the SCU per stop, commodity and crate size.
        var viewModel = WithPlan();

        viewModel.MovePlacementCommand.Execute(new Gesture.Move(Cargo(viewModel, "Tungsten"), "rack-0"));

        viewModel.Invoking(v => new SavedPlan(v.Missions, v.Plan!)).Should().NotThrow();
    }
}
