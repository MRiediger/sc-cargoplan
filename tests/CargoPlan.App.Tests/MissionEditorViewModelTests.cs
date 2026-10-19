using CargoPlan.App.Editor;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Rendering;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public sealed class MissionEditorViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SC-CargoPlan-Tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private static OverlayViewModel EmptyOverlay() => new(ShipLoader.Load("hull_a"));

    private static OverlayViewModel OverlayWithPlan()
    {
        var overlay = EmptyOverlay();
        overlay.ShowPlan(TestPlans.SplitAndShared());
        return overlay;
    }

    private static void Fill(MissionRow row, string destination, string commodity, string scu, int crateSize)
    {
        row.Destination = destination;
        row.Commodity = commodity;
        row.Scu = scu;
        row.CrateSize = crateSize;
    }

    /// <summary>Editor with the four example rows: Riker, Shubin, Orison, then a second commodity for Shubin.</summary>
    private static MissionEditorViewModel FourRows(OverlayViewModel overlay)
    {
        var editor = new MissionEditorViewModel(overlay);
        editor.ClearRowsCommand.Execute(null);
        Fill(editor.Rows[0], "Riker", "Titanium", "8", 4);
        editor.AddRowCommand.Execute(null);
        Fill(editor.Rows[1], "Shubin", "Quartz", "4", 2);
        editor.AddRowCommand.Execute(null);
        Fill(editor.Rows[2], "Orison", "Agricium", "8", 8);
        editor.AddRowCommand.Execute(null);
        Fill(editor.Rows[3], "Shubin", "Gold", "2", 1);
        return editor;
    }

    // --- Start ---

    [Fact]
    public void Without_a_plan_the_editor_starts_with_one_blank_row()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());

        editor.Rows.Should().ContainSingle().Which.IsBlank.Should().BeTrue();
        editor.Problem.Should().Be(MissionRows.NoMissionsText);
        editor.PlanCommand.CanExecute(null).Should().BeFalse();
        editor.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void With_a_plan_the_editor_starts_with_its_missions()
    {
        var overlay = OverlayWithPlan();

        var editor = new MissionEditorViewModel(overlay);

        MissionRows.ToStops(editor.Rows).Stops.Should().BeEquivalentTo(overlay.Missions, o => o.WithStrictOrdering());
        editor.IsDirty.Should().BeFalse();
        editor.Title.Should().Be(MissionEditorViewModel.TitleText);
    }

    // --- Rows ---

    [Fact]
    public void Stop_numbers_follow_the_first_appearance_of_each_destination()
    {
        var editor = FourRows(EmptyOverlay());

        editor.Rows.Select(r => r.StopNumber).Should().Equal(1, 2, 3, 2);
        editor.Problem.Should().BeNull();
        editor.PlanCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void A_new_row_takes_the_crate_size_of_the_row_above()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());
        Fill(editor.Rows[0], "Riker", "Titanium", "8", 4);

        editor.AddRowCommand.Execute(null);

        editor.Rows[1].CrateSize.Should().Be(4);
        editor.SelectedRow.Should().BeSameAs(editor.Rows[1]);
    }

    [Fact]
    public void A_row_added_by_the_data_grid_takes_the_crate_size_of_the_row_above_too()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());
        Fill(editor.Rows[0], "Riker", "Titanium", "8", 8);

        editor.Rows.Add(new MissionRow());

        editor.Rows[1].CrateSize.Should().Be(8);
    }

    [Fact]
    public void Editing_a_row_marks_unplanned_changes_in_the_title()
    {
        var editor = new MissionEditorViewModel(OverlayWithPlan());

        editor.Rows[0].Scu = "4";

        editor.IsDirty.Should().BeTrue();
        editor.Title.Should().Be(MissionEditorViewModel.UnplannedTitleText);
    }

    [Fact]
    public void An_invalid_row_is_flagged_and_blocks_planning()
    {
        var editor = FourRows(EmptyOverlay());

        editor.Rows[1].Scu = "17";
        editor.Rows[1].CrateSize = 4;

        editor.Rows[1].Error.Should().Be("17 SCU can't be packed in 4 SCU crates.");
        editor.Problem.Should().Be("Row 2: 17 SCU can't be packed in 4 SCU crates.");
        editor.PlanCommand.CanExecute(null).Should().BeFalse();
        editor.CanExport.Should().BeFalse();
    }

    [Fact]
    public void MoveUp_on_the_first_row_changes_nothing()
    {
        var editor = FourRows(EmptyOverlay());
        MissionRow[] before = [.. editor.Rows];
        editor.SelectedRow = editor.Rows[0];

        editor.MoveUpCommand.CanExecute(null).Should().BeFalse();
        editor.MoveUpCommand.Execute(null);

        editor.Rows.Should().Equal(before);
    }

    [Fact]
    public void MoveDown_on_the_last_row_changes_nothing()
    {
        var editor = FourRows(EmptyOverlay());
        MissionRow[] before = [.. editor.Rows];
        editor.SelectedRow = editor.Rows[^1];

        editor.MoveDownCommand.CanExecute(null).Should().BeFalse();
        editor.MoveDownCommand.Execute(null);

        editor.Rows.Should().Equal(before);
    }

    [Fact]
    public void MoveUp_changes_the_route_and_keeps_the_selection()
    {
        var editor = FourRows(EmptyOverlay());
        var orison = editor.Rows[2];
        editor.SelectedRow = orison;

        editor.MoveUpCommand.Execute(null);

        editor.Rows[1].Should().BeSameAs(orison);
        editor.Rows.Select(r => r.StopNumber).Should().Equal(1, 2, 3, 3);
        editor.SelectedRow.Should().BeSameAs(orison);
    }

    [Fact]
    public void RemoveRow_removes_the_selected_row_and_selects_its_neighbour()
    {
        var editor = FourRows(EmptyOverlay());
        var next = editor.Rows[3];
        editor.SelectedRow = editor.Rows[2];

        editor.RemoveRowCommand.Execute(null);

        editor.Rows.Should().HaveCount(3);
        editor.SelectedRow.Should().BeSameAs(next);
    }

    [Fact]
    public void Commands_on_the_selection_need_a_selection()
    {
        var editor = FourRows(EmptyOverlay());
        editor.SelectedRow = null;

        editor.RemoveRowCommand.CanExecute(null).Should().BeFalse();
        editor.MoveUpCommand.CanExecute(null).Should().BeFalse();
        editor.MoveDownCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void ClearRows_leaves_one_blank_row_and_keeps_the_plan()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.ClearRowsCommand.Execute(null);

        editor.Rows.Should().ContainSingle().Which.IsBlank.Should().BeTrue();
        editor.IsDirty.Should().BeTrue();
        overlay.Plan.Should().BeSameAs(plan);
    }

    // --- Plan ---

    [Fact]
    public void Plan_shows_the_new_plan_in_the_overlay()
    {
        var overlay = EmptyOverlay();
        var editor = FourRows(overlay);

        editor.PlanCommand.Execute(null);

        overlay.Missions.Select(s => s.Destination).Should().Equal("Riker", "Shubin", "Orison");
        overlay.Plan!.Placements.Sum(p => p.Item.Scu).Should().Be(22);
        editor.IsDirty.Should().BeFalse();
        editor.Problem.Should().BeNull();
    }

    [Fact]
    public void Plan_that_doesnt_fit_the_ship_shows_the_reason_and_keeps_the_old_plan()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);
        editor.ClearRowsCommand.Execute(null);
        string[] destinations = ["A", "B", "C", "D", "E"];
        for (var i = 0; i < destinations.Length; i++)
        {
            if (i > 0)
            {
                editor.AddRowCommand.Execute(null);
            }

            Fill(editor.Rows[i], destinations[i], "Titanium", "16", 16);
        }

        editor.PlanCommand.Execute(null);

        editor.Problem.Should().StartWith("Ship capacity exceeded");
        editor.IsDirty.Should().BeTrue();
        overlay.Plan.Should().BeSameAs(plan);
    }

    [Fact]
    public void Plan_raises_Planned_so_the_window_can_close()
    {
        var editor = FourRows(EmptyOverlay());
        var planned = 0;
        editor.Planned += (_, _) => planned++;

        editor.PlanCommand.Execute(null);

        planned.Should().Be(1);
    }

    [Fact]
    public void Plan_that_fails_raises_no_Planned_so_the_editor_stays_open_for_the_fix()
    {
        var editor = FourRows(EmptyOverlay());
        editor.Rows[0].Scu = "64";
        var planned = 0;
        editor.Planned += (_, _) => planned++;

        editor.PlanCommand.Execute(null);

        planned.Should().Be(0);
        editor.Problem.Should().NotBeNull();
    }

    [Fact]
    public void A_plan_problem_goes_away_with_the_next_edit()
    {
        var editor = FourRows(EmptyOverlay());
        editor.Rows[0].Scu = "64";
        editor.PlanCommand.Execute(null);
        editor.Problem.Should().NotBeNull();

        editor.Rows[0].Scu = "8";

        editor.Problem.Should().BeNull();
    }

    [Fact]
    public void Delivered_stops_show_the_replan_warning_and_planning_clears_them()
    {
        var overlay = OverlayWithPlan();
        var editor = new MissionEditorViewModel(overlay);
        editor.ClearsDelivered.Should().BeFalse();

        overlay.ToggleDeliveredCommand.Execute("Riker");

        editor.ClearsDelivered.Should().BeTrue();
        editor.PlanCommand.Execute(null);
        overlay.Plan!.Placements.Should().OnlyContain(p => !p.Delivered);
        editor.ClearsDelivered.Should().BeFalse();
    }

    [Fact]
    public void Detach_stops_listening_to_the_overlay()
    {
        var overlay = OverlayWithPlan();
        var editor = new MissionEditorViewModel(overlay);
        List<string?> changes = [];
        editor.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        editor.Detach();
        overlay.ToggleDeliveredCommand.Execute("Riker");

        changes.Should().BeEmpty();
    }

    // --- Import, export, text ---

    [Fact]
    public void Export_then_Import_gives_the_same_rows()
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(EmptyOverlay());
        var before = MissionRows.ToStops(editor.Rows).Stops;

        editor.Export(path);
        editor.ClearRowsCommand.Execute(null);
        editor.Import(path);

        MissionRows.ToStops(editor.Rows).Stops.Should().BeEquivalentTo(before, o => o.WithStrictOrdering());
        editor.Problem.Should().BeNull();
    }

    [Fact]
    public void Import_of_a_broken_file_keeps_the_rows_and_shows_the_reason()
    {
        var path = Path.Combine(_folder, "broken.json");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(path, "{ not json");
        var editor = FourRows(EmptyOverlay());

        editor.Import(path);

        editor.Rows.Should().HaveCount(4);
        editor.Problem.Should().Contain(path);
    }

    [Fact]
    public void Import_of_a_missing_file_shows_the_reason()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());

        editor.Import(Path.Combine(_folder, "missing.json"));

        editor.Problem.Should().StartWith("Import failed");
    }

    [Fact]
    public void PlanAsText_is_the_text_renderer_output_of_the_current_plan()
    {
        var overlay = OverlayWithPlan();
        var editor = new MissionEditorViewModel(overlay);

        editor.HasPlan.Should().BeTrue();
        editor.PlanAsText().Should().Be(PlanTextRenderer.Render(overlay.Plan!, overlay.Ship));
    }

    [Fact]
    public void Without_a_plan_there_is_no_text()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());

        editor.HasPlan.Should().BeFalse();
        editor.PlanAsText().Should().BeNull();
    }

    [Fact]
    public void ShowProblem_puts_a_view_problem_into_the_status_line()
    {
        var editor = FourRows(EmptyOverlay());

        editor.ShowProblem("Clipboard is in use.");

        editor.Problem.Should().Be("Clipboard is in use.");
    }
}
