using System.ComponentModel;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Ships;
using CargoPlan.Import.ContractTracker;

namespace CargoPlan.App.Tests;

/// <summary>
/// A contract gone from ContractTracker in the overlay (D81): marked "· gone" in legend and editor, said once by a notice,
/// kept or taken out by <see cref="OverlayViewModel.OnContractGone"/>; back by the same id; a new database clears the
/// undo history and its ids never match the old ones.
/// </summary>
public class GoneOverlayTests
{
    private static readonly Ship HullA = ShipLoader.Load("hull_a");

    private static ContractTrackerCandidate Candidate(long id, string destination, int scu = 4, CrateSize max = CrateSize.Four) =>
        new([id], null, [new MissionStop(destination, 1, [new MissionItem("Stims", scu, max)])], false, false, null, null, null);

    private static ContractTrackerCandidate Candidate(long id, params string[] destinations) =>
        new([id], null, [.. destinations.Select((d, i) => new MissionStop(d, i + 1, [new MissionItem("Stims", 4, CrateSize.Four)]))], false, false, null, null, null);

    private static ContractTrackerReadResult Read(long[] present, params ContractTrackerCandidate[] candidates) =>
        new(candidates, null, new HashSet<long>(present));

    private static Mission Of(OverlayViewModel overlay, long id) => overlay.Missions.Single(m => m.Contract!.ContractIds.Contains(id));

