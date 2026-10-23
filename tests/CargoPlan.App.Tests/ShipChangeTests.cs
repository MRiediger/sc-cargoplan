using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

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
}
