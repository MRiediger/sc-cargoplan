using System.ComponentModel;
using CargoPlan.App.Holo;
using CargoPlan.App.Settings;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Holo;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;
using CargoPlan.Import.ContractTracker;

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

    // --- Info line (T7.4d) ---

    [Fact]
    public void SelectionInfo_follows_the_selection_and_goes_with_a_new_plan()
    {
        var viewModel = WithPlan();
        var crate = viewModel.Plan!.Crates.First(c => c.Destination == "Riker");

        var changes = Changes(viewModel, () => viewModel.Selection = CrateSelection.Empty.Toggle(viewModel.Scene, crate));

        changes.Should().Contain(nameof(OverlayViewModel.SelectionInfo));
        viewModel.SelectionInfo.Should().Be($"from ? · {crate.Commodity} · to Riker");

        changes = Changes(viewModel, () => viewModel.ToggleDeliveredCommand.Execute("Shubin"));

        changes.Should().Contain(nameof(OverlayViewModel.SelectionInfo));
        viewModel.SelectionInfo.Should().BeNull();
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

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public void Tool_windows_hide_with_the_overlay_and_while_auto_locked(bool hidden, bool autoLocked, bool expected)
    {
        var viewModel = WithPlan();

        viewModel.IsOverlayHidden = hidden;
        viewModel.IsAutoLocked = autoLocked;

        viewModel.AreToolWindowsHidden.Should().Be(expected);
    }

    [Fact]
    public void Tool_windows_hide_without_the_manual_lock()
    {
        var viewModel = WithPlan();

        viewModel.IsManuallyLocked = true;

        viewModel.AreToolWindowsHidden.Should().BeFalse();
    }

    [Fact]
    public void Hiding_and_auto_lock_raise_PropertyChanged_for_the_tool_windows()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.IsOverlayHidden = true).Should().Contain(nameof(OverlayViewModel.AreToolWindowsHidden));
        Changes(viewModel, () => viewModel.IsAutoLocked = true).Should().Contain(nameof(OverlayViewModel.AreToolWindowsHidden));
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
    public void Legend_defaults_to_the_layout_before_it_was_a_setting()
    {
        var viewModel = WithPlan();

        viewModel.IsLegendVisible.Should().BeTrue();
        viewModel.LegendPosition.Should().Be(LegendPosition.Default);
    }

    [Fact]
    public void Legend_layout_raises_PropertyChanged_so_the_settings_window_can_switch_it_live()
    {
        // T4.2 changes both while the overlay is open; the window's triggers follow the binding (D33).
        var viewModel = WithPlan();
        LegendPosition topRight = new(LegendEdge.Top, LegendAlignment.Right);

        Changes(viewModel, () => viewModel.LegendPosition = topRight).Should().Equal(nameof(OverlayViewModel.LegendPosition));
        Changes(viewModel, () => viewModel.IsLegendVisible = false).Should().Equal(nameof(OverlayViewModel.IsLegendVisible));
    }

    [Fact]
    public void Same_legend_layout_raises_nothing()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () =>
        {
            viewModel.LegendPosition = LegendPosition.Default;
            viewModel.IsLegendVisible = true;
        }).Should().BeEmpty();
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
    public void ShowPlan_raises_PropertyChanged_for_Saved_Plan_Scene_and_HasPlan()
    {
        var viewModel = WithPlan();
        MissionStop riker = new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]);
        var newPlan = SavedPlan.FromStops([riker], LoadPlanner.Plan(viewModel.Ship, [riker.ToStop()]), CrateCap.Of(viewModel.Ship));

        Changes(viewModel, () => viewModel.ShowPlan(newPlan)).Should().Contain(
            [nameof(OverlayViewModel.Saved), nameof(OverlayViewModel.Plan), nameof(OverlayViewModel.Scene), nameof(OverlayViewModel.HasPlan)]);
        // The entry holds its crates, a list compared by reference, so its values compare one by one.
        viewModel.Scene.Legend.Select(e => (e.PaletteIndex, e.Label, e.Delivered, e.Header)).Should().Equal((0, "To Riker · 4 SCU · Titanium", false, (string?)null));
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
    public void ToggleDeliveredCommand_back_onto_cells_taken_again_lays_the_stop_in_elsewhere()
    {
        // Shubin's 8 in Rack 1, Riker's 4 in Rack 2 (largest first, D64). Riker is ticked, Shubin's 8 goes where Riker's 4
        // lay (D24 addendum): Riker comes back on board into free cells (K6.4, P7), no refusal.
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a"));
        IReadOnlyList<MissionStop> missions =
        [
            new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]),
            new("Shubin", 2, [new MissionItem("Agricium", 8, CrateSize.Eight)]),
        ];
        viewModel.ShowPlan(SavedPlan.FromStops(missions, LoadPlanner.Plan(viewModel.Ship, missions.ToStops()), CrateCap.Of(viewModel.Ship)));
        viewModel.ToggleDeliveredCommand.Execute("Riker");
        viewModel.MoveLegendCommand.Execute(LegendMove(viewModel, "Shubin", "rack-1"));
        var reused = viewModel.Plan!;

        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.Plan!.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => !c.Delivered);
        viewModel.Plan.Crates.Where(c => c.Destination == "Shubin").Should().Equal(reused.Crates.Where(c => c.Destination == "Shubin"));
        viewModel.Notices.Should().NotContain(n => n.StartsWith(OverlayViewModel.TickRefusedPrefix, StringComparison.Ordinal));
    }

    [Fact]
    public void ToggleDeliveredCommand_raises_PropertyChanged_for_Plan_and_Scene()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.ToggleDeliveredCommand.Execute("Shubin"))
            .Should().Contain([nameof(OverlayViewModel.Plan), nameof(OverlayViewModel.Scene)]);
    }

    [Fact]
    public void Capacity_follows_ticks_without_a_path_of_its_own()
    {
        // The header binds Scene.CapacityText (D67); a tick builds a new scene and raises Scene.
        var viewModel = WithPlan();
        viewModel.Scene.CapacityText.Should().Be("54/64 · 10 free");

        Changes(viewModel, () => viewModel.ToggleDeliveredCommand.Execute("Lorville")).Should().Contain(nameof(OverlayViewModel.Scene));

        viewModel.Scene.CapacityText.Should().Be("42/64 · 22 free");
    }

    [Fact]
    public void Rack_load_follows_a_move()
    {
        var viewModel = WithPlan();

        viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, "rack-3", 0, 0, 0));

        viewModel.Scene.Racks.Single(r => r.Number == 3).Load.ToolTip.Should().Be("Rack 3 · 13 / 16 SCU");
        viewModel.Scene.Racks.Single(r => r.Number == 4).Load.ToolTip.Should().Be("Rack 4 · 13 / 16 SCU");
        viewModel.Scene.CapacityText.Should().Be("54/64 · 10 free");
    }

    // Split and shared: Orison's two Gold crates lie on top in Rack 3, Riker's Quartz on the plate of Rack 2 (aft half).
    private static Gesture.MoveCrates GoldTo(OverlayViewModel viewModel, string rackId, int length, int width, int stack)
    {
        var gold = viewModel.Plan!.Crates.First(c => c.Commodity == "Gold");
        return new Gesture.MoveCrates(gold, [gold], rackId, new GridPosition(length, width, stack));
    }

    /// <summary>
    /// A full Hull A where Riker's 4 finds no room elsewhere (D65): Rack 1 holds Riker's 8, the 4 and a 1 on top of the 4;
    /// Racks 2–4 hold two 8s each. Without the 4 the 1 settles into its place, and no rack has a 2×2 gap left.
    /// </summary>
    private static (OverlayViewModel ViewModel, Gesture.MoveCrates Refused) Full()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a"));
        Crate[] crates =
        [
            new("Riker", "Titanium", CrateSize.Eight, "rack-0", new GridPosition(0, 0, 0)),
            new("Riker", "Titanium", CrateSize.Four, "rack-0", new GridPosition(2, 0, 0)),
            new("Riker", "Titanium", CrateSize.One, "rack-0", new GridPosition(3, 0, 1)),
            .. new[] { ("Shubin", "rack-1"), ("Orison", "rack-2"), ("Lorville", "rack-3") }.SelectMany(s => new Crate[]
            {
                new(s.Item1, "Titanium", CrateSize.Eight, s.Item2, new GridPosition(0, 0, 0)),
                new(s.Item1, "Titanium", CrateSize.Eight, s.Item2, new GridPosition(2, 0, 0)),
            }),
        ];
        IReadOnlyList<MissionStop> missions =
        [
            new("Riker", 1, [new MissionItem("Titanium", 13, CrateSize.Eight)]),
            new("Shubin", 2, [new MissionItem("Titanium", 16, CrateSize.Eight)]),
            new("Orison", 3, [new MissionItem("Titanium", 16, CrateSize.Eight)]),
            new("Lorville", 4, [new MissionItem("Titanium", 16, CrateSize.Eight)]),
        ];
        viewModel.ShowPlan(SavedPlan.FromStops(missions, new LoadPlan(viewModel.Ship.Id, crates, []), CrateCap.Of(viewModel.Ship)));
        var four = viewModel.Plan!.Crates[1];
        return (viewModel, new Gesture.MoveCrates(four, [four], "rack-1", new GridPosition(0, 0, 0)));
    }

    [Fact]
    public void MoveCratesCommand_without_room_anywhere_is_refused_with_a_notice_and_keeps_the_plan()
    {
        var (viewModel, refused) = Full();
        var original = viewModel.Plan!;

        var changes = Changes(viewModel, () => viewModel.MoveCratesCommand.Execute(refused));

        viewModel.Plan.Should().BeSameAs(original);
        changes.Should().NotContain(nameof(OverlayViewModel.Plan));
        viewModel.Notices.Should().Equal(OverlayViewModel.MoveRefusedPrefix + "no room for 4 SCU crate");
        viewModel.Scene.Violations.Should().BeEmpty();
    }

    [Fact]
    public void MoveCratesCommand_onto_a_full_rack_makes_room_and_says_what_moved()
    {
        // Rack 1 is full of Riker's Titanium: a Riker crate makes room for Orison's Gold (D65), not a refusal any more.
        var viewModel = WithPlan();

        viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, "rack-0", 0, 0, 1));

        viewModel.Plan!.CratesForRack("rack-0").Should().Contain(c => c.Commodity == "Gold");
        viewModel.Notices.Should().ContainSingle().Which.Should().StartWith("Moved ").And.EndWith(" to make room");
        viewModel.Scene.Violations.Should().BeEmpty();
    }

    [Fact]
    public void A_successful_move_clears_the_refusal()
    {
        var (viewModel, refused) = Full();
        viewModel.MoveCratesCommand.Execute(refused);

        var eight = viewModel.Plan!.Crates[0];
        viewModel.MoveCratesCommand.Execute(new Gesture.MoveCrates(eight, [eight], "rack-0", eight.Position));

        viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void A_new_plan_clears_the_refusal()
    {
        var (viewModel, refused) = Full();
        viewModel.MoveCratesCommand.Execute(refused);

        viewModel.ShowPlan(TestPlans.SplitAndShared());

        viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void MoveCratesCommand_puts_the_crate_on_the_cell_and_derives_hints_anew()
    {
        var viewModel = WithPlan();

        viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, "rack-1", 0, 0, 1));

        viewModel.Plan!.CratesForRack("rack-1").Should().Contain(c => c.Commodity == "Gold" && c.Position == new GridPosition(0, 0, 1));
        // Orison on Riker: laid, nothing red; the plan's hints say it, the overlay doesn't show them (D64).
        viewModel.Scene.Violations.Should().BeEmpty();
        viewModel.Plan!.Warnings.Select(w => w.Message).Should().Contain(
            ["Rack 2 shared by Riker, Shubin, Orison – identify crates by count", "Orison spans racks 2+3", "Rack 2: crate for Orison sits on cargo for Riker"]);
    }

    [Fact]
    public void MoveCratesCommand_back_restores_the_plan()
    {
        var viewModel = WithPlan();
        var original = viewModel.Plan!;
        var gold = original.Crates.First(c => c.Commodity == "Gold");

        viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, "rack-1", 0, 0, 1));
        viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, gold.RackId, gold.Position.Length, gold.Position.Width, gold.Position.Stack));

        viewModel.Plan!.Crates.Should().Equal(original.Crates);
        viewModel.Plan!.Warnings.Should().Equal(original.Warnings);
    }

    [Fact]
    public void MoveCratesCommand_raises_PropertyChanged_for_Plan_so_the_plan_is_saved()
    {
        var viewModel = WithPlan();

        Changes(viewModel, () => viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, "rack-1", 0, 0, 1)))
            .Should().Contain([nameof(OverlayViewModel.Plan), nameof(OverlayViewModel.Scene)]);
    }

    [Fact]
    public void A_moved_plan_still_covers_its_missions()
    {
        // PlanSaver saves SavedPlan(Missions, Plan); its constructor checks the crates per stop, commodity and crate size.
        var viewModel = WithPlan();

        viewModel.MoveCratesCommand.Execute(GoldTo(viewModel, "rack-1", 0, 0, 1));

        viewModel.Invoking(v => new SavedPlan(v.Missions, v.Plan!, CrateCap.Of(v.Ship))).Should().NotThrow();
    }

    [Fact]
    public void MoveCratesCommand_moves_a_selection_keeping_its_offset()
    {
        var viewModel = WithPlan();
        var gold = viewModel.Plan!.Crates.Where(c => c.Commodity == "Gold").ToArray();
        var bow = gold.Single(c => c.Position.Length == 1);

        viewModel.MoveCratesCommand.Execute(new Gesture.MoveCrates(bow, gold, "rack-1", new GridPosition(1, 1, 1)));

        viewModel.Plan!.Crates.Where(c => c.Commodity == "Gold").Select(c => (c.RackId, c.Position))
            .Should().BeEquivalentTo([("rack-1", new GridPosition(1, 1, 1)), ("rack-1", new GridPosition(0, 1, 1))]);
        viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void MoveCratesCommand_with_a_crate_beyond_every_rack_lays_the_selection_into_the_target_rack()
    {
        var viewModel = WithPlan();
        var gold = viewModel.Plan!.Crates.Where(c => c.Commodity == "Gold").ToArray();
        var aft = gold.Single(c => c.Position.Length == 0);

        viewModel.MoveCratesCommand.Execute(new Gesture.MoveCrates(aft, gold, "rack-1", new GridPosition(3, 1, 1)));

        viewModel.Plan!.Crates.Where(c => c.Commodity == "Gold").Should().OnlyContain(c => c.RackId == "rack-1");
        viewModel.Notices.Should().BeEmpty();
    }

    /// <summary>The drag of the destination's legend line (one mission per stop in these plans) onto a rack.</summary>
    private static Gesture.MoveLegend LegendMove(OverlayViewModel viewModel, string destination, string rackId) =>
        new(viewModel.Scene.Legend.Single(e => e.Destination == destination).Crates, rackId);

    [Fact]
    public void MoveLegendCommand_onto_a_full_rack_displaces_other_cargo_with_a_notice()
    {
        var viewModel = WithPlan();

        viewModel.MoveLegendCommand.Execute(LegendMove(viewModel, "Shubin", "rack-0"));

        viewModel.Plan!.Crates.Single(c => c.Destination == "Shubin").RackId.Should().Be("rack-0");
        viewModel.Notices.Should().Equal("Moved 2 crates for Riker to rack 2 to make room");
    }

    [Fact]
    public void MoveLegendCommand_with_room_moves_every_crate_of_the_line()
    {
        // Riker's one 4 SCU crate leaves room for Shubin's 8 in Rack 2 (Shubin, the larger, is in Rack 1, D64).
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a"));
        IReadOnlyList<MissionStop> missions =
        [
            new("Riker", 1, [new MissionItem("Titanium", 4, CrateSize.Four)]),
            new("Shubin", 2, [new MissionItem("Agricium", 8, CrateSize.Eight)]),
        ];
        viewModel.ShowPlan(SavedPlan.FromStops(missions, LoadPlanner.Plan(viewModel.Ship, missions.ToStops()), CrateCap.Of(viewModel.Ship)));

        viewModel.MoveLegendCommand.Execute(LegendMove(viewModel, "Shubin", "rack-1"));

        viewModel.Plan!.Crates.Should().OnlyContain(c => c.RackId == "rack-1");
        viewModel.Notices.Should().BeEmpty();
    }

    // --- Split and merge (D27) ---
    // Split and shared: Riker's four Titanium 4s fill Rack 1, [2, 0, 0] and [0, 0, 0] on the plate, two more on top.
    // The file is schema 1, so every item's max crate size is its crate size: a 4 split into 2s merges back, two
    // Lorville 2s never do.

    private static Crate TitaniumAt(OverlayViewModel viewModel, int length, int width, int stack) =>
        viewModel.Plan!.Crates.Single(c => c.RackId == "rack-0" && c.Position == new GridPosition(length, width, stack));

    private static void Select(OverlayViewModel viewModel, params Crate[] crates) =>
        viewModel.Selection = crates.Aggregate(CrateSelection.Empty, (selection, crate) => selection.Toggle(viewModel.Scene, crate));

    private static IReadOnlySet<Crate> Selected(OverlayViewModel viewModel) => viewModel.Selection.In(viewModel.Scene);

    [Fact]
    public void Split_and_merge_are_disabled_without_a_selection_and_say_why()
    {
        var viewModel = WithPlan();

        viewModel.SplitCommand.CanExecute(null).Should().BeFalse();
        viewModel.MergeCommand.CanExecute(null).Should().BeFalse();
        viewModel.SplitToolTip.Should().Be(OverlayViewModel.SelectOneCrateText);
        viewModel.MergeToolTip.Should().Be(OverlayViewModel.SelectCratesText);
    }

    [Fact]
    public void Split_is_enabled_for_one_crate_larger_than_1_SCU()
    {
        var viewModel = WithPlan();

        Select(viewModel, TitaniumAt(viewModel, 2, 0, 0));

        viewModel.SplitCommand.CanExecute(null).Should().BeTrue();
        viewModel.SplitToolTip.Should().Be(OverlayViewModel.SplitText);
        viewModel.MergeCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Split_is_disabled_for_a_1_SCU_crate_with_Cores_reason()
    {
        var viewModel = WithPlan();

        Select(viewModel, viewModel.Plan!.Crates.First(c => c.Commodity == "Gold"));

        viewModel.SplitCommand.CanExecute(null).Should().BeFalse();
        viewModel.SplitToolTip.Should().Be("1 SCU crates can't be split.");
    }

    [Fact]
    public void A_new_selection_updates_buttons_and_tool_tips()
    {
        var viewModel = WithPlan();
        var raised = 0;
        viewModel.SplitCommand.CanExecuteChanged += (_, _) => raised++;

        var changes = Changes(viewModel, () => Select(viewModel, TitaniumAt(viewModel, 2, 0, 0)));

        changes.Should().Contain([nameof(OverlayViewModel.Selection), nameof(OverlayViewModel.SplitToolTip), nameof(OverlayViewModel.MergeToolTip)]);
        raised.Should().BePositive();
    }

    [Fact]
    public void SplitCommand_splits_the_crate_selects_both_parts_and_rebooks_the_mission()
    {
        var viewModel = WithPlan();
        Select(viewModel, TitaniumAt(viewModel, 2, 0, 0));

        var changes = Changes(viewModel, () => viewModel.SplitCommand.Execute(null));

        Selected(viewModel).Should().BeEquivalentTo([TitaniumAt(viewModel, 2, 0, 0), TitaniumAt(viewModel, 2, 1, 0)]);
        Selected(viewModel).Should().OnlyContain(c => c.CrateSize == CrateSize.Two);
        viewModel.Missions[0].Stops[0].Items[0].Crates.Should().Equal(CrateSize.Four, CrateSize.Four, CrateSize.Four, CrateSize.Two, CrateSize.Two);

        // PlanSaver saves on every change of Plan (D18).
        changes.Should().Contain(nameof(OverlayViewModel.Plan));
    }

    [Fact]
    public void MergeCommand_after_a_split_gives_the_crate_back_and_selects_it()
    {
        var viewModel = WithPlan();
        var original = viewModel.Plan!;
        Select(viewModel, TitaniumAt(viewModel, 2, 0, 0));
        viewModel.SplitCommand.Execute(null);

        // The parts stay selected, so merging back is one click.
        viewModel.MergeCommand.CanExecute(null).Should().BeTrue();
        viewModel.MergeToolTip.Should().Be(OverlayViewModel.MergeText);
        viewModel.MergeCommand.Execute(null);

        viewModel.Plan!.Crates.Should().Equal(original.Crates);
        Selected(viewModel).Should().BeEquivalentTo([TitaniumAt(viewModel, 2, 0, 0)]);
        viewModel.Missions[0].Stops[0].Items[0].CrateList.Should().Equal(CrateSize.Four, CrateSize.Four, CrateSize.Four, CrateSize.Four);
    }

    [Fact]
    public void Merge_is_disabled_for_crates_that_do_not_touch_with_Cores_reason()
    {
        var viewModel = WithPlan();
        Select(viewModel, TitaniumAt(viewModel, 2, 0, 0));
        viewModel.SplitCommand.Execute(null);
        Select(viewModel, TitaniumAt(viewModel, 0, 0, 0));
        viewModel.SplitCommand.Execute(null);

        Select(viewModel, TitaniumAt(viewModel, 2, 0, 0), TitaniumAt(viewModel, 0, 1, 0));

        viewModel.MergeCommand.CanExecute(null).Should().BeFalse();
        viewModel.MergeToolTip.Should().Be("The crates are not adjacent.");
        viewModel.SplitToolTip.Should().Be(OverlayViewModel.SelectOneCrateText);
    }

    [Fact]
    public void A_refused_split_shows_a_notice_and_keeps_the_plan()
    {
        // Unreachable through the disabled button; the command still doesn't trust it.
        var viewModel = WithPlan();
        var original = viewModel.Plan;
        Select(viewModel, viewModel.Plan!.Crates.First(c => c.Commodity == "Gold"));

        viewModel.SplitCommand.Execute(null);

        viewModel.Plan.Should().BeSameAs(original);
        viewModel.Notices.Should().Equal(OverlayViewModel.SplitRefusedPrefix + "1 SCU crates can't be split.");
    }

    // --- ContractTracker (T5.4) ---

    private static ContractTrackerCandidate Candidate(long id, string destination, int scu = 4, CrateSize max = CrateSize.Four, bool sizeAssumed = false) =>
        new([id], null, [new MissionStop(destination, 1, [new MissionItem("Stims", scu, max)])], sizeAssumed, false, null, null, null);

    /// <param name="present">The known ids still in ContractTracker; null = not read.</param>
    private static ContractTrackerReadResult Read(long[]? present, params ContractTrackerCandidate[] candidates) =>
        new(candidates, null, present is null ? null : new HashSet<long>(present));

    private static OverlayViewModel Empty() => new(ShipLoader.Load("hull_a"));

    // --- One stop per place (B1) ---

    private const string MergedText = OverlayViewModel.StopsMergedPrefix + "Thundering Express → Thundering Express Station";

    /// <summary>A plan as before B1: the fragment and the full name of one place as two stops.</summary>
    private static SavedPlan TwoNamesOfOnePlace()
    {
        var ship = ShipLoader.Load("hull_a");
        var empty = new SavedPlan([], new LoadPlan(ship.Id, [], []), CrateCap.Of(ship));
        var first = LoadPlanner.PlaceInto(empty, ship, [new MissionStop("Thundering Express", 1, [new MissionItem("Ice", 4, CrateSize.Four)])]);
        return LoadPlanner.PlaceInto(first, ship, [new MissionStop("Thundering Express Station", 1, [new MissionItem("Stims", 4, CrateSize.Four)])]);
    }

    [Fact]
    public void ShowPlan_merges_names_of_one_place_and_says_so_until_the_next_plan()
    {
        var viewModel = Empty();

        viewModel.ShowPlan(TwoNamesOfOnePlace());

        // One stop, a line per mission (B6).
        viewModel.Scene.Legend.Select(l => l.Destination).Distinct().Should().Equal("Thundering Express Station");
        viewModel.Notices.Should().Equal(MergedText);

        viewModel.ShowPlan(TestPlans.SplitAndShared());

        viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void ApplyContractTracker_with_the_longer_name_renames_the_stop_and_says_so()
    {
        // K1: the sync renames a stop of the plan, so the player sees it like a merge on loading.
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Thundering Express")));

        viewModel.ApplyContractTracker(Read([36], Candidate(37, "Thundering Express Station")));

        viewModel.Scene.Legend.Select(l => l.Destination).Distinct().Should().Equal("Thundering Express Station");
        viewModel.Notices.Should().Equal(MergedText);
    }

    [Fact]
    public void Merge_notice_stays_through_a_read_without_news_and_goes_with_the_next_sync()
    {
        // The start reads ContractTracker once after the debounce; that read must not take the loading notice away.
        var viewModel = Empty();
        viewModel.ShowPlan(TwoNamesOfOnePlace());

        viewModel.ApplyContractTracker(Read([]));
        viewModel.Notices.Should().Equal(MergedText);

        viewModel.ApplyContractTracker(Read([], Candidate(40, "Riker")));
        viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void ApplyContractTracker_without_a_plan_starts_one_with_the_capture()
    {
        var viewModel = Empty();

        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker")));

        viewModel.HasPlan.Should().BeTrue();
        viewModel.Missions.Should().ContainSingle().Which.Contract!.ContractIds.Should().Equal(36);
        viewModel.Watermark.Should().Be(36);
        viewModel.Scene.Legend.Single().Destination.Should().Be("Riker");
    }

    [Fact]
    public void ApplyContractTracker_keeps_crates_and_ticks_of_the_plan()
    {
        var viewModel = WithPlan();
        viewModel.ToggleDeliveredCommand.Execute("Riker");
        var before = viewModel.Plan!.Crates;

        viewModel.ApplyContractTracker(Read(null, Candidate(40, "Area18", 1, CrateSize.One)));

        viewModel.Plan!.Crates.Take(before.Count).Should().Equal(before);
    }

    [Fact]
    public void ApplyContractTracker_without_news_changes_nothing()
    {
        // A checkpoint or our own read: no PropertyChanged, so no new scene (a drag goes on) and no save (D38).
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker")));

        Changes(viewModel, () => viewModel.ApplyContractTracker(Read([36]))).Should().BeEmpty();
    }

    [Fact]
    public void ApplyContractTracker_of_a_mission_that_doesnt_fit_raises_PropertyChanged_for_Saved()
    {
        // The plan's crates stay the same; PlanSaver listens to Saved, so the new mission and watermark are saved.
        var viewModel = Empty();

        Changes(viewModel, () => viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker", 72, CrateSize.Eight))))
            .Should().Contain(nameof(OverlayViewModel.Saved));
        viewModel.Scene.Unplaced.Should().ContainSingle();
    }

    [Fact]
    public void A_deleted_contract_with_crates_on_board_stays_marked_gone_with_one_notice()
    {
        // D81: the legend's "· gone" replaces the notice derived on every change (D43).
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker")));

        viewModel.ApplyContractTracker(Read([]));

        viewModel.HasPlan.Should().BeTrue();
        viewModel.Missions.Single().Contract!.Gone.Should().BeTrue();
        viewModel.Notices.Should().Equal("Contract #36 gone from ContractTracker – kept in plan");
    }

    [Fact]
    public void A_deleted_contract_that_is_ticked_stays_until_the_player_deletes_it()
    {
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.ApplyContractTracker(Read([37]));

        viewModel.Missions.Select(m => (m.Contract!.ContractIds[0], m.Contract.Gone)).Should().Equal((36L, true), (37L, false));
    }

    [Fact]
    public void With_remove_a_deleted_contract_that_is_ticked_leaves_at_the_next_sync()
    {
        var viewModel = Empty();
        viewModel.OnContractGone = ContractGonePolicy.Remove;
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.ApplyContractTracker(Read([37]));

        viewModel.Missions.Should().ContainSingle().Which.Contract!.ContractIds.Should().Equal(37);
        viewModel.Notices.Should().Equal("Contract #36 gone from ContractTracker – removed 1 delivered");
    }

    [Fact]
    public void ApplyContractTracker_shows_the_readers_notice()
    {
        var viewModel = Empty();

        viewModel.ApplyContractTracker(new ContractTrackerReadResult([], ContractTrackerReader.SchemaNotSupportedNotice));

        viewModel.Notices.Should().Equal(ContractTrackerReader.SchemaNotSupportedNotice);
    }

    [Fact]
    public void A_paused_sync_takes_nothing_until_a_new_plan()
    {
        // plan.json couldn't be read: the sync must not overwrite it (T5.4 planning).
        var viewModel = Empty();
        viewModel.PauseContractTracker();

        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker")));

        viewModel.HasPlan.Should().BeFalse();
        viewModel.Notices.Should().Equal(OverlayViewModel.SyncPausedText);

        viewModel.ShowPlan(TestPlans.SplitAndShared());
        viewModel.ApplyContractTracker(Read(null, Candidate(36, "Area18", 1, CrateSize.One)));

        viewModel.Notices.Should().BeEmpty();
        viewModel.Watermark.Should().Be(36);
    }

    [Fact]
    public void A_read_without_news_doesnt_place_a_waiting_mission_but_the_button_does()
    {
        // Retry at the sync (T5.4 planning); a file event without new rows or missing ids stays a no-op (D38).
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], [.. Enumerable.Range(1, 5).Select(i => Candidate(i, $"Stop {i}", 16, CrateSize.Sixteen))]));
        viewModel.ToggleDeliveredCommand.Execute("Stop 1");

        viewModel.ApplyContractTracker(Read([1, 2, 3, 4, 5]));
        viewModel.Missions[^1].Unplaced.Should().BeTrue();

        viewModel.ApplyContractTracker(Read([1, 2, 3, 4, 5]), manual: true);
        viewModel.Missions[^1].Unplaced.Should().BeFalse();
    }

    [Fact]
    public void DropMissionCommand_takes_the_mission_out()
    {
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin", 72, CrateSize.Eight)));
        var crates = viewModel.Plan!.Crates;

        viewModel.DropMissionCommand.Execute(2);

        viewModel.Missions.Select(m => m.Id).Should().Equal(1);
        viewModel.Plan!.Crates.Should().Equal(crates);
        viewModel.Scene.Unplaced.Should().BeEmpty();
    }

    // --- ContractTracker settings (T5.5, D44) ---

    [Fact]
    public void A_new_database_takes_its_captures_below_the_old_watermark_and_says_so()
    {
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(57, "Riker")));

        viewModel.ApplyContractTracker(new ContractTrackerReadResult([Candidate(1, "Shubin")], null, new HashSet<long>(), DatabaseLooksNew: true));

        viewModel.Watermark.Should().Be(1);
        viewModel.Missions.Select(m => m.Contract!.ContractIds.Single()).Should().Equal(57, 1);
        viewModel.Notices.Should().Contain(OverlayViewModel.DatabaseLooksNewText);
    }

    [Fact]
    public void An_empty_new_database_sets_the_watermark_back_and_is_saved()
    {
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(57, "Riker")));
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        var changes = Changes(viewModel, () => viewModel.ApplyContractTracker(new ContractTrackerReadResult([], null, new HashSet<long>(), DatabaseLooksNew: true)));

        changes.Should().Contain(nameof(OverlayViewModel.Saved));
        viewModel.Watermark.Should().Be(0);
    }

    [Fact]
    public void ShowNewContracts_counts_the_captures_and_leaves_the_plan_alone()
    {
        var viewModel = Empty();

        viewModel.ShowNewContracts(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));

        viewModel.HasPlan.Should().BeFalse();
        viewModel.Notices.Should().Equal(OverlayViewModel.NewContractsText(2));
        OverlayViewModel.NewContractsText(2).Should().StartWith("2 new contracts in ContractTracker");
        OverlayViewModel.NewContractsText(1).Should().StartWith("1 new contract in ContractTracker");
    }

    [Fact]
    public void ShowNewContracts_without_captures_says_nothing_and_the_readers_notice_wins()
    {
        var viewModel = Empty();
        viewModel.ShowNewContracts(Read([], Candidate(36, "Riker")));

        viewModel.ShowNewContracts(Read([]));
        viewModel.Notices.Should().BeEmpty();

        viewModel.ShowNewContracts(new ContractTrackerReadResult([], ContractTrackerReader.SchemaNotSupportedNotice));
        viewModel.Notices.Should().Equal(ContractTrackerReader.SchemaNotSupportedNotice);
    }

    [Fact]
    public void ShowNewContracts_keeps_a_paused_sync_paused()
    {
        var viewModel = Empty();
        viewModel.PauseContractTracker();

        viewModel.ShowNewContracts(Read([], Candidate(36, "Riker")));

        viewModel.Notices.Should().Equal(OverlayViewModel.SyncPausedText);
    }

    [Fact]
    public void Taking_the_new_contracts_in_clears_their_notice()
    {
        var viewModel = Empty();
        viewModel.ShowNewContracts(Read([], Candidate(36, "Riker")));

        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker")), manual: true);

        viewModel.Notices.Should().BeEmpty();
        viewModel.Missions.Should().ContainSingle();
    }

    [Fact]
    public void ContractTrackerQuery_names_the_watermark_and_the_plans_capture_ids()
    {
        var viewModel = Empty();
        viewModel.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));

        var (watermark, known) = viewModel.ContractTrackerQuery();

        watermark.Should().Be(37);
        known.Should().BeEquivalentTo([36L, 37L]);
    }

    // --- Bow marker (D62) ---

    private static readonly ShipOutlineCatalog Outlines = ShipOutlineLoader.Load();

    [Fact]
    public void A_ship_with_an_outline_shows_it_instead_of_the_wedge()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a")) { Outlines = Outlines };

        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>();
    }

    // The Aurora Mk II: its hologram a cell higher (holoOffset, K5.3b), so the outline's floor too (D83, T8.5).
    [Fact]
    public void The_outline_lies_on_the_ships_hologram_floor()
    {
        var aurora = ShipLoader.Load("aurora_mk_ii");
        var viewModel = new OverlayViewModel(aurora) { Outlines = Outlines };

        var floor = OutlinePlacement.Floor(Outlines.For(aurora)!, aurora);
        floor.Should().BeLessThan(aurora.Bounds.Min.Z);
        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>().Which.Points.Should().OnlyContain(p => p.Z == floor);
    }

    [Fact]
    public void The_outline_stays_through_a_new_plan_and_a_ship_change()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a"), ShipLoader.LoadAll()) { Outlines = Outlines };

        viewModel.ShowPlan(TestPlans.SplitAndShared());
        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>();

        viewModel.ChangeShip(ShipLoader.Load("c2_hercules"));
        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>();
    }

    [Fact]
    public void A_ship_without_an_outline_keeps_the_wedge()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a")) { Outlines = ShipOutlineCatalog.Empty };

        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Wedge>();
    }

    [Fact]
    public void Mode_none_takes_every_marker_away_and_redraws_keeping_the_plan()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a")) { Outlines = Outlines };
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        var plan = viewModel.Plan;

        var changes = Changes(viewModel, () => viewModel.OutlineMode = ShipOutlineMode.None);

        viewModel.Scene.Marker.Should().BeOfType<BowMarker.None>();
        viewModel.Plan.Should().BeSameAs(plan);
        changes.Should().Contain(nameof(OverlayViewModel.Scene));
    }

    [Fact]
    public void Setting_the_same_mode_again_keeps_the_scene()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a")) { Outlines = Outlines };
        var scene = viewModel.Scene;

        viewModel.OutlineMode = ShipOutlineMode.Outline;

        viewModel.Scene.Should().BeSameAs(scene);
    }

    [Fact]
    public void Outline_problems_make_one_notice_line()
    {
        OverlayViewModel.OutlinesNoticeText([]).Should().BeNull();
        OverlayViewModel.OutlinesNoticeText(["ship-outlines.json can't be read: gone"])
            .Should().Be("Ship outlines: ship-outlines.json can't be read: gone – ships without one show the bow wedge.");
        OverlayViewModel.OutlinesNoticeText(["Outline 2: 'x' needs at least 3 points.", "b", "c"])
            .Should().Be("Ship outlines: Outline 2: 'x' needs at least 3 points. (+2 more) – ships without one show the bow wedge.");
    }

    // --- Hologram (D63) ---

    private static PreparedHolo HoloFor(Ship ship, ShipOutlineMode look = ShipOutlineMode.HoloShaded)
    {
        var (vertices, triangles) = HoloFixtures.Cube();
        System.Numerics.Vector3[] hull = [.. vertices.Select(v => new System.Numerics.Vector3((v.X - 0.5f) * 30, (v.Y - 0.5f) * 12, v.Z * 8))];
        return PreparedHolo.Prepare(ship, HoloReader.Read(HoloFixtures.Write(hull, triangles, HoloFixtures.CubeEdges(), 4, 8, 30)), look);
    }

    [Fact]
    public void A_hologram_of_the_ship_replaces_the_outline_in_a_hologram_look()
    {
        var hullA = ShipLoader.Load("hull_a");
        var viewModel = new OverlayViewModel(hullA) { Outlines = Outlines, OutlineMode = ShipOutlineMode.HoloShaded };
        var holo = HoloFor(hullA);

        var changes = Changes(viewModel, () => viewModel.ShowHolo(holo));

        viewModel.Holo.Should().BeSameAs(holo);
        viewModel.Scene.Marker.Should().Be(new BowMarker.Holo(holo.Placement.Min, holo.Placement.Max));
        changes.Should().Contain([nameof(OverlayViewModel.Holo), nameof(OverlayViewModel.Scene)]);
    }

    [Fact]
    public void Until_the_hologram_is_there_a_hologram_look_shows_the_outline()
    {
        var viewModel = new OverlayViewModel(ShipLoader.Load("hull_a")) { Outlines = Outlines, OutlineMode = ShipOutlineMode.HoloHiddenline };

        viewModel.Holo.Should().BeNull();
        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>();
    }

    [Fact]
    public void Another_ship_hides_the_old_ships_hologram_at_once()
    {
        var hullA = ShipLoader.Load("hull_a");
        var viewModel = new OverlayViewModel(hullA, ShipLoader.LoadAll()) { Outlines = Outlines, OutlineMode = ShipOutlineMode.HoloFlat };
        viewModel.ShowHolo(HoloFor(hullA, ShipOutlineMode.HoloFlat));

        var changes = Changes(viewModel, () => viewModel.ChangeShip(ShipLoader.Load("c2_hercules")));

        viewModel.Holo.Should().BeNull();
        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>();
        changes.Should().Contain(nameof(OverlayViewModel.Holo));
    }

    [Fact]
    public void The_outline_look_hides_the_hologram()
    {
        var hullA = ShipLoader.Load("hull_a");
        var viewModel = new OverlayViewModel(hullA) { Outlines = Outlines, OutlineMode = ShipOutlineMode.HoloShaded };
        viewModel.ShowHolo(HoloFor(hullA));

        var changes = Changes(viewModel, () => viewModel.OutlineMode = ShipOutlineMode.Outline);

        viewModel.Holo.Should().BeNull();
        viewModel.Scene.Marker.Should().BeOfType<BowMarker.Outline>();
        changes.Should().Contain(nameof(OverlayViewModel.Holo));
    }

    [Fact]
    public void Showing_the_same_hologram_again_keeps_the_scene_and_its_selection()
    {
        var hullA = ShipLoader.Load("hull_a");
        var viewModel = new OverlayViewModel(hullA) { Outlines = Outlines, OutlineMode = ShipOutlineMode.HoloShaded };
        var holo = HoloFor(hullA);
        viewModel.ShowHolo(holo);
        var scene = viewModel.Scene;

        viewModel.ShowHolo(holo);
        viewModel.ShowHolo(holo with { Look = ShipOutlineMode.HoloFlat });

        // Another look has the same box: the fit and the selection stay, only the backdrop redraws.
        viewModel.Scene.Should().BeSameAs(scene);
    }

    [Fact]
    public void The_hologram_notice_names_the_reason()
    {
        OverlayViewModel.HoloNoticeText("no answer from github.com within 5 s")
            .Should().Be("Ship hologram not available: no answer from github.com within 5 s – showing the outline");
    }
}
