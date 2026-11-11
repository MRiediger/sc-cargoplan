using CargoPlan.App.Scanning;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Models;
using CargoPlan.Core.Tests;
using CargoPlan.Import.Contracts;

namespace CargoPlan.App.Tests;

/// <summary>
/// A scanned contract into the overlay's plan (T10.2b2, D103): one undo step, numbers never given twice in a session,
/// nothing for a text without a contract or one still typing in.
/// </summary>
public class ApplyScanTests
{
    private static readonly Ship HullA = FrozenShips.Load("hull_a");

    /// <summary>A contract in the reading order of CargoPlan's own OCR (D99), offers or accepted view.</summary>
    internal static ScannedContract Scanned(bool accepted, string reward = "50,250", params string[] objectives) => ScannedContract.Read(
        $"{(accepted ? "ABANDON\n" : "")}DETAILS\nThey will be packaged up in containers 4 SCU or smaller.\nReward {reward}\n"
        + $"{(accepted ? "Contract Deadline N/A" : "Contract Availability 2h 1m")}\nPRIMARY OBJECTIVES\n"
        + string.Join('\n', objectives.Length == 0 ? Riker(8) : objectives)
        + (accepted ? "\nSHARE UNTRACK" : "\nACCEPT OFFER"));

    private static string[] Riker(int scu) => [$"Deliver 0/{scu} SCU of Stims to Riker.", "Collect Stims from Everus Harbor."];

    [Fact]
    public void A_new_contract_is_one_undo_step_also_into_an_empty_plan()
    {
        var overlay = new OverlayViewModel(HullA);

        var report = overlay.ApplyScan(Scanned(accepted: false));

        report.Should().Be(new ScanReport(ScanReportKind.Added, 1));
        overlay.Missions.Should().ContainSingle().Which.Contract!.Scan!.No.Should().Be(1);
        overlay.LastScanNo.Should().Be(1);

        overlay.UndoCommand.Execute(null);
        overlay.Missions.Should().BeEmpty("the first scan into no plan can be undone too");

        overlay.RedoCommand.Execute(null);
        overlay.Missions.Should().ContainSingle();
    }

    [Fact]
    public void The_accepted_contract_with_other_amounts_updates_in_one_undo_step()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyScan(Scanned(accepted: false));

        var report = overlay.ApplyScan(Scanned(accepted: true, objectives: Riker(12)));

        report.Kind.Should().Be(ScanReportKind.Updated);
        report.ScanNo.Should().Be(1);
        report.QuantitiesChanged.Should().BeTrue();
        overlay.Missions.Single().Stops.Single().Items.Single().Scu.Should().Be(12);

