using CargoPlan.App.Editor;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Planning;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Rendering;
using CargoPlan.Core.Ships;
using CargoPlan.Core.Tests;

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

    private static OverlayViewModel EmptyOverlay() => new(FrozenShips.Load("hull_a"));

    private static OverlayViewModel OverlayWithPlan()
    {
        var overlay = EmptyOverlay();
        overlay.ShowPlan(TestPlans.SplitAndShared());
        return overlay;
    }

    // --- The ship, changed in the overlay header (T7.2b) ---

    private static readonly ShipCatalog Catalog = FrozenShips.LoadAll();

    private static OverlayViewModel OverlayOfCatalog()
    {
        var overlay = new OverlayViewModel(Catalog.Get("hull_a"), Catalog);
        overlay.ShowPlan(TestPlans.SplitAndShared());
        return overlay;
    }

    [Fact]
    public void ShipName_is_the_overlays_and_follows_a_ship_change()
    {
        var overlay = OverlayOfCatalog();
        var editor = new MissionEditorViewModel(overlay);
        List<string?> changed = [];
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        overlay.ChangeShip(Catalog.Get("c2_hercules"));

        editor.ShipName.Should().Be("CRUS C2 Hercules");
        changed.Should().Contain(nameof(MissionEditorViewModel.ShipName));
    }

    [Fact]
    public void A_ship_change_keeps_a_dirty_draft_and_says_nothing_of_a_change_outside()
    {
        var overlay = OverlayOfCatalog();
        var editor = new MissionEditorViewModel(overlay);
        editor.AddRowCommand.Execute(null);
        Fill(editor.Rows[^1], "Area18", "Stims", "4", 4);
        var rows = editor.Rows.Count;

        overlay.ChangeShip(Catalog.Get("c2_hercules"));

        editor.IsDirty.Should().BeTrue();
        editor.Rows.Should().HaveCount(rows);
        editor.Status.Should().BeNull();
    }

    [Fact]
    public void A_ship_change_keeps_a_clean_draft_clean()
    {
        var overlay = OverlayOfCatalog();
        var editor = new MissionEditorViewModel(overlay);
        var rows = editor.Rows.Select(r => (r.Destination, r.Commodity, r.Scu)).ToList();

        overlay.ChangeShip(Catalog.Get("c2_hercules"));
        overlay.ToggleDeliveredCommand.Execute("Riker");

        editor.IsDirty.Should().BeFalse();
        editor.Rows.Select(r => (r.Destination, r.Commodity, r.Scu)).Should().Equal(rows);
    }

    private static void Fill(MissionRow row, string destination, string commodity, string scu, int maxCrateSize)
    {
        row.Destination = destination;
        row.Commodity = commodity;
        row.Scu = scu;
        row.MaxCrateSize = maxCrateSize;
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

        MissionRows.ToStops(editor.Rows, CrateCap.None).Stops.Should().BeEquivalentTo(overlay.Saved!.Stops, o => o.WithStrictOrdering());
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
    public void A_new_row_takes_the_max_crate_size_of_the_row_above_but_not_its_crates()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());
        Fill(editor.Rows[0], "Riker", "Titanium", "8", 8);
        editor.Rows[0].Crates = "4+4";

        editor.AddRowCommand.Execute(null);

        editor.Rows[1].MaxCrateSize.Should().Be(8);
        editor.Rows[1].Crates.Should().BeEmpty();
        editor.SelectedRow.Should().BeSameAs(editor.Rows[1]);
    }

    [Fact]
    public void A_new_row_takes_the_pickup_of_the_row_above()
    {
        // Contracts of one board mostly start at one pickup (D68), like the max crate size (D23).
        var editor = new MissionEditorViewModel(EmptyOverlay());
        Fill(editor.Rows[0], "Riker", "Titanium", "8", 8);
        editor.Rows[0].Pickup = "Lorville";

        editor.AddRowCommand.Execute(null);

        editor.Rows[1].Pickup.Should().Be("Lorville");
        editor.Rows[1].IsBlank.Should().BeTrue();
    }

    [Fact]
    public void Editing_a_pickup_marks_unplanned_changes()
    {
        var editor = new MissionEditorViewModel(OverlayWithPlan());

        editor.Rows[0].Pickup = "Lorville";

        editor.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void A_row_added_by_the_data_grid_takes_the_max_crate_size_of_the_row_above_too()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());
        Fill(editor.Rows[0], "Riker", "Titanium", "8", 8);

        editor.Rows.Add(new MissionRow());

        editor.Rows[1].MaxCrateSize.Should().Be(8);
    }

    [Fact]
    public void Each_row_shows_its_crates_derived_or_by_hand()
    {
        var editor = new MissionEditorViewModel(EmptyOverlay());
        Fill(editor.Rows[0], "Green Glade", "Stims", "6", 4);

        editor.Rows[0].CratesText.Should().Be("4+2");

        editor.Rows[0].Crates = "2+2+2";

        editor.Rows[0].CratesText.Should().Be("3×2");
        editor.Rows[0].IsCrateOverride.Should().BeTrue();
        editor.Problem.Should().BeNull();
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

        editor.Rows[1].Crates = "2+2+3";

        editor.Rows[1].Error.Should().Be("3 SCU is not a crate size.");
        editor.Problem.Should().Be("Row 2: 3 SCU is not a crate size.");
        editor.PlanCommand.CanExecute(null).Should().BeFalse();
        editor.CanExport.Should().BeFalse();
    }

    [Fact]
    public void A_blocked_Plan_says_why_in_its_tool_tip()
    {
        var editor = FourRows(EmptyOverlay());
        editor.PlanToolTip.Should().Be(MissionEditorViewModel.PlanText);
        using var monitor = editor.Monitor();

        editor.Rows[3].Crates = "2";

        editor.PlanToolTip.Should().Be("Row 4: 2 SCU crates are larger than the max crate size of 1 SCU.");
        monitor.Should().RaisePropertyChangeFor(e => e.PlanToolTip);
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

        overlay.Saved!.Stops.Select(s => s.Destination).Should().Equal("Riker", "Shubin", "Orison");
        overlay.Plan!.Placements.Sum(p => p.Item.Scu).Should().Be(22);
        editor.IsDirty.Should().BeFalse();
        editor.Problem.Should().BeNull();
    }

    [Fact]
    public void Plan_expands_derived_and_overridden_crates_into_cargo_items()
    {
        // T3.12 verification: 6 Stims at max 4 is one 4 and one 2; overridden as 2+2+2 it is three 2s (D23).
        var overlay = EmptyOverlay();
        var editor = new MissionEditorViewModel(overlay);
        Fill(editor.Rows[0], "Green Glade", "Stims", "6", 4);
        editor.AddRowCommand.Execute(null);
        Fill(editor.Rows[1], "Riker", "Stims", "6", 4);
        editor.Rows[1].Crates = "2+2+2";

        editor.PlanCommand.Execute(null);

        overlay.Plan!.PlacementsForStop("Green Glade").Select(p => (p.Item.Scu, (int)p.Item.CrateSize)).Should().Equal((4, 4), (2, 2));
        overlay.Plan!.PlacementsForStop("Riker").Select(p => (p.Item.Scu, (int)p.Item.CrateSize)).Should().Equal((6, 2));
        overlay.Missions[0].Stops[0].Items[0].Crates.Should().BeNull();
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
    public void Delivered_stops_stay_delivered_through_Plan()
    {
        // K6.4 (D19 addendum): Plan keeps what is loaded and what is delivered; the old warning is gone.
        var overlay = OverlayWithPlan();
        var editor = new MissionEditorViewModel(overlay);
        overlay.ToggleDeliveredCommand.Execute("Riker");

        editor.PlanCommand.Execute(null);

        overlay.Plan!.Crates.Where(c => c.Destination == "Riker").Should().NotBeEmpty().And.OnlyContain(c => c.Delivered);
        overlay.Plan.Crates.Where(c => c.Destination != "Riker").Should().OnlyContain(c => !c.Delivered);
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

    // --- Fill order (T4.5) ---

    private static OverlayViewModel OverlayWithSternFirstPlan()
    {
        var overlay = EmptyOverlay();
        var planned = TestPlans.SplitAndShared();
        overlay.ShowPlan(SavedPlan.FromStops(planned.Stops, LoadPlanner.Plan(overlay.Ship, planned.Stops.ToStops(), FillOrder.SternFirst), CrateCap.Of(overlay.Ship)));
        return overlay;
    }

    [Fact]
    public void Without_a_plan_the_fill_order_is_bow_first()
    {
        new MissionEditorViewModel(EmptyOverlay()).FillOrder.Should().Be(FillOrder.BowFirst);
    }

    [Fact]
    public void The_editor_starts_with_the_fill_order_of_the_current_plan()
    {
        new MissionEditorViewModel(OverlayWithSternFirstPlan()).FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Fact]
    public void Plan_uses_the_chosen_fill_order()
    {
        var overlay = EmptyOverlay();
        var editor = FourRows(overlay);
        var stops = MissionRows.ToStops(editor.Rows, CrateCap.None).Stops.ToStops();

        editor.FillOrder = FillOrder.SternFirst;
        editor.PlanCommand.Execute(null);

        overlay.Plan!.FillOrder.Should().Be(FillOrder.SternFirst);
        // The missions are the editor's rows (D42); the layout is the planner's.
        overlay.Plan.Crates.Select(c => c with { Mission = 0, Item = 0 }).Should().Equal(LoadPlanner.Plan(overlay.Ship, stops, FillOrder.SternFirst).Crates);
        overlay.Plan.Crates.Select(c => c.Position).Should().NotEqual(
            LoadPlanner.Plan(overlay.Ship, stops, FillOrder.BowFirst).Crates.Select(c => c.Position), "the chosen end must make a difference");
    }

    [Fact]
    public void Changing_the_fill_order_marks_unplanned_changes()
    {
        var editor = new MissionEditorViewModel(OverlayWithPlan());

        editor.FillOrder = FillOrder.SternFirst;

        editor.IsDirty.Should().BeTrue();
        editor.Title.Should().Be(MissionEditorViewModel.UnplannedTitleText);
    }

    [Fact]
    public void Changing_the_fill_order_leaves_the_overlay_plan_until_Plan()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.FillOrder = FillOrder.SternFirst;

        overlay.Plan.Should().BeSameAs(plan);
    }

    [Fact]
    public void Clear_rows_keeps_the_fill_order()
    {
        var editor = FourRows(EmptyOverlay());
        editor.FillOrder = FillOrder.SternFirst;

        editor.ClearRowsCommand.Execute(null);

        editor.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    // --- Fill order in mission files (D72) ---

    /// <summary>The split-and-shared missions as a mission file with <paramref name="fillOrder"/>, written by Core's writer.</summary>
    private string MissionFileWith(FillOrder fillOrder)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"{fillOrder}.json");
        using var stream = File.Create(path);
        MissionFileWriter.Write(stream, TestPlans.SplitAndShared().Stops, fillOrder);
        return path;
    }

    [Fact]
    public void Import_of_a_file_with_a_fill_order_sets_it_as_an_unplanned_change()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.Import(MissionFileWith(FillOrder.SternFirst));

        editor.FillOrder.Should().Be(FillOrder.SternFirst);
        editor.IsDirty.Should().BeTrue();
        overlay.Plan.Should().BeSameAs(plan, "like every edit it waits for Plan");
    }

    [Fact]
    public void Import_of_a_file_without_a_fill_order_keeps_the_editors()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "no-fill-order.json");
        File.WriteAllText(path, """
            { "schemaVersion": 4, "stops": [ { "destination": "Riker", "items": [ { "commodity": "Titanium", "scu": 4, "maxCrateSize": 4 } ] } ] }
            """);
        var editor = FourRows(EmptyOverlay());
        editor.FillOrder = FillOrder.SternFirst;

        editor.Import(path);

        editor.Problem.Should().BeNull();
        editor.FillOrder.Should().Be(FillOrder.SternFirst);
    }

    [Theory]
    [InlineData(FillOrder.BowFirst, "bowFirst")]
    [InlineData(FillOrder.SternFirst, "sternFirst")]
    public void Export_writes_the_fill_order_of_the_draft(FillOrder fillOrder, string expected)
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(OverlayWithPlan());
        editor.FillOrder = fillOrder;

        editor.Export(path);

        MissionFileReader.Read(path).FillOrder.Should().Be(fillOrder);
        File.ReadAllText(path).Should().Contain($"\"fillOrder\": \"{expected}\"");
    }

    [Fact]
    public void Import_then_Export_gives_the_file_back_byte_for_byte()
    {
        var source = MissionFileWith(FillOrder.SternFirst);
        var target = Path.Combine(_folder, "exported.json");
        var editor = new MissionEditorViewModel(EmptyOverlay());

        editor.Import(source);
        editor.Export(target);

        File.ReadAllBytes(target).Should().Equal(File.ReadAllBytes(source));
    }

    [Fact]
    public void A_stern_first_file_imported_and_planned_lays_from_the_stern()
    {
        var overlay = EmptyOverlay();
        var editor = new MissionEditorViewModel(overlay);

        editor.Import(MissionFileWith(FillOrder.SternFirst));
        editor.PlanCommand.Execute(null);

        var stops = MissionRows.ToStops(editor.Rows, CrateCap.None).Stops.ToStops();
        overlay.Plan!.FillOrder.Should().Be(FillOrder.SternFirst);
        overlay.Plan.Crates.Select(c => c.Position).Should().Equal(
            LoadPlanner.Plan(overlay.Ship, stops, FillOrder.SternFirst).Crates.Select(c => c.Position));
    }

    [Fact]
    public void The_choices_are_bow_first_and_stern_first()
    {
        MissionEditorViewModel.FillOrders.Should().Equal(
            new FillOrderChoice(FillOrder.BowFirst, "Bow first"),
            new FillOrderChoice(FillOrder.SternFirst, "Stern first"));
    }

    // --- Rows (D88): the fill order's rules ---

    private string MissionFileWith(FillRows fillRows)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"{fillRows}.json");
        using var stream = File.Create(path);
        MissionFileWriter.Write(stream, TestPlans.SplitAndShared().Stops, FillOrder.BowFirst, fillRows);
        return path;
    }

    [Fact]
    public void Without_a_plan_the_rows_are_lengthwise()
    {
        new MissionEditorViewModel(EmptyOverlay()).FillRows.Should().Be(FillRows.Lengthwise);
    }

    [Fact]
    public void The_editor_starts_with_the_rows_of_the_current_plan()
    {
        var overlay = EmptyOverlay();
        var planned = TestPlans.SplitAndShared();
        overlay.ShowPlan(SavedPlan.FromStops(planned.Stops, LoadPlanner.Plan(overlay.Ship, planned.Stops.ToStops(), FillOrder.BowFirst, FillRows.Crosswise), CrateCap.Of(overlay.Ship)));

        new MissionEditorViewModel(overlay).FillRows.Should().Be(FillRows.Crosswise);
    }

    [Fact]
    public void Plan_uses_the_chosen_rows()
    {
        var overlay = EmptyOverlay();
        var editor = FourRows(overlay);

        editor.FillRows = FillRows.Crosswise;
        editor.PlanCommand.Execute(null);

        overlay.Plan!.FillRows.Should().Be(FillRows.Crosswise);
    }

    [Fact]
    public void Changing_the_rows_marks_unplanned_changes_and_leaves_the_overlay_plan_until_Plan()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.FillRows = FillRows.Crosswise;

        editor.IsDirty.Should().BeTrue();
        editor.Title.Should().Be(MissionEditorViewModel.UnplannedTitleText);
        overlay.Plan.Should().BeSameAs(plan);
    }

    [Fact]
    public void Clear_rows_keeps_the_row_axis()
    {
        var editor = FourRows(EmptyOverlay());
        editor.FillRows = FillRows.Crosswise;

        editor.ClearRowsCommand.Execute(null);

        editor.FillRows.Should().Be(FillRows.Crosswise);
    }

    [Fact]
    public void Import_of_a_file_with_rows_sets_them_as_an_unplanned_change()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.Import(MissionFileWith(FillRows.Crosswise));

        editor.FillRows.Should().Be(FillRows.Crosswise);
        editor.IsDirty.Should().BeTrue();
        overlay.Plan.Should().BeSameAs(plan, "like every edit it waits for Plan");
    }

    [Fact]
    public void Import_of_a_file_without_rows_keeps_the_editors()
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "schema3.json");
        File.WriteAllText(path, """
            { "schemaVersion": 3, "fillOrder": "bowFirst", "stops": [ { "destination": "Riker", "items": [ { "commodity": "Titanium", "scu": 4, "maxCrateSize": 4 } ] } ] }
            """);
        var editor = FourRows(EmptyOverlay());
        editor.FillRows = FillRows.Crosswise;

        editor.Import(path);

        editor.Problem.Should().BeNull();
        editor.FillRows.Should().Be(FillRows.Crosswise);
    }

    [Theory]
    [InlineData(FillRows.Lengthwise, "lengthwise")]
    [InlineData(FillRows.Crosswise, "crosswise")]
    public void Export_writes_the_rows_of_the_draft(FillRows fillRows, string expected)
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(OverlayWithPlan());
        editor.FillRows = fillRows;

        editor.Export(path);

        MissionFileReader.Read(path).FillRows.Should().Be(fillRows);
        File.ReadAllText(path).Should().Contain($"\"fillRows\": \"{expected}\"");
    }

    [Fact]
    public void Import_then_Export_keeps_the_rows_byte_for_byte()
    {
        var source = MissionFileWith(FillRows.Crosswise);
        var target = Path.Combine(_folder, "exported.json");
        var editor = new MissionEditorViewModel(EmptyOverlay());

        editor.Import(source);
        editor.Export(target);

        File.ReadAllBytes(target).Should().Equal(File.ReadAllBytes(source));
    }

    [Fact]
    public void The_row_choices_are_lengthwise_and_crosswise()
    {
        MissionEditorViewModel.FillRowsChoices.Should().Equal(
            new FillRowsChoice(FillRows.Lengthwise, "Lengthwise"),
            new FillRowsChoice(FillRows.Crosswise, "Crosswise"));
    }

    // --- Grouping (D89): the fill order's rules ---

    private string MissionFileWith(PlanGrouping grouping)
    {
        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, $"{grouping}.json");
        using var stream = File.Create(path);
        MissionFileWriter.Write(stream, TestPlans.SplitAndShared().Stops, FillOrder.BowFirst, FillRows.Lengthwise, grouping);
        return path;
    }

    [Fact]
    public void Without_a_plan_there_is_no_grouping()
    {
        new MissionEditorViewModel(EmptyOverlay()).Grouping.Should().Be(PlanGrouping.None);
    }

    [Fact]
    public void The_editor_starts_with_the_grouping_of_the_current_plan()
    {
        var overlay = EmptyOverlay();
        var planned = TestPlans.SplitAndShared();
        overlay.ShowPlan(SavedPlan.FromStops(planned.Stops, LoadPlanner.Plan(overlay.Ship, planned.Stops.ToStops(), FillOrder.BowFirst, FillRows.Lengthwise, PlanGrouping.ByDestination), CrateCap.Of(overlay.Ship)));

        new MissionEditorViewModel(overlay).Grouping.Should().Be(PlanGrouping.ByDestination);
    }

    [Fact]
    public void Plan_uses_the_chosen_grouping()
    {
        var overlay = EmptyOverlay();
        var editor = FourRows(overlay);

        editor.Grouping = PlanGrouping.ByDestination;
        editor.PlanCommand.Execute(null);

        overlay.Plan!.Grouping.Should().Be(PlanGrouping.ByDestination);
    }

    [Fact]
    public void Changing_the_grouping_marks_unplanned_changes_and_leaves_the_overlay_plan_until_Plan()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.Grouping = PlanGrouping.ByDestination;

        editor.IsDirty.Should().BeTrue();
        editor.Title.Should().Be(MissionEditorViewModel.UnplannedTitleText);
        overlay.Plan.Should().BeSameAs(plan);
    }

    [Fact]
    public void Clear_rows_keeps_the_grouping()
    {
        var editor = FourRows(EmptyOverlay());
        editor.Grouping = PlanGrouping.ByDestination;

        editor.ClearRowsCommand.Execute(null);

        editor.Grouping.Should().Be(PlanGrouping.ByDestination);
    }

    [Fact]
    public void Import_of_a_file_with_a_grouping_sets_it_and_one_without_keeps_the_editors()
    {
        var editor = FourRows(EmptyOverlay());

        editor.Import(MissionFileWith(PlanGrouping.ByDestination));
        editor.Grouping.Should().Be(PlanGrouping.ByDestination);

        Directory.CreateDirectory(_folder);
        var path = Path.Combine(_folder, "schema3.json");
        File.WriteAllText(path, """
            { "schemaVersion": 3, "fillOrder": "bowFirst", "stops": [ { "destination": "Riker", "items": [ { "commodity": "Titanium", "scu": 4, "maxCrateSize": 4 } ] } ] }
            """);
        editor.Import(path);

        editor.Problem.Should().BeNull();
        editor.Grouping.Should().Be(PlanGrouping.ByDestination);
    }

    [Theory]
    [InlineData(PlanGrouping.None, "none")]
    [InlineData(PlanGrouping.ByDestination, "byDestination")]
    public void Export_writes_the_grouping_of_the_draft(PlanGrouping grouping, string expected)
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(OverlayWithPlan());
        editor.Grouping = grouping;

        editor.Export(path);

        MissionFileReader.Read(path).Grouping.Should().Be(grouping);
        File.ReadAllText(path).Should().Contain($"\"grouping\": \"{expected}\"");
    }

    [Fact]
    public void The_grouping_choices_are_none_and_by_destination()
    {
        MissionEditorViewModel.GroupingChoices.Should().Equal(
            new GroupingChoice(PlanGrouping.None, "None"),
            new GroupingChoice(PlanGrouping.ByDestination, "By destination"));
    }

    /// <summary>Seventeen destinations on the Hull A's four racks: one rack takes five, one more than its four rows.</summary>
    private static SavedPlan Sharing(OverlayViewModel overlay)
    {
        MissionStop[] stops = [.. Enumerable.Range(1, 17).Select(i => new MissionStop($"Stop {i}", i, [new MissionItem("Titanium", 1, CrateSize.One)]))];
        return SavedPlan.FromStops(stops, LoadPlanner.Plan(overlay.Ship, stops.ToStops(), FillOrder.BowFirst, FillRows.Lengthwise, PlanGrouping.ByDestination), CrateCap.Of(overlay.Ship));
    }

    [Fact]
    public void A_shared_section_shows_as_a_notice_and_goes_with_the_plan()
    {
        var overlay = EmptyOverlay();

        overlay.ShowPlan(Sharing(overlay));
        overlay.Notices.Should().Contain(n => n.Contains(" shares a section with ", StringComparison.Ordinal));

        var planned = TestPlans.SplitAndShared();
        overlay.ShowPlan(SavedPlan.FromStops(planned.Stops, LoadPlanner.Plan(overlay.Ship, planned.Stops.ToStops()), CrateCap.Of(overlay.Ship)));
        overlay.Notices.Should().NotContain(n => n.Contains(" shares a section with ", StringComparison.Ordinal));
    }

    // --- Section gap (K9.2b): the fill order's rules ---

    [Fact]
    public void Without_a_plan_there_is_no_gap()
    {
        new MissionEditorViewModel(EmptyOverlay()).SectionGap.Should().Be(0);
    }

    [Fact]
    public void Plan_uses_the_chosen_gap_and_the_next_editor_starts_with_it()
    {
        var overlay = EmptyOverlay();
        var editor = FourRows(overlay);

        editor.Grouping = PlanGrouping.ByDestination;
        editor.SectionGap = 2;
        editor.PlanCommand.Execute(null);

        overlay.Plan!.SectionGap.Should().Be(2);
        new MissionEditorViewModel(overlay).SectionGap.Should().Be(2);
    }

    [Fact]
    public void The_gap_box_works_only_with_grouping_and_the_gap_survives_without()
    {
        var overlay = EmptyOverlay();
        var editor = FourRows(overlay);
        List<string?> changed = [];
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        editor.IsSectionGapEnabled.Should().BeFalse();

        editor.Grouping = PlanGrouping.ByDestination;
        editor.IsSectionGapEnabled.Should().BeTrue();
        changed.Should().Contain(nameof(MissionEditorViewModel.IsSectionGapEnabled));
        editor.SectionGap = 1;

        editor.Grouping = PlanGrouping.None;
        editor.PlanCommand.Execute(null);

        overlay.Plan!.SectionGap.Should().Be(1);
        new MissionEditorViewModel(overlay).SectionGap.Should().Be(1);
    }

    [Fact]
    public void Changing_the_gap_marks_unplanned_changes_and_Clear_rows_keeps_it()
    {
        var overlay = OverlayWithPlan();
        var plan = overlay.Plan;
        var editor = new MissionEditorViewModel(overlay);

        editor.SectionGap = 1;

        editor.IsDirty.Should().BeTrue();
        overlay.Plan.Should().BeSameAs(plan);
        editor.ClearRowsCommand.Execute(null);
        editor.SectionGap.Should().Be(1);
    }

    [Fact]
    public void Export_and_Import_carry_the_gap()
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(OverlayWithPlan());
        editor.SectionGap = 2;

        editor.Export(path);
        File.ReadAllText(path).Should().Contain("\"sectionGap\": 2");

        var other = new MissionEditorViewModel(EmptyOverlay());
        other.Import(path);
        other.SectionGap.Should().Be(2);
    }

    [Fact]
    public void The_gap_choices_are_none_one_and_two_rows()
    {
        MissionEditorViewModel.SectionGapChoices.Should().Equal(new SectionGapChoice(0, "None"), new SectionGapChoice(1, "1 row"), new SectionGapChoice(2, "2 rows"));
    }

    // --- Import, export, text ---

    [Fact]
    public void Export_then_Import_gives_the_same_rows()
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(EmptyOverlay());
        var before = MissionRows.ToStops(editor.Rows, CrateCap.None).Stops;

        editor.Export(path);
        editor.ClearRowsCommand.Execute(null);
        editor.Import(path);

        MissionRows.ToStops(editor.Rows, CrateCap.None).Stops.Should().BeEquivalentTo(before, o => o.WithStrictOrdering());
        editor.Problem.Should().BeNull();
    }

    [Fact]
    public void Export_writes_crates_only_for_the_overridden_row_and_Import_brings_both_back()
    {
        var path = Path.Combine(_folder, "missions.json");
        var editor = FourRows(EmptyOverlay());
        editor.Rows[0].Crates = "2+2+4";

        editor.Export(path);
        editor.ClearRowsCommand.Execute(null);
        editor.Import(path);

        File.ReadAllText(path).Split("\"crates\"").Should().HaveCount(2, "one row has an override");
        editor.Rows.Select(r => r.Crates).Should().Equal("4+2×2", "", "", "");   // back in the one spelling (D96)
        editor.Rows.Select(r => r.IsCrateOverride).Should().Equal(true, false, false, false);
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

    // --- Missions and captures (T5.4; a plan from before T10.4a) ---

    private static Mission Capture(int id, long contractId, string destination, bool sizeAssumed = false) =>
        new(id, [new MissionStop(destination, 1, [new MissionItem("Stims", 4, CrateSize.Four)])], new ContractSource([contractId], sizeAssumed));

    /// <summary>Riker typed, then captures 36 (Shubin) and 37 (Orison, crate size assumed), as a plan saved with watermark 37.</summary>
    private static OverlayViewModel OverlayWithCaptures()
    {
        var overlay = EmptyOverlay();
        Mission riker = new(1, [new MissionStop("Riker", 1, [new MissionItem("Titanium", 8, CrateSize.Four)])]);
        var planned = LoadPlanner.Plan(overlay.Ship, [riker, Capture(2, 36, "Shubin"), Capture(3, 37, "Orison", sizeAssumed: true)], ["Riker", "Shubin", "Orison"]);
        overlay.ShowPlan(new SavedPlan(planned.Missions, planned.Plan, planned.CrateCap, Watermark: 37));
        return overlay;
    }

    [Fact]
    public void Rows_show_where_a_mission_came_from()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());

        editor.Rows.Select(r => (r.Destination, r.Source, r.ContractId)).Should().Equal(("Riker", "", null), ("Shubin", "#36", 36), ("Orison", "#37", 37));
    }

    // --- Sorting the view by mission or destination (K6.3, D19 addendum) ---

    private static string[] Shown(MissionEditorViewModel editor) => [.. editor.RowsView.Cast<MissionRow>().Select(r => r.Destination)];

    [Fact]
    public void A_header_click_sorts_up_then_down_then_back_to_the_standard_order()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());

        editor.SortBy(EditorSortColumn.Destination);
        (editor.Sort, Shown(editor)).Should().BeEquivalentTo((new EditorSort(EditorSortColumn.Destination, false), new[] { "Orison", "Riker", "Shubin" }));

        editor.SortBy(EditorSortColumn.Destination);
        (editor.Sort, Shown(editor)).Should().BeEquivalentTo((new EditorSort(EditorSortColumn.Destination, true), new[] { "Shubin", "Riker", "Orison" }));

        editor.SortBy(EditorSortColumn.Destination);
        editor.Sort.Should().BeNull();
        Shown(editor).Should().Equal("Riker", "Shubin", "Orison");
    }

    [Fact]
    public void Another_column_starts_ascending()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());

        editor.SortBy(EditorSortColumn.Destination);
        editor.SortBy(EditorSortColumn.Mission);

        editor.Sort.Should().Be(new EditorSort(EditorSortColumn.Mission, false));
        Shown(editor).Should().Equal("Shubin", "Orison", "Riker");
    }

    [Fact]
    public void Sorting_leaves_the_rows_as_they_are()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());
        MissionRow[] before = [.. editor.Rows];

        editor.SortBy(EditorSortColumn.Destination);

        editor.Rows.Should().Equal(before);
        editor.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Plan_from_a_sorted_view_is_the_plan_from_the_standard_order()
    {
        // The goldens' rule: the same input gives the same plan. Sorting is the view's, the plan reads the rows.
        var sorted = OverlayWithCaptures();
        var editor = new MissionEditorViewModel(sorted);
        editor.SortBy(EditorSortColumn.Destination);
        editor.SortBy(EditorSortColumn.Destination);
        editor.PlanCommand.Execute(null);

        var standard = OverlayWithCaptures();
        new MissionEditorViewModel(standard).PlanCommand.Execute(null);

        sorted.Saved!.Stops.Select(s => s.Destination).Should().Equal(standard.Saved!.Stops.Select(s => s.Destination));
        sorted.Missions.Should().BeEquivalentTo(standard.Missions, o => o.WithStrictOrdering());
        sorted.Plan!.Crates.Should().BeEquivalentTo(standard.Plan!.Crates, o => o.WithStrictOrdering());
        sorted.Scene.Legend.Select(e => e.Label).Should().Equal(standard.Scene.Legend.Select(e => e.Label));
    }

    [Fact]
    public void Moving_rows_waits_until_the_sort_is_cleared()
    {
        // Up and down move in the standard order; in a sorted view they would jump elsewhere.
        var editor = new MissionEditorViewModel(OverlayWithCaptures());
        editor.SelectedRow = editor.Rows[1];
        editor.MoveDownCommand.CanExecute(null).Should().BeTrue();

        editor.SortBy(EditorSortColumn.Mission);
        (editor.MoveUpCommand.CanExecute(null), editor.MoveDownCommand.CanExecute(null)).Should().Be((false, false));

        editor.SortBy(EditorSortColumn.Mission);
        editor.SortBy(EditorSortColumn.Mission);
        (editor.MoveUpCommand.CanExecute(null), editor.MoveDownCommand.CanExecute(null)).Should().Be((true, true));
    }

    [Fact]
    public void A_row_added_while_sorted_is_sorted_too_and_a_blank_one_stays_last()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());
        editor.SortBy(EditorSortColumn.Destination);

        editor.AddRowCommand.Execute(null);
        Shown(editor)[^1].Should().BeEmpty();

        Fill(editor.Rows[^1], "Area18", "Stims", "4", 4);
        editor.RowsView.Refresh();
        Shown(editor).Should().Equal("Area18", "Orison", "Riker", "Shubin");
    }

    // "Row n" counts the rows as the grid shows them (T7.5a2): sorted, the first invalid row is the first one shown.

    [Fact]
    public void Without_a_sort_the_problem_names_the_first_invalid_row_as_entered()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());

        editor.Rows[0].Scu = "x";
        editor.Rows[2].Commodity = "";

        editor.Problem.Should().Be("Row 1: SCU must be a whole number above 0.");
        editor.PlanToolTip.Should().Be("Row 1: SCU must be a whole number above 0.");
    }

    [Fact]
    public void Sorted_the_problem_names_the_first_invalid_row_the_grid_shows()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());
        editor.Rows[0].Scu = "x";
        editor.Rows[2].Commodity = "";
        var changed = new List<string?>();
        editor.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        editor.SortBy(EditorSortColumn.Destination);

        Shown(editor).Should().Equal("Orison", "Riker", "Shubin");
        editor.Problem.Should().Be("Row 1: Commodity is empty.");
        editor.PlanToolTip.Should().Be("Row 1: Commodity is empty.");
        changed.Should().Contain([nameof(MissionEditorViewModel.Problem), nameof(MissionEditorViewModel.PlanToolTip)]);
    }

    [Fact]
    public void Sorted_by_mission_a_typed_row_counts_after_the_captures()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());
        editor.Rows[0].Scu = "x";

        editor.SortBy(EditorSortColumn.Mission);

        Shown(editor).Should().Equal("Shubin", "Orison", "Riker");
        editor.Problem.Should().Be("Row 3: SCU must be a whole number above 0.");
    }

    [Fact]
    public void Plan_keeps_the_watermark_and_the_captures()
    {
        var overlay = OverlayWithCaptures();
        var editor = new MissionEditorViewModel(overlay);

        editor.PlanCommand.Execute(null);

        overlay.Saved!.Watermark.Should().Be(37);
        overlay.Missions.Select(m => MissionText.Source(m.Contract)).Should().Equal("", "#36", "#37");
    }

    [Fact]
    public void A_changed_max_crate_size_is_no_assumption_anymore()
    {
        var overlay = OverlayWithCaptures();
        var editor = new MissionEditorViewModel(overlay);

        editor.Rows.Single(r => r.Destination == "Orison").MaxCrateSize = 2;
        editor.PlanCommand.Execute(null);

        overlay.Missions.Single(m => m.Contract?.ContractIds[0] == 37).Contract!.CrateSizeAssumed.Should().BeFalse();
    }

    [Fact]
    public void A_picked_max_crate_size_derives_the_crates_again_and_counts_as_a_change()
    {
        // B8: what the drop-down sets reaches the crates column at once, like typing does.
        var editor = new MissionEditorViewModel(OverlayWithCaptures());
        var orison = editor.Rows.Single(r => r.Destination == "Orison");
        orison.Scu = "6";
        editor.IsDirty = false;

        orison.MaxCrateSize = 2;

        orison.CratesText.Should().Be("3×2");
        orison.MaxCrateSizeAssumed.Should().BeFalse();
        editor.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void An_untouched_assumed_size_stays_assumed()
    {
        var overlay = OverlayWithCaptures();

        new MissionEditorViewModel(overlay).PlanCommand.Execute(null);

        overlay.Missions.Single(m => m.Contract?.ContractIds[0] == 37).Contract!.CrateSizeAssumed.Should().BeTrue();
    }

    [Fact]
    public void DeleteMission_takes_out_its_rows_and_crates_at_once_and_moves_nothing_else()
    {
        var overlay = OverlayWithCaptures();
        overlay.ToggleDeliveredCommand.Execute("Riker");
        var editor = new MissionEditorViewModel(overlay);
        var others = overlay.Plan!.Crates.Where(c => c.Destination != "Shubin").ToArray();

        editor.SelectedRow = editor.Rows.Single(r => r.Destination == "Shubin");
        editor.DeleteMissionCommand.Execute(null);

        overlay.Plan!.Crates.Should().Equal(others);
        editor.Rows.Select(r => r.Destination).Should().Equal("Riker", "Orison");
        editor.IsDirty.Should().BeFalse();
        editor.Status.Should().Be(MissionEditorViewModel.MissionDeletedText);
    }

    [Fact]
    public void DeleteMission_on_a_typed_row_takes_the_typed_rows_of_its_stop()
    {
        var overlay = OverlayWithCaptures();
        var editor = new MissionEditorViewModel(overlay);

        editor.SelectedRow = editor.Rows.Single(r => r.Destination == "Riker");
        editor.DeleteMissionCommand.Execute(null);

        overlay.Plan!.Crates.Should().NotContain(c => c.Destination == "Riker");
        editor.Rows.Select(r => r.Destination).Should().Equal("Shubin", "Orison");
    }

    [Fact]
    public void DeleteMission_is_off_for_a_row_not_planned_yet()
    {
        var editor = new MissionEditorViewModel(OverlayWithCaptures());

        editor.AddRowCommand.Execute(null);

        editor.DeleteMissionCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Scan_runs_the_overlays_scan()
    {
        var scanned = 0;
        var editor = new MissionEditorViewModel(EmptyOverlay(), scanContract: () => scanned++);

        editor.ScanCommand.Execute(null);

        scanned.Should().Be(1);
    }

    [Fact]
    public void Scan_is_off_without_a_scanner()
    {
        new MissionEditorViewModel(EmptyOverlay()).ScanCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public void Scan_is_off_while_a_scan_runs_and_on_again_after()
    {
        // The button sees the state, so it needs no "Scan running" (T10.3); the hotkey does.
        var overlay = EmptyOverlay();
        var editor = new MissionEditorViewModel(overlay, scanContract: () => { });
        var changed = 0;
        editor.ScanCommand.CanExecuteChanged += (_, _) => changed++;

        overlay.IsScanning = true;
        editor.ScanCommand.CanExecute(null).Should().BeFalse();
        overlay.IsScanning = false;

        editor.ScanCommand.CanExecute(null).Should().BeTrue();
        changed.Should().Be(2);
    }

    [Fact]
    public void A_scan_while_the_draft_is_changed_keeps_the_edits_adds_its_rows_and_says_so()
    {
        var overlay = OverlayWithCaptures();
        var editor = new MissionEditorViewModel(overlay);
        editor.Rows[0].Scu = "12";

        overlay.ApplyScan(Import.Contracts.ScannedContract.Read(
            "ABANDON\nDETAILS\nReward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\n"
            + "Deliver 0/8 SCU of Stims to Lorville.\nCollect Stims from Everus Harbor.\nSHARE UNTRACK"));

        editor.Rows.Select(r => r.Destination).Should().Contain("Lorville");
        editor.IsDirty.Should().BeTrue();
        editor.Status.Should().Be(MissionEditorViewModel.ChangedOutsideText);
        MissionEditorViewModel.ChangedOutsideText.Should().Contain("a scan");
    }

    [Fact]
    public void A_scan_while_the_editor_is_open_shows_up_in_a_clean_draft()
    {
        var overlay = OverlayWithCaptures();
        var editor = new MissionEditorViewModel(overlay);

        overlay.ApplyScan(Import.Contracts.ScannedContract.Read(
            "ABANDON\nDETAILS\nReward 50,250\nContract Deadline N/A\nPRIMARY OBJECTIVES\n"
            + "Deliver 0/8 SCU of Stims to Lorville.\nCollect Stims from Everus Harbor.\nSHARE UNTRACK"));

        editor.Rows.Select(r => r.Destination).Should().Equal("Riker", "Shubin", "Orison", "Lorville");
        editor.IsDirty.Should().BeFalse();
    }

}
