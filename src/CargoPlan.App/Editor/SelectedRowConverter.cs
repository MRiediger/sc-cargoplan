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
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value as MissionRow;
}
