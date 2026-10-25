using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace CargoPlan.App.ShipView;

/// <summary>
/// Legend swatch: a stop's ordinal and the chosen palette to the brush the ship view paints the stop's crates with, hatch
/// included (K4.2). Two values, because the palette is the overlay's and the ordinal the line's. The view itself draws in
/// <c>OnRender</c> and needs no converter, there is no binding per cell.
/// </summary>
// Source: https://learn.microsoft.com/dotnet/api/system.windows.data.imultivalueconverter
public sealed class PaletteBrushConverter : IMultiValueConverter
{
    public object Convert(object?[] values, Type targetType, object? parameter, CultureInfo culture) =>
        values is [int ordinal, StopPalette palette] ? StopBrushes.Swatch(palette, ordinal) : DependencyProperty.UnsetValue;

    // One-way only: a swatch never writes back.
    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
