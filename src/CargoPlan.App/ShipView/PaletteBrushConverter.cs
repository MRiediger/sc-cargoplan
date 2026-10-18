using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Legend swatch: a stop's palette index to the same brush the ship view paints with. The view itself draws in
/// <c>OnRender</c> and needs no converter, there is no binding per segment.
/// </summary>
// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.ivalueconverter
[ValueConversion(typeof(int), typeof(Brush))]
public sealed class PaletteBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int index ? MissionPalette.Brush(index) : DependencyProperty.UnsetValue;

    // One-way only: a swatch never writes back.
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
