using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

/// <summary>
/// The door limit (D70) in the overlay: a notice while a derived mix is capped, a plan file whose mixes were split under
/// another cap planned anew, the editor's crates column split up to the ship's cap.
/// </summary>
public class DoorLimitOverlayTests
{
    private static readonly ShipCatalog Catalog = ShipLoader.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");
    private static readonly Ship Clipper = Catalog.Get("clipper");
    private const string DoorNotice = "crates capped to 2 SCU (door limit)";

    private static MissionStop Riker => new("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Eight)]);

    private static SavedPlan Planned(Ship ship) => LoadPlanner.Plan(ship, [new Mission(1, [Riker])], ["Riker"]);

    [Fact]
    public void A_plan_with_a_capped_mix_shows_the_cap_notice()
    {
        var viewModel = new OverlayViewModel(Clipper, Catalog);

        viewModel.ShowPlan(Planned(Clipper));

        viewModel.Notices.Should().Contain(DoorNotice);
    }

    [Fact]
    public void A_ship_change_brings_the_notice_and_takes_it_away()
    {
        var viewModel = new OverlayViewModel(HullA, Catalog);
        viewModel.ShowPlan(Planned(HullA));
        viewModel.Notices.Should().NotContain(n => n.StartsWith("crates capped", StringComparison.Ordinal));

        viewModel.ChangeShip(Clipper);
        viewModel.Notices.Should().Contain(DoorNotice);

        viewModel.ChangeShip(HullA);
        viewModel.Notices.Should().NotContain(n => n.StartsWith("crates capped", StringComparison.Ordinal));
    }

    // The Clipper's plan.json from before D70: its Riker 8 lay in the grid, the door now refuses it.
    [Fact]
    public void A_plan_file_split_under_another_cap_is_planned_anew_and_says_so()
    {
        var withoutDoor = new Ship(Clipper.Id, Clipper.Name, Clipper.TotalScu,
            [.. Clipper.Racks.Select(r => new Rack(r.Id, r.Index, r.CapacityScu, r.Grid, r.Position, r.Origin, r.StackAxis))]);
        using var stream = new MemoryStream();
        PlanFile.Write(stream, Planned(withoutDoor));
        stream.Position = 0;
        var read = PlanFile.Read(stream, "plan.json", Clipper);
        var viewModel = new OverlayViewModel(Clipper, Catalog);

        viewModel.ShowPlan(read);

        viewModel.Plan!.Crates.Should().HaveCount(4).And.OnlyContain(c => c.CrateSize == CrateSize.Two);
        viewModel.Notices.Should().Contain(OverlayViewModel.RacksReplannedText(Clipper.Name)).And.Contain(DoorNotice);
    }

    [Fact]
    public void The_editor_shows_a_derived_mix_split_up_to_the_ships_cap_and_follows_a_ship_change()
    {
        var overlay = new OverlayViewModel(Clipper, Catalog);
        var editor = new MissionEditorViewModel(overlay);
        var row = editor.Rows[0];
        (row.Destination, row.Commodity, row.Scu, row.MaxCrateSize) = ("Riker", "Titanium", "8", 8);

        row.CratesText.Should().Be("2+2+2+2");

        overlay.ChangeShip(HullA);

        row.CratesText.Should().Be("8");
    }
}
