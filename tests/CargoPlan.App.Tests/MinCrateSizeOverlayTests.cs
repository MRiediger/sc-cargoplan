using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>A rack's minimum (K3.3) in the overlay: a Hull C plan saved before it, with a 16 on board, is planned anew.</summary>
public class MinCrateSizeOverlayTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullC = Catalog.Get("hull_c");

    [Fact]
    public void A_plan_file_with_a_crate_below_the_minimum_is_planned_anew_and_the_mission_waits()
    {
        var withoutMin = new Ship(HullC.Id, HullC.Name, HullC.TotalScu,
            [.. HullC.Racks.Select(r => new Rack(r.Id, r.Index, r.CapacityScu, r.Grid, r.Position, r.Origin, r.StackAxis, r.MaxCrateSize))]);
        MissionStop riker = new("Riker", 1, [new MissionItem("Titanium", 16, CrateSize.Sixteen)]);
        using var stream = new MemoryStream();
        PlanFile.Write(stream, LoadPlanner.Plan(withoutMin, [new Mission(1, [riker])], ["Riker"]));
        stream.Position = 0;
        var viewModel = new OverlayViewModel(HullC, Catalog);

        viewModel.ShowPlan(PlanFile.Read(stream, "plan.json", HullC));

        viewModel.Notices.Should().Contain(OverlayViewModel.RacksReplannedText(HullC.Name));
        viewModel.Missions.Should().ContainSingle().Which.Unplaced.Should().BeTrue();
        viewModel.Plan!.Crates.Should().BeEmpty();
    }
}
