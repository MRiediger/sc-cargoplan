using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// A plan saved before T10.4a may hold contracts marked gone or detached (D81). Nothing marks or reads them since
/// (D105): legend and editor show such a contract like any other, with its ids. Core drops the fields in T10.4b.
/// </summary>
public class GoneOverlayTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static Mission Capture(
        int id, long contractId, string destination, int scu = 4, CrateSize max = CrateSize.Four, bool gone = false, bool detached = false, bool unplaced = false) =>
        new(id, [new MissionStop(destination, 1, [new MissionItem("Stims", scu, max)])], new ContractSource([contractId], Gone: gone, Detached: detached), unplaced);

    /// <summary>A plan of <paramref name="placed"/> and the <paramref name="waiting"/> missions that didn't fit.</summary>
    private static OverlayViewModel Showing(Mission[] placed, params Mission[] waiting)
    {
        var overlay = new OverlayViewModel(HullA);
        var planned = LoadPlanner.Plan(HullA, placed, [.. placed.Select(m => m.Stops[0].Destination)]);
        overlay.ShowPlan(new SavedPlan([.. planned.Missions, .. waiting], planned.Plan, planned.CrateCap));
        return overlay;
    }

    /// <summary>Captures 36 to Riker, marked gone, and 37 to Shubin.</summary>
    private static OverlayViewModel OneGone() =>
        Showing([Capture(1, 36, "Riker", gone: true), Capture(2, 37, "Shubin")]);

    [Fact]
    public void The_legend_header_of_a_contract_marked_gone_is_a_plain_one()
    {
        var overlay = OneGone();

        var riker = overlay.Scene.Legend.Single(e => e.Destination == "Riker");
        riker.HeaderLabel.Should().Be("From ? · #36");
        riker.HeaderSegments.Select(s => s.Text).Should().NotContain(t => t.Contains("gone"));
        overlay.Scene.Legend.Single(e => e.Destination == "Shubin").HeaderLabel.Should().Be("From ? · #37");
    }

    [Fact]
    public void A_waiting_contract_marked_gone_has_a_plain_line()
    {
        var overlay = Showing(
            [
                Capture(1, 1, "Riker", 16, CrateSize.Sixteen), Capture(2, 2, "Shubin", 16, CrateSize.Sixteen),
                Capture(3, 3, "Orison", 16, CrateSize.Sixteen), Capture(4, 4, "Lorville", 16, CrateSize.Sixteen),
            ],
            Capture(5, 5, "Hurston", 16, CrateSize.Sixteen, gone: true, unplaced: true));

        overlay.Scene.Unplaced.Single().Label.Should().StartWith("Hurston · 16 SCU").And.Contain(" · Stims · ⚠ ").And.NotContain("gone");
    }

    [Fact]
    public void The_editor_shows_the_ids_of_a_contract_marked_gone_and_its_plan_leaves_the_mark_to_Core()
    {
        var overlay = OneGone();
        var editor = new MissionEditorViewModel(overlay);

        editor.Rows.Where(r => r.ContractId is not null).Select(r => r.MissionLabel).Should().Equal("#36", "#37");

        editor.PlanCommand.Execute(null);

        // Not shown, not dropped: Core carries the field until T10.4b.
        overlay.Missions.Single(m => m.Contract!.ContractIds.Contains(36)).Contract!.Gone.Should().BeTrue();
    }

    [Fact]
    public void A_row_changed_by_hand_in_a_contract_marked_gone_shows_the_hand_mark_only()
    {
        var editor = new MissionEditorViewModel(OneGone());
        var row = editor.Rows.Single(r => r.ContractId == 36);

        row.Scu = "8";

        row.MissionLabel.Should().Be("#36 ✎");
    }

    [Fact]
    public void A_detached_contract_shows_its_ids_as_before()
    {
        // D81: a new database detached the old captures; their ids stay what the player saw.
        var overlay = Showing([Capture(1, 12, "Riker", gone: true, detached: true), Capture(2, 13, "Shubin")]);
        var editor = new MissionEditorViewModel(overlay);

        overlay.Scene.Legend.Single(e => e.Destination == "Riker").HeaderLabel.Should().Be("From ? · #12");
        editor.Rows.Where(r => r.ContractId is not null).Select(r => r.MissionLabel).Should().Equal("#12", "#13");
    }

    [Fact]
    public void A_waiting_mission_that_fits_now_says_Replan_places_it()
    {
        // Without the sync (T10.4a) the header's or the editor's Re-plan, the editor's Plan and undo place it.
        ShipViewBuilder.FitsNowText.Should().Be("fits now – Re-plan places it");
    }
}
