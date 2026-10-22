using System.Globalization;
using System.Windows.Data;

namespace CargoPlan.App.Editor;

/// <summary>
/// The data grid's new-item row is selected as a placeholder object, not a <see cref="MissionRow"/>. Without this the
/// binding would drop it and keep the previous row, so Remove would hit a row that isn't selected anymore.
/// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.collectionview.newitemplaceholder
/// </summary>
public sealed class SelectedRowConverter : IValueConverter
{
    /// <summary>
    /// Null is never written into the grid. After ConvertBack turned the placeholder into null, the binding converts the
    /// new source value back to the target: that null left the placeholder in SelectedItems with SelectedItem null, and
    /// with a UI Automation client listening DataGrid builds a peer for SelectedItem and throws (the editor crash). The
    /// view model sets null only when the selected row leaves the rows, and the grid drops that selection by itself.
    /// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.binding.donothing
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value ?? Binding.DoNothing;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value as MissionRow;
}