        overlay.UndoCommand.Execute(null);
        overlay.Missions.Single().Stops.Single().Items.Single().Scu.Should().Be(8);
        overlay.Missions.Single().Contract!.Scan!.QuantitiesEstimated.Should().BeTrue();
    }

    [Fact]
    public void The_same_contract_again_is_no_step()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyScan(Scanned(accepted: false));
        var saved = overlay.Saved;

        overlay.ApplyScan(Scanned(accepted: false)).Kind.Should().Be(ScanReportKind.AlreadyInPlan);

        overlay.Saved.Should().BeSameAs(saved);
        overlay.UndoCommand.Execute(null);
        overlay.Missions.Should().BeEmpty("the one step is the first scan");
    }

    [Fact]
    public void A_number_is_never_given_twice_also_after_an_undo()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyScan(Scanned(accepted: false));
        overlay.UndoCommand.Execute(null);

        var report = overlay.ApplyScan(Scanned(accepted: false, reward: "72,500"));

        report.ScanNo.Should().Be(2, "#1 meant another contract in this session");
    }

    [Fact]
    public void The_next_number_goes_above_the_scans_in_the_plan()
    {
        // A discard undone brings its scans back while the counter starts again (D102).
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyScan(Scanned(accepted: false));
        overlay.ApplyScan(Scanned(accepted: false, reward: "72,500"));
        overlay.DiscardPlanCommand.Execute(null);
        overlay.UndoCommand.Execute(null);

        overlay.ApplyScan(Scanned(accepted: false, reward: "81,250")).ScanNo.Should().Be(3);
    }

    [Fact]
    public void A_text_without_a_contract_changes_nothing()
    {
        var overlay = new OverlayViewModel(HullA);

        overlay.ApplyScan(ScannedContract.Read("Covalex Independent Contractor\nDETAILS\nSome prose.")).Kind.Should().Be(ScanReportKind.NoContract);

        overlay.Saved.Should().BeNull();
        overlay.LastScanNo.Should().Be(0);
    }

    [Fact]
    public void A_contract_still_typing_in_changes_nothing()
    {
        var overlay = new OverlayViewModel(HullA);

        overlay.ApplyScan(Scanned(accepted: true, objectives: ["Collect Silicon from"])).Kind.Should().Be(ScanReportKind.Incomplete);

        overlay.Saved.Should().BeNull();
    }

    [Fact]
    public void A_contract_with_fewer_destinations_than_listed_comes_in_warned_until_the_stop_is_added()
    {
        // E1 (Martin): taken in with its listed count, the legend warns as for a capture; the editor adds the stop.
        var overlay = new OverlayViewModel(HullA);
        var listed = ScannedContract.Read(
            $"ABANDON\nDETAILS\nDROP OFF LOCATIONS (ANY ORDER)\n- Freight elevator at Riker Memorial Spaceport\n- Freight elevator at Orison\n"
            + "Reward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\n" + string.Join('\n', Riker(8)) + "\nSHARE UNTRACK");
        listed.ListedDestinationCount.Should().Be(2);

        overlay.ApplyScan(listed).Kind.Should().Be(ScanReportKind.Added);
        overlay.Scene.Legend.Single().Label.Should().EndWith("⚠ 1 of 2 destinations");

        var editor = new MissionEditorViewModel(overlay);
        editor.SelectedRow = editor.Rows.Single();
        editor.AddToMissionCommand.Execute(null);
        (editor.SelectedRow!.Destination, editor.SelectedRow.Commodity, editor.SelectedRow.Scu) = ("Orison", "Stims", "4");
        editor.PlanCommand.Execute(null);

        overlay.Scene.Legend.Should().HaveCount(2).And.OnlyContain(l => !l.Label.Contains("destinations"));
    }

    // --- What the sync's tests held for a contract coming in (T10.4a successors) ---

    private static string[] To(string destination, int scu = 4) =>
        [$"Deliver 0/{scu} SCU of Stims to {destination}.", "Collect Stims from Everus Harbor."];

    [Fact]
    public void A_scan_with_the_longer_name_renames_the_stop_and_says_so()
    {
        // B1, K1: one stop per place; the player sees the rename like a merge on loading.
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyScan(Scanned(accepted: true, objectives: To("Thundering Express")));

        overlay.ApplyScan(Scanned(accepted: true, reward: "60,000", objectives: To("Thundering Express Station")));

        overlay.Scene.Legend.Select(l => l.Destination).Distinct().Should().Equal("Thundering Express Station");
        overlay.Notices.Should().Contain(OverlayViewModel.StopsMergedPrefix + "Thundering Express → Thundering Express Station");
    }

    [Fact]
    public void A_scan_keeps_crates_and_ticks_of_the_plan()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ShowPlan(TestPlans.SplitAndShared());
        overlay.ToggleDeliveredCommand.Execute("Riker");
        var before = overlay.Plan!.Crates;

        overlay.ApplyScan(Scanned(accepted: true, objectives: To("Area18", 1)));

        overlay.Plan!.Crates.Take(before.Count).Should().Equal(before);
    }

    [Fact]
    public void A_scan_that_doesnt_fit_raises_PropertyChanged_for_Saved()
    {
        // PlanSaver listens to Saved: the waiting mission is saved although no crate changed.
        var overlay = new OverlayViewModel(HullA);
        List<string?> changed = [];
        overlay.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        overlay.ApplyScan(Scanned(accepted: true, objectives: To("Riker", 72)));

        changed.Should().Contain(nameof(OverlayViewModel.Saved));
        overlay.Scene.Unplaced.Should().ContainSingle();
    }
}
