using System.ComponentModel;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;

namespace CargoPlan.App.Tests;

public class OverlayViewModelTests
{
    private static OverlayViewModel WithSamplePlan()
    {
        var (ship, plan) = SamplePlan.Load();
        return new OverlayViewModel(ship) { Plan = plan };
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
        WithSamplePlan().IsLocked.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void IsLocked_is_manual_or_auto(bool manual, bool auto, bool expected)
    {
        var viewModel = WithSamplePlan();

        viewModel.IsManuallyLocked = manual;
        viewModel.IsAutoLocked = auto;

        viewModel.IsLocked.Should().Be(expected);
    }

    [Fact]
    public void IsManuallyLocked_raises_PropertyChanged_for_IsLocked_so_the_window_can_switch_click_through()
    {
        var viewModel = WithSamplePlan();

        Changes(viewModel, () => viewModel.IsManuallyLocked = true).Should().Contain(nameof(OverlayViewModel.IsLocked));
    }

    [Fact]
    public void IsAutoLocked_raises_PropertyChanged_for_IsLocked_so_the_window_can_switch_click_through()
    {
        var viewModel = WithSamplePlan();

        Changes(viewModel, () => viewModel.IsAutoLocked = true).Should().Contain(nameof(OverlayViewModel.IsLocked));
    }

    [Fact]
    public void ToggleLockCommand_flips_the_manual_lock_and_back()
    {
        var viewModel = WithSamplePlan();

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
        var viewModel = WithSamplePlan();
        viewModel.IsAutoLocked = true;

        viewModel.ToggleLockCommand.Execute(null);

        viewModel.IsManuallyLocked.Should().BeTrue();
        viewModel.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void Manual_lock_survives_the_auto_lock_ending()
    {
        // Locked at the terminal, then flying (auto) and back at the terminal (auto ends): still locked.
        var viewModel = WithSamplePlan();
        viewModel.IsManuallyLocked = true;
        viewModel.IsAutoLocked = true;

        viewModel.IsAutoLocked = false;

        viewModel.IsLocked.Should().BeTrue();
    }

    [Fact]
    public void ToggleLockCommand_raises_PropertyChanged_for_IsLocked()
    {
        var viewModel = WithSamplePlan();

        Changes(viewModel, () => viewModel.ToggleLockCommand.Execute(null))
            .Should().Contain(nameof(OverlayViewModel.IsLocked));
    }

    [Fact]
    public void Notice_defaults_to_null_so_the_notice_line_is_hidden()
    {
        WithSamplePlan().Notice.Should().BeNull();
    }

    [Fact]
    public void Setting_Notice_raises_PropertyChanged()
    {
        var viewModel = WithSamplePlan();

        Changes(viewModel, () => viewModel.Notice = "Settings not saved.")
            .Should().Contain(nameof(OverlayViewModel.Notice));
    }

    [Fact]
    public void Scene_shows_the_plan_with_its_validation()
    {
        var viewModel = WithSamplePlan();

        viewModel.Scene.Legend.Select(e => e.Destination).Should().Equal("Riker", "Shubin", "Orison", "Lorville");
        viewModel.Scene.Violations.Should().BeEmpty();
    }

    [Fact]
    public void Scene_without_a_plan_shows_the_empty_ship()
    {
        var (ship, _) = SamplePlan.Load();
        var viewModel = new OverlayViewModel(ship);

        viewModel.HasPlan.Should().BeFalse();
        viewModel.Scene.Racks.Should().HaveCount(ship.Racks.Count);
        viewModel.Scene.Legend.Should().BeEmpty();
    }

    [Fact]
    public void Setting_Plan_raises_PropertyChanged_for_Scene_and_HasPlan()
    {
        var viewModel = WithSamplePlan();
        Stop riker = new("Riker", 1, [new CargoItem("Titanium", 4, CrateSize.Four)]);
        var newPlan = LoadPlanner.Plan(viewModel.Ship, [riker]);

        Changes(viewModel, () => viewModel.Plan = newPlan)
            .Should().Contain([nameof(OverlayViewModel.Scene), nameof(OverlayViewModel.HasPlan)]);
        viewModel.Scene.Legend.Should().Equal(new LegendEntry(0, "Riker", 4));
    }
}
