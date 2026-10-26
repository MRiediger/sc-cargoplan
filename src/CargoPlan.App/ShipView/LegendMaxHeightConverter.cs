using System.Globalization;
using System.Windows.Data;

namespace CargoPlan.App.ShipView;

/// <summary>
/// The stop lines' highest height (K7, D80): the overlay's height times the share from the settings, so the lines scroll
/// beyond it and the ship view keeps the rest. Without a height (before the first layout pass) no limit, MaxHeight's own
/// default: 0 would hide the lines.
/// </summary>
// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.imultivalueconverter
public sealed class LegendMaxHeightConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [double height, double share] ? height * share : double.PositiveInfinity;

    // One-way only: the height comes from the layout and the settings.
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
