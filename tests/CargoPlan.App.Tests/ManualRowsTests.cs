using CargoPlan.App.Editor;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// ContractTracker sometimes misses a destination (K6.5, D44 addendum): the player adds it in the editor to the same
/// mission ("Add to mission"), so it stands under the mission's #id in the legend. Rows added or changed by hand are
/// manual and stay until ContractTracker no longer lists the mission; the sync works as before.
/// </summary>
public class ManualRowsTests
{
    private static readonly ShipCatalog Catalog = FrozenShips.LoadAll();
    private static readonly Ship HullA = Catalog.Get("hull_a");

    /// <summary>Capture 37 to Riker, and a typed mission to Shubin.</summary>
    private static OverlayViewModel Overlay()
    {
        var overlay = new OverlayViewModel(HullA, Catalog);
        overlay.ShowPlan(LoadPlanner.Plan(
            HullA, [new Mission(1, [new MissionStop("Riker", 1, [new MissionItem("Stims", 2, CrateSize.Two)])], new ContractSource([37]))], ["Riker"]));
        var editor = new MissionEditorViewModel(overlay);
        editor.AddRowCommand.Execute(null);
        (editor.Rows[^1].Destination, editor.Rows[^1].Commodity, editor.Rows[^1].Scu) = ("Shubin", "Gold", "4");
        editor.PlanCommand.Execute(null);
        return overlay;
    }

    private static MissionRow Ct(MissionEditorViewModel editor) => editor.Rows.Single(r => r.ContractId == 37 && !r.Manual);

    /// <summary>Melodic Fields Station added by hand to capture 37, planned.</summary>
    private static OverlayViewModel WithAddedDestination(string destination = "Melodic Fields Station")
    {
        var overlay = Overlay();
        var editor = new MissionEditorViewModel(overlay);
        editor.SelectedRow = Ct(editor);
        editor.AddToMissionCommand.Execute(null);
        (editor.SelectedRow!.Destination, editor.SelectedRow.Commodity, editor.SelectedRow.Scu) = (destination, "Stims", "2");
        editor.PlanCommand.Execute(null);
        return overlay;
    }

    // --- Adding a destination to a ContractTracker mission ---

    [Fact]
    public void Add_to_mission_puts_a_manual_row_into_the_selected_rows_mission_below_it()
    {
        var editor = new MissionEditorViewModel(Overlay());
        var ct = Ct(editor);
        editor.SelectedRow = ct;

        editor.AddToMissionCommand.Execute(null);

        var added = editor.SelectedRow!;
        editor.Rows.IndexOf(added).Should().Be(editor.Rows.IndexOf(ct) + 1);
        (added.MissionId, added.ContractId, added.Source, added.Manual, added.MissionLabel).Should().Be((ct.MissionId, 37L, "#37", true, "#37 ✎"));
        (added.MaxCrateSize, added.Pickup).Should().Be((ct.MaxCrateSize, ct.Pickup));
    }

    [Fact]
    public void Add_to_mission_needs_a_ContractTracker_row_selected()
    {
        var editor = new MissionEditorViewModel(Overlay());

        editor.SelectedRow = null;
        editor.AddToMissionCommand.CanExecute(null).Should().BeFalse();

        editor.SelectedRow = editor.Rows.Single(r => r.Destination == "Shubin");
        editor.AddToMissionCommand.CanExecute(null).Should().BeFalse("a typed mission is one destination (D42)");

        editor.SelectedRow = Ct(editor);
        editor.AddToMissionCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void Plan_puts_the_added_destination_under_the_missions_id()
    {
        var overlay = WithAddedDestination();

        var mission = overlay.Missions.Single(m => m.Contract is not null);
        mission.Stops.Select(s => s.Destination).Should().Equal("Riker", "Melodic Fields Station");
        mission.Items.Select(at => at.Item.Manual).Should().Equal(false, true);
        overlay.Scene.Legend.Where(e => e.Mission == mission.Id).Select(e => e.Destination).Should().Equal("Riker", "Melodic Fields Station");
        overlay.Scene.Legend.First(e => e.Mission == mission.Id).HeaderLabel.Should().EndWith("· #37");
    }

    // --- Changing a ContractTracker row ---

    [Fact]
    public void Changing_a_ContractTracker_row_makes_it_manual_and_loading_it_doesnt()
    {
        var editor = new MissionEditorViewModel(Overlay());
        editor.Rows.Should().OnlyContain(r => !r.Manual);

        Ct(editor).Scu = "4";

        editor.Rows.Single(r => r.ContractId == 37).Manual.Should().BeTrue();
        editor.Rows.Single(r => r.Destination == "Shubin").Manual.Should().BeFalse();
    }

    [Fact]
    public void A_typed_row_changed_stays_without_the_mark()
    {
        var editor = new MissionEditorViewModel(Overlay());
        var typed = editor.Rows.Single(r => r.Destination == "Shubin");

        typed.Scu = "8";

        (typed.Manual, typed.MissionLabel).Should().Be((false, ""));
    }

    [Fact]
    public void A_scan_with_the_longer_name_renames_the_manual_stop_and_it_stays_manual()
    {
        // B1: "Melodic Fields" and "Melodic Fields Station" are one place, whichever way the other contract came in.
        var overlay = WithAddedDestination("Melodic Fields");

        overlay.ApplyScan(Import.Contracts.ScannedContract.Read(
            "ABANDON\nDETAILS\nReward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\n"
            + "Deliver 0/2 SCU of Stims to Melodic Fields Station.\nCollect Stims from Everus Harbor.\nSHARE UNTRACK"));

        var mission = overlay.Missions.Single(m => m.Contract?.ContractIds.Contains(37) == true);
        mission.Stops.Select(s => s.Destination).Should().Equal("Riker", "Melodic Fields Station");
        mission.Items[1].Item.Manual.Should().BeTrue();
    }

}