    /// <summary>Captures 36 to Riker and 37 to Shubin; 36 gone since.</summary>
    private static OverlayViewModel OneGone(ContractGonePolicy onGone = ContractGonePolicy.Keep)
    {
        var overlay = new OverlayViewModel(HullA) { OnContractGone = onGone };
        overlay.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));
        overlay.ApplyContractTracker(Read([37]));
        return overlay;
    }

    private static List<string?> Changes(INotifyPropertyChanged source, Action act)
    {
        List<string?> changed = [];
        source.PropertyChanged += Record;
        act();
        source.PropertyChanged -= Record;
        return changed;

        void Record(object? sender, PropertyChangedEventArgs e) => changed.Add(e.PropertyName);
    }

    // --- Notices ---

    [Fact]
    public void A_gone_contract_stays_marked_and_the_notice_says_so_once()
    {
        var overlay = OneGone();

        overlay.Missions.Should().HaveCount(2);
        Of(overlay, 36).Contract!.Gone.Should().BeTrue();
        overlay.Notices.Should().Equal("Contract #36 gone from ContractTracker – kept in plan");
    }

    [Fact]
    public void With_remove_planned_cargo_leaves_loaded_stays_and_the_notice_counts_both()
    {
        var overlay = new OverlayViewModel(HullA) { OnContractGone = ContractGonePolicy.Remove };
        overlay.ApplyContractTracker(Read([], Candidate(36, "Riker", "Shubin"), Candidate(40, "Lorville")));
        overlay.ToggleLineCommand.Execute(overlay.Scene.Legend.Single(e => e.Destination == "Riker"));

        overlay.ApplyContractTracker(Read([40]));

        Of(overlay, 36).Stops.Select(s => s.Destination).Should().Equal("Riker");
        Of(overlay, 36).Contract!.Gone.Should().BeTrue();
        overlay.Notices.Should().Contain("Contract #36 gone from ContractTracker – removed 1 planned, kept 1 loaded");
    }

    [Fact]
    public void With_remove_a_waiting_contract_leaves_and_the_notice_says_it_waited()
    {
        var overlay = new OverlayViewModel(HullA) { OnContractGone = ContractGonePolicy.Remove };
        overlay.ApplyContractTracker(Read([],
            Candidate(1, "Riker", 16, CrateSize.Sixteen), Candidate(2, "Shubin", 16, CrateSize.Sixteen), Candidate(3, "Orison", 16, CrateSize.Sixteen),
            Candidate(4, "Lorville", 16, CrateSize.Sixteen), Candidate(5, "Hurston", 16, CrateSize.Sixteen)));

        overlay.ApplyContractTracker(Read([1, 2, 3, 4]));

        overlay.Missions.Should().HaveCount(4);
        overlay.Notices.Should().Contain("Contract #5 gone from ContractTracker – removed, it was waiting");
    }

    [Fact]
    public void A_contract_back_in_ContractTracker_loses_its_mark_and_the_notice_says_so()
    {
        var overlay = OneGone();

        overlay.ApplyContractTracker(Read([36, 37]));

        overlay.Missions.Should().OnlyContain(m => !m.Contract!.Gone);
        overlay.Notices.Should().Contain("Contract #36 back in ContractTracker");
    }

    [Fact]
    public void All_contracts_gone_at_once_are_kept_even_with_remove_with_one_notice()
    {
        var overlay = new OverlayViewModel(HullA) { OnContractGone = ContractGonePolicy.Remove };
        overlay.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));

        overlay.ApplyContractTracker(Read([]));

        overlay.Missions.Should().HaveCount(2).And.OnlyContain(m => m.Contract!.Gone);
        overlay.Notices.Should().Contain(OverlayViewModel.AllGoneAtOnceText);
    }

    [Fact]
    public void A_watcher_read_without_news_while_a_contract_is_gone_changes_nothing()
    {
        // No new scene (a drag goes on), no save, no notice taken away (D38).
        var overlay = OneGone();
        var notices = overlay.Notices.ToList();

        Changes(overlay, () => overlay.ApplyContractTracker(Read([37]))).Should().BeEmpty();
        overlay.Notices.Should().Equal(notices);
    }

    [Fact]
    public void The_policy_set_later_applies_at_the_next_read()
    {
        // Settings apply on change (D30); delivered cargo of a gone contract leaves once the policy says remove.
        var overlay = OneGone();
        overlay.ToggleDeliveredCommand.Execute("Riker");
        overlay.OnContractGone = ContractGonePolicy.Remove;

        overlay.ApplyContractTracker(Read([37]), manual: true);

        overlay.Missions.Select(m => m.Contract!.ContractIds[0]).Should().Equal(37);
        overlay.Notices.Should().Contain("Contract #36 gone from ContractTracker – removed 1 delivered");
    }

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
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyContractTracker(Read([],
            Candidate(1, "Riker", 16, CrateSize.Sixteen), Candidate(2, "Shubin", 16, CrateSize.Sixteen), Candidate(3, "Orison", 16, CrateSize.Sixteen),
            Candidate(4, "Lorville", 16, CrateSize.Sixteen), Candidate(5, "Hurston", 16, CrateSize.Sixteen)));

        overlay.ApplyContractTracker(Read([1, 2, 3, 4]));

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

    // --- Undo and a new database ---

    [Fact]
    public void Undo_after_a_sync_that_marked_a_contract_keeps_the_mark()
    {
        // K2.4: a sync is no step; the snapshot from before it is restored, then synced again (D81, point 9).
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyContractTracker(Read([], Candidate(36, "Riker"), Candidate(37, "Shubin")));
        overlay.ToggleLineCommand.Execute(overlay.Scene.Legend.Single(e => e.Destination == "Shubin"));
        overlay.ApplyContractTracker(Read([37]));

        overlay.UndoCommand.Execute(null);

        Of(overlay, 36).Contract!.Gone.Should().BeTrue();
    }

    [Fact]
    public void A_new_database_clears_the_undo_history()
    {
        // The snapshots hold the old database's ids undetached; a resync would match them with the new ones (D81).
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyContractTracker(Read([], Candidate(36, "Riker")));
        overlay.ToggleLineCommand.Execute(overlay.Scene.Legend.Single());
        overlay.UndoCommand.CanExecute(null).Should().BeTrue();

        overlay.ApplyContractTracker(new ContractTrackerReadResult([Candidate(1, "Orison")], null, new HashSet<long>(), DatabaseLooksNew: true));

        overlay.UndoCommand.CanExecute(null).Should().BeFalse();
        overlay.Notices.Should().Contain(OverlayViewModel.HistoryClearedByDatabaseText);
    }

    [Fact]
    public void A_new_database_s_contract_1_is_never_the_old_1()
    {
        var overlay = new OverlayViewModel(HullA);
        overlay.ApplyContractTracker(Read([], Candidate(1, "Riker")));
        overlay.ApplyContractTracker(new ContractTrackerReadResult([Candidate(1, "Orison")], null, new HashSet<long>(), DatabaseLooksNew: true));

        overlay.ContractTrackerQuery().KnownIds.Should().Equal(1);
        overlay.ApplyContractTracker(Read([1]), manual: true);

        overlay.Missions.Select(m => (m.Stops[0].Destination, m.Contract!.Gone, m.Contract.Detached))
            .Should().Equal(("Riker", true, true), ("Orison", false, false));
    }
}
