using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using CargoPlan.App.Routing;

namespace CargoPlan.App.Tests;

/// <summary>The port matrix's columns from the view model (T13.2): a frozen pickup column, then one per drop-off.</summary>
public class PortMatrixGridTests
{
    private static DataGrid Grid()
    {
        var grid = new DataGrid();
        grid.Resources[PortMatrixGrid.CellTextStyleKey] = new Style(typeof(TextBlock));
        grid.Resources[PortMatrixGrid.PickupTextStyleKey] = new Style(typeof(TextBlock));
        return grid;
    }

    private static string PathOf(DataGridColumn column) => ((Binding)((DataGridTextColumn)column).Binding).Path.Path;

    [Fact]
    public void Columns_follow_the_drop_offs_after_the_pickup_column() => Sta.Run(() =>
    {
        var grid = Grid();

        PortMatrixGrid.SetColumns(grid, ["HUR-L1", "HUR-L2"]);

        grid.Columns.Select(c => c.Header).Should().Equal("From \\ To", "HUR-L1", "HUR-L2");
        grid.Columns.Select(PathOf).Should().Equal("Pickup", "Cells[0].Text", "Cells[1].Text");
        var tip = ((DataGridTextColumn)grid.Columns[2]).ElementStyle.Setters.OfType<Setter>().Single(s => s.Property == FrameworkElement.ToolTipProperty);
        ((Binding)tip.Value).Path.Path.Should().Be("Cells[1].ToolTip");

        PortMatrixGrid.SetColumns(grid, ["Baijini Point"]);
        grid.Columns.Select(c => c.Header).Should().Equal("From \\ To", "Baijini Point");
    });
}
