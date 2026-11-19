using CargoPlan.App.Scanning;
using CargoPlan.App.ShipView;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The legend in scan order (B16): a new contract goes to the bottom, and nothing but a new scan changes the order –
/// not Re-plan, a tick, undo, redo or the editor's Plan.
/// </summary>
public class LegendScanOrderTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    private static string[] To(string destination) => [$"Deliver 0/4 SCU of Stims to {destination}.", "Collect Stims from Everus Harbor."];

    private static void Scan(OverlayViewModel overlay, string reward, string destination) =>
        overlay.ApplyScan(ApplyScanTests.Scanned(accepted: true, reward, To(destination))).Kind.Should().Be(ScanReportKind.Added);

    /// <summary>The case of the report: #3 goes where #1 goes, so before B16 it stood above #2.</summary>
    private static OverlayViewModel ThreeScans()
    {
        var overlay = new OverlayViewModel(HullA);
        Scan(overlay, "10,000", "Riker");
        Scan(overlay, "20,000", "Orison");
        Scan(overlay, "30,000", "Riker");
        return overlay;
    }

    /// <summary>The legend's missions top to bottom, by scan number.</summary>
    private static int[] Legend(OverlayViewModel overlay) =>
        [.. overlay.Scene.Legend.Select(l => l.Mission).Distinct().Select(id => overlay.Saved!.MissionOf(id).Contract!.Scan.No)];

    [Fact]
    public void Three_scans_read_top_to_bottom_in_scan_order()
    {
        Legend(ThreeScans()).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Re_plan_tick_undo_redo_and_the_editors_plan_keep_the_order()
    {
        var overlay = ThreeScans();

        overlay.ReplanCommand.Execute(null);
        Legend(overlay).Should().Equal(1, 2, 3);

        overlay.ToggleDeliveredCommand.Execute("Orison");
        Legend(overlay).Should().Equal(1, 2, 3);

        overlay.UndoCommand.Execute(null);
        Legend(overlay).Should().Equal(1, 2, 3);

        overlay.RedoCommand.Execute(null);
        Legend(overlay).Should().Equal(1, 2, 3);

        new MissionEditorViewModel(overlay).PlanCommand.Execute(null);
        Legend(overlay).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Waiting_missions_stand_in_scan_order_too()
    {
        // The list in another order than the scans, as the editor's Plan leaves it (MissionRows.ToMissions).
        static Mission Waiting(int id, int scanNo) => new(
            id, [new MissionStop("Riker", 1, [new MissionItem("Stims", 4, CrateSize.Four)])], new ContractSource(new ScanIdentity(scanNo, $"KEY {scanNo}", false)), Unplaced: true);
        SavedPlan saved = new([Waiting(1, 2), Waiting(2, 1)], new LoadPlan(HullA.Id, [], []), CrateCap.Of(HullA));

        ShipViewBuilder.Build(saved, HullA, []).Unplaced.Select(u => u.MissionId).Should().Equal(2, 1);
    }
}
