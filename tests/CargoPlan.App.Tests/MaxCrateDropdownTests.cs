using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using CargoPlan.App.Editor;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Missions;
using CargoPlan.Core.Models;
using CargoPlan.Core.Plans;
using CargoPlan.Core.Tests;

namespace CargoPlan.App.Tests;

/// <summary>
/// The max crate column picked with the mouse (B8, issue #22): the editor's DockPanel ends every cell edit on a button
/// click, and the combo box's own drop-down arrow is a button too. Grid, columns and the click handler as
/// MissionEditorWindow.xaml has them; the click goes through ButtonBase.OnClick, the path of a mouse click.
/// </summary>
public class MaxCrateDropdownTests
{
    [Fact]
    public void Picking_a_size_from_the_drop_down_changes_the_row()
    {
        Sta.Run(() =>
        {
            var editor = new MissionEditorViewModel(new OverlayViewModel(FrozenShips.Load("hull_a")));
            var view = new EditorGrid(editor);

            view.Pick(editor.Rows[0], 8);

            editor.Rows[0].MaxCrateSize.Should().Be(8);
        });
    }

    [Fact]
    public void Path_1_synced_rows_and_a_new_row_after_them_can_be_changed()
    {
        // Issue #22, path 1: synced contracts stand on an assumed 32, a row added by hand takes 32 from the row above (D23).
        Sta.Run(() =>
        {
            var overlay = new OverlayViewModel(FrozenShips.Load("hull_a"));
            // Too big for the Hull A: it waits, as the capture did.
            Mission endgame = new(1, [new MissionStop("Endgame", 1, [new MissionItem("Waste", 124, CrateSize.ThirtyTwo)])],
                new ContractSource([1], CrateSizeAssumed: true), Unplaced: true);
            overlay.ShowPlan(new SavedPlan([endgame], new LoadPlan(overlay.Ship.Id, [], []), CrateCap.Of(overlay.Ship)));
            var editor = new MissionEditorViewModel(overlay);
            var view = new EditorGrid(editor);
            var synced = editor.Rows.Single(r => r.Destination == "Endgame");
            synced.MaxCrateSizeAssumed.Should().BeTrue("the test starts from an assumed size");

            view.Pick(synced, 8);

            synced.MaxCrateSize.Should().Be(8);
            synced.MaxCrateSizeAssumed.Should().BeFalse();
            synced.CratesText.Should().NotContain("32", "the crates follow the new max");
            editor.IsDirty.Should().BeTrue();

            editor.AddRowCommand.Execute(null);
            var added = editor.Rows[^1];
            added.MaxCrateSize.Should().Be(8, "a new row takes the max of the row above (D23)");
            view.Pick(added, 4);

            added.MaxCrateSize.Should().Be(4);
        });
    }

    [Fact]
    public void Path_2_a_new_list_starts_at_4_and_can_be_changed()
    {
        // Issue #22, path 2: the first row's default is 4, each new row takes the row above.
        Sta.Run(() =>
        {
            var editor = new MissionEditorViewModel(new OverlayViewModel(FrozenShips.Load("hull_a")));
            var view = new EditorGrid(editor);
            editor.Rows[0].MaxCrateSize.Should().Be(MissionRows.DefaultMaxCrateSize);

            view.Pick(editor.Rows[0], 2);
            editor.AddRowCommand.Execute(null);
            editor.Rows[1].MaxCrateSize.Should().Be(2);
            view.Pick(editor.Rows[1], 16);

            editor.Rows[0].MaxCrateSize.Should().Be(2);
            editor.Rows[1].MaxCrateSize.Should().Be(16);
        });
    }

    [Fact]
    public void A_button_outside_the_grid_still_ends_the_edit_first()
    {
        // What the handler is for: moving or removing rows during an edit transaction throws, so a toolbar button commits
        // the open cell first. The test column commits only then (no UpdateSourceTrigger), so the value shows the commit.
        Sta.Run(() =>
        {
            var editor = new MissionEditorViewModel(new OverlayViewModel(FrozenShips.Load("hull_a")));
            var view = new EditorGrid(editor);
            var row = editor.Rows[0];
            var box = (TextBox)view.Edit(row, view.Scu);

            box.Text = "12";
            view.ClickToolbarButton();

            row.Scu.Should().Be("12");
            ((IEditableCollectionView)editor.RowsView).IsEditingItem.Should().BeFalse();
        });
    }

    /// <summary>The editor's grid in a DockPanel with a toolbar button, the click handler as the window has it.</summary>
    private sealed class EditorGrid
    {
        private static readonly MethodInfo OnClick =
            typeof(ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private readonly DataGrid _grid;
        private readonly Button _toolbarButton = new() { Content = "Add" };
        private readonly DataGridComboBoxColumn _maxCrate;

        public DataGridTextColumn Scu { get; }

        public EditorGrid(MissionEditorViewModel editor)
        {
            _grid = new DataGrid { DataContext = editor, AutoGenerateColumns = false, CanUserAddRows = true, SelectionMode = DataGridSelectionMode.Single };
            _grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MissionEditorViewModel.RowsView)));
            Scu = new DataGridTextColumn { Header = "SCU", Binding = new Binding(nameof(MissionRow.Scu)) };
            _maxCrate = new DataGridComboBoxColumn
            {
                Header = "Max size",
                ItemsSource = MissionRows.MaxCrateSizes,
                SelectedItemBinding = new Binding(nameof(MissionRow.MaxCrateSize)) { UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged },
            };
            _grid.Columns.Add(Scu);
            _grid.Columns.Add(_maxCrate);

            DockPanel panel = new();
            DockPanel.SetDock(_toolbarButton, Dock.Top);
            panel.Children.Add(_toolbarButton);
            panel.Children.Add(_grid);
            panel.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((_, e) =>
            {
                if (MissionEditorWindow.EndsRowEdit(_grid, e.OriginalSource))
                {
                    _grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
                }
            }));
            ViewPictures.Layout(panel, new Size(800, 400));
        }

        /// <summary>Opens the cell's editor; returns the editing element.</summary>
        public FrameworkElement Edit(MissionRow row, DataGridColumn column)
        {
            _grid.UpdateLayout();
            _grid.ScrollIntoView(row, column);
            _grid.CurrentCell = new DataGridCellInfo(row, column);
            _grid.BeginEdit().Should().BeTrue("the cell must open its editor");
            _grid.UpdateLayout();
            return column.GetCellContent(row);
        }

        /// <summary>Clicks the combo box's arrow as a mouse does, then picks <paramref name="size"/> and leaves the cell.</summary>
        public void Pick(MissionRow row, int size)
        {
            var combo = (ComboBox)Edit(row, _maxCrate);
            combo.ApplyTemplate();
            var arrow = Descendants(combo).OfType<ToggleButton>().First();

            OnClick.Invoke(arrow, null);
            _grid.UpdateLayout();

            _maxCrate.GetCellContent(row).Should().BeSameAs(combo, "the click on the arrow must not end the edit");
            combo.SelectedItem = size;
            combo.IsDropDownOpen = false;
            _grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
        }

        public void ClickToolbarButton() => OnClick.Invoke(_toolbarButton, null);

        private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                yield return child;
                foreach (var below in Descendants(child))
                {
                    yield return below;
                }
            }
        }
    }
}
