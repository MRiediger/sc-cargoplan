using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// A contract gone from ContractTracker in the overlay (D81): marked "· gone" in legend and editor. Since T10.4a nothing
/// marks a contract gone; a plan saved before still holds the mark, and these tests load such plans.
/// </summary>
public class GoneOverlayTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static Mission Capture(int id, long contractId, string destination, int scu = 4, CrateSize max = CrateSize.Four, bool gone = false, bool unplaced = false) =>
        new(id, [new MissionStop(destination, 1, [new MissionItem("Stims", scu, max)])], new ContractSource([contractId], Gone: gone), unplaced);

    /// <summary>A plan of <paramref name="placed"/> and the <paramref name="waiting"/> missions that didn't fit.</summary>
    private static OverlayViewModel Showing(Mission[] placed, params Mission[] waiting)
    {
        var overlay = new OverlayViewModel(HullA);
        var planned = LoadPlanner.Plan(HullA, placed, [.. placed.Select(m => m.Stops[0].Destination)]);
        overlay.ShowPlan(new SavedPlan([.. planned.Missions, .. waiting], planned.Plan, planned.CrateCap));
        return overlay;
    }

    /// <summary>Captures 36 to Riker and 37 to Shubin; 36 gone since.</summary>
    private static OverlayViewModel OneGone() =>
        Showing([Capture(1, 36, "Riker", gone: true), Capture(2, 37, "Shubin")]);

    private static Mission Of(OverlayViewModel overlay, long id) => overlay.Missions.Single(m => m.Contract!.ContractIds.Contains(id));

    // --- Legend and editor ---

    [Fact]
    public void The_legend_header_ends_in_gone()
    {
        var overlay = OneGone();

        overlay.Scene.Legend.Single(e => e.Destination == "Riker").HeaderLabel.Should().Be("From ? · #36 · gone");
        overlay.Scene.Legend.Single(e => e.Destination == "Shubin").HeaderLabel.Should().Be("From ? · #37");
    }

    [Fact]
    public void A_waiting_gone_contract_says_gone_in_its_line()
    {
        var overlay = Showing(
            [
                Capture(1, 1, "Riker", 16, CrateSize.Sixteen), Capture(2, 2, "Shubin", 16, CrateSize.Sixteen),
                Capture(3, 3, "Orison", 16, CrateSize.Sixteen), Capture(4, 4, "Lorville", 16, CrateSize.Sixteen),
            ],
            Capture(5, 5, "Hurston", 16, CrateSize.Sixteen, gone: true, unplaced: true));

        overlay.Scene.Unplaced.Single().Label.Should().StartWith("Hurston · 16 SCU").And.Contain(" · gone · ⚠ ");
    }

    [Fact]
    public void The_editor_shows_gone_in_the_mission_column_and_its_plan_keeps_the_mark()
    {
        var overlay = OneGone();
        var editor = new MissionEditorViewModel(overlay);

        editor.Rows.Where(r => r.ContractId is not null).Select(r => r.MissionLabel).Should().Equal("#36 · gone", "#37");

        editor.PlanCommand.Execute(null);

        Of(overlay, 36).Contract!.Gone.Should().BeTrue();
        Of(overlay, 37).Contract!.Gone.Should().BeFalse();
    }

    [Fact]
    public void A_row_changed_by_hand_in_a_gone_contract_shows_both_marks()
    {
        var editor = new MissionEditorViewModel(OneGone());
        var row = editor.Rows.Single(r => r.ContractId == 36);

        row.Scu = "8";

        row.MissionLabel.Should().Be("#36 ✎ · gone");
    }
}
