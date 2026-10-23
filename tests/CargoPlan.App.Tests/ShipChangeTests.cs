using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.App.Tests;

/// <summary>The overlay on another ship (D59): a plan made for one is planned anew, never thrown away or refused.</summary>
public class ShipChangeTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");
    private static readonly Ship C2 = Catalog.Get("c2_hercules");

    private static OverlayViewModel Overlay(Ship ship) => new(ship, Catalog);

    /// <summary>The split-and-shared plan as the C2 would hold it.</summary>
    private static SavedPlan OnC2() => LoadPlanner.Replan(TestPlans.SplitAndShared(), C2);

    // --- A plan for another ship, as plan.json brings it (startup) ---

    [Fact]
    public void ShowPlan_of_a_plan_for_another_ship_replans_it_and_says_so()
    {
        var viewModel = Overlay(HullA);
        var onC2 = OnC2();

        viewModel.ShowPlan(onC2);

        viewModel.Plan!.ShipId.Should().Be("hull_a");
        viewModel.Missions.Should().BeEquivalentTo(onC2.Missions, o => o.WithStrictOrdering());
        viewModel.Scene.Ship.Should().BeSameAs(HullA);
        viewModel.Notices.Should().Contain("Plan was made for CRUS C2 Hercules, replanned for MISC Hull A.");
    }

    [Fact]
    public void ShowPlan_of_a_plan_for_an_unknown_ship_names_its_id()
    {
        var viewModel = Overlay(HullA);
        var onC2 = OnC2();
        SavedPlan unknown = new(onC2.Missions, onC2.Plan with { ShipId = "spike_gone" }, onC2.Watermark);

        viewModel.ShowPlan(unknown);

        viewModel.Plan!.ShipId.Should().Be("hull_a");
        viewModel.Notices.Should().Contain("Plan was made for spike_gone, replanned for MISC Hull A.");
    }

    [Fact]
    public void ShowPlan_of_a_plan_for_another_ship_keeps_its_ticks()
    {
        var viewModel = Overlay(HullA);
        var onC2 = OnC2();
        SavedPlan ticked = new(onC2.Missions, LoadPlanner.SetDelivered(onC2.Plan, C2, "Riker", true), onC2.Watermark);

        viewModel.ShowPlan(ticked);

        viewModel.Plan!.Crates.Where(c => c.Destination == "Riker").Should().OnlyContain(c => c.Delivered);
    }

    [Fact]
    public void ShowPlan_of_a_plan_for_the_ship_clears_the_replan_notice()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(OnC2());

        viewModel.ShowPlan(TestPlans.SplitAndShared());

        viewModel.Notices.Should().NotContain(n => n.StartsWith("Plan was made for", StringComparison.Ordinal));
    }

    // --- A ship change from the editor ---

    [Fact]
    public void ChangeShip_without_a_plan_shows_the_empty_new_ship()
    {
        var viewModel = Overlay(HullA);

        var changes = Changes(viewModel, () => viewModel.ChangeShip(C2));

        viewModel.Ship.Should().BeSameAs(C2);
        viewModel.HasPlan.Should().BeFalse();
        viewModel.Scene.Ship.Should().BeSameAs(C2);
        viewModel.Scene.Racks.Should().HaveCount(C2.Racks.Count);
        changes.Should().Contain([nameof(OverlayViewModel.Ship), nameof(OverlayViewModel.Scene)]);
    }

    [Fact]
    public void ChangeShip_with_a_plan_replans_it_and_keeps_missions_and_watermark()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        var before = viewModel.Saved!;

        viewModel.ChangeShip(C2);

        viewModel.Plan!.ShipId.Should().Be("c2_hercules");
        viewModel.Missions.Should().BeEquivalentTo(before.Missions, o => o.WithStrictOrdering());
        viewModel.Watermark.Should().Be(before.Watermark);
        viewModel.Scene.Ship.Should().BeSameAs(C2);
    }

    [Fact]
    public void ChangeShip_keeps_the_ticks()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        viewModel.ToggleDeliveredCommand.Execute("Riker");

        viewModel.ChangeShip(C2);

        viewModel.Scene.Legend.Single(l => l.Destination == "Riker").Delivered.Should().BeTrue();
        viewModel.Scene.Legend.Where(l => l.Destination != "Riker").Should().OnlyContain(l => !l.Delivered);
    }

    [Fact]
    public void ChangeShip_to_a_smaller_ship_places_what_fits_and_lists_the_rest_as_unplaced()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());

        // Two Hull A racks, 32 of the plan's 54 SCU.
        viewModel.ChangeShip(Catalog.Get("apollo_medivac"));

        viewModel.Missions.Should().Contain(m => m.Unplaced).And.Contain(m => !m.Unplaced);
        viewModel.Scene.Unplaced.Should().NotBeEmpty();
    }

    [Fact]
    public void ChangeShip_clears_the_ship_and_replan_notices()
    {
        var viewModel = Overlay(HullA);
        viewModel.SetNotice(NoticeSource.Ship, "Ship 'nope' not loaded: not found – using MISC Hull A.");
        viewModel.ShowPlan(OnC2());

        viewModel.ChangeShip(C2);

        viewModel.Notices.Should().BeEmpty();
    }

    [Fact]
    public void ChangeShip_to_the_current_ship_changes_nothing()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());

        Changes(viewModel, () => viewModel.ChangeShip(HullA)).Should().BeEmpty();
    }

    [Fact]
    public void ShipChangeQuestion_is_null_without_a_plan()
    {
        Overlay(HullA).ShipChangeQuestion(C2).Should().BeNull();
    }

    [Fact]
    public void ShipChangeQuestion_with_a_plan_names_the_ship_and_what_is_lost()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());

        viewModel.ShipChangeQuestion(C2).Should().Be(
            "Replan for CRUS C2 Hercules? Crate positions change and manual moves are lost; split crates keep their sizes.");
    }

    [Fact]
    public void ShipChangeQuestion_with_ticks_says_they_stay()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        viewModel.ToggleDeliveredCommand.Execute("Riker");
        viewModel.ToggleDeliveredCommand.Execute("Shubin");

        viewModel.ShipChangeQuestion(C2).Should().EndWith(Environment.NewLine + "2 stops already ticked – ticks stay.");
    }

    [Fact]
    public void ContractTracker_sync_after_a_change_places_on_the_new_ship()
    {
        var viewModel = Overlay(HullA);
        viewModel.ShowPlan(TestPlans.SplitAndShared());
        viewModel.ChangeShip(C2);
        ContractTrackerCandidate capture = new([36], null, [new MissionStop("Area18", 1, [new MissionItem("Stims", 4, CrateSize.Four)])], false, false, null, null, null);

        viewModel.ApplyContractTracker(new ContractTrackerReadResult([capture], null, null));

        viewModel.Plan!.ShipId.Should().Be("c2_hercules");
        viewModel.Missions.Should().Contain(m => m.Contract != null && m.Contract.ContractIds.Contains(36) && !m.Unplaced);
        viewModel.Watermark.Should().Be(36);
    }

    private static List<string?> Changes(OverlayViewModel source, Action act)
    {
        List<string?> names = [];
        void Handler(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => names.Add(e.PropertyName);
        source.PropertyChanged += Handler;
        act();
        source.PropertyChanged -= Handler;
        return names;
    }
}
