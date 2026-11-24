using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
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

        PortMatrixGrid.SetColumns(grid, ["HUR-L1 Green Glade Station", "Everus Harbor"]);

        // Picture gate: an L station's column shows its code, the header's tool tip the full name.
        grid.Columns.Select(c => c.Header).Should().Equal("From \\ To", "HUR-L1", "Everus Harbor");
        grid.Columns.Skip(1).Select(c => c.HeaderStyle.Setters.OfType<Setter>().Single(s => s.Property == FrameworkElement.ToolTipProperty).Value)
            .Should().Equal("HUR-L1 Green Glade Station", "Everus Harbor");
        grid.Columns.Select(PathOf).Should().Equal("Label", "Cells[0].Text", "Cells[1].Text");
        var tip = ((DataGridTextColumn)grid.Columns[2]).ElementStyle.Setters.OfType<Setter>().Single(s => s.Property == FrameworkElement.ToolTipProperty);
        ((Binding)tip.Value).Path.Path.Should().Be("Cells[1].ToolTip");

        PortMatrixGrid.SetColumns(grid, ["Baijini Point"]);
        grid.Columns.Select(c => c.Header).Should().Equal("From \\ To", "Baijini Point");
    });

    // A view of 100 × 100 px over content of 1000 × 1000 px, pixel scrolling like the matrix's columns.
    private static ScrollViewer Scroller()
    {
        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new Border { Width = 1000, Height = 1000 },
        };
        scroller.Measure(new Size(100, 100));
        scroller.Arrange(new Rect(0, 0, 100, 100));
        scroller.UpdateLayout();
        return scroller;
    }

    // One notch scrolls as far sideways as down: the system's lines per notch, a line as the ScrollViewer has it.
    private static double OneNotch()
    {
        var reference = Scroller();
        for (var i = 0; i < SystemParameters.WheelScrollLines; i++)
        {
            reference.LineDown();
        }

        reference.UpdateLayout();
        return reference.VerticalOffset;
    }

    [Fact]
    public void Shift_and_the_wheel_scroll_sideways_down_is_right() => Sta.Run(() =>
    {
        var scroller = Scroller();
        var notch = OneNotch();

        PortMatrixGrid.WheelSideways(scroller, -Mouse.MouseWheelDeltaForOneLine, ModifierKeys.Shift).Should().BeTrue();
        scroller.UpdateLayout();
        scroller.HorizontalOffset.Should().Be(notch);
        scroller.VerticalOffset.Should().Be(0);

        PortMatrixGrid.WheelSideways(scroller, 2 * Mouse.MouseWheelDeltaForOneLine, ModifierKeys.Shift).Should().BeTrue();
        scroller.UpdateLayout();
        scroller.HorizontalOffset.Should().Be(0, "two notches up scroll back to the start, no further");
    });

    [Theory]
    [InlineData(ModifierKeys.None)]
    [InlineData(ModifierKeys.Control)]
    [InlineData(ModifierKeys.Shift | ModifierKeys.Control)]
    public void Without_Shift_alone_the_wheel_is_left_to_the_grid(ModifierKeys modifiers) => Sta.Run(() =>
    {
        // Plain wheel stays vertical, also where nothing overflows downwards (approval of K): never turned sideways.
        var scroller = Scroller();

        PortMatrixGrid.WheelSideways(scroller, -Mouse.MouseWheelDeltaForOneLine, modifiers).Should().BeFalse();
        scroller.UpdateLayout();
        scroller.HorizontalOffset.Should().Be(0);
    });
}
