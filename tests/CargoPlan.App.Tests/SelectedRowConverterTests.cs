using System.Runtime.ExceptionServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using CargoPlan.App.Editor;
using CargoPlan.App.ViewModels;
using CargoPlan.Core.Ships;

namespace CargoPlan.App.Tests;

public class SelectedRowConverterTests
{
    [Fact]
    public void Selecting_the_new_item_row_keeps_it_selected_in_the_grid()
    {
        // The crash (T6.4b follow-up): the binding wrote null back into SelectedItem while the placeholder stayed in
        // SelectedItems; with a UI Automation client listening, DataGrid then builds a peer for SelectedItem = null.
        OnSta(() =>
        {
            var editor = new MissionEditorViewModel(new OverlayViewModel(ShipLoader.Load("hull_a")));
            var grid = Grid(editor);
            grid.SelectedItem = editor.Rows[0];

            grid.SelectedItem = CollectionView.NewItemPlaceholder;

            grid.SelectedItem.Should().BeSameAs(CollectionView.NewItemPlaceholder);
            grid.SelectedItems.Cast<object>().Should().ContainSingle().Which.Should().BeSameAs(CollectionView.NewItemPlaceholder);
            editor.SelectedRow.Should().BeNull();
        });
    }

    [Fact]
    public void Selecting_a_row_in_the_view_model_selects_it_in_the_grid()
    {
        OnSta(() =>
        {
            var editor = new MissionEditorViewModel(new OverlayViewModel(ShipLoader.Load("hull_a")));
            var grid = Grid(editor);
            editor.AddRowCommand.Execute(null);

            grid.SelectedItem.Should().BeSameAs(editor.Rows[1]);
        });
    }

    [Fact]
    public void Clearing_the_rows_leaves_no_removed_row_selected_in_the_grid()
    {
        // The view model's null is not written into the grid; the grid drops the removed row's selection itself.
        OnSta(() =>
        {
            var editor = new MissionEditorViewModel(new OverlayViewModel(ShipLoader.Load("hull_a")));
            var grid = Grid(editor);
            var removed = editor.Rows[0];
            grid.SelectedItem = removed;

            editor.ClearRowsCommand.Execute(null);

            editor.SelectedRow.Should().BeNull();
            grid.SelectedItems.Cast<object>().Should().NotContain(removed);
            grid.SelectedItem.Should().NotBeSameAs(removed);
        });
    }

    /// <summary>The editor's grid as MissionEditorWindow.xaml binds it: rows, the new-item row, the selected row.</summary>
    private static DataGrid Grid(MissionEditorViewModel editor)
    {
        DataGrid grid = new() { DataContext = editor, CanUserAddRows = true, AutoGenerateColumns = false };
        grid.SetBinding(ItemsControl.ItemsSourceProperty, new Binding(nameof(MissionEditorViewModel.Rows)));
        grid.SetBinding(Selector.SelectedItemProperty, new Binding(nameof(MissionEditorViewModel.SelectedRow))
        {
            Converter = new SelectedRowConverter(),
            Mode = BindingMode.TwoWay,
        });
        grid.Items.Cast<object>().Should().Contain(CollectionView.NewItemPlaceholder, "the grid must offer its new-item row for this test");
        return grid;
    }

    // WPF controls need a single-threaded apartment; xUnit's threads aren't.
    private static void OnSta(Action test)
    {
        ExceptionDispatchInfo? failure = null;
        Thread thread = new(() =>
        {
            try
            {
                test();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        failure?.Throw();
    }
}
