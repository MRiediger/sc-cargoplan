using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace CargoPlan.App.Routing;

/// <summary>
/// The port matrix's columns (T13.2): one per drop-off of <see cref="RoutesViewModel.Columns"/>, built when the filter
/// changes them, after a frozen pickup column. XAML can't bind a DataGrid's columns, so this attached property does.
/// A click on a cell opens its tool tip, the hover's too: the list of templates (design §8.5). Shift and the wheel
/// scroll sideways (<see cref="WheelSideways"/>).
/// Source: https://learn.microsoft.com/dotnet/api/system.windows.controls.datagrid.columns (Columns is read-only, not bindable)
/// </summary>
public static class PortMatrixGrid
{
    /// <summary>The key of the TextBlock style every cell uses (colour, size and halo of the legend), in the grid's resources.</summary>
    public const string CellTextStyleKey = "MatrixCellTextStyle";

    /// <summary>The key of the TextBlock style of the pickup column.</summary>
    public const string PickupTextStyleKey = "MatrixPickupTextStyle";

    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
        "Columns", typeof(IReadOnlyList<string>), typeof(PortMatrixGrid), new PropertyMetadata(null, OnColumnsChanged));

    private static readonly DependencyProperty OpenTipProperty = DependencyProperty.RegisterAttached(
        "OpenTip", typeof(ToolTip), typeof(PortMatrixGrid));

    public static IReadOnlyList<string>? GetColumns(DependencyObject element) => (IReadOnlyList<string>?)element.GetValue(ColumnsProperty);

    public static void SetColumns(DependencyObject element, IReadOnlyList<string>? value) => element.SetValue(ColumnsProperty, value);

    private static void OnColumnsChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not DataGrid grid)
        {
            return;
        }

        if (e.OldValue is null)
        {
            grid.PreviewMouseLeftButtonUp += OnCellClicked;
            grid.PreviewMouseWheel += OnWheel;
        }

        grid.Columns.Clear();
        grid.Columns.Add(new DataGridTextColumn
        {
            Header = "From \\ To",
            Binding = new Binding(nameof(PortRowView.Label)),
            ElementStyle = (Style)grid.FindResource(PickupTextStyleKey),
        });

        var cellStyle = (Style)grid.FindResource(CellTextStyleKey);
        var columns = (IReadOnlyList<string>?)e.NewValue ?? [];
        for (var i = 0; i < columns.Count; i++)
        {
            // An L station by its code ("HUR-L1", picture gate), the full name in the header's tool tip.
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = Core.Routing.PlaceNames.Short(columns[i]),
                HeaderStyle = new Style(typeof(DataGridColumnHeader), grid.ColumnHeaderStyle)
                {
                    Setters = { new Setter(FrameworkElement.ToolTipProperty, columns[i]) },
                },
                Binding = new Binding($"{nameof(PortRowView.Cells)}[{i}].{nameof(PortCellView.Text)}"),
                ElementStyle = new Style(typeof(TextBlock), cellStyle)
                {
                    Setters = { new Setter(FrameworkElement.ToolTipProperty, new Binding($"{nameof(PortRowView.Cells)}[{i}].{nameof(PortCellView.ToolTip)}")) },
                },
            });
        }
    }

    /// <summary>
    /// Shift and the wheel scroll sideways (K of T13.3a), down is right, as far per notch as the wheel scrolls down: the
    /// system's lines per notch (a page where it says so). Any other wheel is the grid's: plain is vertical, also where
    /// nothing overflows downwards. The tilt wheel is in the backlog (WPF raises no event for it).
    /// Sources: https://learn.microsoft.com/dotnet/api/system.windows.systemparameters.wheelscrolllines (-1 = a page),
    /// https://learn.microsoft.com/dotnet/api/system.windows.input.mouse.mousewheeldeltaforoneline
    /// </summary>
    /// <returns>Whether the wheel was taken; the caller marks the event handled.</returns>
    public static bool WheelSideways(ScrollViewer scroller, int delta, ModifierKeys modifiers)
    {
        if (modifiers != ModifierKeys.Shift || delta == 0)
        {
            return false;
        }

        var right = delta < 0;
        var lines = SystemParameters.WheelScrollLines;
        if (lines < 0)
        {
            if (right)
            {
                scroller.PageRight();
            }
            else
            {
                scroller.PageLeft();
            }

            return true;
        }

        // A fine wheel sends less than a notch: still at least one line.
        var steps = Math.Max(1, Math.Abs(delta) * lines / Mouse.MouseWheelDeltaForOneLine);
        for (var i = 0; i < steps; i++)
        {
            if (right)
            {
                scroller.LineRight();
            }
            else
            {
                scroller.LineLeft();
            }
        }

        return true;
    }

    private static void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!e.Handled && Scroller((DependencyObject)sender) is { } scroller)
        {
            e.Handled = WheelSideways(scroller, e.Delta, Keyboard.Modifiers);
        }
    }

    // The grid's own ScrollViewer, the first in its template.
    private static ScrollViewer? Scroller(DependencyObject element)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
        {
            var child = VisualTreeHelper.GetChild(element, i);
            if ((child as ScrollViewer ?? Scroller(child)) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // A click shows what a hover shows, for players who click: a tool tip of its own on the cell's text, closed by the next click.
    private static void OnCellClicked(object sender, MouseButtonEventArgs e)
    {
        var grid = (DataGrid)sender;
        if (grid.GetValue(OpenTipProperty) is ToolTip open)
        {
            open.IsOpen = false;
            grid.ClearValue(OpenTipProperty);
        }

        // A text element (Run) has no visual parent; its logical one leads to the TextBlock.
        static DependencyObject? Parent(DependencyObject element) =>
            element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);

        for (var element = e.OriginalSource as DependencyObject; element is not null and not DataGrid; element = Parent(element))
        {
            if (element is TextBlock { ToolTip: string text } block)
            {
                var tip = new ToolTip { Content = text, PlacementTarget = block, Placement = PlacementMode.Bottom, StaysOpen = false };
                grid.SetValue(OpenTipProperty, tip);
                tip.IsOpen = true;
                return;
            }
        }
    }
}
